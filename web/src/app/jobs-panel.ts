/**
 * Jobs panel (todo Phase 2: collection/job workflows). Creating a job is idempotent by
 * client key; progress arrives over the SSE endpoint (events are advisory — the job
 * resource stays authoritative, so the panel also re-reads the job once on completion).
 */
import { Component, OnDestroy, signal } from '@angular/core';
import { ApiClient } from './api-client';

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
      <button type="button" (click)="enqueue()">Enqueue noop job</button>
      <p>Job: <code>{{ jobId() ?? '—' }}</code></p>
      <ul>
        @for (event of events(); track $index) {
          <li><b>{{ event.kind }}</b>: <code>{{ event.data }}</code></li>
        } @empty {
          <li>No events yet.</li>
        }
      </ul>
    </section>
  `,
})
export class JobsPanel implements OnDestroy {
  protected readonly jobId = signal<string | null>(null);
  protected readonly events = signal<JobEventLine[]>([]);
  private source: EventSource | null = null;

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  protected enqueue(): void {
    this.api.createJob('noop', 'workspace').subscribe(job => {
      this.jobId.set(job.id);
      this.events.set([]);
      this.subscribe(job.id);
    });
  }

  private subscribe(jobId: string): void {
    this.source?.close();
    this.source = new EventSource(this.api.jobEventsUrl(jobId));
    for (const kind of ['job.created', 'job.progress', 'job.completed', 'job.failed', 'job.cancelled']) {
      this.source.addEventListener(kind, (e: MessageEvent) => {
        this.events.update(list => [...list, { kind, data: String(e.data).slice(0, 200) }]);
        if (kind === 'job.completed' || kind === 'job.failed' || kind === 'job.cancelled') {
          this.source?.close();
          this.source = null;
        }
      });
    }
  }

  ngOnDestroy(): void {
    this.source?.close();
  }
}
