/**
 * Jobs panel (todo Phase 2: collection/job workflows). Lists recent jobs from the
 * database-backed endpoint, lets an operator watch any job over SSE (events are advisory;
 * the job resource stays authoritative), and keeps the idempotent noop enqueue as a
 * smoke action.
 */
import { Component, DestroyRef, effect, inject, InjectionToken, OnDestroy, OnInit, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, Subscription } from 'rxjs';
import { ApiClient, JobSummary } from './api-client';
import { apiKey, apiKeyRevision, ApiKeyStorageKey, cacheCredentialGeneration } from './auth';
import { cacheAuthorityRevision, CacheAuthorityRevisionStorageKey, clearCache } from './offline-cache-storage';
import { subscribeWorkspaceBoundary } from './workspace-access';
import { openHeaderJobStream, HeaderJobStream, HeaderJobStreamOptions } from './header-job-stream';

export const JOB_STREAM_FACTORY = new InjectionToken<typeof openHeaderJobStream>('HEADER_JOB_STREAM_FACTORY', { providedIn: 'root', factory: () => openHeaderJobStream });
interface JobCredentialScope { key: string; counter: number; revision: number; generation: number; authority: string | null; }

interface JobEventLine {
  kind: string;
  data: string;
}

@Component({
  selector: 'app-jobs-panel',
  standalone: true,
  template: `
    <section>
      <h2>Jobs</h2>
      <div class="tools">
        <button type="button" (click)="refresh()">Refresh</button>
        <button type="button" (click)="enqueue()" [disabled]="enqueuing()">Enqueue noop job</button>
        @if (watching()) { <span class="hint">watching <code>{{ watching() }}</code></span> }
      </div>
      @if (status()) { <p class="hint" role="status">{{ status() }}</p> }

      <table>
        <thead>
          <tr><th>Job</th><th>Type</th><th>Status</th><th>Retries</th><th></th></tr>
        </thead>
        <tbody>
          @for (job of jobs(); track job.id) {
            <tr>
              <td><code>{{ job.id }}</code></td>
              <td>{{ job.type }}</td>
              <td><b>{{ job.status }}</b></td>
              <td>{{ job.retriesUsed }}</td>
              <td><button type="button" (click)="watch(job.id)">Watch</button></td>
            </tr>
          } @empty {
            <tr><td colspan="5">No jobs yet.</td></tr>
          }
        </tbody>
      </table>

      <h3>Events</h3>
      @if (omittedEvents() > 0) { <p class="hint">Showing the latest200 advisory events; refresh for current job state.</p> }
      <ul>
        @for (event of events(); track $index) {
          <li><b>{{ event.kind }}</b>: <code>{{ event.data }}</code></li>
        } @empty {
          <li>No events yet.</li>
        }
      </ul>
    </section>
  `,
  styles: [`
    table { border-collapse: collapse; margin-top: 0.5rem; }
    th, td { border-bottom: 1px solid #dde; padding: 0.25rem 0.6rem; text-align: left; font-size: 0.9rem; }
    .tools { display: flex; gap: 0.5rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class JobsPanel implements OnInit, OnDestroy {
  protected readonly jobs = signal<JobSummary[]>([]);
  protected readonly watching = signal<string | null>(null);
  protected readonly events = signal<JobEventLine[]>([]);
  protected readonly enqueuing = signal(false);
  protected readonly status = signal('');
  protected readonly omittedEvents = signal(0);
  private source: HeaderJobStream | null = null;
  private readonly openStream = inject(JOB_STREAM_FACTORY);
  private readonly unsubscribeBoundary: () => void;
  private watchGeneration = 0;
  private refreshGeneration = 0;
  private snapshotGeneration = 0;
  private credentialGeneration = 0;
  private revision = apiKeyRevision();
  private destroyed = false;
  private refreshRequest: Subscription | null = null;
  private snapshotRequest: Subscription | null = null;
  private enqueueRequest: Subscription | null = null;
  private readonly destroyRef = inject(DestroyRef);
  private readonly onStorage = (event: StorageEvent) => {
    if (event.key === ApiKeyStorageKey || event.key === CacheAuthorityRevisionStorageKey || event.key === null) this.credentialsChanged();
  };

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
    this.unsubscribeBoundary = subscribeWorkspaceBoundary(() => this.pauseCredentials());
    effect(() => {
      const revision = apiKeyRevision();
      if (revision !== this.revision) { this.revision = revision; untracked(() => this.credentialsChanged()); }
    });
  }

  ngOnInit(): void {
    window.addEventListener('storage', this.onStorage);
    this.refresh();
  }

  protected refresh(selectedJobId?: string): void {
    if (this.destroyed) return;
    this.cancelSnapshot();
    this.refreshRequest?.unsubscribe();
    const generation = ++this.refreshGeneration; const credential = this.captureCredentials();
    const watched = selectedJobId ?? this.watching(); const watch = this.watchGeneration;
    const current = () => !this.destroyed && generation === this.refreshGeneration
      && this.currentCredentials(credential);
    this.refreshRequest = this.api.jobs(credential.key, current).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: list => {
        if (!current()) return;
        const visible = list.slice(0, 100);
        this.jobs.set(visible);
        if (watched && watch === this.watchGeneration && !visible.some(job => job.id === watched))
          this.refreshWatched(watched, watch, credential);
      },
      error: error => {
        if (!current()) return;
        this.jobs.set([]);
        if (error?.status === 401 || error?.status === 403) clearCache();
      },
    });
  }

  protected enqueue(): void {
    if (this.destroyed || this.enqueuing()) return;
    const generation = this.watchGeneration; const credential = this.captureCredentials();
    this.enqueuing.set(true);
    this.enqueueRequest = this.api.createJob('noop', 'workspace', undefined, credential.key, () => this.currentCredentials(credential)).pipe(takeUntilDestroyed(this.destroyRef),
      finalize(() => this.enqueuing.set(false))).subscribe({
      next: job => {
        if (!this.currentCredentials(credential)) return;
        if (generation === this.watchGeneration) this.watch(job.id);
        this.refresh();
      },
      error: error => {
        if (!this.currentCredentials(credential)) return;
        // A write-role refusal does not revoke otherwise valid read access.
        if (error?.status === 401) clearCache();
        this.status.set('Could not confirm the job. Refresh before enqueueing again.');
      },
    });
  }

  protected watch(jobId: string): void {
    if (this.destroyed) return;
    this.source?.close();
    this.cancelSnapshot();
    const generation = ++this.watchGeneration; const credential = this.captureCredentials();
    this.watching.set(jobId);
    this.events.set([]);
    this.omittedEvents.set(0); this.status.set('');
    let source: HeaderJobStream;
    const current = () => this.source === source && generation === this.watchGeneration && this.currentCredentials(credential);
    const options: HeaderJobStreamOptions = {
      current,
      onEvent: event => {
        if (!current()) return;
        const kind = event.type;
        if (['job.created', 'job.progress', 'job.completed', 'job.failed', 'job.cancelled'].includes(kind)) {
          this.record(kind, event.data);
          if (kind === 'job.completed' || kind === 'job.failed' || kind === 'job.cancelled') {
            this.stopSource(source);
            this.refresh(jobId); // authoritative status, never the advisory payload
          }
        } else if (kind === 'job.resync-required') {
          this.record(kind, 'Refreshing authoritative job state.');
          let capacity = false;
          try { capacity = event.data.length <= 1024 && JSON.parse(event.data).reason === 'subscription_capacity'; } catch { /* malformed advisory still requests a refresh */ }
          this.status.set(capacity ? 'Live update capacity reached. Refresh for current status.' : 'Live history has a gap; current job state is being refreshed.');
          if (capacity) this.stopSource(source);
          this.refreshWatched(jobId, generation, credential);
        }
      },
      onState: notice => {
        if (!current() || notice.state !== 'closed' || notice.reason === 'manual' || notice.reason === 'terminal' || notice.reason === 'stale') return;
        if (notice.reason === 'authorization' && (notice.status === 401 || notice.status === 403)) {
          // Only this still-current request may revoke shared cached authority.
          // The notification pauses every workspace before the authority changes.
          clearCache();
          if (!this.destroyed && apiKey() === credential.key)
            this.status.set('Live update access was refused. Refresh with the current credential.');
          return;
        }
        this.stopSource(source);
        if (notice.reason === 'not-found') {
          this.jobs.set([]); this.events.set([]); this.omittedEvents.set(0);
          this.status.set('Live update access was refused. Refresh with the current credential.');
        } else {
          this.status.set(notice.reason === 'capacity' ? 'Live update capacity reached. Refresh for current status.' : 'Live updates ended. Refresh for current status.');
          this.refreshWatched(jobId, generation, credential);
        }
      },
    };
    try { source = this.openStream(this.api.jobEventsUrl(jobId), credential.key, options); }
    catch { this.source = null; this.watching.set(null); this.status.set('Live updates are unavailable. Refresh for current job state.'); return; }
    this.source = source;
    void source.settled.catch(() => {
      if (generation === this.watchGeneration && this.currentCredentials(credential)) {
        this.stopSource(source); this.status.set('Live updates ended. Refresh for current status.');
      }
    });
  }

  private record(kind: string, data: string): void {
    this.events.update(list => {
      const next = [...list, { kind, data: data.slice(0, 200) }];
      if (next.length > 200) { this.omittedEvents.update(count => count + next.length - 200); return next.slice(-200); }
      return next;
    });
  }

  private refreshWatched(jobId: string, generation: number, credential: JobCredentialScope): void {
    if (this.snapshotRequest && !this.snapshotRequest.closed) return;
    // One authoritative read lane: a later selected-job read supersedes an
    // in-flight list read, just as a later list refresh supersedes a snapshot.
    this.refreshGeneration++; this.refreshRequest?.unsubscribe();
    const snapshot = ++this.snapshotGeneration;
    const current = () => snapshot === this.snapshotGeneration && generation === this.watchGeneration && this.currentCredentials(credential);
    this.snapshotRequest = this.api.jobSnapshot(jobId, credential.key, current).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: job => {
        if (!current() || job.id !== jobId) return;
        this.jobs.update(list => [job, ...list.filter(item => item.id !== jobId)].slice(0, 100));
      },
      error: error => {
        if (!current()) return;
        this.status.set('Could not refresh current job state.');
        if (error?.status === 401 || error?.status === 403) {
          clearCache(); // Fence all mounted workspaces on this current authoritative refusal.
        }
      },
    });
  }

  private captureCredentials(): JobCredentialScope {
    return { key: apiKey(), counter: this.credentialGeneration, revision: apiKeyRevision(),
      generation: cacheCredentialGeneration(), authority: cacheAuthorityRevision() };
  }
  private currentCredentials(scope: JobCredentialScope): boolean {
    return !this.destroyed && scope.counter === this.credentialGeneration && scope.revision === apiKeyRevision()
      && scope.key === apiKey() && scope.generation === cacheCredentialGeneration() && scope.authority === cacheAuthorityRevision();
  }

  private cancelSnapshot(): void {
    this.snapshotGeneration++; this.snapshotRequest?.unsubscribe(); this.snapshotRequest = null;
  }

  private stopSource(source: HeaderJobStream): void {
    source.close();
    if (this.source === source) { this.source = null; this.watching.set(null); }
  }

  private pauseCredentials(): void {
    if (this.destroyed) return;
    this.credentialGeneration++; this.watchGeneration++; this.refreshGeneration++;
    if (this.source) this.stopSource(this.source);
    this.cancelSnapshot(); this.refreshRequest?.unsubscribe(); this.enqueueRequest?.unsubscribe();
    this.events.set([]); this.jobs.set([]); this.omittedEvents.set(0);
    this.status.set('Access changed. Refresh jobs with the current credential.');
  }
  private credentialsChanged(): void {
    if (this.destroyed) return;
    this.pauseCredentials(); this.refresh(); // storage/key notifications occur AFTER mutation
  }

  ngOnDestroy(): void {
    this.destroyed = true; this.unsubscribeBoundary(); this.watchGeneration++; this.refreshGeneration++;
    this.source?.close();
    this.source = null;
    this.cancelSnapshot(); this.refreshRequest?.unsubscribe(); this.enqueueRequest?.unsubscribe();
    window.removeEventListener('storage', this.onStorage);
  }
}
