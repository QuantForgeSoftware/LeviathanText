/**
 * Line editor (todo Phase 2): polytonic editing with grapheme-safe input, uncertainty
 * and gap annotation tools, and revision-based undo (every edit creates a revision;
 * undo restores the previous revision's text as a NEW revision — history is never
 * deleted). The L2 layer is written separately from L1; the surface view shows both.
 */
import { Component, computed, input, signal } from '@angular/core';
import { ApiClient, AnnotationView, LinePayload, RevisionPayload } from './api-client';
import { applyScalarEdit, confidenceSegments, markSegments, scalarCount, scalarOffsetFromUtf16 } from './text-utils';
import { ProposalPanel } from './proposal-panel';

interface UndoStack {
  text: string;
  revisionId: string | null;
}

@Component({
  selector: 'app-line-editor',
  standalone: true,
  imports: [ProposalPanel],
  template: `
    @if (payload(); as data) {
      <section>
        <h3>Line <code>{{ data.line }}</code></h3>
        @if (imageUrl()) {
          <figure class="source-excerpt">
            <img [src]="cropUrl()" alt="Line source excerpt" />
            <figcaption class="hint"><a [href]="imageUrl()" target="_blank" rel="noopener">Open source capture</a></figcaption>
          </figure>
        }

        <label>Diplomatic (L1)
          <input class="greek" [attr.dir]="direction() === 'rtl' ? 'rtl' : null"
                 [value]="text()" (input)="onEdit($event)" (keydown)="onKeydown($event)" spellcheck="false" />
        </label>
        @if (draftRestored()) {
          <p class="hint">Unsaved draft restored after reload (⌘/Ctrl+S saves, ⌘/Ctrl+Z undoes).</p>
        }
        @if (conflict()) {
          <p class="conflict">
            A newer revision exists. Reload the latest parent, keep your text, then Save again.
            <button type="button" (click)="reloadAfterConflict()">Reload latest</button>
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
          <button type="button" (click)="markUnclear()" [disabled]="caret() === 0 && text().length === 0">Mark unclear (at caret)</button>
          <button type="button" (click)="markLost()">Insert lost gap (position)</button>
          <span class="hint" role="status">position {{ caret() }} (scalars: {{ scalarTotal() }})</span>
          <button type="button" (click)="copyLatestL0()" [disabled]="!latestL0()">Copy latest L0</button>
        </div>

        <div class="tools">
          <button type="button" (click)="undo()" [disabled]="!canUndo()">Undo</button>
          <button type="button" (click)="save()" [disabled]="!dirty()">Save revision</button>
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
          <label>Script <input [value]="script()" (input)="onScript($event)" placeholder="Grek" /></label>
          <button type="button" (click)="applyOrientation()">Apply orientation</button>
          <span class="hint" role="status">{{ orientationStatus() }}</span>
        </fieldset>

        <fieldset class="annotation-form">
          <legend>Add annotation</legend>
          <label>Kind
            <select [value]="formKind()" (change)="onKind($event)">
              @for (kind of annotationKinds; track kind) { <option [value]="kind">{{ kind }}</option> }
            </select>
          </label>
          <label>Start <input type="number" min="0" [value]="formStart()" (input)="onStart($event)" /></label>
          <label>End <input type="number" min="0" [value]="formEnd()" (input)="onEnd($event)" /></label>
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
                <input type="number" min="1" [value]="formEstimatedCount()" (input)="onEstimatedCount($event)" />
              </label>
            }
          }
          @if (formKind() === 'AlternativeReading') {
            <label>Alternative <input [value]="formAlternative()" (input)="onAlternative($event)" /></label>
          }
          @if (formKind() === 'Abbreviation') {
            <label>Expansion <input placeholder="expanded text" [value]="formExpansion()" (input)="onExpansion($event)" /></label>
            <span class="hint">Creates an L2 revision; L1 stays unchanged.</span>
          }
          @if (formKind() === 'DifferentHand' || formKind() === 'CorrectionSequence') {
            <label>Hand label
              <input [value]="formHandLabel()" (input)="onHandLabel($event)" placeholder="unattributed or hand ID" />
            </label>
          }
          @if (formKind() === 'CorrectionSequence') {
            <label>Sequence group
              <input [value]="formSequenceId()" (input)="onSequenceId($event)" placeholder="e.g. main" />
            </label>
            <label>Sequence index
              <input type="number" min="0" [value]="formSequenceIndex() ?? ''" (input)="onSequenceIndex($event)" />
            </label>
            <label>Order basis
              <select [value]="formOrderBasis()" (change)="onOrderBasis($event)">
                <option value="Unknown">unknown</option>
                <option value="Explicit">explicit</option>
                <option value="Inferred">inferred</option>
              </select>
            </label>
          }
          <label>Note <input [value]="formDescription()" (input)="onDescription($event)" /></label>
          <button type="button" (click)="addAnnotation()" [disabled]="!canAnnotate()">Add</button>
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
                  <button type="button" (click)="acceptAlternative(annotation)">Accept as L2</button>
                }
                @if (annotation.expansionText) {
                  — expansion: {{ annotation.expansionText }}
                }
                @if (annotation.sequenceId || annotation.sequenceIndex !== undefined) {
                  — sequence: {{ annotation.sequenceId || 'ungrouped' }} / {{ annotation.sequenceIndex ?? '?' }} ({{ annotation.orderBasis || 'unknown' }})
                }
                <button type="button" (click)="retractAnnotation(annotation)">Retract</button>
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
            <button type="button" (click)="recordReview('accept')" [disabled]="!currentRevision()">Accept</button>
            <button type="button" (click)="recordReview('dispute')" [disabled]="!currentRevision()">Dispute</button>
            <button type="button" (click)="recordReview('correct')" [disabled]="!currentRevision()">Correct with current text</button>
            <span class="hint">reviewer: {{ authorId() }}</span>
          </div>
        </details>

        @if (currentRevision(); as revision) {
          <app-proposal-panel
            [lineId]="lineId()"
            [revisionId]="revision.id"
            [rangeStart]="0"
            [rangeEnd]="scalarTotal()" />
        }
      </section>
    } @else {
      <p>Editor offline.</p>
    }
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
  lineId = input.required<string>();
  authorId = input.required<string>();
  imageUrl = input<string>('');

  protected readonly cropUrl = computed(() => this.api.lineCropUrl(this.lineId()));
  protected readonly payload = signal<LinePayload | null>(null);
  protected readonly text = signal('');
  protected readonly caret = signal(0);
  protected readonly history = signal<RevisionPayload[]>([]);
  protected readonly annotations = signal<AnnotationView[]>([]);
  protected readonly direction = signal<string | null>(null);
  protected readonly script = signal('');
  protected readonly orientationStatus = signal('');
  protected readonly conflict = signal(false);
  private readonly undoStack = signal<UndoStack[]>([]);

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnInit(): void {
    this.api.line(this.lineId()).subscribe({
      next: (data) => {
        this.payload.set(data);
        this.history.set([...data.history]);
        this.direction.set(data.direction ?? null);
        this.script.set(data.script ?? '');

        // Crash recovery: restore an unsaved draft if one exists for this line AND it
        // differs from the server text (drafts never override a newer saved revision).
        const draft = this.readDraft();
        const serverText = data.diplomatic ?? '';
        this.text.set(draft !== null && draft !== serverText ? draft : serverText);
        this.draftRestored.set(draft !== null && draft !== serverText);
        this.loadAnnotations();
      },
      error: () => this.payload.set({ line: this.lineId(), diplomatic: null, history: [] }),
    });
  }

  protected readonly draftRestored = signal(false);

  protected readonly annotationKinds = [
    'Unclear', 'Illegible', 'Lost', 'SuppliedEditorial', 'AlternativeReading',
    'Erased', 'Added', 'Overwritten', 'DifferentHand', 'CorrectionSequence',
    'GlyphForm', 'Sign', 'UnidentifiedMark', 'UnannotatedRegion', 'Abbreviation',
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
    this.formStart() >= 0 && this.formEnd() >= this.formStart() && this.formEnd() <= this.scalarTotal());

  protected readonly correctionEvents = computed(() => {
    const events = this.annotations()
      .filter(annotation => annotation.kind === 'CorrectionSequence')
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
    this.selectedCorrection.set(index);
  }

  protected stepCorrection(delta: number): void {
    const next = this.selectedCorrection() + delta;
    if (next >= 0 && next < this.correctionEvents().length) this.selectedCorrection.set(next);
  }

  private readonly api: ApiClient;

  protected onDirection(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.direction.set(value || null);
  }

  protected onScript(event: Event): void {
    this.script.set((event.target as HTMLInputElement).value);
  }

  protected applyOrientation(): void {
    this.api.setLineOrientation(this.lineId(), this.direction(), this.script().trim() || null).subscribe({
      next: (result) => {
        this.direction.set(result.direction ?? null);
        this.script.set(result.script ?? '');
        this.orientationStatus.set('orientation saved');
      },
      error: () => this.orientationStatus.set('orientation save failed'),
    });
  }

  protected onKind(event: Event): void {
    this.formKind.set((event.target as HTMLSelectElement).value);
  }

  protected onStart(event: Event): void {
    this.formStart.set(Number((event.target as HTMLInputElement).value));
  }

  protected onEnd(event: Event): void {
    this.formEnd.set(Number((event.target as HTMLInputElement).value));
  }

  protected onDescription(event: Event): void {
    this.formDescription.set((event.target as HTMLInputElement).value);
  }

  protected onAlternative(event: Event): void {
    this.formAlternative.set((event.target as HTMLInputElement).value);
  }

  protected onHandLabel(event: Event): void {
    this.formHandLabel.set((event.target as HTMLInputElement).value);
  }

  protected onExpansion(event: Event): void {
    this.formExpansion.set((event.target as HTMLInputElement).value);
  }

  protected onSequenceId(event: Event): void {
    this.formSequenceId.set((event.target as HTMLInputElement).value);
  }

  protected onSequenceIndex(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.formSequenceIndex.set(value === '' ? null : Math.max(0, Number(value) || 0));
  }

  protected onOrderBasis(event: Event): void {
    this.formOrderBasis.set((event.target as HTMLSelectElement).value);
  }

  protected onGapExtent(event: Event): void {
    this.formGapExtent.set((event.target as HTMLSelectElement).value);
  }

  protected onEstimatedCount(event: Event): void {
    this.formEstimatedCount.set(Math.max(1, Number((event.target as HTMLInputElement).value) || 1));
  }

  protected retractAnnotation(annotation: AnnotationView): void {
    this.api.retractAnnotation(annotation.id, this.authorId()).subscribe({
      next: () => this.loadAnnotations(),
      error: () => { /* surfaced by the API; state kept */ },
    });
  }

  /** Compose an L2 editorial revision from the current L1 text and an alternative span. */
  protected acceptAlternative(annotation: AnnotationView): void {
    const alternative = annotation.alternativeText;
    if (!alternative) return;
    const parent = this.currentRevision()?.id;
    const composed = applyScalarEdit(this.text(), annotation.range.start, annotation.range.end, alternative);
    this.api.appendRevision(this.lineId(), 'L2Editorial', composed, this.authorId(), parent)
      .subscribe({
        next: () => this.refresh(),
        error: () => { /* surfaced by the API; state kept */ },
      });
  }

  protected addAnnotation(): void {
    if (!this.canAnnotate()) return;
    const kind = this.formKind();
    if (kind === 'Abbreviation') {
      this.addExpansion();
      return;
    }
    this.api.addAnnotation(this.lineId(), {
      kind,
      anchoredRevisionId: null,
      range: { start: this.formStart(), end: this.formEnd() },
      description: this.formDescription() || null,
      alternativeText: kind === 'AlternativeReading' ? this.formAlternative() || null : null,
      handLabel: kind === 'DifferentHand' || kind === 'CorrectionSequence' ? this.formHandLabel() || null : null,
      gapExtent: kind === 'Lost' ? this.formGapExtent() : null,
      estimatedScalarCount: kind === 'Lost' && this.formGapExtent() === 'Estimated' ? this.formEstimatedCount() : null,
      sequenceId: kind === 'CorrectionSequence' ? this.formSequenceId() || null : null,
      sequenceIndex: kind === 'CorrectionSequence' ? this.formSequenceIndex() : null,
      orderBasis: kind === 'CorrectionSequence' ? this.formOrderBasis() : null,
    }).subscribe({
      next: () => {
        this.formDescription.set('');
        this.formAlternative.set('');
        this.formExpansion.set('');
        this.loadAnnotations();
      },
      error: () => { /* surfaced by the API; state kept */ },
    });
  }

  /** Create an L2 expansion revision first, then a reversible L1→L2 annotation link. */
  private addExpansion(): void {
    const expansion = this.formExpansion().trim();
    const start = this.formStart(), end = this.formEnd();
    if (!expansion || start >= end) return;
    const composed = applyScalarEdit(this.text(), start, end, expansion);
    // L2 has its own chain; do not name the L1 revision as its parent.
    this.api.appendRevision(this.lineId(), 'L2Editorial', composed, this.authorId())
      .subscribe({
        next: ({ revisionId }) => {
          this.api.addAnnotation(this.lineId(), {
            kind: 'Abbreviation',
            anchoredRevisionId: null,
            range: { start, end },
            expansionText: expansion,
            expansionRevisionId: revisionId,
            expansionRange: { start, end: start + scalarCount(expansion) },
            description: this.formDescription() || null,
          }).subscribe({
            next: () => {
              this.formExpansion.set('');
              this.formDescription.set('');
              this.refresh();
            },
            error: () => { /* surfaced by the API; state kept */ },
          });
        },
        error: () => { /* surfaced by the API; state kept */ },
      });
  }

  protected readonly reviews = computed(() => this.payload()?.reviews ?? []);

  protected recordReview(outcome: 'accept' | 'correct' | 'dispute'): void {
    this.api.recordReview(this.lineId(), {
      reviewer: this.authorId(),
      outcome,
      correctionText: outcome === 'correct' ? this.text() : null,
    }).subscribe({
      next: () => this.refresh(),
      error: () => { /* surfaced by the API; state kept */ },
    });
  }

  protected readonly latestL0 = computed(() => {
    const revisions = this.payload()?.revisions ?? [];
    return [...revisions].reverse().find(r => r.layer === 'L0RawRecognition') ?? null;
  });

  protected copyLatestL0(): void {
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
    markSegments(this.text(), this.annotations().map(a => ({ start: a.range.start, end: a.range.end, kind: a.kind }))));

  protected readonly currentRevision = computed(() => {
    const history = this.history();
    return history.length > 0 ? history[history.length - 1] : null;
  });

  protected onEdit(event: Event): void {
    const input = event.target as HTMLInputElement;
    const previous = this.text();
    this.undoStack.update(stack => [...stack, { text: previous, revisionId: this.currentRevisionId() }]);
    this.text.set(input.value);
    // Caret position measured in scalars (grapheme-safe currency) from the UTF-16 index.
    this.caret.set(scalarOffsetFromUtf16(input.value, input.selectionStart ?? 0));
    this.writeDraft(input.value);
  }

  protected onKeydown(event: KeyboardEvent): void {
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
    const selectionStart = this.caret();
    this.api.addAnnotation(this.lineId(), {
      kind: 'Unclear',
      anchoredRevisionId: null, // binds server-side to the current revision
      range: { start: selectionStart, end: selectionStart },
    }).subscribe({
      next: () => this.refresh(),
      error: () => { /* surfaced by the API; the editor keeps its state */ },
    });
  }

  protected markLost(): void {
    this.api.addAnnotation(this.lineId(), {
      kind: 'Lost',
      anchoredRevisionId: null,
      range: { start: this.caret(), end: this.caret() },
      gapExtent: 'Unknown',
    }).subscribe({ next: () => this.refresh() });
  }

  protected undo(): void {
    const stack = this.undoStack();
    if (stack.length === 0) return;
    const previous = stack[stack.length - 1];
    this.undoStack.set(stack.slice(0, -1));
    this.text.set(previous.text);
  }

  protected save(): void {
    const parent = this.currentRevisionId();
    this.api.appendRevision(this.lineId(), 'L1Diplomatic', this.text(), this.authorId(), parent ?? undefined)
      .subscribe({
        next: () => {
          this.conflict.set(false);
          this.clearDraft();
          this.refresh();
        },
        error: (error: { status?: number }) => {
          if (error.status === 409) this.conflict.set(true);
        },
      });
  }

  /** Keep the user's draft text but advance the parent revision after a 409. */
  protected reloadAfterConflict(): void {
    this.api.line(this.lineId()).subscribe({
      next: data => {
        this.payload.set(data);
        this.history.set([...data.history]);
        this.conflict.set(false);
        this.loadAnnotations();
      },
      error: () => { /* surfaced by the API; keep the draft */ },
    });
  }

  private currentRevisionId(): string | null {
    const history = this.history();
    return history.length > 0 ? history[history.length - 1].id : null;
  }

  private draftKey(): string {
    return `leviathan:draft:${this.lineId()}`;
  }

  private readDraft(): string | null {
    try {
      return globalThis.localStorage?.getItem(this.draftKey()) ?? null;
    } catch {
      return null; // storage unavailable (private mode): drafts degrade to none
    }
  }

  private writeDraft(text: string): void {
    try {
      globalThis.localStorage?.setItem(this.draftKey(), text);
    } catch {
      /* ignore: no draft persistence available */
    }
  }

  private clearDraft(): void {
    try {
      globalThis.localStorage?.removeItem(this.draftKey());
    } catch {
      /* ignore */
    }
    this.draftRestored.set(false);
  }

  private refresh(): void {
    this.api.line(this.lineId()).subscribe(data => {
      this.payload.set(data);
      this.text.set(data.diplomatic ?? '');
      this.history.set([...data.history]);
      this.undoStack.set([]);
      this.loadAnnotations();
    });
  }

  private loadAnnotations(): void {
    this.api.annotations(this.lineId()).subscribe({
      next: list => this.annotations.set(list),
      error: () => this.annotations.set([]),
    });
  }
}
