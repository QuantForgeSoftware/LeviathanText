/**
 * Surface view: diplomatic text + mandatory loss report (interchange-and-api.md §1.3)
 * with grapheme-aware line rows. Clicking a line shows its scalar span; the display is
 * Unicode-scalar-aware so combining sequences never break (todo Phase 2 gate).
 */
import { Component, computed, signal, OnInit, input } from '@angular/core';
import { ApiClient, DiplomaticExport, ProjectPayload } from './api-client';
import { LineEditor } from './line-editor';
import { DualPane } from './dual-pane';
import { GeometryEditor } from './geometry-editor';

@Component({
  selector: 'app-surface-view',
  standalone: true,
  template: `
    @if (exportData(); as data) {
      <section>
        <h2>Diplomatic text</h2>
        @if (captures().length > 1) {
          <label>View
            <select (change)="onCapture($event)">
              @for (capture of captures(); track capture.id; let index = $index) {
                <option [value]="index" [selected]="index === captureIndex()">view {{ index + 1 }}: {{ capture.imageLocation }}</option>
              }
            </select>
          </label>
        }
        <ol class="lines">
          @for (line of lines(); track $index) {
            <li><span class="greek" (click)="select($index)" role="button" tabindex="0">{{ line }}</span></li>
          }
        </ol>
        <p>Selected scalar span: {{ selectedStart() }}–{{ selectedEnd() }} (of {{ totalScalars() }})</p>

        @if (editingLineId()) {
          <app-line-editor [lineId]="editingLineId()!" [authorId]="'workspace-editor'" />
          <app-dual-pane [lineId]="editingLineId()!" [imageLink]="imageLink()" />
        }

        <p><button type="button" (click)="toggleGeometry()">Geometry / reading order</button></p>
        @if (showGeometry() && imageLink()) {
          <app-geometry-editor [projectId]="projectId()" [surfaceId]="surfaceId()" [imageUrl]="imageLink()" />
        }

        <p class="hint">
          QA profile {{ qaProfile() }}: {{ qaFlagged() }} flagged line(s)
          @for (issue of qaIssues().slice(0, 3); track $index) {
            · {{ issue.lineId }} [{{ issue.scalarOffset }}] {{ issue.category }}
          }
        </p>

        <p class="tools">
          <button type="button" (click)="downloadTei()">Download TEI/EpiDoc</button>
          <button type="button" (click)="downloadTraining()">Download training JSON</button>
          <button type="button" (click)="detectLayout()">Detect layout</button>
          <a [href]="bundleUrl()" download>Download bundle (.zip)</a>
          <span class="hint">{{ exportStatus() }}</span>
        </p>

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
  imports: [LineEditor, DualPane, GeometryEditor],
})
export class SurfaceView {
  projectId = input.required<string>();
  surfaceId = input.required<string>();

  protected readonly exportData = signal<DiplomaticExport | null>(null);
  protected readonly selectedStart = signal(0);
  protected readonly selectedEnd = signal(0);
  protected readonly totalScalars = signal(0);
  protected readonly editingLineId = signal<string | null>(null);
  protected readonly imageLink = signal('');
  protected readonly captures = signal<{ id: string; imageLocation?: string }[]>([]);
  protected readonly captureIndex = signal(0);
  protected readonly showGeometry = signal(false);
  protected readonly exportStatus = signal('');
  protected readonly qaProfile = signal('');
  protected readonly qaFlagged = signal(0);
  protected readonly qaIssues = signal<{ lineId: string; scalarOffset: number; category: string; scalar: string }[]>([]);
  private readonly lineIdsByIndex = signal<string[]>([]);

  protected onCapture(event: Event): void {
    const index = Number((event.target as HTMLSelectElement).value);
    this.captureIndex.set(index);
    this.imageLink.set(this.captures()[index]?.imageLocation ?? '');
  }

  protected toggleGeometry(): void {
    this.showGeometry.update(v => !v);
  }

  protected downloadTei(): void {
    this.api.tei(this.projectId(), this.surfaceId()).subscribe({
      next: (xml) => {
        this.download(`${this.surfaceId()}.tei.xml`, xml, 'application/xml');
        this.exportStatus.set('TEI downloaded');
      },
      error: () => this.exportStatus.set('TEI export failed'),
    });
  }

  protected downloadTraining(): void {
    this.api.trainingExport(this.projectId()).subscribe({
      next: (document) => {
        this.download(`${this.projectId()}-training.json`, JSON.stringify(document, null, 2), 'application/json');
        this.exportStatus.set('training export downloaded');
      },
      error: () => this.exportStatus.set('training export failed'),
    });
  }

  protected bundleUrl(): string {
    return `/api/v1/projects/${this.projectId()}/bundle`;
  }

  protected detectLayout(): void {
    this.api.detectLayout(this.surfaceId()).subscribe({
      next: (result: { lineCount: number; method: string }) => {
        this.exportStatus.set(`detected ${result.lineCount} lines (${result.method})`);
        this.api.diplomaticExport(this.projectId(), this.surfaceId()).subscribe(data => {
          this.exportData.set(data);
          this.totalScalars.set(scalarCount(data.text));
        });
      },
      error: () => this.exportStatus.set('layout detection refused (existing layout is never overwritten)'),
    });
  }

  private download(filename: string, content: string, type: string): void {
    const blob = new Blob([content], { type });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }

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

    this.api.profileReport(this.projectId()).subscribe({
      next: (report) => {
        const surface = report.surfaces.find(s => s.surfaceId === this.surfaceId());
        this.qaProfile.set(`${report.profile}@${report.version}`);
        this.qaFlagged.set(surface?.flagged ?? 0);
        this.qaIssues.set(surface?.issues ?? []);
      },
      error: () => this.qaProfile.set('unavailable'),
    });

    // Line ids (in stored order) so clicking a rendered line opens its editor.
    this.api.loadProject(this.projectId()).subscribe(payload => {
      const surface = payload.objects
        .flatMap(o => o.surfaces)
        .find(s => s.id === this.surfaceId());
      this.lineIdsByIndex.set(surface?.lines.map(l => l.id) ?? []);
      // Captures (views) are listed; the selected view drives overlays and links.
      const captureList = (surface?.captures ?? []).map(c => ({ id: c.id, imageLocation: c.imageLocation }));
      this.captures.set(captureList);
      this.captureIndex.set(0);
      this.imageLink.set(captureList[0]?.imageLocation ?? '');
    });
  }

  protected select(lineIndex: number): void {
    const lines = this.lines();
    let start = 0;
    for (let i = 0; i < lineIndex; i++) start += scalarCount(lines[i]) + 1; // +1 for the newline
    this.selectedStart.set(start);
    this.selectedEnd.set(start + scalarCount(lines[lineIndex]));
    this.editingLineId.set(this.lineIdsByIndex()[lineIndex] ?? null);
  }
}

function scalarCount(text: string): number {
  let count = 0;
  for (const _ of text) count++;
  return count;
}
