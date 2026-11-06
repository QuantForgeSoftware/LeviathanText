/** Admin-only project membership editor (opt-in per-project scoping). */
import { Component, OnChanges, SimpleChanges, input, signal } from '@angular/core';
import { WorkspaceComponentScope } from './workspace-component-scope';
import { ApiClient, ProjectInviteView, UserAccount } from './api-client';

@Component({
  selector: 'app-project-members-panel',
  standalone: true,
  template: `
    <details data-workspace-protected data-workspace-role="admin" [hidden]="!workspaceAvailable() || !workspaceAdmin()" [attr.inert]="!workspaceAvailable() || !workspaceAdmin() ? '' : null" class="members">
      <summary>Project members (opt-in access scoping)</summary>
      <div class="tools">
        <button type="button" (click)="load()">Load members</button>
        <span class="hint" role="status">{{ status() }}</span>
      </div>
      @if (loaded()) {
        <div class="tools invite">
          <select [value]="inviteRole()" (change)="onInviteRole($event)" aria-label="Invitation role">
            <option value="reader">reader</option>
            <option value="annotator">annotator</option>
            <option value="reviewer">reviewer</option>
          </select>
          <input type="number" min="1" max="168" [value]="inviteHours()" (input)="onInviteHours($event)" aria-label="Invitation hours" />
          <button type="button" (click)="createInvite()">Create invitation</button>
        </div>
        @if (inviteToken()) {
          <p class="key">Invitation token (shown once): <code>{{ inviteToken() }}</code></p>
        }
        @if (invites().length > 0) {
          <ul class="invites">
            @for (invite of invites(); track invite.id) {
              <li>
                <code>{{ invite.id }}</code> {{ invite.role }} · expires {{ invite.expiresAt }}
                @if (invite.usedAt) {
                  <span class="hint">used</span>
                } @else {
                  <button type="button" (click)="revokeInvite(invite.id)">Revoke</button>
                }
              </li>
            }
          </ul>
        }
        <ul>
          @for (user of users(); track user.id) {
            <li>
              <code>{{ user.id }}</code> {{ user.name }} <span class="hint">({{ user.role }})</span>
              @if (isMember(user.id)) {
                <button type="button" (click)="remove(user.id)">Remove</button>
              } @else {
                <button type="button" (click)="add(user.id)">Add</button>
              }
            </li>
          } @empty {
            <li class="hint">No accounts yet.</li>
          }
        </ul>
      }
    </details>
  `,
  styles: [`
    .members { margin-top: 0.6rem; }
    .tools { display: flex; gap: 0.5rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
    .key { background: #fff4d6; padding: 0.35rem; }
    li { margin: 0.2rem 0; }
  `],
})
export class ProjectMembersPanel implements OnChanges {
  protected workspaceAvailable(): boolean { return this.scope.active(); }
  protected workspaceAdmin(): boolean { return this.scope.role('admin'); }
  protected workspaceCanEdit(): boolean { return this.scope.mayEdit(); }
  private get api(): ApiClient { const id = this.projectId(); return this.scope.request(this.baseApi, id, () => this.projectId() === id); }

  projectId = input.required<string>();
  protected readonly users = signal<UserAccount[]>([]);
  protected readonly members = signal<string[]>([]);
  protected readonly loaded = signal(false);
  protected readonly status = signal('');
  protected readonly inviteRole = signal('annotator');
  protected readonly inviteHours = signal(24);
  protected readonly inviteToken = signal('');
  protected readonly invites = signal<ProjectInviteView[]>([]);

  private readonly baseApi: ApiClient;
  private readonly scope: WorkspaceComponentScope;

  constructor(api: ApiClient) {
    this.baseApi = api;
    this.scope = new WorkspaceComponentScope();
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Lazy by design: the panel is visible to readers but the admin endpoints are only
    // called when the user explicitly opens/loads it. Switching projects clears the prior
    // member list so a stale project is never shown.
    if (changes['projectId'] && !changes['projectId'].firstChange) {
      this.loaded.set(false);
      this.members.set([]);
      this.invites.set([]);
      this.status.set('');
    }
  }

  protected onInviteRole(event: Event): void {
    if (!this.workspaceAdmin()) { (event.target as HTMLInputElement).value = this.inviteRole(); return; }
    this.inviteRole.set((event.target as HTMLSelectElement).value);
  }

  protected onInviteHours(event: Event): void {
    if (!this.workspaceAdmin()) { (event.target as HTMLInputElement).value = String(this.inviteHours()); return; }
    this.inviteHours.set(Math.max(1, Math.min(168, Number((event.target as HTMLInputElement).value) || 24)));
  }

  protected createInvite(): void {
    if (!this.workspaceAdmin()) return;
    this.api.createInvite(this.projectId(), this.inviteRole(), this.inviteHours()).subscribe({
      next: result => {
        this.inviteToken.set(result.token);
        this.status.set(`invitation valid until ${result.expiresAt}`);
        this.loadInvites();
      },
      error: () => this.status.set('invitation creation failed (admin only)'),
    });
  }

  protected isMember(userId: string): boolean {
    return this.members().includes(userId);
  }

  protected load(): void {
    if (!this.workspaceAdmin()) return;
    this.status.set('loading…');
    this.api.users().subscribe({
          next: users => {
            this.users.set(users);
            this.api.projectMembers(this.projectId()).subscribe({
              next: members => {
                this.members.set(members);
                this.loaded.set(true);
                this.status.set(`${members.length} member(s)`);
                this.loadInvites();
              },
              error: () => this.status.set('membership unavailable'),
            });
          },
          error: () => this.status.set('account list unavailable (admin only)'),
    });
  }

  private loadInvites(): void {
    if (!this.workspaceAdmin()) return;
    this.api.invites(this.projectId()).subscribe({
      next: invites => this.invites.set(invites),
      error: () => { /* admin-only; keep the current list */ },
    });
  }

  protected revokeInvite(inviteId: string): void {
    if (!this.workspaceAdmin()) return;
    this.api.revokeInvite(this.projectId(), inviteId).subscribe({
      next: () => {
        this.invites.update(invites => invites.filter(invite => invite.id !== inviteId));
        this.status.set('invitation revoked');
      },
      error: () => this.status.set('only an unused invitation can be revoked'),
    });
  }

  protected add(userId: string): void {
    if (!this.workspaceAdmin()) return;
    this.api.addProjectMember(this.projectId(), userId).subscribe({
      next: members => { this.members.set(members); this.status.set(`${members.length} member(s)`); },
      error: () => this.status.set('add failed'),
    });
  }

  protected remove(userId: string): void {
    if (!this.workspaceAdmin()) return;
    this.api.removeProjectMember(this.projectId(), userId).subscribe({
      next: () => {
        this.members.update(members => members.filter(id => id !== userId));
        this.status.set(`${this.members().length} member(s)`);
      },
      error: () => this.status.set('remove failed'),
    });
  }
}
