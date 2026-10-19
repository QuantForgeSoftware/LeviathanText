/**
 * Jobs panel (todo Phase 2: collection/job workflows). Lists recent jobs from the
 * database-backed endpoint, lets an operator watch any job over SSE (events are advisory;
 * the job resource stays authoritative), and keeps the idempotent noop enqueue as a
 * smoke action.
 */
import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { ApiClient, JobSummary } from './api-client';

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
        <button type="button" (click)="enqueue()">Enqueue noop job</button>
        @if (watching()) { <span class="hint">watching <code>{{ watching() }}</code></span> }
      </div>

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
            <li>No jobs yet.</li>
          }
        </tbody>
      </table>

      <h3>Events</h3>
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
  private source: EventSource | null = null;

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnInit(): void {
    this.refresh();
  }

  protected refresh(): void {
    this.api.jobs().subscribe({
      next: list => this.jobs.set(list),
      error: () => this.jobs.set([]),
    });
  }

  protected enqueue(): void {
    this.api.createJob('noop', 'workspace').subscribe(job => {
      this.events.set([]);
      this.watch(job.id);
      this.refresh();
    });
  }

  protected watch(jobId: string): void {
    this.source?.close();
    this.watching.set(jobId);
    this.events.set([]);
    this.source = new EventSource(this.api.jobEventsUrl(jobId));
    for (const kind of ['job.created', 'job.progress', 'job.completed', 'job.failed', 'job.cancelled']) {
      this.source.addEventListener(kind, (e: MessageEvent) => {
        this.events.update(list => [...list, { kind, data: String(e.data).slice(0, 200) }]);
        if (kind === 'job.completed' || kind === 'job.failed' || kind === 'job.cancelled') {
          this.source?.close();
          this.source = null;
          this.refresh(); // authoritative status, not the event payload
        }
      });
    }
  }

  ngOnDestroy(): void {
    this.source?.close();
  }
}
