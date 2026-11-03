/**
 * Editing-session panel (todo Phase 3 collaboration): lists the server-issued TTL sessions
 * for this surface and lets the owner/admin revoke one. Sessions are advisory coordination
 * records, not locks or authentication tokens.
 */
import { Component, OnInit, input, signal } from '@angular/core';
import { ApiClient, SessionView } from './api-client';

@Component({
  selector: 'app-session-panel',
  standalone: true,
  template: `
    <details class="sessions">
      <summary>Sessions ({{ sessions().length }})</summary>
      <p class="hint">
        Server-issued sessions expire without a heartbeat. Revoking one ends its presence;
        it does not undo edits or change project access.
      </p>
      <button type="button" (click)="refresh()">Refresh</button>
      @if (status()) { <span class="hint" role="status">{{ status() }}</span> }
      @if (sessions().length === 0) {
        <p class="hint">No active sessions.</p>
      } @else {
        <ul>
          @for (session of sessions(); track session.id) {
            <li>
              <strong>{{ session.userName }}</strong>
              on <code>{{ session.nodeId }}</code>
              · expires {{ session.expiresAt }}
              <button type="button" (click)="revoke(session.id)">Revoke</button>
            </li>
          }
        </ul>
      }
    </details>
  `,
})
export class SessionPanel implements OnInit {
  surfaceId = input.required<string>();

  protected readonly sessions = signal<SessionView[]>([]);
  protected readonly status = signal('');

  constructor(private readonly api: ApiClient) {}

  ngOnInit(): void {
    this.refresh();
  }

  protected refresh(): void {
    this.api.sessions(this.surfaceId()).subscribe({
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
