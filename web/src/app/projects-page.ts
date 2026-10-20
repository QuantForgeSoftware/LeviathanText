import { Component, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ApiClient, ProjectPayload } from './api-client';
import { SurfaceView } from './surface-view';

interface ProjectRow {
  id: string;
  name: string;
}

/**
 * Workspace landing page: collection list (todo §4 web workspace). Operates against
 * the versioned REST contract; when the API is offline the page degrades gracefully.
 */
@Component({
  selector: 'app-projects-page',
  standalone: true,
  template: `
    <section>
      <h2>Collections</h2>
      <button type="button" (click)="create()">New collection</button>
      <p>New id: {{ createdId() }}</p>
      <ul>
        @for (project of projects(); track project.id) {
          <li>
            <button type="button" class="link" (click)="select(project.id)">{{ project.name }}</button>
            <code>{{ project.id }}</code>
          </li>
        } @empty {
          <li>No collections yet (API offline or empty).</li>
        }
      </ul>
    </section>

    @if (selected(); as project) {
      @if (payload(selectedId()!); as payload) {
        <section>
          <h2>{{ payload.name }}</h2>
          @if (stats(); as s) {
            <p class="hint">
              {{ s.surfaces }} surface(s) · {{ s.lines }} line(s), {{ s.transcribedLines }} transcribed ·
              {{ s.annotations }} annotation(s) · {{ s.flaggedLines }} flagged by profile QA
            </p>
          }
          <details class="bulk">
            <summary>Import line text (CSV)</summary>
            <p class="hint">Header must contain <code>line_id,text</code>. Rows become normal revisions.</p>
            <div class="tools">
              <label>CSV file <input type="file" accept=".csv,text/csv" (change)="onCsvFile($event)" /></label>
              <select [value]="importLayer()" (change)="onImportLayer($event)">
                <option value="L1Diplomatic">L1 diplomatic</option>
                <option value="L2Editorial">L2 editorial</option>
              </select>
              <span class="hint">{{ importStatus() }}</span>
            </div>
          </details>

          @if (review(); as r) {
            <p class="hint">
              Review: {{ r.reviewedLines }}/{{ r.transcribedLines }} reviewed ·
              {{ r.doubleReviewedLines }} double-reviewed · {{ r.disputedLines }} disputed ·
              <b>{{ r.needsAdjudication }}</b> need adjudication
              <a [href]="adjudicationUrl()" download>adjudication.csv</a>
            </p>
            @if (r.needsAdjudication > 0) {
              <ul>
                @for (entry of adjudicationLines(); track entry.lineId) {
                  <li>
                    <button type="button" class="link" (click)="openReviewLine(entry)">
                      {{ entry.lineId }}
                    </button>
                    <span class="hint">{{ entry.reviewers.join(', ') }} → {{ entry.outcomes.join(', ') }} · {{ entry.text }}</span>
                  </li>
                }
              </ul>
            }
          }
          <ul>
            @for (object of payload.objects; track object.id) {
              @for (surface of object.surfaces; track surface.id) {
                <li>
                  <button type="button" class="link" (click)="openSurface(surface.id)">
                    surface {{ surface.id }} ({{ surface.kind }}, {{ surface.lines.length }} lines)
                  </button>
                </li>
              }
            }
          </ul>
        </section>
      }
    }

    @if (selectedId()) {
      <section>
        <h3>Search</h3>
        <div class="tools">
          <input [value]="query()" (input)="onQuery($event)" placeholder="word or phrase" aria-label="Search text" />
          <select [value]="searchMode()" (change)="onMode($event)">
            <option value="literal">literal (marks significant)</option>
            <option value="base">base (ignore marks/case)</option>
          </select>
          <button type="button" (click)="runSearch()">Search</button>
          <span class="hint">{{ searchStatus() }}</span>
        </div>
        <ul>
          @for (hit of searchHits(); track hit.revisionId + hit.match.start) {
            <li>
              <button type="button" class="link" (click)="openHit(hit)">{{ hit.lineId }} · {{ hit.layer }}</button>
              <span class="hint">[{{ hit.match.start }}..{{ hit.match.end }}] {{ hit.snippet }}</span>
            </li>
          } @empty {
            <li class="hint">No hits.</li>
          }
        </ul>
      </section>
    }

    @if (openSurfaceId()) {
      <app-surface-view [projectId]="selectedId()!" [surfaceId]="openSurfaceId()!" [focusLineId]="focusLineId()" />
    }
  `,
  styles: [`
    .link { background: none; border: none; color: #334; cursor: pointer; text-decoration: underline; padding: 0; }
    .tools { display: flex; gap: 0.5rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
    li { margin: 0.2rem 0; }
  `],
  imports: [SurfaceView],
})
export class ProjectsPage {
  private readonly api: ApiClient;
  protected readonly projects = signal<ProjectRow[]>([]);
  protected readonly createdId = signal('');
  protected readonly selectedId = signal<string | null>(null);
  protected readonly selected = computed(() =>
    this.projects().find(p => p.id === this.selectedId()) ?? null);
  protected readonly payloads = signal<Record<string, ProjectPayload>>({});
  protected readonly openSurfaceId = signal<string | null>(null);
  protected readonly stats = signal<{
    surfaces: number; lines: number; transcribedLines: number; annotations: number; flaggedLines: number;
  } | null>(null);
  protected readonly importLayer = signal<'L1Diplomatic' | 'L2Editorial'>('L1Diplomatic');
  protected readonly importStatus = signal('');

  protected onImportLayer(event: Event): void {
    this.importLayer.set((event.target as HTMLSelectElement).value as 'L1Diplomatic' | 'L2Editorial');
  }

  protected onCsvFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.selectedId()) return;
    file.text().then(text => {
      this.api.importLineText(this.selectedId()!, text, this.importLayer()).subscribe({
        next: (result) => {
          this.importStatus.set(`applied ${result.applied}, skipped ${result.skipped}`);
          const id = this.selectedId()!;
          this.payloads.update(map => {
            const copy = { ...map };
            delete copy[id];
            return copy;
          });
          this.select(id);
        },
        error: () => this.importStatus.set('import failed (header, ids or revision conflict)'),
      });
    });
  }

  protected readonly review = signal<{
    transcribedLines: number;
    reviewedLines: number;
    doubleReviewedLines: number;
    disputedLines: number;
    needsAdjudication: number;
    lines: {
      lineId: string; surfaceId: string; text: string; decisions: number;
      reviewers: string[]; outcomes: string[]; latestOutcome: string | null;
      doubleReviewed: boolean; needsAdjudication: boolean;
    }[];
  } | null>(null);
  protected readonly focusLineId = signal('');
  protected readonly query = signal('');
  protected readonly searchMode = signal<'literal' | 'base'>('literal');
  protected readonly searchStatus = signal('');
  protected readonly searchHits = signal<{
    surfaceId: string; lineId: string; revisionId: string; layer: string; snippet: string;
    match: { start: number; end: number };
  }[]>([]);

  protected onQuery(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected onMode(event: Event): void {
    this.searchMode.set((event.target as HTMLSelectElement).value as 'literal' | 'base');
  }

  protected runSearch(): void {
    const query = this.query().trim();
    if (!query || !this.selectedId()) return;
    this.api.search(this.selectedId()!, query, this.searchMode()).subscribe({
      next: (result) => {
        this.searchHits.set(result.hits);
        this.searchStatus.set(`${result.totalHits} hit(s)${result.truncated ? ' (truncated)' : ''}`);
      },
      error: () => {
        this.searchHits.set([]);
        this.searchStatus.set('search failed');
      },
    });
  }

  protected openHit(hit: { surfaceId: string; lineId: string }): void {
    this.openSurfaceId.set(hit.surfaceId);
    this.focusLineId.set(hit.lineId);
  }

  protected adjudicationLines(): {
    lineId: string; surfaceId: string; text: string; reviewers: string[]; outcomes: string[];
  }[] {
    return (this.review()?.lines ?? []).filter(l => l.needsAdjudication).slice(0, 10);
  }

  protected openReviewLine(entry: { surfaceId: string; lineId: string }): void {
    this.openHit(entry);
  }

  protected adjudicationUrl(): string {
    return this.api.adjudicationUrl(this.selectedId() ?? '');
  }

  constructor(http: HttpClient) {
    this.api = new ApiClient(http);
    this.api.listProjects().subscribe({
      next: (rows) => this.projects.set(rows ?? []),
      error: () => this.projects.set([]),
    });
  }

  protected create(): void {
    this.api.createProject('workspace-collection-' + (this.projects().length + 1))
      .subscribe({
        next: (p) => this.createdId.set(p.id),
        error: () => this.createdId.set('API unreachable'),
      });
  }

  protected select(id: string): void {
    this.selectedId.set(id);
    this.openSurfaceId.set(null);
    this.stats.set(null);
    this.review.set(null);
    this.api.stats(id).subscribe({
      next: counts => this.stats.set(counts),
      error: () => this.stats.set(null),
    });
    this.api.reviewSummary(id).subscribe({
      next: summary => this.review.set(summary),
      error: () => this.review.set(null),
    });
    if (!this.payloads()[id]) {
      this.api.loadProject(id).subscribe(payload =>
        this.payloads.update(map => ({ ...map, [id]: payload })));
    }
  }

  protected openSurface(surfaceId: string): void {
    this.openSurfaceId.set(surfaceId);
  }

  protected payload(id: string): ProjectPayload | undefined {
    return this.payloads()[id];
  }
}
