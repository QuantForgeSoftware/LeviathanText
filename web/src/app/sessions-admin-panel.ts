/**
 * Workspace-level session list (todo Phase 3 collaboration). Admins see every active session;
 * other principals see their own. Sessions are advisory TTL records, not locks or tokens.
 */
import { Component, OnInit, signal } from '@angular/core';
import { WorkspaceComponentScope } from './workspace-component-scope';
import { ApiClient, SessionView } from './api-client';

@Component({
  selector: 'app-sessions-admin-panel',
  standalone: true,
  template: `
    <details data-workspace-protected [hidden]="!workspaceAvailable()" [attr.inert]="!workspaceAvailable() ? '' : null" class="sessions-admin">
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
              <button type="button" (click)="revoke(session.id)" [disabled]="!workspaceCanEdit()">Revoke</button>
            </li>
          }
        </ul>
      }
    </details>
  `,
})
export class SessionsAdminPanel implements OnInit {
  protected workspaceAvailable(): boolean { return this.scope.active(); }
  protected workspaceAdmin(): boolean { return this.scope.role('admin'); }
  protected workspaceCanEdit(): boolean { return this.scope.mayEdit(); }
  private get api(): ApiClient { return this.scope.request(this.baseApi); }

  private readonly baseApi: ApiClient;
  private readonly scope: WorkspaceComponentScope;
  protected readonly sessions = signal<SessionView[]>([]);
  protected readonly status = signal('');

  constructor(api: ApiClient) { this.baseApi = api; this.scope = new WorkspaceComponentScope(() => { if (this.ready && this.workspaceAvailable()) this.refresh(); }); }

  private ready = false;
  ngOnInit(): void {
    this.ready = true;
    this.refresh();
  }

  protected refresh(): void {
    if (!this.workspaceAvailable()) return;
    this.api.sessions().subscribe({
      next: list => {
        this.sessions.set(list);
        this.status.set('');
      },
      error: () => this.status.set('Sessions are unavailable (advisory).'),
    });
  }

  protected revoke(id: string): void {
    if (!this.workspaceCanEdit()) return;
    const accountId = this.scope.access?.state().principal?.accountId;
    if (!this.workspaceAdmin() && this.sessions().find(session => session.id === id)?.userId !== accountId) return;
    this.api.revokeSession(id).subscribe({
      next: () => {
        this.status.set('Session revoked.');
        this.refresh();
      },
      error: () => this.status.set('Only the owner or an administrator may revoke this session.'),
    });
  }
}
