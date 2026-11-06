import { Component, computed, OnDestroy, signal, viewChild, inject, effect, ElementRef, untracked } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { apiKey, apiKeyRevision, ApiKeyStorageKey, cacheCredentialGeneration } from './auth';
import { cacheAuthorityRevision, ensureCacheAuthorityRevision, CacheAuthorityRevisionStorageKey, clearCache } from './offline-cache-storage';
import { WorkspaceAccessCoordinator } from './workspace-access';
import { WORKSPACE_ACCESS } from './workspace-access-types';
import type { WorkspaceAccessState, WorkspaceRequestIntent } from './workspace-access-types';
import { persistedAccountFromFreshProfile, persistedWorkspaceOwnerFromFreshProfile } from './principal-profile';
import type { PrincipalProfile, WorkspaceOwner } from './principal-profile';
import { WorkspaceComponentScope, uniqueWorkspaceProject } from './workspace-component-scope';
import { Subscription } from 'rxjs';
import { ApiClient, ProjectPayload } from './api-client';
import { SurfaceView } from './surface-view';
import { ProjectMembersPanel } from './project-members-panel';
import { SessionsAdminPanel } from './sessions-admin-panel';
import { ModelRegistryPanel } from './model-registry-panel';
import { ProjectHealthPanel } from './project-health-panel';

interface ProjectRow {
  id: string;
  name: string;
}

/**
 * Workspace landing page: collection list (project-plan §4 web workspace). Operates against
 * the versioned REST contract; when the API is offline the page degrades gracefully.
 */
@Component({
  selector: 'app-projects-page',
  providers: [{ provide: WORKSPACE_ACCESS, useFactory: () => inject(ProjectsPage).workspaceAccess }],
  standalone: true,
  template: `
    @if (!collectionsAvailable() || selectedId() && !workspaceAvailable()) {
      <p role="status">{{ accessMessage() }}</p>
      <button type="button" (click)="retryAccess()">Retry access</button>
    }
    <section data-workspace-protected data-workspace-collections [hidden]="!collectionsAvailable()" [attr.inert]="!collectionsAvailable() ? '' : null">
      <h2>Collections</h2>
      <button type="button" (click)="create()">New collection</button>
      <p>New id: {{ createdId() }}</p>
      <ul>
        @for (project of projects(); track project.id) {
          <li>
            <button type="button" class="link" (click)="select(project.id)" [disabled]="navigationBlocked()">{{ project.name }}</button>
            <code>{{ project.id }}</code>
          </li>
        } @empty {
          <li>No collections yet (API offline or empty).</li>
        }
      </ul>
    </section>

    @if (selected(); as project) {
      @if (payload(selectedId()!); as payload) {
        <section data-workspace-protected [hidden]="!workspaceAvailable()" [attr.inert]="!workspaceAvailable() ? '' : null">
          <h2>{{ payload.name }}</h2>
          <p class="tools">
            <button type="button" (click)="downloadEdition()">Download edition HTML</button>
            <button type="button" (click)="downloadProjectPdf()">Download searchable PDF</button>
            <span class="hint" role="status">{{ editionStatus() }}</span>
          </p>
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
              {{ r.acceptedLines ?? 0 }} accepted · {{ r.correctedLines ?? 0 }} corrected ·
              <b>{{ r.needsAdjudication }}</b> need adjudication
              <button type="button" (click)="downloadAdjudication()">Download adjudication CSV</button>
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
          <app-project-health-panel [projectId]="selectedId()!" />
          <app-project-members-panel [projectId]="selectedId()!" />
          <app-sessions-admin-panel />
          <app-model-registry-panel />
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
      <section data-workspace-protected [hidden]="!workspaceAvailable()" [attr.inert]="!workspaceAvailable() ? '' : null">
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
      <div data-workspace-protected [hidden]="!workspaceAvailable()" [attr.inert]="!workspaceAvailable() ? '' : null">
      <p class="tools">
        <button type="button" (click)="navigateSurface(-1)" [disabled]="navigationBlocked() || !hasSurfaceNeighbour(-1)">← Previous surface</button>
        <button type="button" (click)="navigateSurface(1)" [disabled]="navigationBlocked() || !hasSurfaceNeighbour(1)">Next surface →</button>
      </p>
      @if (navigationBlocked()) { <p role="status">Save or resolve text, geometry and order work before changing lines, surfaces or collections.</p> }
      @for (viewKey of [selectedId() + '|' + openSurfaceId()]; track viewKey) {
        <app-surface-view [projectId]="selectedId()!" [surfaceId]="openSurfaceId()!" [focusLineId]="focusLineId()" (navigationBlocked)="navigationBlocked.set($event)" />
      }
      </div>
    }
  `,
  styles: [`
    .link { background: none; border: none; color: #334; cursor: pointer; text-decoration: underline; padding: 0; }
    .tools { display: flex; gap: 0.5rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
    li { margin: 0.2rem 0; }
  `],
  imports: [SurfaceView, ProjectMembersPanel, SessionsAdminPanel, ModelRegistryPanel, ProjectHealthPanel],
})
export class ProjectsPage implements OnDestroy {
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private selectionVersion = 0;
  private searchVersion = 0;
  private importVersion = 0;
  private layerVersion = 0;
  private destroyed = false;

  private globalEpoch = 0;
  private bootstrapVersion = 0;
  private globalReady = signal(false);
  private globalAdmission: WorkspaceRequestIntent | null = null;
  private globalAnchor: { owner: WorkspaceOwner | null; mode: string | undefined; accountId: string | null; scope: WorkspaceRequestIntent | null } | null = null;
  private globalOwnerlessLost = false;
  private globalProfile: PrincipalProfile | null = null;
  private pendingOpen: { surfaceId: string; lineId: string } | null = null;
  private bootstrapRequest: Subscription | null = null;
  readonly workspaceAccess: WorkspaceAccessCoordinator<ProjectPayload>;
  private readonly scope: WorkspaceComponentScope;
  protected readonly accessMessage = signal('Checking current access…');
  private readonly storageChanged = (event: StorageEvent) => {
    if (event.key === null || event.key === ApiKeyStorageKey || event.key === CacheAuthorityRevisionStorageKey) this.retryAccess();
  };
  private captureGlobal(): WorkspaceRequestIntent {
    const authority = ensureCacheAuthorityRevision(), credential = apiKey(), generation = cacheCredentialGeneration();
    const epoch = this.globalEpoch, selection = this.selectionVersion;
    return { credential, projectId: '', generation, current: () => !this.destroyed && epoch === this.globalEpoch
      && selection === this.selectionVersion && credential === apiKey() && generation === cacheCredentialGeneration()
      && authority === cacheAuthorityRevision() };
  }
  private captureIntent() {
    const intent = this.workspaceAccess.capture(), projectId = this.selectedId(), selection = this.selectionVersion;
    return intent && intent.projectId === projectId ? { ...intent, projectId,
      current: () => !this.destroyed && selection === this.selectionVersion && projectId === this.selectedId() && intent.current() }
      : { credential: '', projectId: null, generation: -1, current: () => false };
  }
  protected workspaceAvailable(): boolean { return this.workspaceAccess.isActive() && this.workspaceAccess.projectId() === this.selectedId(); }
  protected collectionsAvailable(): boolean {
    const active = this.globalReady() && (this.selectedId() ? this.workspaceAvailable() : this.globalAdmission?.current() === true);
    if (!active) this.concealCollections();
    return active;
  }
  private concealCollections(): void {
    const node = this.element.nativeElement.querySelector<HTMLElement>('[data-workspace-collections]');
    if (node) { node.hidden = true; node.inert = true; }
  }
  private canEditCollections(): boolean {
    if (!this.collectionsAvailable()) return false;
    const role = this.selectedId() ? this.workspaceAccess.state().principal?.role : this.globalProfile?.role;
    return ['annotator','reviewer','admin'].includes(role?.toLowerCase() ?? '');
  }
  private onAccessState(state: WorkspaceAccessState): void {
    if (this.destroyed) return;
    if (state.status !== 'active') {
      if (state.reason !== 'checking-access') {
        this.globalEpoch++; this.globalAdmission = null; this.globalReady.set(false);
        this.globalOwnerlessLost = true;
        if (this.globalAnchor) this.globalAnchor.scope = null;
        this.searchVersion++; this.importVersion++;
      }
      this.accessMessage.set(state.status === 'quarantined'
        ? 'This preserved workspace belongs to the original account. Restore that account to recover its work. Opening another account here is unavailable until recovery is resolved.'
        : 'Workspace access is paused. Restore the original account and connection, then retry. Mounted work remains here.');
      return;
    }
    this.globalReady.set(false); this.concealCollections();
    this.refreshCollections(); this.refreshSummary();
    const pending = this.pendingOpen; this.pendingOpen = null;
    if (pending) this.openSurface(pending.surfaceId, pending.lineId);
  }
  protected retryAccess(): void {
    if (this.destroyed) return;
    if (this.selectedId()) this.workspaceAccess.retry(); else this.bootstrapCollections();
  }
  private bootstrapCollections(): void {
    const continuity = this.captureGlobal(), version = ++this.bootstrapVersion;
    const intent = { ...continuity, current: () => version === this.bootstrapVersion && continuity.current() };
    this.bootstrapRequest?.unsubscribe();
    this.globalReady.set(false); this.accessMessage.set('Checking current access…');
    this.bootstrapRequest = this.baseApi.forWorkspaceRequest(intent).me(intent.credential).subscribe({
      next: profile => {
        if (!intent.current()) return;
        const mode = profile.mode, accountId = persistedAccountFromFreshProfile(profile);
        if (!(mode === 'account' && accountId !== null || mode === 'local' && profile.authenticated === false || mode === 'bootstrap' && profile.authenticated === true)) {
          if (this.globalAnchor) this.globalOwnerlessLost = true;
          this.accessMessage.set('The current account could not be confirmed.'); return;
        }
        const owner = persistedWorkspaceOwnerFromFreshProfile(profile), anchor = this.globalAnchor;
        if (anchor) {
          const same = anchor.owner !== null && owner !== null && anchor.owner.accountId === owner.accountId && anchor.owner.workspaceNamespace === owner.workspaceNamespace;
          const initial = anchor.owner === null && owner === null && !this.globalOwnerlessLost && anchor.scope?.current() === true && anchor.mode === mode && anchor.accountId === accountId;
          if (!same && !initial) { this.globalOwnerlessLost = true; this.accessMessage.set('Restore the original account. This mounted workspace cannot transfer its retained work to another owner.'); return; }
        }
        this.globalProfile = profile; this.globalAdmission = intent;
        this.fetchCollections(intent, true);
      },
      error: () => { if (this.globalAnchor) this.globalOwnerlessLost = true; this.accessMessage.set('Current access could not be checked. Restore the original account and connection, then retry.'); },
    });
  }
  private fetchCollections(intent: WorkspaceRequestIntent, restore: boolean): void {
    this.baseApi.forWorkspaceRequest(intent).listProjectsFresh().subscribe({
      next: rows => {
        if (!intent.current()) return;
        if (!Array.isArray(rows) || rows.some(row => !row || typeof row.id !== 'string' || typeof row.name !== 'string') || new Set(rows.map(row => row.id)).size !== rows.length) {
          if (this.globalAnchor) this.globalOwnerlessLost = true;
          this.globalReady.set(false); this.accessMessage.set('The current collection list could not be confirmed.'); return;
        }
        if (!this.globalAnchor && this.globalProfile) {
          const profile = this.globalProfile, owner = persistedWorkspaceOwnerFromFreshProfile(profile);
          this.globalAnchor = { owner, mode: profile.mode, accountId: persistedAccountFromFreshProfile(profile), scope: owner ? null : this.captureGlobal() };
        }
        this.projects.set(rows); this.globalReady.set(true); this.scope.present(true);
        if (restore) this.restoreDeepLink(rows);
      },
      error: () => { if (this.globalAnchor) this.globalOwnerlessLost = true; this.globalReady.set(false); this.accessMessage.set('The current collection list is unavailable. Retained work has not been discarded.'); },
    });
  }
  private refreshCollections(): void {
    const intent = this.workspaceAccess.capture();
    if (intent) this.fetchCollections(intent, false);
  }
  private refreshSummary(): void {
    const intent = this.captureIntent(); if (!intent.projectId) return;
    this.api.stats(intent.projectId, intent.credential).subscribe({ next: counts => { if (intent.current()) this.stats.set(counts); } });
    this.api.reviewSummary(intent.projectId, intent.credential).subscribe({ next: review => { if (intent.current()) this.review.set(review); } });
  }
  ngOnDestroy(): void {
    this.destroyed = true; this.bootstrapRequest?.unsubscribe(); this.workspaceAccess.destroy();
    globalThis.removeEventListener('storage', this.storageChanged);
  }

  private clearSearch(): void {
    this.searchVersion++;
    this.searchHits.set([]);
    this.searchStatus.set('');
  }
  private readonly baseApi: ApiClient;
  private get api(): ApiClient { const intent = this.captureIntent(); return this.baseApi.forWorkspaceRequest(intent); }
  protected readonly projects = signal<ProjectRow[]>([]);
  protected readonly createdId = signal('');
  protected readonly selectedId = signal<string | null>(null);
  protected readonly editionStatus = signal('');
  protected readonly selected = computed(() =>
    this.projects().find(p => p.id === this.selectedId()) ?? null);
  protected readonly payloads = signal<Record<string, ProjectPayload>>({});
  protected readonly openSurfaceId = signal<string | null>(null);
  protected readonly navigationBlocked = signal(false);
  private readonly activeSurface = viewChild(SurfaceView);
  private navigationIsBlocked(): boolean { return this.navigationBlocked() || this.activeSurface()?.hasNavigationWork() === true; }
  protected readonly stats = signal<{
    surfaces: number; lines: number; transcribedLines: number; annotations: number; flaggedLines: number;
  } | null>(null);
  protected readonly importLayer = signal<'L1Diplomatic' | 'L2Editorial'>('L1Diplomatic');
  protected readonly importStatus = signal('');

  protected onImportLayer(event: Event): void {
    if (!(this.workspaceAvailable() && this.scope.mayEdit())) { (event.target as HTMLInputElement).value = this.importLayer(); return; }
    this.layerVersion++;
    this.importLayer.set((event.target as HTMLSelectElement).value as 'L1Diplomatic' | 'L2Editorial');
  }

  protected onCsvFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!this.workspaceAvailable() || !this.scope.mayEdit() || this.navigationIsBlocked()) { input.value = ''; return; }
    const file = input.files?.[0];
    const intent = this.captureIntent();
    if (!file || !intent.projectId || this.destroyed) return;
    const projectId = intent.projectId;
    const layer = this.importLayer();
    const layerVersion = this.layerVersion;
    const version = ++this.importVersion;
    const current = () => intent.current() && version === this.importVersion
      && layerVersion === this.layerVersion && layer === this.importLayer();
    file.text().then(text => {
      if (!current()) return;
      this.api.importLineText(projectId, text, layer, intent.credential).subscribe({
        next: (result) => {
          if (!current()) return;
          this.workspaceAccess.retry();
          this.importStatus.set(`applied ${result.applied}, skipped ${result.skipped}`);
        },
        error: () => { if (current()) this.importStatus.set('import failed (header, ids or revision conflict)'); },
      });
    }).catch(() => { if (current()) this.importStatus.set('CSV file could not be read'); });
  }

  protected readonly review = signal<{
    transcribedLines: number;
    reviewedLines: number;
    doubleReviewedLines: number;
    disputedLines: number;
    acceptedLines?: number;
    correctedLines?: number;
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
    if (!(this.workspaceAvailable())) { (event.target as HTMLInputElement).value = this.query(); return; }
    this.clearSearch();
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected onMode(event: Event): void {
    if (!(this.workspaceAvailable())) { (event.target as HTMLInputElement).value = this.searchMode(); return; }
    this.clearSearch();
    this.searchMode.set((event.target as HTMLSelectElement).value as 'literal' | 'base');
  }

  protected runSearch(): void {
    if (!this.workspaceAvailable()) return;
    const query = this.query().trim();
    const intent = this.captureIntent();
    if (!query || !intent.projectId || this.destroyed) return;
    const mode = this.searchMode();
    const version = ++this.searchVersion;
    const current = () => intent.current() && version === this.searchVersion
      && query === this.query().trim() && mode === this.searchMode();
    this.api.search(intent.projectId, query, mode, intent.credential).subscribe({
      next: (result) => {
        if (!current()) return;
        this.searchHits.set(result.hits);
        this.searchStatus.set(`${result.totalHits} hit(s)${result.truncated ? ' (truncated)' : ''}`);
      },
      error: () => {
        if (!current()) return;
        this.searchHits.set([]);
        this.searchStatus.set('search failed');
      },
    });
  }

  protected openHit(hit: { surfaceId: string; lineId: string }): void {
    this.openSurface(hit.surfaceId, hit.lineId);
  }

  protected adjudicationLines(): {
    lineId: string; surfaceId: string; text: string; reviewers: string[]; outcomes: string[];
  }[] {
    return (this.review()?.lines ?? []).filter(l => l.needsAdjudication).slice(0, 10);
  }

  protected openReviewLine(entry: { surfaceId: string; lineId: string }): void {
    this.openHit(entry);
  }

  protected downloadAdjudication(): void {
    if (!this.workspaceAvailable()) return;
    const intent = this.captureIntent();
    const projectId = intent.projectId;
    if (!projectId || this.destroyed) return;
    this.api.adjudicationCsv(projectId, intent.credential).subscribe({
      next: csv => {
        if (!intent.current()) return;
        const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
        try {
          const anchor = document.createElement('a');
          anchor.href = url;
          anchor.download = `${projectId}-adjudication.csv`;
          anchor.click();
          this.editionStatus.set('adjudication CSV downloaded');
        } finally { URL.revokeObjectURL(url); }
      },
      error: () => { if (intent.current()) this.editionStatus.set('adjudication CSV export failed'); },
    });
  }

  constructor(http: HttpClient) {
    this.baseApi = new ApiClient(http);
    ensureCacheAuthorityRevision();
    this.workspaceAccess = new WorkspaceAccessCoordinator({ api: this.baseApi,
      readScope: () => ({ credential: apiKey(), localGeneration: cacheCredentialGeneration(), authorityRevision: cacheAuthorityRevision() }),
      onDenied: () => clearCache(),
      onConfirmed: payload => {
        const id = this.selectedId();
        if (!id || !uniqueWorkspaceProject(payload, id)) throw new Error('Ambiguous native identity');
        this.payloads.update(map => ({ ...map, [id]: payload }));
      },
    });
    this.scope = new WorkspaceComponentScope(state => this.onAccessState(state), this.workspaceAccess);
    globalThis.addEventListener('storage', this.storageChanged);
    this.bootstrapCollections();
    let initial = true;
    effect(() => { apiKeyRevision(); if (initial) { initial = false; return; } untracked(() => this.retryAccess()); });
  }

  protected create(): void {
    if (this.destroyed || !this.canEditCollections()) return;
    const intent = this.selectedId() ? this.workspaceAccess.capture() : this.globalAdmission;
    if (!intent || !intent.current()) return;
    this.baseApi.forWorkspaceRequest(intent).createProject('workspace-collection-' + (this.projects().length + 1), intent.credential)
      .subscribe({ next: project => { if (intent.current()) { this.createdId.set(project.id); this.fetchCollections(intent, false); } },
        error: () => { if (intent.current()) this.createdId.set('API unreachable'); } });
  }

  protected select(id: string): void {
    if (this.destroyed || !this.collectionsAvailable() || this.navigationIsBlocked() || !this.projects().some(project => project.id === id)) return;
    this.selectionVersion++; this.clearSearch(); this.importStatus.set(''); this.editionStatus.set('');
    if (id !== this.selectedId()) { this.openSurfaceId.set(null); this.focusLineId.set(''); }
    this.selectedId.set(id); this.stats.set(null); this.review.set(null); this.syncUrl(id);
    this.workspaceAccess.open(id);
  }

  protected downloadProjectPdf(): void {
    if (!this.workspaceAvailable()) return;
    const intent = this.captureIntent();
    const projectId = intent.projectId;
    if (!projectId || this.destroyed) return;
    this.api.projectSearchablePdf(projectId, intent.credential).subscribe({
      next: blob => {
        if (!intent.current()) return;
        const url = URL.createObjectURL(blob);
        try {
          const anchor = document.createElement('a');
          anchor.href = url;
          anchor.download = `${projectId}.pdf`;
          anchor.click();
          this.editionStatus.set('searchable PDF downloaded');
        } finally { URL.revokeObjectURL(url); }
      },
      error: () => { if (intent.current()) this.editionStatus.set('searchable PDF export failed'); },
    });
  }

  protected downloadEdition(): void {
    if (!this.workspaceAvailable()) return;
    const intent = this.captureIntent();
    const projectId = intent.projectId;
    if (!projectId || this.destroyed) return;
    this.api.projectEditionHtml(projectId, intent.credential).subscribe({
      next: html => {
        if (!intent.current()) return;
        const url = URL.createObjectURL(new Blob([html], { type: 'text/html' }));
        try {
          const anchor = document.createElement('a');
          anchor.href = url;
          anchor.download = `${projectId}-edition.html`;
          anchor.click();
          this.editionStatus.set('edition HTML downloaded');
        } finally { URL.revokeObjectURL(url); }
      },
      error: () => { if (intent.current()) this.editionStatus.set('edition HTML export failed'); },
    });
  }

  protected openSurface(surfaceId: string, lineId = ''): void {
    if (this.destroyed || !this.workspaceAvailable()) return;
    if (this.navigationIsBlocked() && (surfaceId !== this.openSurfaceId() || !!lineId && lineId !== this.activeSurface()?.currentLineId())) return;
    const project = this.payloads()[this.selectedId()!];
    const surface = project?.objects.flatMap(object => object.surfaces).find(surface => surface.id === surfaceId);
    if (!surface || lineId && !surface.lines.some(line => line.id === lineId)) return;
    this.openSurfaceId.set(surfaceId);
    this.focusLineId.set(lineId);
    if (this.selectedId()) this.syncUrl(this.selectedId()!, surfaceId, lineId);
  }

  /** Restore `?project=&surface=&line=` without adding a history entry. */
  private restoreDeepLink(rows: ProjectRow[]): void {
    if (typeof window === 'undefined') return;
    const params = new URLSearchParams(window.location.search);
    const project = params.get('project');
    if (!project || !rows.some(p => p.id === project)) return;
    const surface = params.get('surface');
    this.pendingOpen = surface ? { surfaceId: surface, lineId: params.get('line') ?? '' } : null;
    this.select(project);
  }

  private syncUrl(projectId: string, surfaceId?: string, lineId?: string): void {
    if (typeof window === 'undefined') return;
    const params = new URLSearchParams();
    params.set('project', projectId);
    if (surfaceId) params.set('surface', surfaceId);
    if (lineId) params.set('line', lineId);
    window.history.replaceState(null, '', `${window.location.pathname}?${params.toString()}`);
  }

  protected surfaceIds(): string[] {
    const project = this.selectedId() ? this.payloads()[this.selectedId()!] : undefined;
    return project?.objects.flatMap(object => object.surfaces.map(surface => surface.id)) ?? [];
  }

  protected hasSurfaceNeighbour(delta: number): boolean {
    const ids = this.surfaceIds();
    const index = ids.indexOf(this.openSurfaceId() ?? '');
    const target = index + delta;
    return index >= 0 && target >= 0 && target < ids.length;
  }

  protected navigateSurface(delta: number): void {
    const ids = this.surfaceIds();
    const index = ids.indexOf(this.openSurfaceId() ?? '');
    const target = index + delta;
    if (index >= 0 && target >= 0 && target < ids.length) this.openSurface(ids[target]);
  }

  protected payload(id: string): ProjectPayload | undefined {
    return this.payloads()[id];
  }
}
