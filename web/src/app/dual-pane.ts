/**
 * Dual-pane reading view (todo Phase 2 gate: diplomatic and editorial readings side by
 * side, with the image link preserved). Left: L1 diplomatic; right: L2 editorial (or a
 * placeholder when no editorial reading exists). Scalar totals are shown per pane so
 * wire-level combining-sequence breakage is visible immediately.
 */
import { Component, input, signal, OnInit } from '@angular/core';
import { ApiClient, RevisionPayload } from './api-client';

@Component({
  selector: 'app-dual-pane',
  standalone: true,
  template: `
    <section>
      <h3>Line <code>{{ lineId() }}</code></h3>
      <div class="panes">
        <div class="pane">
          <h4>Diplomatic (L1)</h4>
          <p class="greek">{{ diplomatic() }}</p>
          <p class="hint">{{ scalarCountOf(diplomatic()) }} scalars</p>
        </div>
        <div class="pane">
          <h4>Editorial (L2)</h4>
          <p class="greek">{{ editorial() }}</p>
          <p class="hint">{{ editorial() === '—' ? 'no editorial reading yet' : scalarCountOf(editorial()) + ' scalars' }}</p>
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
  `],
})
export class DualPane {
  lineId = input.required<string>();
  imageLink = input.required<string>();

  protected readonly diplomatic = signal('—');
  protected readonly editorial = signal('—');

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnInit(): void {
    this.api.line(this.lineId()).subscribe(data => {
      this.diplomatic.set(data.diplomatic ?? '—');
    });
    this.api.lineLayerHistory(this.lineId(), 'L2Editorial').subscribe(history => {
      this.editorial.set(history.length > 0 ? history[history.length - 1].text : '—');
    });
  }

  protected scalarCountOf(text: string): number {
    let count = 0;
    for (const _ of text) count++;
    return count;
  }
}
