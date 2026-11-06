/**
 * Line editor (todo Phase 2): polytonic editing with grapheme-safe input, uncertainty
 * and gap annotation tools, and revision-based undo (every edit creates a revision;
 * undo restores the previous revision's text as a NEW revision — history is never
 * deleted). The L2 layer is written separately from L1; the surface view shows both.
 */
import { Component, computed, input, output, signal, SimpleChanges, Inject, Optional, effect, inject, ElementRef } from '@angular/core';
import { catchError, EMPTY, filter, MonoTypeOperatorFunction, Subject, Subscription, take, takeUntil, throwError } from 'rxjs';
import { ApiClient, AnnotationView, LinePayload } from './api-client';
import { applyScalarEdit, confidenceSegments, markSegments, scalarCount, scalarOffsetFromUtf16 } from './text-utils';
import { ProposalPanel, ProposalSavedEvent } from './proposal-panel';
import { mergeText } from './text-merge';
import { WORKSPACE_ACCESS, WorkspaceAccess, WorkspaceAccessState, WorkspaceDraftHandle, WorkspaceDraftScope, WorkspaceOpaqueRecord, WorkspaceJson } from './workspace-access-types';
import { WorkspaceDraftStore } from './workspace-draft-store';

interface ManualWorkflow {
  token: number;
  operation: string;
  phase: 'pending' | 'unknown' | 'partial' | 'link-pending' | 'acknowledged';
  lineId: string;
  surfaceId: string;
  targetLayer: string;
  sourceRevisionId: string | null;
  request: WorkspaceJson;
  followup: WorkspaceJson;
  editorialBase: WorkspaceJson;
  knownRevisionId: string | null;
  knownResultId: string | null;
  linkAttempted: boolean;
  linkAcknowledged: boolean;
}

interface UndoStack {
  text: string;
  revisionId: string | null;
}

@Component({
  selector: 'app-line-editor',
  standalone: true,
  imports: [ProposalPanel],
  template: `
    @if (!workspaceAvailable()) { <p role="status">Editor suspended. Your mounted work is retained.</p> }
    <div data-workspace-line-editor [hidden]="!workspaceAvailable()" [attr.inert]="workspaceAvailable() ? null : ''">
    @if (!workspaceWritable()) { <p role="status">Read-only access. Your mounted draft is retained.</p> }
    @if (draftPersistenceStatus()) { <p role="status">{{ draftPersistenceStatus() }}</p> }
    @if (manualNotice()) {
      <p role="alert">{{ manualNotice() }}
        @if (editorialHistoryUnavailable()) { <button type="button" (click)="reloadEditorialHistory()" [disabled]="!mutationAllowed()">Reload current layers</button> }
      </p>
    }
    @if (unconfirmedWrite()) { <p role="alert">A submitted write has not been confirmed. Your draft and its base are retained; new writes remain blocked pending authoritative reconciliation.</p> }
    @if (loadFailed()) {
      <p role="alert">Could not load this line. <button type="button" (click)="retryLoad()">Retry loading</button></p>
    }
    @if (payload(); as data) {
      <section>
        <h3>Line <code>{{ data.line }}</code></h3>
        @if (imageUrl()) {
          <figure class="source-excerpt">
            <img [src]="cropUrl()" alt="Line source excerpt" />
            @if (cropStatus()) { <figcaption role="status">{{ cropStatus() }}</figcaption> }
            @if (previewLink() ?? imageUrl()) {
              <figcaption class="hint"><a [href]="previewLink() ?? imageUrl()" target="_blank" rel="noopener">Open capture preview</a></figcaption>
            }
          </figure>
        }

        <label>Diplomatic (L1)
          <input [readOnly]="!workspaceWritable()" class="greek" [attr.dir]="direction() === 'rtl' ? 'rtl' : null"
                 [value]="text()" (input)="onEdit($event)" (keydown)="onKeydown($event)" spellcheck="false" />
        </label>
        @if (draftRestored()) {
          <p class="hint">Unsaved draft restored after reload (⌘/Ctrl+S saves, ⌘/Ctrl+Z undoes).</p>
        }
        @if (conflict()) {
          <p class="conflict">
            Your draft needs comparison with the latest saved reading before it can be saved.
            @if (latestUnavailable()) { Latest saved reading could not be loaded. Reload latest before comparing. }
            @else { Latest saved reading: <span class="greek">{{ payload()?.diplomatic ?? '' }}</span> }
            <button type="button" (click)="reloadAfterConflict()">Reload latest</button>
            <button type="button" (click)="keepDraftAfterCompare()" [disabled]="!mutationAllowed() || (savingRevision() || latestUnavailable())">Keep my draft as replacement</button>
            <button type="button" (click)="useLatestSavedText()" [disabled]="!mutationAllowed() || (savingRevision() || latestUnavailable())">Use latest saved text</button>
          </p>
        }
        @if (latestL0(); as l0) {
          <p class="l0" aria-label="model output">
            <span class="hint">L0 raw (may be stale):</span>
            @for (segment of l0Segments(); track $index) {
              <span [class]="segment.level ? 'conf-' + segment.level : ''">{{ segment.text }}</span>
            }
          </p>
        }
        <p class="marked" aria-label="marked text">
          @for (segment of markedSegments(); track $index) {
            <span [class]="segment.kind ? 'mark-' + segment.kind : ''">{{ segment.text }}</span>
          }
        </p>

        <div class="tools">
          <button type="button" (click)="markUnclear()" [disabled]="!mutationAllowed() || (conflict() || dirty() || savingRevision() || text().length === 0)">Mark unclear (at caret)</button>
          <button type="button" (click)="markLost()" [disabled]="!mutationAllowed() || (conflict() || dirty() || savingRevision())">Insert lost gap (position)</button>
          <button type="button" (click)="markWordBoundary()" [disabled]="!mutationAllowed() || (conflict() || dirty() || savingRevision())">Insert word boundary (L2 proposal)</button>
          <span class="hint" role="status">position {{ caret() }} (scalars: {{ scalarTotal() }})</span>
          <button type="button" (click)="copyLatestL0()" [disabled]="!workspaceWritable() || (!latestL0())">Copy latest L0</button>
        </div>

        <div class="tools">
          <button type="button" (click)="undo()" [disabled]="!workspaceWritable() || (!canUndo())">Undo</button>
          <button type="button" (click)="save()" [disabled]="!mutationAllowed() || (!dirty() || savingRevision() || conflict() || unconfirmedWrite())">Save revision</button>
          <button type="button" (click)="useLatestSavedText()" [disabled]="!mutationAllowed() || (!dirty() || savingRevision() || latestUnavailable())">Discard text draft</button>
          @if (dirty()) {<span>unsaved</span>}
        </div>

        <fieldset class="orientation">
          <legend>Direction and script</legend>
          <label>Direction
            <select [value]="direction() ?? ''" (change)="onDirection($event)">
              <option value="">inherit / undeclared</option>
              <option value="ltr">left-to-right</option>
              <option value="rtl">right-to-left</option>
              <option value="verticalTopToBottom">vertical, top-to-bottom</option>
              <option value="verticalBottomToTop">vertical, bottom-to-top</option>
            </select>
          </label>
          <label>Script <input [readOnly]="!workspaceWritable()" [value]="script()" (input)="onScript($event)" placeholder="Grek" /></label>
          <button type="button" (click)="applyOrientation()" [disabled]="!mutationAllowed()">Apply orientation</button>
          <span class="hint" role="status">{{ orientationStatus() }}</span>
        </fieldset>

        <fieldset class="annotation-form">
          <legend>Add annotation</legend>
          <label>Kind
            <select [value]="formKind()" (change)="onKind($event)">
              @for (kind of annotationKinds; track kind) { <option [value]="kind">{{ kind }}</option> }
            </select>
          </label>
          <label>Start <input [readOnly]="!workspaceWritable()" type="number" min="0" [value]="formStart()" (input)="onStart($event)" /></label>
          <label>End <input [readOnly]="!workspaceWritable()" type="number" min="0" [value]="formEnd()" (input)="onEnd($event)" /></label>
          @if (formKind() === 'Lost') {
            <label>Extent
              <select [value]="formGapExtent()" (change)="onGapExtent($event)">
                <option value="Unknown">Unknown</option>
                <option value="Known">Known</option>
                <option value="Estimated">Estimated</option>
              </select>
            </label>
            @if (formGapExtent() === 'Estimated') {
              <label>Estimated scalars
                <input [readOnly]="!workspaceWritable()" type="number" min="1" [value]="formEstimatedCount()" (input)="onEstimatedCount($event)" />
              </label>
            }
          }
          @if (formKind() === 'AlternativeReading') {
            <label>Alternative <input [readOnly]="!workspaceWritable()" [value]="formAlternative()" (input)="onAlternative($event)" /></label>
          }
          @if (formKind() === 'Abbreviation') {
            <label>Expansion <input [readOnly]="!workspaceWritable()" placeholder="expanded text" [value]="formExpansion()" (input)="onExpansion($event)" /></label>
            <span class="hint">Creates an L2 revision; L1 stays unchanged.</span>
          }
          @if (formKind() === 'DifferentHand' || formKind() === 'CorrectionSequence') {
            <label>Hand label
              <input [readOnly]="!workspaceWritable()" [value]="formHandLabel()" (input)="onHandLabel($event)" placeholder="unattributed or hand ID" />
            </label>
          }
          @if (formKind() === 'CorrectionSequence') {
            <label>Sequence group
              <input [readOnly]="!workspaceWritable()" [value]="formSequenceId()" (input)="onSequenceId($event)" placeholder="e.g. main" />
            </label>
            <label>Sequence index
              <input [readOnly]="!workspaceWritable()" type="number" min="0" [value]="formSequenceIndex() ?? ''" (input)="onSequenceIndex($event)" />
            </label>
            <label>Order basis
              <select [value]="formOrderBasis()" (change)="onOrderBasis($event)">
                <option value="Unknown">unknown</option>
                <option value="Explicit">explicit</option>
                <option value="Inferred">inferred</option>
              </select>
            </label>
          }
          <label>Note <input [readOnly]="!workspaceWritable()" [value]="formDescription()" (input)="onDescription($event)" /></label>
          <button type="button" (click)="addAnnotation()" [disabled]="!mutationAllowed() || (!canAnnotate())">Add</button>
        </fieldset>

        <details>
          <summary>History ({{ history().length }})</summary>
          <ul>
            @for (rev of history(); track rev.id) {
              <li><code>{{ rev.id }}</code> {{ rev.author }}: {{ rev.text }}</li>
            }
          </ul>
        </details>

        <details open>
          <summary>Annotations ({{ annotations().length }})</summary>
          <ul>
            @for (annotation of annotations(); track annotation.id) {
              <li>
                <b>{{ annotation.kind }}</b>
                [{{ annotation.range.start }}, {{ annotation.range.end }})
                @if (annotation.handLabel) { — hand/sequence: {{ annotation.handLabel }} }
                @if (annotation.description) { — {{ annotation.description }} }
                @if (annotation.alternativeText) {
                  — alt: {{ annotation.alternativeText }}
                  @if (!isAdvisory(annotation) && isCurrentAnnotation(annotation)) {
                    <button type="button" (click)="acceptAlternative(annotation)" [disabled]="!mutationAllowed()">Accept as L2</button>
                  }
                }
                @if (annotation.expansionText) {
                  — expansion: {{ annotation.expansionText }}
                }
                @if (annotation.sequenceId || annotation.sequenceIndex !== undefined) {
                  — sequence: {{ annotation.sequenceId || 'ungrouped' }} / {{ annotation.sequenceIndex ?? '?' }} ({{ annotation.orderBasis || 'unknown' }})
                }
                <button type="button" (click)="retractAnnotation(annotation)" [disabled]="!mutationAllowed()">Retract</button>
              </li>
            } @empty {
              <li>No annotations on this line.</li>
            }
          </ul>
        </details>

        @if (correctionEvents().length > 0) {
          <details open>
            <summary>Correction sequence ({{ correctionEvents().length }})</summary>
            <div class="tools">
              <button type="button" (click)="stepCorrection(-1)" [disabled]="selectedCorrection() <= 0">Previous</button>
              <button type="button" (click)="stepCorrection(1)" [disabled]="selectedCorrection() >= correctionEvents().length - 1">Next</button>
            </div>
            <ol>
              @for (event of correctionEvents(); track event.annotation.id; let i = $index) {
                <li [class.selected]="selectedCorrection() === i">
                  <button type="button" (click)="selectCorrection(i)">{{ event.label }}</button>
                  <span>[{{ event.annotation.range.start }}, {{ event.annotation.range.end }})</span>
                  @if (event.annotation.handLabel) { — hand: {{ event.annotation.handLabel }} }
                  @if (event.annotation.description) { — {{ event.annotation.description }} }
                </li>
              }
            </ol>
            @if (selectedCorrectionEvent(); as event) {
              <p class="hint">Selected intervention spans scalars {{ event.annotation.range.start }}–{{ event.annotation.range.end }}. No text is reconstructed from the sequence.</p>
            }
          </details>
        }

        <details open>
          <summary>Reviews ({{ reviews().length }})</summary>
          <ul>
            @for (review of reviews(); track review.id) {
              <li>
                <b>{{ review.outcome }}</b> · {{ review.reviewer }}
                @if (review.note) { — {{ review.note }} }
              </li>
            } @empty {
              <li>No reviews yet (double review: one accept/dispute per reviewer).</li>
            }
          </ul>
          <div class="tools">
            <button type="button" (click)="recordReview('accept')" [disabled]="!workspaceCanReview() || !mutationAllowed() || (!currentRevision() || dirty() || savingRevision() || conflict())">Accept</button>
            <button type="button" (click)="recordReview('dispute')" [disabled]="!workspaceCanReview() || !mutationAllowed() || (!currentRevision() || dirty() || savingRevision() || conflict())">Dispute</button>
            <button type="button" (click)="recordReview('correct')" [disabled]="!workspaceCanReview() || !mutationAllowed() || (!currentRevision() || savingRevision() || conflict())">Correct with current text</button>
            <span class="hint">reviewer: {{ authorId() }}</span>
          </div>
        </details>

        @if (currentRevision(); as revision) {
          <app-proposal-panel
            [writeBlocked]="proposalWriteBlocked"
            [lineId]="lineId()"
            [revisionId]="revision.id"
            [rangeStart]="0"
            [rangeEnd]="scalarCountOf(revision.text)"
            (saved)="onProposalSaved($event)" (mutationPending)="proposalMutationPending.set($event)" />
        }
      </section>
    } @else {
      <p>Editor offline.</p>
    }
    </div>
  `,
  styles: [`
    .greek { font-size: 1.3rem; width: 24rem; }
    .marked { font-size: 1.3rem; line-height: 1.6; }
    .mark-unclear { text-decoration: underline dotted #b36b00; background: rgba(255, 200, 0, 0.12); }
    .mark-lost { outline: 1px dashed #a33; background: rgba(255, 0, 0, 0.08); }
    .mark-supplied { color: #345; font-style: italic; text-decoration: overline; }
    .mark-alternative { text-decoration: underline double #47a; }
    .mark-erased { text-decoration: line-through #888; }
    .l0 { font-size: 1.15rem; line-height: 1.5; }
    .conf-high { color: #2f6f3e; }
    .conf-medium { color: #9a6a00; }
    .conf-low { color: #b3261e; background: rgba(179, 38, 30, 0.08); }
    .tools { margin-top: 0.4rem; display: flex; gap: 0.5rem; align-items: center; }
    .orientation { margin-top: 0.5rem; display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap; }
    .hint { color: #667; font-size: 0.85rem; }
    .conflict { background: #fff4d6; padding: 0.4rem; }
    .source-excerpt { margin: 0.4rem 0; }
    .source-excerpt img { max-width: 100%; max-height: 8rem; border: 1px solid #ccd; }
    li.selected { background: #e8f0fe; outline: 1px solid #47a; }
  `],
})
export class LineEditor {
  readonly readingSaved = output<ProposalSavedEvent>();
  lineId = input.required<string>();
  surfaceId = input<string>('');
  readonly navigationBlocked = output<boolean>();
  hasNavigationWork(): boolean { return this.dirty() || this.savingRevision() || this.pendingWrite !== null || this.unconfirmedWrite() || this.proposalMutationPending() || this.manualWorkflow() !== null; }
  authorId = input.required<string>();
  imageUrl = input<string>('');
  previewLink = input<string | undefined>(undefined);

  protected readonly cropUrl = signal<string | null>(null);
  protected readonly cropStatus = signal('');
  private cropRequest: Subscription | null = null;
  private cropVersion = 0;
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly payload = signal<LinePayload | null>(null);
  protected readonly text = signal('');
  protected readonly caret = signal(0);
  protected readonly history = signal<LinePayload['history']>([]);
  protected readonly annotations = signal<AnnotationView[]>([]);
  protected readonly direction = signal<string | null>(null);
  protected readonly script = signal('');
  protected readonly orientationStatus = signal('');
  protected readonly conflict = signal(false);
  protected readonly savingRevision = signal(false);
  protected readonly latestUnavailable = signal(false);
  protected readonly loadFailed = signal(false);
  private readonly undoStack = signal<UndoStack[]>([]);
  protected readonly proposalMutationPending = signal(false);
  private readonly lineChanged = new Subject<void>();
  private lineVersion = 0;
  private refreshVersion = 0;
  private annotationVersion = 0;
  private writeVersion = 0;
  private pendingWrite: number | null = null;
  private draftBaseText: string | null = null;
  private draftBaseRevisionId: string | null = null;
  private draftHandle: WorkspaceDraftHandle | undefined;
  private readonly draftStore = new WorkspaceDraftStore();
  protected readonly draftPersistenceStatus = signal('');
  protected readonly unconfirmedWrite = signal(false);
  private readonly scopeQuarantined = signal(false);
  protected readonly manualNotice = signal('');
  protected readonly editorialHistoryUnavailable = signal(false);
  private readonly manualWorkflow = signal<ManualWorkflow | null>(null);
  private manualVersion = 0;
  private manualRecovery: { scope: WorkspaceDraftScope; base: { text: string; revisionId: string | null } } | null = null;

  protected readonly proposalWriteBlocked = (): boolean => {
    return this.savingRevision() || this.pendingWrite !== null || this.unconfirmedWrite() || this.manualWorkflow() !== null;
  };
  protected mutationAllowed(): boolean {
    return this.workspaceWritable() && !this.proposalWriteBlocked() && !this.proposalMutationPending();
  }

  protected workspaceAvailable(): boolean {
    const active = this.access?.isActive() === true && !this.scopeQuarantined();
    if (!active) this.presentEditor(false);
    return active;
  }
  private presentEditor(active: boolean): void {
    const node = this.element.nativeElement.querySelector<HTMLElement>('[data-workspace-line-editor]');
    if (node) { node.hidden = !active; node.inert = !active; }
  }
  private clearCrop(): void {
    this.cropVersion++;
    const request = this.cropRequest; this.cropRequest = null; request?.unsubscribe();
    const url = this.cropUrl(); this.cropUrl.set(null);
    if (url) URL.revokeObjectURL(url);
  }
  private refreshCrop(): void {
    this.clearCrop();
    if (!this.workspaceAvailable() || !this.imageUrl() || this.payload()?.line !== this.lineId()) return;
    const intent = this.access?.capture(); if (!intent) return;
    const line = this.lineId(), surface = this.surfaceId(), version = this.cropVersion;
    const current = () => version === this.cropVersion && intent.current()
      && line === this.lineId() && surface === this.surfaceId() && this.workspaceAvailable();
    this.cropStatus.set('Loading line source…');
    this.cropRequest = this.api.forWorkspaceRequest(intent).lineCrop(line).subscribe({
      next: blob => {
        if (!current()) return;
        try {
          const url = URL.createObjectURL(blob);
          if (!current()) { URL.revokeObjectURL(url); return; }
          const old = this.cropUrl(); this.cropUrl.set(url); if (old) URL.revokeObjectURL(old);
          this.cropStatus.set('');
        } catch { if (current()) this.cropStatus.set('Line source is unavailable.'); }
      },
      error: () => { if (current()) this.cropStatus.set('Line source is unavailable.'); },
    });
  }
  protected workspaceWritable(): boolean {
    return this.workspaceAvailable()
      && ['admin', 'annotator', 'reviewer'].includes(this.access?.state().principal?.role.toLowerCase() ?? '');
  }
  protected workspaceCanReview(): boolean {
    return this.workspaceAvailable()
      && ['admin', 'reviewer'].includes(this.access?.state().principal?.role.toLowerCase() ?? '');
  }
  private initialized = false;
  private previouslyActive = false;
  private unsubscribeAccess: (() => void) | null = null;
  private recoveredUnconfirmedWrites: readonly WorkspaceOpaqueRecord[] = [];
  private pendingAcknowledged = false;
  private pendingSubmission: { text: string; parentRevisionId: string | null } | null = null;

  constructor(api: ApiClient, @Optional() @Inject(WORKSPACE_ACCESS) private readonly access: WorkspaceAccess | null) {
    this.api = api;
    effect(() => this.navigationBlocked.emit(this.hasNavigationWork()));
    this.unsubscribeAccess = access?.subscribe(state => this.onWorkspaceAccess(state)) ?? null;
  }

  private requestApi(): ApiClient | null {
    const intent = this.access?.capture();
    return intent ? this.api.forWorkspaceRequest(intent) : null;
  }

  private onWorkspaceAccess(state: WorkspaceAccessState): void {
    const active = state.status === 'active' && this.workspaceAvailable();
    if (!active) {
      this.presentEditor(false); this.clearCrop();
      this.lineVersion++; this.refreshVersion++; this.annotationVersion++;
      this.retainManualOnBoundary();
      this.lineChanged.next();
      if (this.pendingWrite !== null) {
        this.unconfirmedWrite.set(!this.pendingAcknowledged);
        this.latestUnavailable.set(true);
      }
    } else if (this.initialized && !this.previouslyActive) {
      this.presentEditor(true);
      if (this.payload()?.line === this.lineId()) this.resumeLine();
      else this.loadLine();
    }
    this.previouslyActive = active;
  }

  private resumeLine(): void {
    const request = ++this.refreshVersion;
    this.requestApi()?.lineFresh(this.lineId()).pipe(this.forCurrentLine()).subscribe({
      next: data => {
        if (data.line !== this.lineId() || request !== this.refreshVersion) return;
        if (this.manualWorkflow()) {
          const workflow = this.manualWorkflow()!;
          if (workflow.phase === 'acknowledged' && this.hasAcknowledgedEditorial(data, workflow)) {
            this.reconcile(data, this.draftBaseText); this.finishManual(workflow.token); this.loadAnnotations();
          } else {
            this.payload.set(data); this.history.set([...data.history]); this.writeDraft(this.text());
          }
          this.refreshCrop(); return;
        }
        if (this.unconfirmedWrite() || (this.pendingWrite !== null && !this.pendingAcknowledged)) {
          // A fresh read cannot prove an in-flight write rolled back or did not commit.
          this.payload.set(data); this.history.set([...data.history]);
          this.latestUnavailable.set(false); this.unconfirmedWrite.set(true);
          this.writeDraft(this.text()); this.refreshCrop(); return;
        }
        this.reconcile(data, this.draftBaseText, this.pendingAcknowledged ? this.pendingSubmission?.text : undefined);
        if (this.pendingWrite !== null) this.endWrite(this.pendingWrite);
        this.refreshCrop(); this.loadAnnotations();
      },
      error: () => this.latestUnavailable.set(true),
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['lineId'] && !changes['surfaceId']) {
      if (changes['imageUrl']) this.refreshCrop();
      return;
    }
    if (this.scopeQuarantined() || (this.initialized && (this.manualWorkflow() !== null || this.recoveredUnconfirmedWrites.some(record => record.kind === 'line-editor-manual-workflow'))) || (this.initialized && changes['surfaceId'] && !changes['lineId'])) {
      // A surface-only change cannot reassign this loaded line or its draft.
      // Keep the old mounted copy and fence ABA completions, including a pending initial read.
      this.scopeQuarantined.set(true); this.presentEditor(false); this.clearCrop();
      this.lineVersion++; this.refreshVersion++; this.annotationVersion++;
      this.retainManualOnBoundary();
      this.lineChanged.next();
      if (this.pendingWrite !== null) {
        this.unconfirmedWrite.set(!this.pendingAcknowledged); this.latestUnavailable.set(true);
      }
      return;
    }
    if (!changes['lineId']) return;
    this.initialized = true; this.clearCrop();
    this.draftHandle = undefined;
    this.recoveredUnconfirmedWrites = [];
    this.lineVersion++;
    this.refreshVersion++;
    this.pendingWrite = null;
    this.draftBaseText = null;
    this.draftBaseRevisionId = null;
    this.pendingAcknowledged = false;
    this.pendingSubmission = null;
    this.unconfirmedWrite.set(false);
    this.lineChanged.next();
    this.savingRevision.set(false);
    this.latestUnavailable.set(false);
    this.payload.set(null);
    this.history.set([]);
    this.annotations.set([]);
    this.text.set('');
    this.caret.set(0);
    this.direction.set(null);
    this.script.set('');
    this.orientationStatus.set('');
    this.conflict.set(false);
    this.undoStack.set([]);
    this.draftRestored.set(false);
    this.formKind.set('Unclear');
    this.formStart.set(0);
    this.formEnd.set(0);
    this.formDescription.set('');
    this.formAlternative.set('');
    this.formHandLabel.set('');
    this.formExpansion.set('');
    this.formSequenceId.set('');
    this.formSequenceIndex.set(null);
    this.formOrderBasis.set('Unknown');
    this.selectedCorrection.set(0);
    this.formGapExtent.set('Unknown');
    this.formEstimatedCount.set(1);
    this.loadLine();
  }

  protected retryLoad(): void {
    if (!this.workspaceAvailable()) return;
    if (!this.isLoadedLine()) this.loadLine();
  }

  private loadLine(): void {
    this.loadFailed.set(false);
    this.requestApi()?.lineFresh(this.lineId()).pipe(this.forCurrentLine()).subscribe({
      next: (data) => {
        if (data.line !== this.lineId()) return;
        this.payload.set(data);
        this.history.set([...data.history]);
        this.direction.set(data.direction ?? null);
        this.script.set(data.script ?? '');

        // A recovery record carries its ancestor atomically; legacy text needs comparison.
        const draft = this.readDraft();
        const serverText = data.diplomatic ?? '';
        this.draftBaseText = draft === null ? serverText : draft.base;
        this.draftBaseRevisionId = draft === null ? data.history.at(-1)?.id ?? null : draft.baseRevisionId;
        this.text.set(draft?.text ?? serverText);
        this.refreshCrop();
        if (this.unconfirmedWrite()) {
          this.conflict.set(true); this.latestUnavailable.set(false);
          this.draftRestored.set(true);
          // Matching current text is not a receipt for this particular request.
          // Preserve the recorded ancestor and undo until the write is adjudicated.
          return;
        }
        this.reconcile(data, this.draftBaseText);
        this.draftRestored.set(this.text() !== serverText);
        this.loadAnnotations();
      },
      error: () => { this.loadFailed.set(true); },
    });
  }

  ngOnDestroy(): void {
    this.retainManualOnBoundary();
    this.clearCrop();
    this.unsubscribeAccess?.(); this.unsubscribeAccess = null;
    this.lineVersion++;
    this.lineChanged.next();
    this.lineChanged.complete();
  }

  /** Reads and mutation completions belong to the view that initiated them. A server
   * may already have committed a cancelled write; its response must not affect a new line. */
  private forCurrentLine<T>(): MonoTypeOperatorFunction<T> {
    const line = this.lineId();
    const surface = this.surfaceId();
    const version = this.lineVersion;
    const intent = this.access?.capture();
    const current = () => intent?.current() === true && line === this.lineId() && surface === this.surfaceId() && version === this.lineVersion;
    return source => source.pipe(
      takeUntil(this.lineChanged),
      filter(current),
      catchError(error => current() ? throwError(() => error) : EMPTY),
    );
  }

  private isLoadedLine(): boolean { return this.workspaceAvailable() && this.payload()?.line === this.lineId(); }

  protected readonly draftRestored = signal(false);

  protected readonly annotationKinds = [
    'Unclear', 'Illegible', 'Lost', 'SuppliedEditorial', 'AlternativeReading',
    'Erased', 'Added', 'Overwritten', 'DifferentHand', 'CorrectionSequence',
    'GlyphForm', 'Sign', 'UnidentifiedMark', 'UnannotatedRegion', 'Abbreviation', 'WordBoundary',
  ];
  protected readonly formKind = signal('Unclear');
  protected readonly formStart = signal(0);
  protected readonly formEnd = signal(0);
  protected readonly formDescription = signal('');
  protected readonly formAlternative = signal('');
  protected readonly formHandLabel = signal('');
  protected readonly formExpansion = signal('');
  protected readonly formSequenceId = signal('');
  protected readonly formSequenceIndex = signal<number | null>(null);
  protected readonly formOrderBasis = signal('Unknown');
  protected readonly selectedCorrection = signal(0);
  protected readonly formGapExtent = signal('Unknown');
  protected readonly formEstimatedCount = signal(1);

  protected readonly canAnnotate = computed(() =>
    !this.conflict() && !this.dirty() && !this.savingRevision() && this.manualWorkflow() === null && !this.proposalMutationPending() && this.formStart() >= 0 && this.formEnd() >= this.formStart() && this.formEnd() <= this.scalarTotal());

  protected readonly correctionEvents = computed(() => {
    const events = this.annotations()
      .filter(annotation => annotation.kind === 'CorrectionSequence' || annotation.kind === 'correctionSequence')
      .map((annotation, creationIndex) => ({
        annotation,
        creationIndex,
        group: annotation.sequenceId || 'ungrouped',
        index: annotation.sequenceIndex ?? Number.MAX_SAFE_INTEGER,
        label: annotation.sequenceId
          ? `${annotation.sequenceId} / ${annotation.sequenceIndex ?? '?'}`
          : 'ungrouped',
      }));
    return events.sort((a, b) =>
      a.group.localeCompare(b.group)
      || a.index - b.index
      || a.creationIndex - b.creationIndex);
  });

  protected readonly selectedCorrectionEvent = computed(() => {
    const events = this.correctionEvents();
    return events.length === 0 ? null : events[Math.min(this.selectedCorrection(), events.length - 1)];
  });

  protected selectCorrection(index: number): void {
    if (!this.workspaceAvailable()) return;
    this.selectedCorrection.set(index);
  }

  protected stepCorrection(delta: number): void {
    if (!this.workspaceAvailable()) return;
    const next = this.selectedCorrection() + delta;
    if (next >= 0 && next < this.correctionEvents().length) this.selectedCorrection.set(next);
  }

  private readonly api: ApiClient;

  protected onDirection(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.direction() ?? '';
      return;
    }
    const value = (event.target as HTMLSelectElement).value;
    this.direction.set(value || null);
  }

  protected onScript(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.script();
      return;
    }
    this.script.set((event.target as HTMLInputElement).value);
  }

  protected applyOrientation(): void {
    if (!this.mutationAllowed()) return;
    if (!this.isLoadedLine()) return;
    const submittedDirection = this.direction(), submittedScript = this.script();
    const body = { direction: submittedDirection, script: submittedScript.trim() || null };
    const mutation = this.startManual('orientation', 'L1Diplomatic', body);
    if (!mutation) return;
    mutation.api.setLineOrientation(this.lineId(), body.direction, body.script).pipe(this.forManual(mutation.token)).subscribe({
      next: (result) => {
        const sameDirection = this.direction() === submittedDirection, sameScript = this.script() === submittedScript;
        if (sameDirection) this.direction.set(result.direction ?? null);
        if (sameScript) this.script.set(result.script ?? '');
        this.orientationStatus.set(sameDirection && sameScript ? 'orientation saved' : 'submitted orientation saved; newer field edits remain unsaved');
        this.finishManual(mutation.token);
      },
      error: error => { this.failManual(mutation.token, error); this.orientationStatus.set('orientation save not confirmed'); },
    });
  }

  protected onKind(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.formKind();
      return;
    }
    this.formKind.set((event.target as HTMLSelectElement).value);
  }

  protected onStart(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = String(this.formStart());
      return;
    }
    this.formStart.set(Number((event.target as HTMLInputElement).value));
  }

  protected onEnd(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = String(this.formEnd());
      return;
    }
    this.formEnd.set(Number((event.target as HTMLInputElement).value));
  }

  protected onDescription(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.formDescription();
      return;
    }
    this.formDescription.set((event.target as HTMLInputElement).value);
  }

  protected onAlternative(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.formAlternative();
      return;
    }
    this.formAlternative.set((event.target as HTMLInputElement).value);
  }

  protected onHandLabel(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.formHandLabel();
      return;
    }
    this.formHandLabel.set((event.target as HTMLInputElement).value);
  }

  protected onExpansion(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.formExpansion();
      return;
    }
    this.formExpansion.set((event.target as HTMLInputElement).value);
  }

  protected onSequenceId(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.formSequenceId();
      return;
    }
    this.formSequenceId.set((event.target as HTMLInputElement).value);
  }

  protected onSequenceIndex(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.formSequenceIndex() === null ? '' : String(this.formSequenceIndex());
      return;
    }
    const value = (event.target as HTMLInputElement).value;
    this.formSequenceIndex.set(value === '' ? null : Math.max(0, Number(value) || 0));
  }

  protected onOrderBasis(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.formOrderBasis();
      return;
    }
    this.formOrderBasis.set((event.target as HTMLSelectElement).value);
  }

  protected onGapExtent(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = this.formGapExtent();
      return;
    }
    this.formGapExtent.set((event.target as HTMLSelectElement).value);
  }

  protected onEstimatedCount(event: Event): void {
    if (!this.workspaceWritable()) {
      if (event.target) (event.target as HTMLInputElement).value = String(this.formEstimatedCount());
      return;
    }
    this.formEstimatedCount.set(Math.max(1, Number((event.target as HTMLInputElement).value) || 1));
  }

  protected retractAnnotation(annotation: AnnotationView): void {
    if (!this.mutationAllowed()) return;
    if (!this.isLoadedLine()) return;
    const mutation = this.startManual('annotation-retraction', 'L1Diplomatic', { annotationId: annotation.id, reviewer: this.authorId() });
    if (!mutation) return;
    mutation.api.retractAnnotation(annotation.id, this.authorId()).pipe(this.forManual(mutation.token)).subscribe({
      next: () => { this.finishManual(mutation.token); this.loadAnnotations(); },
      error: error => this.failManual(mutation.token, error),
    });
  }

  protected isAdvisory(annotation: AnnotationView): boolean {
    return ['Translation', 'translation', 'StructuredExtraction', 'structuredExtraction',
      'LexicalAnnotation', 'lexicalAnnotation', 'MorphologicalAnnotation',
      'morphologicalAnnotation'].includes(annotation.kind);
  }

  protected isCurrentAnnotation(annotation: AnnotationView): boolean {
    const current = this.currentRevision();
    return !this.conflict() && !this.savingRevision() && current !== null && annotation.anchoredRevisionId === current.id
      && this.text() === current.text;
  }

  /** Compose an L2 editorial revision from the current L1 text and an alternative span. */
  protected acceptAlternative(annotation: AnnotationView): void {
    if (!this.mutationAllowed()) return;
    if (this.isAdvisory(annotation) || !this.isCurrentAnnotation(annotation)) return;
    const alternative = annotation.alternativeText;
    if (!alternative || !this.editorialShortcutAllowed()) return;
    // Editorial revisions have their own chain; an L1 parent is invalid for this write.
    const parent = [...(this.payload()?.revisions ?? [])].reverse()
      .find(revision => revision.layer === 'L2Editorial' || revision.layer === 'l2Editorial')?.id;
    const composed = applyScalarEdit(this.text(), annotation.range.start, annotation.range.end, alternative);
    // merge=true with a null parent is the server's atomic expected-empty guard.
    // A known parent uses strict append; never merge a span whose offsets we recorded directly.
    const expectEmpty = parent === undefined;
    const mutation = this.startManual('editorial-alternative', 'L2Editorial',
      { text: composed, authorId: this.authorId(), parentRevisionId: parent ?? null, merge: expectEmpty });
    if (!mutation) return;
    mutation.api.appendRevision(this.lineId(), 'L2Editorial', composed, this.authorId(), parent, expectEmpty)
      .pipe(this.forManual(mutation.token)).subscribe({
        next: result => this.acknowledgeEditorial(mutation.token, result.revisionId),
        error: error => this.failManual(mutation.token, error),
      });
  }

  protected addAnnotation(): void {
    if (!this.mutationAllowed()) return;
    if (!this.isLoadedLine() || !this.canAnnotate()) return;
    const kind = this.formKind();
    if (kind === 'Abbreviation') {
      this.addExpansion();
      return;
    }
    const submittedForm = { description: this.formDescription(), alternative: this.formAlternative(), expansion: this.formExpansion() };
    const body = {
      kind,
      anchoredRevisionId: this.currentRevisionId(),
      range: { start: this.formStart(), end: this.formEnd() },
      description: this.formDescription() || null,
      alternativeText: kind === 'AlternativeReading' ? this.formAlternative() || null : null,
      handLabel: kind === 'DifferentHand' || kind === 'CorrectionSequence' ? this.formHandLabel() || null : null,
      gapExtent: kind === 'Lost' ? this.formGapExtent() : null,
      estimatedScalarCount: kind === 'Lost' && this.formGapExtent() === 'Estimated' ? this.formEstimatedCount() : null,
      sequenceId: kind === 'CorrectionSequence' ? this.formSequenceId() || null : null,
      sequenceIndex: kind === 'CorrectionSequence' ? this.formSequenceIndex() : null,
      orderBasis: kind === 'CorrectionSequence' ? this.formOrderBasis() : null,
    };
    const mutation = this.startManual('annotation-add', 'L1Diplomatic', body);
    if (!mutation) return;
    mutation.api.addAnnotation(this.lineId(), body).pipe(this.forManual(mutation.token)).subscribe({
      next: () => {
        this.finishManual(mutation.token);
        if (this.formDescription() === submittedForm.description) this.formDescription.set('');
        if (this.formAlternative() === submittedForm.alternative) this.formAlternative.set('');
        if (this.formExpansion() === submittedForm.expansion) this.formExpansion.set('');
        this.loadAnnotations();
      },
      error: error => this.failManual(mutation.token, error),
    });
  }

  /** Create an L2 expansion revision first, then a reversible L1→L2 annotation link. */
  private addExpansion(): void {
    const expansionInput = this.formExpansion();
    const expansion = expansionInput.trim();
    const start = this.formStart(), end = this.formEnd();
    if (!expansion || start >= end || !this.editorialShortcutAllowed()) return;
    const composed = applyScalarEdit(this.text(), start, end, expansion);
    const sourceRevision = this.currentRevisionId();
    const descriptionInput = this.formDescription();
    const description = descriptionInput || null;
    // L2 has its own chain; do not name the L1 revision as its parent.
    const followup = { kind: 'Abbreviation', anchoredRevisionId: sourceRevision, range: { start, end },
      expansionText: expansion, expansionRevisionId: null,
      expansionRange: { start, end: start + scalarCount(expansion) }, description };
    const parent = [...(this.payload()?.revisions ?? [])].reverse()
      .find(revision => revision.layer === 'L2Editorial' || revision.layer === 'l2Editorial')?.id;
    const expectEmpty = parent === undefined;
    const mutation = this.startManual('abbreviation-expansion', 'L2Editorial',
      { text: composed, authorId: this.authorId(), parentRevisionId: parent ?? null, merge: expectEmpty }, followup);
    if (!mutation) return;
    mutation.api.appendRevision(this.lineId(), 'L2Editorial', composed, this.authorId(), parent, expectEmpty)
      .pipe(this.forManual(mutation.token)).subscribe({
        next: ({ revisionId }) => {
          if (typeof revisionId !== 'string' || !revisionId) { this.failManual(mutation.token, { status: 0 }); return; }
          const body = { ...followup, expansionRevisionId: revisionId };
          this.updateManual(mutation.token, { phase: 'partial', knownRevisionId: revisionId, followup: this.jsonSnapshot(body) });
          if (!mutation.current()) { this.retainManualOnBoundary(); return; }
          this.updateManual(mutation.token, { phase: 'link-pending', linkAttempted: true });
          mutation.api.addAnnotation(this.lineId(), body).pipe(this.forManual(mutation.token)).subscribe({
            next: result => {
              const resultId = result?.annotationId;
              this.updateManual(mutation.token, { knownResultId: typeof resultId === 'string' ? resultId : null, linkAcknowledged: true });
              if (this.formExpansion() === expansionInput) this.formExpansion.set('');
              if (this.formDescription() === descriptionInput) this.formDescription.set('');
              this.acknowledgeEditorial(mutation.token, revisionId);
            },
            error: error => this.failManual(mutation.token, error),
          });
        },
        error: error => this.failManual(mutation.token, error),
      });
  }

  protected readonly reviews = computed(() => this.payload()?.reviews ?? []);

  protected recordReview(outcome: 'accept' | 'correct' | 'dispute'): void {
    if (!this.workspaceCanReview() || !this.mutationAllowed()) return;
    if (!this.isLoadedLine() || !this.currentRevision() || this.savingRevision() || this.conflict()
      || (outcome !== 'correct' && this.dirty())) return;
    const submitted = this.text();
    const write = outcome === 'correct' ? this.beginWrite() : undefined;
    if (write !== undefined) {
      this.pendingSubmission = { text: submitted, parentRevisionId: this.currentRevisionId() };
      this.writeDraft(this.text());
    }
    const body = {
      reviewer: this.authorId(),
      outcome,
      correctionText: outcome === 'correct' ? submitted : null,
      revisionId: this.currentRevision()?.id ?? null,
    };
    const manual = write === undefined ? this.startManual('review-' + outcome, 'L1Diplomatic', body) : null;
    if (write === undefined && !manual) return;
    (manual?.api ?? this.requestApi())?.recordReview(this.lineId(), body).pipe(this.forCurrentLine(),
      filter(() => !manual || this.manualWorkflow()?.token === manual.token), take(1)).subscribe({
      next: () => { if (manual) this.finishManual(manual.token); this.refresh(write === undefined ? undefined : { write, submitted }); },
      error: (error: { status?: number }) => {
        if (write !== undefined) this.failWrite(write, error);
        else if (manual) this.failManual(manual.token, error);
      },
    });
  }

  protected readonly latestL0 = computed(() => {
    const revisions = this.payload()?.revisions ?? [];
    return [...revisions].reverse().find(r => (r.layer === 'L0RawRecognition' || r.layer === 'l0RawRecognition')) ?? null;
  });

  protected copyLatestL0(): void {
    if (!this.workspaceWritable()) return;
    if (!this.isLoadedLine()) return;
    const l0 = this.latestL0();
    if (!l0) return;
    // Copy only: the model output becomes L1 only after a human saves a revision.
    this.text.set(l0.text);
    this.caret.set(scalarCount(l0.text));
    this.writeDraft(l0.text);
  }

  protected readonly l0Segments = computed(() => {
    const l0 = this.latestL0();
    if (!l0) return [];
    const spans = (l0.graphemeConfidences ?? [])
      .filter(s => typeof s.confidence === 'object' && s.confidence !== null && (s.confidence as { value: number }).value !== undefined
        && !Number.isNaN((s.confidence as { value: number }).value))
      .map(s => ({ start: s.start, end: s.end, value: (s.confidence as { value: number }).value }));
    return confidenceSegments(l0.text, spans);
  });

  protected readonly markedSegments = computed(() =>
    markSegments(this.text(), this.annotations().filter(a => this.isCurrentAnnotation(a))
      .map(a => ({ start: a.range.start, end: a.range.end, kind: a.kind }))));

  protected readonly currentRevision = computed(() => {
    if (this.payload()?.line !== this.lineId()) return null;
    const history = this.history();
    return history.length > 0 ? history[history.length - 1] : null;
  });

  protected scalarCountOf(text: string): number { return scalarCount(text); }

  protected onEdit(event: Event): void {
    if (!this.workspaceWritable()) {
      const input = event.target as HTMLInputElement | null;
      if (input) {
        input.value = this.text();
        const offset = Array.from(this.text()).slice(0, this.caret()).join('').length;
        input.setSelectionRange?.(offset, offset);
      }
      return;
    }
    if (!this.isLoadedLine()) return;
    const input = event.target as HTMLInputElement;
    const previous = this.text();
    this.undoStack.update(stack => [...stack, { text: previous, revisionId: this.currentRevisionId() }]);
    this.text.set(input.value);
    // Caret position measured in scalars (grapheme-safe currency) from the UTF-16 index.
    this.caret.set(scalarOffsetFromUtf16(input.value, input.selectionStart ?? 0));
    this.writeDraft(input.value);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (!this.workspaceWritable()) { event.preventDefault(); return; }
    const modifier = event.metaKey || event.ctrlKey;
    if (!modifier) return;
    if (event.key === 's') {
      event.preventDefault();
      if (this.dirty()) this.save();
    } else if (event.key === 'z') {
      event.preventDefault();
      this.undo();
    }
  }

  protected readonly dirty = computed(() =>
    (this.payload()?.diplomatic ?? '') !== this.text());

  protected readonly scalarTotal = computed(() => scalarCount(this.text()));

  protected readonly canUndo = computed(() => this.undoStack().length > 0);

  protected markUnclear(): void {
    if (!this.mutationAllowed()) return;
    if (!this.isLoadedLine() || this.conflict() || this.dirty() || this.savingRevision()) return;
    const start = this.caret();
    const end = Math.min(start + 1, this.scalarTotal());
    if (start >= end) return;
    const body = {
      kind: 'Unclear',
      anchoredRevisionId: this.currentRevisionId(), // binds server-side to the current revision
      range: { start, end },
    };
    const mutation = this.startManual('annotation-shortcut', 'L1Diplomatic', body);
    if (!mutation) return;
    mutation.api.addAnnotation(this.lineId(), body).pipe(this.forManual(mutation.token)).subscribe({
      next: () => { this.finishManual(mutation.token); this.refresh(); },
      error: error => this.failManual(mutation.token, error),
    });
  }

  protected markWordBoundary(): void {
    if (!this.mutationAllowed()) return;
    if (!this.isLoadedLine() || this.conflict() || this.dirty() || this.savingRevision()) return;
    const position = this.caret();
    const body = {
      kind: 'WordBoundary',
      anchoredRevisionId: this.currentRevisionId(),
      range: { start: position, end: position },
      description: 'L2 word-division proposal',
    };
    const mutation = this.startManual('annotation-shortcut', 'L1Diplomatic', body);
    if (!mutation) return;
    mutation.api.addAnnotation(this.lineId(), body).pipe(this.forManual(mutation.token)).subscribe({
      next: () => { this.finishManual(mutation.token); this.refresh(); },
      error: error => this.failManual(mutation.token, error),
    });
  }

  protected markLost(): void {
    if (!this.mutationAllowed()) return;
    if (!this.isLoadedLine() || this.conflict() || this.dirty() || this.savingRevision()) return;
    const body = {
      kind: 'Lost',
      anchoredRevisionId: this.currentRevisionId(),
      range: { start: this.caret(), end: this.caret() },
      gapExtent: 'Unknown',
    };
    const mutation = this.startManual('annotation-lost', 'L1Diplomatic', body);
    if (!mutation) return;
    mutation.api.addAnnotation(this.lineId(), body).pipe(this.forManual(mutation.token)).subscribe({
      next: () => { this.finishManual(mutation.token); this.refresh(); }, error: error => this.failManual(mutation.token, error),
    });
  }

  protected undo(): void {
    if (!this.workspaceWritable()) return;
    const stack = this.undoStack();
    if (stack.length === 0) return;
    const previous = stack[stack.length - 1];
    this.undoStack.set(stack.slice(0, -1));
    this.text.set(previous.text);
    this.writeDraft(previous.text);
  }

  protected save(): void {
    if (!this.mutationAllowed()) return;
    if (!this.isLoadedLine() || !this.dirty() || this.savingRevision() || this.conflict()) return;
    const parent = this.currentRevisionId();
    const submitted = this.text();
    const write = this.beginWrite();
    this.pendingSubmission = { text: submitted, parentRevisionId: parent };
    this.writeDraft(this.text());
    this.requestApi()?.appendRevision(this.lineId(), 'L1Diplomatic', submitted, this.authorId(), parent ?? undefined, true)
      .pipe(this.forCurrentLine()).subscribe({
        next: () => {
          this.refresh({ write, submitted });
        },
        error: (error: { status?: number }) => this.failWrite(write, error),
      });
  }

  /** A new parent is usable only after the existing draft is safely reconciled. */
  protected reloadAfterConflict(): void {
    if (!this.workspaceAvailable()) return;
    if (!this.savingRevision()) this.refresh();
  }

  protected keepDraftAfterCompare(): void {
    if (!this.mutationAllowed()) return;
    if (!this.isLoadedLine() || this.savingRevision() || this.latestUnavailable()) return;
    this.draftBaseText = this.payload()?.diplomatic ?? '';
    this.draftBaseRevisionId = this.currentRevisionId();
    this.conflict.set(false);
    this.undoStack.set([]);
    this.writeDraft(this.text());
  }

  protected useLatestSavedText(): void {
    if (!this.mutationAllowed()) return;
    if (!this.isLoadedLine() || this.savingRevision() || this.latestUnavailable()) return;
    this.draftBaseText = this.payload()?.diplomatic ?? '';
    this.draftBaseRevisionId = this.currentRevisionId();
    this.text.set(this.draftBaseText);
    this.conflict.set(false);
    this.undoStack.set([]);
    this.resetOffsets();
    this.clearDraft();
  }

  private currentRevisionId(): string | null {
    return this.currentRevision()?.id ?? null;
  }

  protected onProposalSaved(saved: ProposalSavedEvent): void {
    if (!this.workspaceAvailable()) return;
    if (!this.isLoadedLine() || saved.lineId !== this.lineId() || saved.sourceRevisionId !== this.currentRevisionId()) return;
    // Existing reconciliation preserves dirty L1 text and refuses to acknowledge an
    // unrelated pending L1 write. The sibling pane can still reload persisted readings.
    this.refresh();
    this.readingSaved.emit(saved);
  }

  private draftScope(): WorkspaceDraftScope | null {
    const owner = this.access?.owner();
    const projectId = this.access?.projectId();
    const surfaceId = this.surfaceId();
    return owner && projectId && surfaceId ? { ...owner, projectId, surfaceId,
      lineId: this.lineId(), layer: 'L1Diplomatic' } : null;
  }

  private readDraft(): { text: string; base: string; baseRevisionId: string | null } | null {
    const scope = this.draftScope();
    if (!scope) {
      this.draftPersistenceStatus.set('Owner-scoped recovery is unavailable. Keep this editor open until your work is saved. Existing ownerless recovery bytes are preserved.');
      return null;
    }
    const result = this.draftStore.load(scope);
    if (result.status === 'found') {
      this.draftHandle = result.handle;
      const undo = result.snapshot.undo;
      if (undo?.kind === 'line-editor-undo' && Array.isArray(undo.data)
        && undo.data.every(entry => typeof entry === 'object' && entry !== null && !Array.isArray(entry)
          && typeof entry['text'] === 'string' && (entry['revisionId'] === null || typeof entry['revisionId'] === 'string'))) {
        this.undoStack.set(undo.data.map(entry => ({ text: (entry as { text: string }).text,
          revisionId: (entry as { revisionId: string | null }).revisionId })));
      }
      this.recoveredUnconfirmedWrites = result.snapshot.unconfirmedWrites;
      if (this.recoveredUnconfirmedWrites.length > 0) {
        // Recovery cannot turn an unconfirmed request into a known rollback.
        this.unconfirmedWrite.set(true); this.savingRevision.set(true);
        if (this.recoveredUnconfirmedWrites.some(record => record.kind === 'line-editor-manual-workflow'))
          this.manualNotice.set('Recovered manual workflow is unresolved. Its original payload is retained; all writes remain blocked without replay.');
      }
      this.draftPersistenceStatus.set('');
      return { text: result.snapshot.draftText, base: result.snapshot.base.text,
        baseRevisionId: result.snapshot.base.revisionId };
    }
    if (result.status !== 'missing')
      this.draftPersistenceStatus.set('Recovery records need explicit adjudication. Their bytes are preserved; this editor has not adopted them.');
    return null;
  }

  private writeDraft(text: string): void {
    const scope = this.manualRecovery?.scope ?? this.draftScope();
    const base = this.manualRecovery?.base ?? (this.draftBaseText === null ? null : { text: this.draftBaseText, revisionId: this.draftBaseRevisionId });
    if (!scope || base === null) {
      this.draftPersistenceStatus.set('Recovery could not be persisted with a verified owner and base. Your mounted draft is retained; keep this editor open.');
      return;
    }
    const result = this.draftStore.save({ schemaVersion: 1, scope,
      base, draftText: text,
      frame: null, undo: { version: 1, kind: 'line-editor-undo', data: this.undoStack().map(entry => ({ ...entry })) },
      geometry: null, readingOrder: null,
      unconfirmedWrites: [ ...this.recoveredUnconfirmedWrites,
        ...(this.manualWorkflow() ? [{ version: 1 as const, kind: 'line-editor-manual-workflow', data: this.jsonSnapshot(this.manualWorkflow()) }] : []),
        ...(this.pendingWrite !== null ? [{ version: 1 as const, kind: 'line-editor-write',
          data: { submitted: this.pendingSubmission?.text ?? null,
            parentRevisionId: this.pendingSubmission?.parentRevisionId ?? null, acknowledged: this.pendingAcknowledged } }] : []),
      ],
    }, this.draftHandle);
    if (result.ok) { this.draftHandle = result.handle; this.draftPersistenceStatus.set(''); }
    else this.draftPersistenceStatus.set('Recovery persistence failed. Your mounted draft is retained; any unresolved operation is retained only in this editor. Keep it open until your work is safely saved.');
  }

  private clearDraft(): void {
    // Publish a resolved owner-scoped snapshot. Old generations and legacy bytes
    // remain available for explicit recovery; this operation never deletes them.
    this.writeDraft(this.text());
    this.draftRestored.set(false);
  }

  private editorialShortcutAllowed(): boolean {
    const revisions = this.payload()?.revisions;
    if (!Array.isArray(revisions)) {
      this.editorialHistoryUnavailable.set(true);
      this.manualNotice.set('Editorial history is unavailable. Reload current layers before using this shortcut; no replacement was submitted.');
      return false;
    }
    this.editorialHistoryUnavailable.set(false);
    const current = [...revisions].reverse().find(revision => revision.layer === 'L2Editorial' || revision.layer === 'l2Editorial');
    if (current && current.text !== (this.payload()?.diplomatic ?? '')) {
      this.manualNotice.set(`Current editorial text is retained: ${current.text}. This shortcut would replace it; anchored layer reconciliation is required.`);
      return false;
    }
    return true;
  }
  protected reloadEditorialHistory(): void {
    if (!this.mutationAllowed()) return;
    this.refresh();
  }

  private forManual<T>(token: number): MonoTypeOperatorFunction<T> {
    return source => source.pipe(this.forCurrentLine(), filter(() => this.manualWorkflow()?.token === token), take(1));
  }

  private jsonSnapshot(value: unknown): WorkspaceJson { return JSON.parse(JSON.stringify(value)) as WorkspaceJson; }

  private startManual(operation: string, targetLayer: string, request: unknown, followup: unknown = null) {
    if (!this.mutationAllowed() || !this.isLoadedLine()) return null;
    const intent = this.access?.capture();
    if (!intent) return null;
    const scope = this.draftScope();
    if (scope && this.draftBaseText !== null)
      this.manualRecovery = { scope: { ...scope }, base: { text: this.draftBaseText, revisionId: this.draftBaseRevisionId } };
    const editorial = [...(this.payload()?.revisions ?? [])].reverse().find(revision =>
      revision.layer === 'L2Editorial' || revision.layer === 'l2Editorial');
    const token = ++this.manualVersion;
    this.manualWorkflow.set({ token, operation, phase: 'pending', lineId: this.lineId(), surfaceId: this.surfaceId(),
      targetLayer, sourceRevisionId: this.currentRevisionId(), request: this.jsonSnapshot(request), followup: this.jsonSnapshot(followup),
      editorialBase: editorial ? { state: 'known', revisionId: editorial.id, text: editorial.text }
        : this.payload()?.revisions === undefined ? { state: 'unknown' } : { state: 'known-empty' },
      knownRevisionId: null, knownResultId: null, linkAttempted: false, linkAcknowledged: false });
    this.manualNotice.set('Manual request pending. Its original payload is retained; other writes and navigation are blocked.');
    this.writeDraft(this.text());
    return { token, api: this.api.forWorkspaceRequest(intent), current: intent.current };
  }

  private updateManual(token: number, changes: Partial<ManualWorkflow>): void {
    const current = this.manualWorkflow();
    if (!current || current.token !== token) return;
    this.manualWorkflow.set({ ...current, ...changes }); this.writeDraft(this.text());
  }
  private retainManualOnBoundary(): void {
    const current = this.manualWorkflow();
    if (!current) return;
    const phase = current.phase === 'acknowledged' ? 'acknowledged' : current.knownRevisionId ? 'partial' : 'unknown';
    this.updateManual(current.token, { phase });
    this.manualNotice.set(current.knownRevisionId
      ? `Manual workflow remains unresolved after L2 ${current.knownRevisionId}. Do not repeat either request.`
      : 'Manual request was interrupted and may have committed. Its original payload is retained; retry is blocked.');
  }
  private failManual(token: number, error: { status?: number }): void {
    const current = this.manualWorkflow();
    if (!current || current.token !== token) return;
    if (current.knownRevisionId) {
      this.updateManual(token, { phase: 'partial' });
      this.manualNotice.set(`L2 ${current.knownRevisionId} was acknowledged but the workflow is incomplete. Both requests remain blocked.`);
    } else if ([400, 409, 412, 422].includes(error?.status ?? 0)) {
      this.finishManual(token);
      this.manualNotice.set('Manual request was refused. The submitted form is retained.');
    } else {
      this.updateManual(token, { phase: 'unknown' });
      this.manualNotice.set('Manual request has not been confirmed and may have committed. Its original payload is retained; retry is blocked.');
    }
  }
  private finishManual(token: number): void {
    if (this.manualWorkflow()?.token !== token) return;
    this.manualWorkflow.set(null); this.manualRecovery = null; this.manualNotice.set(''); this.writeDraft(this.text());
  }
  private hasAcknowledgedEditorial(data: LinePayload, workflow: ManualWorkflow): boolean {
    return data.line === workflow.lineId && !!workflow.knownRevisionId && (data.revisions ?? []).some(revision =>
      revision.id === workflow.knownRevisionId && (revision.layer === 'L2Editorial' || revision.layer === 'l2Editorial'));
  }
  private acknowledgeEditorial(token: number, revisionId: string): void {
    if (this.manualWorkflow()?.token !== token) return;
    if (typeof revisionId !== 'string' || !revisionId) { this.failManual(token, { status: 0 }); return; }
    this.updateManual(token, { phase: 'acknowledged', knownRevisionId: revisionId });
    this.manualNotice.set(`L2 ${revisionId} was acknowledged. Waiting for its current saved parent; do not submit again.`);
    this.requestApi()?.lineFresh(this.lineId()).pipe(this.forManual(token)).subscribe({
      next: data => {
        const workflow = this.manualWorkflow();
        if (!workflow || workflow.token !== token || !this.hasAcknowledgedEditorial(data, workflow)) return;
        this.reconcile(data, this.draftBaseText); this.finishManual(token); this.loadAnnotations();
      },
      error: () => { /* An acknowledged write is not undone by a metadata-read failure. */ },
    });
  }

  private beginWrite(): number {
    const write = ++this.writeVersion;
    this.pendingWrite = write;
    this.pendingAcknowledged = false;
    this.unconfirmedWrite.set(false);
    this.refreshVersion++; // Any older metadata read belongs to the previous parent.
    this.savingRevision.set(true);
    return write;
  }

  private failWrite(write: number, error: { status?: number }): void {
    if (this.pendingWrite !== write) return;
    // These route-contract refusals reject the write. Transport loss, timeouts,
    // parse failures and server errors cannot establish whether it committed.
    if ([400, 409, 412, 422].includes(error?.status ?? 0)) {
      this.endWrite(write);
      if (error.status === 409) { this.conflict.set(true); this.latestUnavailable.set(true); }
      return;
    }
    this.unconfirmedWrite.set(true); this.latestUnavailable.set(true);
    this.savingRevision.set(true); this.writeDraft(this.text());
  }

  private endWrite(write: number): void {
    if (this.pendingWrite !== write) return;
    this.pendingWrite = null;
    this.pendingSubmission = null;
    this.pendingAcknowledged = false;
    this.unconfirmedWrite.set(false);
    this.savingRevision.set(false);
    this.writeDraft(this.text());
  }

  private resetOffsets(): void {
    this.caret.set(0);
    this.formStart.set(0);
    this.formEnd.set(0);
  }

  private reconcile(data: LinePayload, base: string | null, mergePivot?: string): void {
    const remote = data.diplomatic ?? '';
    const previousSaved = this.payload()?.diplomatic ?? '';
    const previousText = this.text();
    const pivot = mergePivot ?? base;
    const result = pivot === null && previousText !== remote
      ? { success: false, text: previousText }
      : mergeText(pivot ?? remote, previousText, remote);
    this.payload.set(data);
    this.history.set([...data.history]);
    this.latestUnavailable.set(false);
    this.conflict.set(!result.success);
    if (result.success) {
      this.draftBaseText = remote;
      this.draftBaseRevisionId = data.history.at(-1)?.id ?? null;
      this.text.set(result.text);
      if (previousSaved !== remote || previousText !== result.text) {
        this.undoStack.set([]);
        this.resetOffsets();
      }
      if (result.text === remote) this.clearDraft();
      else this.writeDraft(result.text);
    } else {
      // Keep the ancestor: naming the newest parent without reconciling loses edits.
      this.draftBaseText = base;
      this.writeDraft(previousText);
    }
  }

  private refresh(acknowledgement?: { write: number; submitted: string }): void {
    if (this.manualWorkflow() !== null || this.recoveredUnconfirmedWrites.length > 0) return;
    if (acknowledgement) {
      if (this.pendingWrite !== acknowledgement.write) return;
      this.pendingAcknowledged = true;
      // The submitted value is a transient merge pivot, not an actual loaded
      // ancestor. Keep the recorded base text and revision paired until fresh data.
      this.writeDraft(this.text());
    } else if (this.pendingWrite !== null) return;
    const request = ++this.refreshVersion;
    this.requestApi()?.lineFresh(this.lineId()).pipe(this.forCurrentLine()).subscribe({
      next: data => {
        if (data.line !== this.lineId() || request !== this.refreshVersion) return;
        this.reconcile(data, this.draftBaseText, acknowledgement?.submitted);
        if (this.editorialHistoryUnavailable() && Array.isArray(data.revisions)) {
          this.editorialHistoryUnavailable.set(false); this.manualNotice.set('');
        }
        if (acknowledgement) this.endWrite(acknowledgement.write);
        this.loadAnnotations();
      },
      error: () => {
        if (request !== this.refreshVersion) return;
        if (acknowledgement) {
          this.endWrite(acknowledgement.write);
          this.conflict.set(true);
          this.latestUnavailable.set(true);
        }
        // Keep the draft and its base; a successful write still needs its new parent.
      },
    });
  }

  private loadAnnotations(): void {
    const request = ++this.annotationVersion;
    this.requestApi()?.annotations(this.lineId()).pipe(this.forCurrentLine()).subscribe({
      next: list => { if (request === this.annotationVersion) this.annotations.set(list); },
      error: () => { if (request === this.annotationVersion) this.annotations.set([]); },
    });
  }
}
