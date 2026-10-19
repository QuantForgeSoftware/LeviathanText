/**
 * Dual-pane reading view (todo Phase 2 gate: diplomatic and editorial readings side by
 * side, with the image link preserved). Left: L1 diplomatic; right: L2 editorial (or a
 * placeholder when no editorial reading exists). Scalar totals are shown per pane so
 * wire-level combining-sequence breakage is visible immediately.
 */
import { Component, computed, input, signal, OnInit } from '@angular/core';
import { ApiClient } from './api-client';
import { scalarCount, scalarDiff } from './text-utils';

@Component({
  selector: 'app-dual-pane',
  standalone: true,
  template: `
    <section>
      <h3>Line <code>{{ lineId() }}</code></h3>
      <div class="panes">
        <div class="pane">
          <h4>Diplomatic (L1)</h4>
          @if (hasEditorial() && !diff().equal) {
            <p class="greek">{{ diff().prefix }}<span class="changed">{{ diff().aMiddle }}</span>{{ diff().suffix }}</p>
          } @else {
            <p class="greek">{{ diplomatic() }}</p>
          }
          <p class="hint">{{ scalarCountOf(diplomatic()) }} scalars</p>
        </div>
        <div class="pane">
          <h4>Editorial (L2)</h4>
          @if (hasEditorial() && !diff().equal) {
            <p class="greek">{{ diff().prefix }}<span class="changed">{{ diff().bMiddle }}</span>{{ diff().suffix }}</p>
          } @else {
            <p class="greek">{{ editorial() }}</p>
          }
          <p class="hint">
            {{ hasEditorial() ? scalarCountOf(editorial()) + ' scalars' : 'no editorial reading yet' }}
            @if (hasEditorial() && !diff().equal) { · prefix/suffix diff, not word alignment }
          </p>
        </div>
      </div>
      <a [href]="imageLink()" target="_blank" rel="noopener">original image</a>
    </section>
  `,
  styles: [`
    .panes { display: flex; gap: 2rem; }
    .pane { flex: 1; }
    .greek { font-size: 1.4rem; margin: 0; }
    .hint { color: #667; font-size: 0.85rem; }
    .changed { background: rgba(255, 200, 0, 0.28); border-bottom: 1px solid #c90; }
  `],
})
export class DualPane {
  lineId = input.required<string>();
  imageLink = input.required<string>();

  protected readonly diplomatic = signal('—');
  protected readonly editorial = signal('—');
  protected readonly hasEditorial = signal(false);
  protected readonly diff = computed(() => scalarDiff(this.diplomatic(), this.editorial()));

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnInit(): void {
    this.api.line(this.lineId()).subscribe(data => {
      this.diplomatic.set(data.diplomatic ?? '—');
    });
    this.api.lineLayerHistory(this.lineId(), 'L2Editorial').subscribe(history => {
      const latest = history.length > 0 ? history[history.length - 1].text : null;
      this.hasEditorial.set(latest !== null);
      this.editorial.set(latest ?? '—');
    });
  }

  protected scalarCountOf(text: string): number {
    let count = 0;
    for (const _ of text) count++;
    return count;
  }
}
