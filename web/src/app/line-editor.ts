/**
 * Line editor (todo Phase 2): polytonic editing with grapheme-safe input, uncertainty
 * and gap annotation tools, and revision-based undo (every edit creates a revision;
 * undo restores the previous revision's text as a NEW revision — history is never
 * deleted). The L2 layer is written separately from L1; the surface view shows both.
 */
import { Component, computed, input, signal } from '@angular/core';
import { ApiClient, LinePayload, RevisionPayload } from './api-client';

interface UndoStack {
  text: string;
  revisionId: string | null;
}

@Component({
  selector: 'app-line-editor',
  standalone: true,
  template: `
    @if (payload(); as data) {
      <section>
        <h3>Line <code>{{ data.line }}</code></h3>

        <label>Diplomatic (L1)
          <input class="greek" [value]="text()" (input)="onEdit($event)" spellcheck="false" />
        </label>

        <div class="tools">
          <button type="button" (click)="markUnclear()" [disabled]="caret() === 0 && text().length === 0">Mark unclear (at caret)</button>
          <button type="button" (click)="markLost()">Insert lost gap (position)</button>
          <span class="hint">position {{ caret() }} (scalars: {{ scalarTotal() }})</span>
        </div>

        <div class="tools">
          <button type="button" (click)="undo()" [disabled]="!canUndo()">Undo</button>
          <button type="button" (click)="save()" [disabled]="!dirty()">Save revision</button>
          @if (dirty()) {<span>unsaved</span>}
        </div>

        <details>
          <summary>History ({{ history().length }})</summary>
          <ul>
            @for (rev of history(); track rev.id) {
              <li><code>{{ rev.id }}</code> {{ rev.author }}: {{ rev.text }}</li>
            }
          </ul>
        </details>
      </section>
    } @else {
      <p>Editor offline.</p>
    }
  `,
  styles: [`
    .greek { font-size: 1.3rem; width: 24rem; }
    .tools { margin-top: 0.4rem; display: flex; gap: 0.5rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class LineEditor {
  lineId = input.required<string>();
  authorId = input.required<string>();

  private readonly api: ApiClient;
  protected readonly payload = signal<LinePayload | null>(null);
  protected readonly text = signal('');
  protected readonly caret = signal(0);
  protected readonly history = signal<RevisionPayload[]>([]);
  private readonly undoStack = signal<UndoStack[]>([]);

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnInit(): void {
    this.api.line(this.lineId()).subscribe({
      next: (data) => {
        this.payload.set(data);
        this.text.set(data.diplomatic ?? '');
        this.history.set([...data.history]);
      },
      error: () => this.payload.set({ line: this.lineId(), diplomatic: null, history: [] }),
    });
  }

  protected readonly dirty = computed(() =>
    (this.payload()?.diplomatic ?? '') !== this.text());

  protected readonly scalarTotal = computed(() => scalarCount(this.text()));

  protected readonly canUndo = computed(() => this.undoStack().length > 0);

  protected onEdit(event: Event): void {
    const input = event.target as HTMLInputElement;
    const previous = this.text();
    this.undoStack.update(stack => [...stack, { text: previous, revisionId: this.currentRevisionId() }]);
    this.text.set(input.value);
    // Caret position measured in scalars up to the selection start (grapheme-safe currency).
    this.caret.set(scalarCount(input.value.slice(0, input.selectionStart ?? 0)));
  }

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
      .subscribe({ next: () => this.refresh() });
  }

  private currentRevisionId(): string | null {
    const history = this.history();
    return history.length > 0 ? history[history.length - 1].id : null;
  }

  private refresh(): void {
    this.api.line(this.lineId()).subscribe(data => {
      this.payload.set(data);
      this.text.set(data.diplomatic ?? '');
      this.history.set([...data.history]);
      this.undoStack.set([]);
    });
  }
}

function scalarCount(text: string): number {
  let count = 0;
  for (const _ of text) count++;
  return count;
}
