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

        <label>Diplomatic (L1)
          <input class="greek" [value]="text()" (input)="onEdit($event)" (keydown)="onKeydown($event)" spellcheck="false" />
        </label>
        @if (draftRestored()) {
          <p class="hint">Unsaved draft restored after reload (⌘/Ctrl+S saves, ⌘/Ctrl+Z undoes).</p>
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
          <span class="hint">position {{ caret() }} (scalars: {{ scalarTotal() }})</span>
          <button type="button" (click)="copyLatestL0()" [disabled]="!latestL0()">Copy latest L0</button>
        </div>

        <div class="tools">
          <button type="button" (click)="undo()" [disabled]="!canUndo()">Undo</button>
          <button type="button" (click)="save()" [disabled]="!dirty()">Save revision</button>
          @if (dirty()) {<span>unsaved</span>}
        </div>

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
                @if (annotation.description) { — {{ annotation.description }} }
                @if (annotation.alternativeText) {
                  — alt: {{ annotation.alternativeText }}
                  <button type="button" (click)="acceptAlternative(annotation)">Accept as L2</button>
                }
                <button type="button" (click)="retractAnnotation(annotation)">Retract</button>
              </li>
            } @empty {
              <li>No annotations on this line.</li>
            }
          </ul>
        </details>

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
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class LineEditor {
  lineId = input.required<string>();
  authorId = input.required<string>();

  protected readonly payload = signal<LinePayload | null>(null);
  protected readonly text = signal('');
  protected readonly caret = signal(0);
  protected readonly history = signal<RevisionPayload[]>([]);
  protected readonly annotations = signal<AnnotationView[]>([]);
  private readonly undoStack = signal<UndoStack[]>([]);

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnInit(): void {
    this.api.line(this.lineId()).subscribe({
      next: (data) => {
        this.payload.set(data);
        this.history.set([...data.history]);

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
    'Erased', 'Added', 'Overwritten', 'DifferentHand', 'GlyphForm', 'Sign',
    'UnidentifiedMark', 'UnannotatedRegion',
  ];
  protected readonly formKind = signal('Unclear');
  protected readonly formStart = signal(0);
  protected readonly formEnd = signal(0);
  protected readonly formDescription = signal('');
  protected readonly formAlternative = signal('');
  protected readonly formGapExtent = signal('Unknown');
  protected readonly formEstimatedCount = signal(1);

  protected readonly canAnnotate = computed(() =>
    this.formStart() >= 0 && this.formEnd() >= this.formStart() && this.formEnd() <= this.scalarTotal());

  private readonly api: ApiClient;

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
    this.api.addAnnotation(this.lineId(), {
      kind,
      anchoredRevisionId: null,
      range: { start: this.formStart(), end: this.formEnd() },
      description: this.formDescription() || null,
      alternativeText: kind === 'AlternativeReading' ? this.formAlternative() || null : null,
      gapExtent: kind === 'Lost' ? this.formGapExtent() : null,
      estimatedScalarCount: kind === 'Lost' && this.formGapExtent() === 'Estimated' ? this.formEstimatedCount() : null,
    }).subscribe({
      next: () => {
        this.formDescription.set('');
        this.formAlternative.set('');
        this.loadAnnotations();
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
          this.clearDraft();
          this.refresh();
        },
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
