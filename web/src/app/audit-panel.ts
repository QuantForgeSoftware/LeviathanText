/** Admin-only authentication/authorization audit viewer. */
import { Component, signal } from '@angular/core';
import { ApiClient, AuthAuditEntry } from './api-client';

@Component({
  selector: 'app-audit-panel',
  standalone: true,
  template: `
    <details class="audit">
      <summary>Auth audit (admin)</summary>
      <div class="tools">
        <button type="button" (click)="load()">Load recent events</button>
        <span class="hint" role="status">{{ status() }}</span>
      </div>
      @if (events().length > 0) {
        <table>
          <thead><tr><th>When</th><th>User</th><th>Action</th><th>Request</th><th>Status</th></tr></thead>
          <tbody>
            @for (event of events(); track event.id) {
              <tr>
                <td>{{ event.at }}</td>
                <td>{{ event.userName ?? '—' }} <span class="hint">{{ event.role ?? '' }}</span></td>
                <td>{{ event.action }}</td>
                <td><code>{{ event.method }} {{ event.path }}</code></td>
                <td>{{ event.status ?? '' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </details>
  `,
  styles: [`
    .audit { margin-top: 0.6rem; }
    .tools { display: flex; gap: 0.5rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
    table { border-collapse: collapse; margin-top: 0.4rem; }
    th, td { border-bottom: 1px solid #dde; padding: 0.2rem 0.4rem; text-align: left; font-size: 0.85rem; }
  `],
})
export class AuditPanel {
  protected readonly events = signal<AuthAuditEntry[]>([]);
  protected readonly status = signal('');

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  protected load(): void {
    this.status.set('loading…');
    this.api.me().subscribe({
      next: me => {
        if (me.role !== 'admin') {
          this.status.set('admin only');
          return;
        }
        this.api.audit().subscribe({
          next: events => {
            this.events.set(events);
            this.status.set(`${events.length} event(s)`);
          },
          error: () => this.status.set('audit unavailable (admin only)'),
        });
      },
      error: () => this.status.set('identity unavailable'),
    });
  }
}
