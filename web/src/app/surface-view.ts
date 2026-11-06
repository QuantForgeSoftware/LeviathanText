/**
 * Surface view: diplomatic text + mandatory loss report (interchange-and-api.md §1.3)
 * with grapheme-aware line rows. Clicking a line shows its scalar span; the display is
 * Unicode-scalar-aware so combining sequences never break (todo Phase 2 gate).
 */
import { Component, computed, effect, signal, OnInit, OnDestroy, OnChanges, SimpleChanges, input, output, viewChild, inject, untracked } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { authorId } from './identity';
import { ApiKeyStorageKey, apiKey, apiKeyRevision, cacheCredentialGeneration } from './auth';
import { cacheAuthorityRevision, ensureCacheAuthorityRevision } from './offline-cache-storage';
import { Subscription, map, switchMap } from 'rxjs';
import { WorkspaceComponentScope, WorkspaceAccessGate, uniqueWorkspaceProject } from './workspace-component-scope';
import { WORKSPACE_ACCESS } from './workspace-access-types';
import type { WorkspaceAccess, WorkspaceAccessState } from './workspace-access-types';
import { ApiClient, CaptureDisplayDescriptor, DiplomaticExport, ProjectPayload } from './api-client';
import { LineEditor } from './line-editor';
import { ProposalSavedEvent } from './proposal-panel';
import { DualPane } from './dual-pane';
import { GeometryEditor } from './geometry-editor';
import { SessionPanel } from './session-panel';
import { FragmentPanel } from './fragment-panel';

@Component({
  selector: 'app-surface-view',
  providers: [{ provide: WORKSPACE_ACCESS, useFactory: () => inject(SurfaceView).descendantAccess }],
  standalone: true,
  template: `
    @if (!workspaceAvailable()) { <p role="status">Surface access is paused. Your mounted text and geometry work is retained.</p> }
    @if (exportData(); as data) {
      <section data-workspace-protected [hidden]="!workspaceAvailable()" [attr.inert]="!workspaceAvailable() ? '' : null">
        <h2>Diplomatic text</h2>
        @if (captures().length > 1) {
          <label>View
            <select (change)="onCapture($event)" [disabled]="captureSwitchBlocked()">
              @for (capture of captures(); track capture.id; let index = $index) {
                <option [value]="index" [selected]="index === captureIndex()">view {{ index + 1 }}</option>
              }
            </select>
          </label>
        }
        <ol class="lines">
          @for (line of lines(); track $index) {
            <li>
              <span class="greek"
                    [attr.dir]="lineDirection($index) === 'rtl' ? 'rtl' : null"
                    [class.vertical]="isVerticalLine($index)"
                    [attr.title]="lineScript($index) ? 'script: ' + lineScript($index) : null"
                    (click)="select($index)" role="button" tabindex="0">{{ line }}</span>
            </li>
          }
        </ol>
        <p>Selected scalar span: {{ selectedStart() }}–{{ selectedEnd() }} (of {{ totalScalars() }})</p>

        @if (editingLineId() && editorMounted()) {
          <p class="tools">
            <button type="button" (click)="navigateLine(-1)" [disabled]="!hasNeighbour(-1)">← Previous line</button>
            <span class="hint">editing as {{ editorName }}</span>
            <button type="button" (click)="navigateLine(1)" [disabled]="!hasNeighbour(1)">Next line →</button>
          </p>
          <app-line-editor [lineId]="editingLineId()!" [surfaceId]="surfaceId()" [authorId]="editorName" [imageUrl]="imageLink()" [previewLink]="selectedLineImageLink()" (readingSaved)="onReadingSaved($event)" (navigationBlocked)="lineNavigationBlocked.set($event)" />
          <app-dual-pane [lineId]="editingLineId()!" [imageLink]="selectedLineImageLink()" [refreshToken]="readingRefreshToken()" />
        }

        <p>
          <button type="button" (click)="toggleGeometry()">
            {{ showGeometry() ? 'View only' : 'Edit geometry / reading order' }}
          </button>
        </p>
        @if (captures().length > 0) {
          <p role="status">{{ captureStatus() }}</p>
          <button type="button" (click)="retryPreview()">Retry image</button>
          <app-geometry-editor
            [projectId]="projectId()"
            [surfaceId]="surfaceId()"
            [imageUrl]="imageLink()"
            [readOnly]="!showGeometry() || !captureDisplay()" [structureBlocked]="lineHasNavigationWork()"
            [display]="captureDisplay()"
            (captureSwitchBlocked)="captureSwitchBlocked.set($event)"
            (imageFailed)="onPreviewImageFailed()"
            (lineSelected)="selectLineId($event)" />
        }

        @if (presence().length > 0) {
          <p class="hint">Editing now: {{ presence().join(', ') }}</p>
        }
        @if (sessionNode()) {
          <p class="hint">Editing session on {{ sessionNode() }}</p>
        }
        <app-session-panel [surfaceId]="surfaceId()" />

        <p class="hint">
          QA profile {{ qaProfile() }}: {{ qaFlagged() }} flagged line(s)
          @for (issue of qaIssues().slice(0, 3); track $index) {
            · {{ issue.lineId }} [{{ issue.scalarOffset }}] {{ issue.category }}
          }
        </p>

        <p class="tools">
          @if (models().length > 0) {
            <label>Model
              <select [value]="selectedModel()" (change)="onModel($event)">
                @for (model of models(); track model.name) { <option [value]="model.name">{{ model.name }}</option> }
              </select>
            </label>
            <button type="button" (click)="recognize()" [disabled]="recognizeBusy()">Recognize surface (L0)</button>
            <span class="hint">{{ recognizeStatus() }}</span>
          } @else {
            <span class="hint">No model packages found on the server.</span>
          }
        </p>

        @if (reviewQueue().length > 0) {
          <p class="hint">Review queue (lowest confidence first):</p>
          <ul class="queue">
            @for (entry of reviewQueue().slice(0, 10); track entry.lineId) {
              <li>
                <button type="button" class="link" (click)="selectLineId(entry.lineId)">{{ entry.lineId }}</button>
                @if (entry.emptyOutput) { <span class="hint">empty output</span> }
                @else if (entry.meanConfidence !== null) { <span class="hint">conf {{ entry.meanConfidence | number:'1.2-2' }}</span> }
                @else { <span class="hint">no confidence</span> }
              </li>
            }
          </ul>
        }

        <p class="tools">
          <button type="button" (click)="downloadTei()">Download TEI/EpiDoc</button>
          <button type="button" (click)="downloadSearchablePdf()">Download searchable PDF</button>
          <button type="button" (click)="downloadWordBoundaries()">Download word boundaries</button>
          <button type="button" (click)="downloadEditionHtml()">Download edition HTML</button>
          <button type="button" (click)="downloadTraining()">Download training JSON</button>
          <button type="button" (click)="detectLayout()">Detect layout</button>
          <button type="button" (click)="downloadBundle()">Download bundle (.zip)</button>
          <span class="hint">{{ exportStatus() }}</span>
        </p>

        <app-fragment-panel [surfaceId]="surfaceId()" />

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
    .greek.vertical {
      display: inline-block;
      writing-mode: vertical-rl;
      text-orientation: upright;
      max-height: 12rem;
      line-height: 1.2;
    }
    .lines { line-height: 2; }
  `],
  imports: [LineEditor, DualPane, GeometryEditor, DecimalPipe, SessionPanel, FragmentPanel],
})
export class SurfaceView implements OnDestroy, OnChanges {
  private exportScopeVersion = 0;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['projectId'] || changes['surfaceId']) {
      this.exportScopeVersion++;
      if (this.initialized && this.hasNavigationWork()) this.identityQuarantined.set(true);
      this.identityReady.set(false); this.descendantAccess.refresh();
      if (this.initialized && this.scope.active() && !this.identityQuarantined()) this.loadSurface();
    }
  }
  projectId = input.required<string>();
  surfaceId = input.required<string>();
  focusLineId = input<string>('');
  navigationBlocked = output<boolean>();
  private readonly geometryEditor = viewChild(GeometryEditor);
  private readonly lineEditor = viewChild(LineEditor);
  protected readonly lineNavigationBlocked = signal(false);
  protected readonly identityReady = signal(false);
  protected readonly identityQuarantined = signal(false);
  protected readonly editorMounted = signal(false);
  private validatedProject = '';
  private validatedSurface = '';
  protected lineHasNavigationWork(): boolean { return this.lineNavigationBlocked() || this.lineEditor()?.hasNavigationWork() === true; }
  currentLineId(): string | null { return this.editingLineId(); }
  hasNavigationWork(): boolean { return this.captureSwitchBlocked() || this.geometryEditor()?.hasNavigationWork() === true || this.lineHasNavigationWork(); }
  protected workspaceAvailable(): boolean {
    const allowed = this.scope.active() && this.identityReady() && !this.identityQuarantined()
      && this.validatedProject === this.projectId() && this.validatedSurface === this.surfaceId();
    if (!allowed) this.scope.concealNow();
    return allowed;
  }
  protected workspaceCanEdit(): boolean { return this.workspaceAvailable() && this.scope.mayEdit(); }
  protected selectLineId(id: string): void {
    if (!this.workspaceAvailable() || id === this.editingLineId() || this.hasNavigationWork() || !this.lineIdsByIndex().includes(id)) return;
    this.editingLineId.set(id);
  }

  protected readonly exportData = signal<DiplomaticExport | null>(null);
  protected readonly selectedStart = signal(0);
  protected readonly selectedEnd = signal(0);
  protected readonly totalScalars = signal(0);
  protected readonly editingLineId = signal<string | null>(null);
  protected readonly readingRefreshToken = signal(0);
  protected readonly imageLink = signal('');
  protected readonly captureDisplay = signal<CaptureDisplayDescriptor | null>(null);
  protected readonly captureStatus = signal('');
  protected readonly captureSwitchBlocked = signal(false);
  private readonly previewRetry = signal(0);
  private readonly storageEpoch = signal(0);
  private readonly lineFrames = signal(new Map<string, string>());
  protected readonly selectedLineImageLink = computed(() => {
    const descriptor = this.captureDisplay();
    return descriptor && this.lineFrames().get(this.editingLineId() ?? '') === descriptor.sourceFrameRef
      ? this.imageLink() : '';
  });
  private previewRequest: Subscription | null = null;
  private previewGeneration = 0;
  private objectUrl: string | null = null;
  private destroyed = false;
  private readonly storageChanged = (event: StorageEvent) => {
    if (event.key === null || event.key === ApiKeyStorageKey) this.storageEpoch.update(epoch => epoch + 1);
  };
  protected readonly captures = signal<{ id: string; imageLocation?: string }[]>([]);
  protected readonly captureIndex = signal(0);
  protected readonly showGeometry = signal(false);
  protected readonly exportStatus = signal('');
  protected readonly qaProfile = signal('');
  protected readonly qaFlagged = signal(0);
  protected readonly qaIssues = signal<{ lineId: string; scalarOffset: number; category: string; scalar: string }[]>([]);
  protected readonly models = signal<{ name: string; modifiedAt: string; hasManifest: boolean }[]>([]);
  protected readonly selectedModel = signal('');
  protected readonly recognizeStatus = signal('');
  protected readonly recognizeBusy = signal(false);
  protected readonly reviewQueue = signal<{ lineId: string; meanConfidence: number | null; emptyOutput: boolean }[]>([]);
  protected readonly editorName = authorId();
  protected readonly presence = signal<string[]>([]);
  protected readonly sessionNode = signal('');
  private sessionId: string | null = null;
  private presenceEditor = this.editorName;
  private presenceTimer: ReturnType<typeof setInterval> | null = null;

  protected hasNeighbour(delta: number): boolean {
    const ids = this.lineIdsByIndex();
    const index = ids.indexOf(this.editingLineId() ?? '');
    const target = index + delta;
    return index >= 0 && target >= 0 && target < ids.length;
  }

  protected navigateLine(delta: number): void {
    if (!this.workspaceAvailable() || this.hasNavigationWork()) return;
    const ids = this.lineIdsByIndex();
    const index = ids.indexOf(this.editingLineId() ?? '');
    const target = index + delta;
    if (index >= 0 && target >= 0 && target < ids.length) this.selectLineId(ids[target]);
  }

  protected onReadingSaved(saved: ProposalSavedEvent): void {
    if (!this.workspaceAvailable()) return;
    if (saved.lineId === this.editingLineId()) this.readingRefreshToken.update(token => token + 1);
  }

  protected onModel(event: Event): void {
    if (!this.workspaceAvailable()) { (event.target as HTMLSelectElement).value = this.selectedModel(); return; }
    this.selectedModel.set((event.target as HTMLSelectElement).value);
  }

  protected recognize(): void {
    if (!this.workspaceCanEdit()) return;
    const model = this.selectedModel();
    if (!model) return;
    this.recognizeBusy.set(true);
    this.recognizeStatus.set('recognizing…');
    this.api.recognizeSurface(this.surfaceId(), model).subscribe({
      next: (result) => {
        this.recognizeBusy.set(false);
        this.reviewQueue.set(result.reviewQueue ?? []);
        this.recognizeStatus.set(`${result.lines} line(s) recognized${result.skipped ? `, ${result.skipped} skipped` : ''} (L0)`);
      },
      error: () => {
        this.recognizeBusy.set(false);
        this.recognizeStatus.set('recognition failed (model package or surface)');
      },
    });
  }
  private readonly lineIdsByIndex = signal<string[]>([]);
  protected readonly lineOrientations = signal<{ direction?: string | null; script?: string | null }[]>([]);

  protected lineDirection(index: number): string | null {
    return this.lineOrientations()[index]?.direction ?? null;
  }

  protected lineScript(index: number): string | null {
    return this.lineOrientations()[index]?.script ?? null;
  }

  protected isVerticalLine(index: number): boolean {
    return this.lineDirection(index)?.startsWith('vertical') ?? false;
  }

  protected onCapture(event: Event): void {
    if (!this.workspaceAvailable()) { (event.target as HTMLSelectElement).value = String(this.captureIndex()); return; }
    const index = Number((event.target as HTMLSelectElement).value);
    if (this.hasNavigationWork()) { (event.target as HTMLSelectElement).value = String(this.captureIndex()); return; }
    if (Number.isInteger(index) && index >= 0 && index < this.captures().length) this.captureIndex.set(index);
  }

  protected toggleGeometry(): void {
    if (!this.workspaceAvailable()) return;
    this.showGeometry.update(v => !v);
  }

  /** Export-only intent: it neither grants project access nor changes draft ownership. */
  private captureExportIntent() {
    if (this.destroyed || !this.workspaceAvailable()) return null;
    const authority = ensureCacheAuthorityRevision();
    const projectId = this.projectId();
    const surfaceId = this.surfaceId();
    const scope = this.exportScopeVersion;
    const credential = apiKey();
    const generation = cacheCredentialGeneration();
    return { projectId, surfaceId, credential, current: () => !this.destroyed
      && scope === this.exportScopeVersion && projectId === this.projectId() && surfaceId === this.surfaceId()
      && credential === apiKey() && generation === cacheCredentialGeneration()
      && authority === cacheAuthorityRevision() && this.workspaceAvailable() };
  }

  protected downloadTei(): void {
    const intent = this.captureExportIntent(); if (!intent) return;
    this.api.tei(intent.projectId, intent.surfaceId, intent.credential).subscribe({
      next: xml => {
        if (!intent.current()) return;
        this.download(`${intent.surfaceId}.tei.xml`, xml, 'application/xml');
        this.exportStatus.set('TEI downloaded');
      },
      error: () => { if (intent.current()) this.exportStatus.set('TEI export failed'); },
    });
  }

  protected downloadEditionHtml(): void {
    const intent = this.captureExportIntent(); if (!intent) return;
    this.api.editionHtml(intent.projectId, intent.surfaceId, intent.credential).subscribe({
      next: html => {
        if (!intent.current()) return;
        this.download(`${intent.surfaceId}-edition.html`, html, 'text/html');
        this.exportStatus.set('edition HTML downloaded');
      },
      error: () => { if (intent.current()) this.exportStatus.set('edition HTML export failed'); },
    });
  }

  protected downloadWordBoundaries(): void {
    const intent = this.captureExportIntent(); if (!intent) return;
    this.api.wordBoundaryCsv(intent.projectId, intent.credential).subscribe({
      next: csv => {
        if (!intent.current()) return;
        this.download(`${intent.surfaceId}-word-boundaries.csv`, csv, 'text/csv');
        this.exportStatus.set('word boundaries downloaded');
      },
      error: () => { if (intent.current()) this.exportStatus.set('word-boundary export failed'); },
    });
  }

  protected downloadSearchablePdf(): void {
    const intent = this.captureExportIntent(); if (!intent) return;
    this.api.searchablePdf(intent.projectId, intent.surfaceId, intent.credential).subscribe({
      next: blob => {
        if (!intent.current()) return;
        this.downloadBlob(`${intent.surfaceId}.pdf`, blob);
        this.exportStatus.set('searchable PDF downloaded');
      },
      error: () => { if (intent.current()) this.exportStatus.set('searchable PDF export failed'); },
    });
  }

  protected downloadTraining(): void {
    const intent = this.captureExportIntent(); if (!intent) return;
    this.api.trainingExport(intent.projectId, intent.credential).subscribe({
      next: document => {
        if (!intent.current()) return;
        this.download(`${intent.projectId}-training.json`, JSON.stringify(document, null, 2), 'application/json');
        this.exportStatus.set('training export downloaded');
      },
      error: () => { if (intent.current()) this.exportStatus.set('training export failed'); },
    });
  }

  protected downloadBundle(): void {
    const intent = this.captureExportIntent(); if (!intent) return;
    this.api.projectBundle(intent.projectId, intent.credential).subscribe({
      next: blob => {
        if (!intent.current()) return;
        this.downloadBlob(`${intent.projectId}.zip`, blob);
        this.exportStatus.set('bundle downloaded');
      },
      error: () => { if (intent.current()) this.exportStatus.set('bundle export failed'); },
    });
  }

  protected detectLayout(): void {
    if (!this.workspaceCanEdit()) return;
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
    this.downloadBlob(filename, new Blob([content], { type }));
  }

  private downloadBlob(filename: string, blob: Blob): void {
    const url = URL.createObjectURL(blob);
    try {
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = filename;
      anchor.click();
    } finally { URL.revokeObjectURL(url); }
  }

  protected readonly lines = computed(() => (this.exportData()?.text ?? '').split('\n'));

  private readonly baseApi: ApiClient;
  private readonly scope: WorkspaceComponentScope;
  readonly descendantAccess: WorkspaceAccessGate;
  private initialized = false;
  private get api(): ApiClient {
    const project = this.projectId(), surface = this.surfaceId(), version = this.exportScopeVersion;
    return this.scope.request(this.baseApi, project, () => this.projectId() === project && this.surfaceId() === surface && version === this.exportScopeVersion && !this.identityQuarantined());
  }
  private onWorkspaceState(state: WorkspaceAccessState): void {
    this.stopPresence(); this.clearPreview();
    if (state.status !== 'active') {
      this.identityReady.set(false); this.descendantAccess.refresh();
      this.sessionId = null; this.sessionNode.set(''); this.recognizeBusy.set(false);
      return;
    }
    if (this.initialized && !this.identityQuarantined()) this.loadSurface();
  }
  private stopPresence(): void {
    if (this.presenceTimer) clearInterval(this.presenceTimer);
    this.presenceTimer = null;
  }
  private startPresence(): void {
    if (!this.workspaceCanEdit()) return;
    this.stopPresence();
    this.api.createSession(this.surfaceId(), 60).subscribe({
      next: session => { if (!this.workspaceCanEdit()) return; this.sessionId = session.id; this.sessionNode.set(session.nodeId); this.heartbeatPresence(); },
      error: () => { if (this.workspaceCanEdit()) this.heartbeatPresence(); },
    });
    this.presenceTimer = setInterval(() => this.heartbeatPresence(), 20_000);
  }

  constructor(api: ApiClient) {
    this.baseApi = api;
    const parent = inject(WORKSPACE_ACCESS, { optional: true, skipSelf: true });
    this.descendantAccess = new WorkspaceAccessGate(parent, () => this.identityReady() && !this.identityQuarantined()
      && this.validatedProject === this.projectId() && this.validatedSurface === this.surfaceId());
    this.scope = new WorkspaceComponentScope(state => this.onWorkspaceState(state), parent);
    globalThis.addEventListener('storage', this.storageChanged);
    effect(() => this.navigationBlocked.emit(this.hasNavigationWork()));
    effect(() => {
      apiKeyRevision(); this.storageEpoch(); this.previewRetry();
      if (this.workspaceAvailable()) this.loadPreview(this.projectId(), this.surfaceId(), this.captures()[this.captureIndex()]);
      else this.clearPreview();
    });
    // Search hits navigate within the already-open surface: react to focus changes.
    effect(() => {
      const focused = this.focusLineId();
      if (focused && this.workspaceAvailable()) untracked(() => this.selectLineId(focused));
    });
  }

  ngOnInit(): void { this.initialized = true; if (this.scope.active()) this.loadSurface(); }

  private loadSurface(): void {
    if (!this.scope.active() || this.identityQuarantined()) return;
    this.api.diplomaticExport(this.projectId(), this.surfaceId())
      .subscribe({
        next: (data) => {
          this.exportData.set(data);
          this.totalScalars.set(scalarCount(data.text));
        },
        error: () => this.exportData.set({ text: '', lossReport: { format: 'unavailable', contractVersion: '-', entries: [] } }),
      });

    this.api.reviewQueue(this.projectId(), this.surfaceId()).subscribe({
      next: (queue) => {
        if (queue.length > 0)
          this.reviewQueue.set(queue.map(e => ({
            lineId: e.lineId, meanConfidence: e.meanConfidence, emptyOutput: e.emptyOutput,
          })));
      },
      error: () => { /* no persisted queue yet */ },
    });

    this.api.models().subscribe({
      next: (list) => {
        this.models.set(list);
        if (list.length > 0) this.selectedModel.set(list[0].name);
      },
      error: () => this.models.set([]),
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
    this.api.loadProjectFresh(this.projectId()).subscribe(payload => {
      if (!uniqueWorkspaceProject(payload, this.projectId())) { this.identityQuarantined.set(true); this.identityReady.set(false); this.descendantAccess.refresh(); this.scope.concealNow(); return; }
      const surface = payload.objects
        .flatMap(o => o.surfaces)
        .find(s => s.id === this.surfaceId());
      if (!surface || this.editingLineId() && !surface.lines.some(line => line.id === this.editingLineId())) {
        this.identityQuarantined.set(true); this.identityReady.set(false); this.descendantAccess.refresh(); this.scope.concealNow(); return;
      }
      this.validatedProject = this.projectId(); this.validatedSurface = this.surfaceId();
      this.lineIdsByIndex.set(surface.lines.map(l => l.id));
      this.lineFrames.set(new Map((surface?.lines ?? []).filter(line => line.frameRef).map(line => [line.id, line.frameRef!])));
      const regionsById = new Map((surface?.regions ?? []).map(r => [r.id, r]));
      this.lineOrientations.set(surface?.lines.map(l => {
        const region = l.regionId === null ? undefined : regionsById.get(l.regionId);
        return {
          direction: l.direction ?? region?.direction ?? null,
          script: l.script ?? region?.script ?? null,
        };
      }) ?? []);
      // Captures (views) are listed; the selected view drives overlays and links.
      const captureList = (surface?.captures ?? []).map(c => ({ id: c.id, imageLocation: c.imageLocation }));
      this.captures.set(captureList);
      if (this.captureIndex() >= captureList.length) this.captureIndex.set(0);
      this.identityReady.set(true); this.editorMounted.set(true); this.descendantAccess.refresh(); this.scope.present(true);
      const focused = this.focusLineId(); if (focused && !this.editingLineId()) this.selectLineId(focused);
      this.startPresence();

    });
  }

  protected retryPreview(): void { if (!this.workspaceAvailable()) return; this.previewRetry.update(value => value + 1); }

  protected onPreviewImageFailed(): void {
    if (!this.workspaceAvailable()) return;
    this.clearPreview();
    this.captureStatus.set('The image could not be displayed. Retry or choose a supported capture.');
  }

  private clearPreview(): number {
    const generation = ++this.previewGeneration;
    this.previewRequest?.unsubscribe(); this.previewRequest = null;
    this.imageLink.set(''); this.captureDisplay.set(null);
    if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
    this.objectUrl = null;
    return generation;
  }

  private loadPreview(projectId: string, surfaceId: string, capture?: { id: string; imageLocation?: string }): void {
    const generation = this.clearPreview();
    if (!capture || this.destroyed || !this.workspaceAvailable()) { this.captureStatus.set(''); return; }
    const current = () => !this.destroyed && generation === this.previewGeneration;
    this.captureStatus.set('Loading image…');
    if (/^https?:\/\//i.test(capture.imageLocation ?? '')) {
      // Refresh authorization without offline fallback before retaining the old direct-view behavior.
      this.previewRequest = this.api.loadProjectFresh(projectId).subscribe({
        next: payload => {
          if (!current()) return;
          const fresh = payload.objects.flatMap(object => object.surfaces).find(surface => surface.id === surfaceId)
            ?.captures.find(candidate => candidate.id === capture.id)?.imageLocation ?? '';
          if (/^https?:\/\//i.test(fresh)) {
            this.imageLink.set(fresh);
            this.captureStatus.set('Remote image view. Geometry is unavailable until its image frame is verified.');
          } else this.captureStatus.set('The capture changed. Reload this surface.');
        },
        error: () => { if (current()) this.captureStatus.set('Image access denied or unavailable. Check your API key and project access.'); },
      });
      return;
    }
    this.previewRequest = this.api.captureDisplay(projectId, surfaceId, capture.id).pipe(
      switchMap(descriptor => {
        if (!validCaptureDisplay(descriptor, projectId, surfaceId, capture.id)) throw new Error('Invalid display descriptor');
        return this.api.captureDisplayImage(projectId, surfaceId, capture.id, descriptor.derivativeId)
          .pipe(map(blob => ({ descriptor, blob })));
      }),
    ).subscribe({
      next: ({ descriptor, blob }) => {
        if (!current()) return;
        if (blob.type !== 'image/png' || blob.size === 0) { this.onPreviewImageFailed(); return; }
        try {
          if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
          this.objectUrl = URL.createObjectURL(blob);
          this.captureDisplay.set(descriptor); this.imageLink.set(this.objectUrl);
          this.captureStatus.set(descriptor.recordedChecksumStatus === 'unavailable'
            ? 'The original image checksum was not recorded.' : '');
        } catch { this.onPreviewImageFailed(); }
      },
      error: error => {
        if (!current()) return;
        this.captureStatus.set(error.status === 401 || error.status === 403
          ? 'Image access denied. Check your API key and project access.'
          : error.status === 412 ? 'The image changed. Retry the preview.'
          : 'Preview unavailable. Choose a supported local PNG or JPEG capture.');
      },
    });
  }

  private heartbeatPresence(): void {
    if (!this.workspaceCanEdit()) return;
    this.api.touchPresence(this.surfaceId(), this.editorName, this.sessionId ?? undefined).subscribe({
      next: result => {
        this.presenceEditor = result.editor ?? this.editorName;
        this.presence.set(result.editors);
      },
      error: () => { /* presence is advisory */ },
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    globalThis.removeEventListener('storage', this.storageChanged);
    this.clearPreview();
    this.stopPresence();
    this.descendantAccess.destroy();
    if (!this.scope.active() || !this.scope.mayEdit()) return;
    this.api.leavePresence(this.surfaceId(), this.presenceEditor).subscribe({
      error: () => { /* presence is advisory */ },
    });
    if (this.sessionId) {
      this.api.revokeSession(this.sessionId).subscribe({
        error: () => { /* sessions are advisory; expiry is the fallback */ },
      });
    }
  }

  protected select(lineIndex: number): void {
    if (!this.workspaceAvailable() || this.hasNavigationWork()) return;
    const lines = this.lines();
    let start = 0;
    for (let i = 0; i < lineIndex; i++) start += scalarCount(lines[i]) + 1; // +1 for the newline
    this.selectedStart.set(start);
    this.selectedEnd.set(start + scalarCount(lines[lineIndex]));
    const id = this.lineIdsByIndex()[lineIndex]; if (id) this.selectLineId(id);
  }
}

function scalarCount(text: string): number {
  let count = 0;
  for (const _ of text) count++;
  return count;
}

export function validCaptureDisplay(value: CaptureDisplayDescriptor, project: string, surface: string, capture: string): boolean {
  return !!value && value.schemaVersion === 1 && value.projectId === project && value.surfaceId === surface
    && value.captureId === capture && typeof value.sourceFrameRef === 'string' && value.sourceFrameRef.length > 0
    && value.orientation === 'raw' && value.mediaType === 'image/png'
    && /^[0-9a-f]{64}$/.test(value.derivativeId)
    && [value.sourceWidth, value.sourceHeight, value.displayWidth, value.displayHeight].every(number => Number.isSafeInteger(number) && number > 0)
    && Number.isFinite(value.scaleX) && value.scaleX > 0 && Number.isFinite(value.scaleY) && value.scaleY > 0
    && Math.abs(value.scaleX - value.displayWidth / value.sourceWidth) < 1e-12
    && Math.abs(value.scaleY - value.displayHeight / value.sourceHeight) < 1e-12;
}
