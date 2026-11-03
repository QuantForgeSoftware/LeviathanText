/** Read-only project integrity/overlap checks with CSV export. */
import { Component, OnChanges, SimpleChanges, input, signal } from '@angular/core';
import { ApiClient, ProjectIntegrityIssue, ProjectOverlap } from './api-client';

@Component({
  selector: 'app-project-health-panel',
  standalone: true,
  template: `
    <details class="health">
      <summary>Project health (read-only)</summary>
      <div class="tools">
        <button type="button" (click)="load()">Check project</button>
        <button type="button" (click)="downloadIntegrity()">Integrity CSV</button>
        <button type="button" (click)="downloadOverlap()">Overlap CSV</button>
        <span class="hint" role="status">{{ status() }}</span>
      </div>
      @if (loaded()) {
        <p class="hint">{{ issues().length }} integrity issue(s), {{ overlaps().length }} overlap(s).</p>
        @if (issues().length > 0) {
          <ul>
            @for (issue of issues().slice(0, 10); track issue.code + issue.scope) {
              <li><b>{{ issue.code }}</b> {{ issue.scope }} — {{ issue.message }}</li>
            }
          </ul>
        }
        @if (overlaps().length > 0) {
          <ul>
            @for (overlap of overlaps().slice(0, 10); track overlap.kind + overlap.value) {
              <li><b>{{ overlap.kind }}</b> {{ overlap.value }} — {{ overlap.note }}</li>
            }
          </ul>
        }
        <p class="hint">Audits never rewrite or merge source records.</p>
      }
    </details>
  `,
  styles: [`
    .health { border: 1px solid #ccd; border-radius: 6px; padding: 0.5rem 0.75rem; margin: 0.75rem 0; }
    .tools { display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center; margin: 0.5rem 0; }
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class ProjectHealthPanel implements OnChanges {
  projectId = input.required<string>();

  protected readonly issues = signal<ProjectIntegrityIssue[]>([]);
  protected readonly overlaps = signal<ProjectOverlap[]>([]);
  protected readonly loaded = signal(false);
  protected readonly status = signal('');

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnChanges(_changes: SimpleChanges): void {
    this.issues.set([]);
    this.overlaps.set([]);
    this.loaded.set(false);
    this.status.set('');
  }

  protected load(): void {
    this.status.set('checking…');
    this.api.projectIntegrity(this.projectId()).subscribe({
      next: issues => {
        this.issues.set(issues);
        this.api.projectOverlap(this.projectId()).subscribe({
          next: overlaps => {
            this.overlaps.set(overlaps);
            this.loaded.set(true);
            this.status.set('checked');
          },
          error: () => this.status.set('overlap audit unavailable'),
        });
      },
      error: () => this.status.set('integrity audit unavailable'),
    });
  }

  protected downloadIntegrity(): void {
    this.api.integrityCsv(this.projectId()).subscribe({
      next: csv => this.download('integrity.csv', csv),
      error: () => this.status.set('integrity export failed'),
    });
  }

  protected downloadOverlap(): void {
    this.api.overlapCsv(this.projectId()).subscribe({
      next: csv => this.download('overlap.csv', csv),
      error: () => this.status.set('overlap export failed'),
    });
  }

  private download(name: string, content: string): void {
    const url = URL.createObjectURL(new Blob([content], { type: 'text/csv' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = name;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
