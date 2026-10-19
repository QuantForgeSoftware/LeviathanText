/**
 * Surface view: diplomatic text + mandatory loss report (interchange-and-api.md §1.3)
 * with grapheme-aware line rows. Clicking a line shows its scalar span; the display is
 * Unicode-scalar-aware so combining sequences never break (todo Phase 2 gate).
 */
import { Component, computed, signal, OnInit, input } from '@angular/core';
import { ApiClient, DiplomaticExport } from './api-client';

@Component({
  selector: 'app-surface-view',
  standalone: true,
  template: `
    @if (exportData(); as data) {
      <section>
        <h2>Diplomatic text</h2>
        <ol class="lines">
          @for (line of lines(); track $index) {
            <li><span class="greek" (click)="select($index)" role="button" tabindex="0">{{ line }}</span></li>
          }
        </ol>
        <p>Selected scalar span: {{ selectedStart() }}–{{ selectedEnd() }} (of {{ totalScalars() }})</p>

        <h3>Loss report <code>{{ data.lossReport.format }}</code></h3>
        @if (data.lossReport.entries.length === 0) {
          <p>Lossless: nothing was dropped.</p>
        } @else {
          <ul>
            @for (entry of data.lossReport.entries; track entry.category) {
              <li><b>{{ entry.category }}</b> ×{{ entry.count }} — {{ entry.description }}</li>
            }
          </ul>
        }
      </section>
    } @else {
      <p>Loading surface…</p>
    }
  `,
  styles: [`
    .greek { font-size: 1.3rem; cursor: pointer; }
    .lines { line-height: 2; }
  `],
})
export class SurfaceView {
  projectId = input.required<string>();
  surfaceId = input.required<string>();

  protected readonly exportData = signal<DiplomaticExport | null>(null);
  protected readonly selectedStart = signal(0);
  protected readonly selectedEnd = signal(0);
  protected readonly totalScalars = signal(0);

  protected readonly lines = computed(() => (this.exportData()?.text ?? '').split('\n'));

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnInit(): void {
    this.api.diplomaticExport(this.projectId(), this.surfaceId())
      .subscribe({
        next: (data) => {
          this.exportData.set(data);
          this.totalScalars.set(scalarCount(data.text));
        },
        error: () => this.exportData.set({ text: '', lossReport: { format: 'unavailable', contractVersion: '-', entries: [] } }),
      });
  }

  protected select(lineIndex: number): void {
    const lines = this.lines();
    let start = 0;
    for (let i = 0; i < lineIndex; i++) start += scalarCount(lines[i]) + 1; // +1 for the newline
    this.selectedStart.set(start);
    this.selectedEnd.set(start + scalarCount(lines[lineIndex]));
  }
}

function scalarCount(text: string): number {
  let count = 0;
  for (const _ of text) count++;
  return count;
}
