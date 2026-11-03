/**
 * Workspace-level session list (todo Phase 3 collaboration). Admins see every active session;
 * other principals see their own. Sessions are advisory TTL records, not locks or tokens.
 */
import { Component, OnInit, signal } from '@angular/core';
import { ApiClient, SessionView } from './api-client';

@Component({
  selector: 'app-sessions-admin-panel',
  standalone: true,
  template: `
    <details class="sessions-admin">
      <summary>Active sessions ({{ sessions().length }})</summary>
      <button type="button" (click)="refresh()">Refresh</button>
      @if (status()) { <span class="hint" role="status">{{ status() }}</span> }
      @if (sessions().length === 0) {
        <p class="hint">No active sessions visible to this account.</p>
      } @else {
        <ul>
          @for (session of sessions(); track session.id) {
            <li>
              <strong>{{ session.userName }}</strong>
              on <code>{{ session.nodeId }}</code>
              @if (session.surfaceId) { · surface <code>{{ session.surfaceId }}</code> }
              · expires {{ session.expiresAt }}
              <button type="button" (click)="revoke(session.id)">Revoke</button>
            </li>
          }
        </ul>
      }
    </details>
  `,
})
export class SessionsAdminPanel implements OnInit {
  protected readonly sessions = signal<SessionView[]>([]);
  protected readonly status = signal('');

  constructor(private readonly api: ApiClient) {}

  ngOnInit(): void {
    this.refresh();
  }

  protected refresh(): void {
    this.api.sessions().subscribe({
      next: list => {
        this.sessions.set(list);
        this.status.set('');
      },
      error: () => this.status.set('Sessions are unavailable (advisory).'),
    });
  }

  protected revoke(id: string): void {
    this.api.revokeSession(id).subscribe({
      next: () => {
        this.status.set('Session revoked.');
        this.refresh();
      },
      error: () => this.status.set('Only the owner or an administrator may revoke this session.'),
    });
  }
}
