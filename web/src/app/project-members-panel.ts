/** Admin-only project membership editor (opt-in per-project scoping). */
import { Component, OnChanges, SimpleChanges, input, signal } from '@angular/core';
import { ApiClient, UserAccount } from './api-client';

@Component({
  selector: 'app-project-members-panel',
  standalone: true,
  template: `
    <details class="members">
      <summary>Project members (opt-in access scoping)</summary>
      <div class="tools">
        <button type="button" (click)="load()">Load members</button>
        <span class="hint" role="status">{{ status() }}</span>
      </div>
      @if (loaded()) {
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
    li { margin: 0.2rem 0; }
  `],
})
export class ProjectMembersPanel implements OnChanges {
  projectId = input.required<string>();
  protected readonly users = signal<UserAccount[]>([]);
  protected readonly members = signal<string[]>([]);
  protected readonly loaded = signal(false);
  protected readonly status = signal('');

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Lazy by design: the panel is visible to readers but the admin endpoints are only
    // called when the user explicitly opens/loads it. Switching projects clears the prior
    // member list so a stale project is never shown.
    if (changes['projectId'] && !changes['projectId'].firstChange) {
      this.loaded.set(false);
      this.members.set([]);
      this.status.set('');
    }
  }

  protected isMember(userId: string): boolean {
    return this.members().includes(userId);
  }

  protected load(): void {
    this.status.set('loading…');
    this.api.me().subscribe({
      next: me => {
        if (me.role !== 'admin') {
          this.status.set('admin only');
          return;
        }
        this.api.users().subscribe({
          next: users => {
            this.users.set(users);
            this.api.projectMembers(this.projectId()).subscribe({
              next: members => {
                this.members.set(members);
                this.loaded.set(true);
                this.status.set(`${members.length} member(s)`);
              },
              error: () => this.status.set('membership unavailable'),
            });
          },
          error: () => this.status.set('account list unavailable (admin only)'),
        });
      },
      error: () => this.status.set('identity unavailable'),
    });
  }

  protected add(userId: string): void {
    this.api.addProjectMember(this.projectId(), userId).subscribe({
      next: members => { this.members.set(members); this.status.set(`${members.length} member(s)`); },
      error: () => this.status.set('add failed'),
    });
  }

  protected remove(userId: string): void {
    this.api.removeProjectMember(this.projectId(), userId).subscribe({
      next: () => {
        this.members.update(members => members.filter(id => id !== userId));
        this.status.set(`${this.members().length} member(s)`);
      },
      error: () => this.status.set('remove failed'),
    });
  }
}
