/**
 * Dual-pane reading view (todo Phase 2 gate: diplomatic and editorial readings side by
 * side, with the image link preserved). Left: L1 diplomatic; right: L2 editorial (or a
 * placeholder when no editorial reading exists). Scalar totals are shown per pane so
 * wire-level combining-sequence breakage is visible immediately.
 */
import { Component, computed, input, signal, SimpleChanges } from '@angular/core';
import { catchError, combineLatest, of, Subscription } from 'rxjs';
import { WorkspaceComponentScope } from './workspace-component-scope';
import { ApiClient, AnnotationView } from './api-client';
import { scalarCount, scalarDiff } from './text-utils';

@Component({
  selector: 'app-dual-pane',
  standalone: true,
  template: `
    <section data-workspace-protected [hidden]="!workspaceAvailable()" [attr.inert]="!workspaceAvailable() ? '' : null">
      <h3>Line <code>{{ lineId() }}</code></h3>
      <div class="panes">
        <div class="pane">
          <h4>Diplomatic (L1)</h4>
          @if (hasEditorial() && !diff().equal) {
            <p class="greek" [attr.dir]="direction() === 'rtl' || direction() === 'Rtl' ? 'rtl' : null">{{ diff().prefix }}<span class="changed">{{ diff().aMiddle }}</span>{{ diff().suffix }}</p>
          } @else {
            <p class="greek" [attr.dir]="direction() === 'rtl' || direction() === 'Rtl' ? 'rtl' : null">{{ diplomatic() }}</p>
          }
          <p class="hint">{{ scalarCountOf(diplomatic()) }} scalars</p>
          @if (boundaries().length > 0) {
            <p class="boundary greek" [attr.dir]="direction() === 'rtl' || direction() === 'Rtl' ? 'rtl' : null">{{ withBoundaries(diplomatic()) }}</p>
            <p class="hint">· proposed word boundaries (derived; witness text is unchanged)</p>
          }
        </div>
        <div class="pane">
          <h4>Editorial (L2)</h4>
          @if (hasEditorial() && !diff().equal) {
            <p class="greek" [attr.dir]="direction() === 'rtl' || direction() === 'Rtl' ? 'rtl' : null">{{ diff().prefix }}<span class="changed">{{ diff().bMiddle }}</span>{{ diff().suffix }}</p>
          } @else {
            <p class="greek" [attr.dir]="direction() === 'rtl' || direction() === 'Rtl' ? 'rtl' : null">{{ editorial() }}</p>
          }
          <p class="hint">
            {{ hasEditorial() ? scalarCountOf(editorial()) + ' scalars' : 'no editorial reading yet' }}
            @if (hasEditorial() && !diff().equal) { · prefix/suffix diff, not word alignment }
          </p>
          @if (editorialBoundaries().length > 0) {
            <p class="boundary greek" [attr.dir]="direction() === 'rtl' || direction() === 'Rtl' ? 'rtl' : null">{{ withBoundaries(editorial(), editorialBoundaries()) }}</p>
            <p class="hint">· proposed word boundaries (derived; witness text is unchanged)</p>
          }
        </div>
      </div>
      @if (imageLink()) { <a [href]="imageLink()" target="_blank" rel="noopener">Open capture preview</a> }
    </section>
  `,
  styles: [`
    .panes { display: flex; gap: 2rem; }
    .pane { flex: 1; }
    .greek { font-size: 1.4rem; margin: 0; }
    .hint { color: #667; font-size: 0.85rem; }
    .changed { background: rgba(255, 200, 0, 0.28); border-bottom: 1px solid #c90; }
    .boundary { color: #345; }
    .greek[dir="rtl"] { text-align: right; }
  `],
})
export class DualPane {
  lineId = input.required<string>();
  imageLink = input.required<string>();
  refreshToken = input(0);

  protected readonly diplomatic = signal('—');
  protected readonly editorial = signal('—');
  protected readonly hasEditorial = signal(false);
  private readonly annotations = signal<AnnotationView[]>([]);
  private readonly diplomaticRevision = signal<string | null>(null);
  private readonly editorialRevision = signal<string | null>(null);
  protected readonly boundaries = computed(() => this.boundariesFor(this.diplomaticRevision()));
  protected readonly editorialBoundaries = computed(() => this.boundariesFor(this.editorialRevision()));
  protected readonly direction = signal<string | null>(null);
  protected readonly diff = computed(() => scalarDiff(this.diplomatic(), this.editorial()));

  private readonly baseApi: ApiClient;
  private readonly scope: WorkspaceComponentScope;
  private ready = false;
  protected workspaceAvailable(): boolean { return this.scope.active(); }
  private get api(): ApiClient { const lineId = this.lineId(); return this.scope.request(this.baseApi, undefined, () => this.lineId() === lineId); }
  private request: Subscription | undefined;
  private loadVersion = 0;

  constructor(api: ApiClient) {
    this.baseApi = api;
    this.scope = new WorkspaceComponentScope(state => {
      this.loadVersion++; this.request?.unsubscribe();
      if (this.ready && state.status === 'active') this.load();
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['lineId'] && !changes['refreshToken']) return;
    this.ready = true; this.load();
  }

  private load(): void {
    if (!this.workspaceAvailable()) return;
    const version = ++this.loadVersion;
    const lineId = this.lineId();
    this.request?.unsubscribe();
    this.diplomaticRevision.set(null);
    this.editorialRevision.set(null);
    this.annotations.set([]);
    this.diplomatic.set('—');
    this.editorial.set('—');
    this.hasEditorial.set(false);
    this.direction.set(null);
    this.request = combineLatest({
      line: this.api.lineFresh(this.lineId()),
      annotations: this.api.annotations(this.lineId()).pipe(catchError(() => of([]))),
      history: this.api.lineLayerHistory(this.lineId(), 'L2Editorial').pipe(catchError(() => of([]))),
    }).subscribe({
      next: ({ line, annotations, history }) => {
        if (version !== this.loadVersion || lineId !== this.lineId() || line.line !== lineId) return;
        this.diplomatic.set(line.diplomatic ?? '—');
        this.direction.set(line.direction ?? null);
        this.diplomaticRevision.set(line.history.at(-1)?.id ?? null);
        const latest = history.at(-1);
        this.hasEditorial.set(latest !== undefined);
        this.editorial.set(latest?.text ?? '—');
        this.editorialRevision.set(latest?.id ?? null);
        this.annotations.set(annotations);
      },
      error: () => { /* Keep placeholders when the line itself is unavailable. */ },
    });
  }

  ngOnDestroy(): void { this.loadVersion++; this.request?.unsubscribe(); }

  private boundariesFor(revisionId: string | null): number[] {
    return revisionId === null ? [] : this.annotations()
      .filter(annotation => annotation.anchoredRevisionId === revisionId
        && (annotation.kind === 'WordBoundary' || annotation.kind === 'wordBoundary'))
      .map(annotation => annotation.range.start).sort((a, b) => a - b);
  }

  protected withBoundaries(text: string, boundaries = this.boundaries()): string {
    const scalars = Array.from(text);
    const positions = new Set(boundaries.filter(position => position > 0 && position < scalars.length));
    return scalars.map((scalar, index) => positions.has(index) ? `·${scalar}` : scalar).join('');
  }

  protected scalarCountOf(text: string): number {
    let count = 0;
    for (const _ of text) count++;
    return count;
  }
}
