/** Experiment registry: comparable runs with configs, metrics and a learning-curve sparkline. */
import { Component, OnInit, signal } from '@angular/core';
import { ApiClient, RunRecord } from './api-client';

@Component({
  selector: 'app-runs-panel',
  standalone: true,
  template: `
    <section>
      <h2>Experiment runs</h2>
      <div class="tools">
        <select [value]="kind()" (change)="onKind($event)">
          <option value="">all kinds</option>
          <option value="recognition">recognition</option>
          <option value="layout">layout</option>
          <option value="evaluation">evaluation</option>
        </select>
        <button type="button" (click)="refresh()">Refresh</button>
        <span class="hint">{{ status() }}</span>
      </div>

      <table>
        <thead><tr><th>Run</th><th>Kind</th><th>Name</th><th>Metric</th><th></th></tr></thead>
        <tbody>
          @for (run of runs(); track run.id) {
            <tr>
              <td><code>{{ run.id }}</code></td>
              <td>{{ run.kind }}</td>
              <td>{{ run.name }}</td>
              <td class="hint">{{ metricSummary(run) }}</td>
              <td><button type="button" (click)="show(run.id)">Details</button></td>
            </tr>
          } @empty {
            <tr><td colspan="5" class="hint">No runs recorded yet (train via the CLI).</td></tr>
          }
        </tbody>
      </table>

      @if (selected(); as run) {
        <div class="detail">
          <h3>{{ run.name }} <small>({{ run.kind }})</small></h3>
          <p class="hint">created {{ run.createdAt }} · dataset {{ run.datasetDigest ?? '—' }}</p>
          <p class="hint">artifacts: {{ run.artifacts.join(' · ') || '—' }}</p>
          @if (run.history.length > 1) {
            <svg viewBox="0 0 100 30" class="spark" role="img" aria-label="learning curve">
              <polyline [attr.points]="sparkline(run.history)" />
            </svg>
          }
          <table>
            <tbody>
              @for (entry of configEntries(run); track entry[0]) {
                <tr><td class="hint">{{ entry[0] }}</td><td>{{ entry[1] }}</td></tr>
              }
            </tbody>
          </table>
          @if (run.notes) { <p class="hint">{{ run.notes }}</p> }
        </div>
      }
    </section>
  `,
  styles: [`
    table { border-collapse: collapse; margin-top: 0.4rem; }
    th, td { border-bottom: 1px solid #dde; padding: 0.2rem 0.5rem; text-align: left; font-size: 0.9rem; }
    .tools { display: flex; gap: 0.5rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
    .spark { width: 220px; height: 70px; display: block; margin: 0.4rem 0; }
    .spark polyline { fill: none; stroke: #2878dc; stroke-width: 1.5; }
  `],
})
export class RunsPanel implements OnInit {
  protected readonly runs = signal<RunRecord[]>([]);
  protected readonly selected = signal<RunRecord | null>(null);
  protected readonly kind = signal('');
  protected readonly status = signal('');

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnInit(): void {
    this.refresh();
  }

  protected onKind(event: Event): void {
    this.kind.set((event.target as HTMLSelectElement).value);
    this.refresh();
  }

  protected refresh(): void {
    this.api.runs(this.kind() || undefined).subscribe({
      next: (runs) => {
        this.runs.set(runs);
        this.status.set(`${runs.length} run(s)`);
      },
      error: () => {
        this.runs.set([]);
        this.status.set('runs unavailable');
      },
    });
  }

  protected show(id: string): void {
    this.api.run(id).subscribe({
      next: run => this.selected.set(run),
      error: () => this.status.set('run details unavailable'),
    });
  }

  protected metricSummary(run: RunRecord): string {
    const keys = ['bestDevCer', 'cer', 'devF1', 'devPrecision', 'devRecall', 'markErrorRate'];
    const parts = keys.filter(k => run.metrics[k] !== undefined)
      .map(k => `${k}=${run.metrics[k].toFixed(3)}`);
    return parts.length > 0 ? parts.join(' ') : '—';
  }

  protected configEntries(run: RunRecord): [string, string][] {
    return Object.entries(run.config).sort(([a], [b]) => a.localeCompare(b));
  }

  protected sparkline(history: number[]): string {
    const min = Math.min(...history);
    const max = Math.max(...history);
    const span = max - min || 1;
    return history.map((value, index) =>
      `${(index / (history.length - 1)) * 100},${30 - ((value - min) / span) * 30}`).join(' ');
  }
}
