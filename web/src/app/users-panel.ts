/** Admin-only account management: create, rotate and disable local accounts. */
import { Component, signal } from '@angular/core';
import { ApiClient, UserAccount } from './api-client';

@Component({
  selector: 'app-users-panel',
  standalone: true,
  template: `
    <details class="users">
      <summary>Accounts (admin)</summary>
      <div class="tools">
        <button type="button" (click)="load()">Load accounts</button>
        <span class="hint" role="status">{{ status() }}</span>
      </div>
      @if (loaded()) {
        <div class="tools">
          <input [value]="newName()" (input)="onName($event)" placeholder="account name" aria-label="Account name" />
          <select [value]="newRole()" (change)="onRole($event)" aria-label="Account role">
            <option value="reader">reader</option>
            <option value="annotator">annotator</option>
            <option value="reviewer">reviewer</option>
            <option value="admin">admin</option>
          </select>
          <button type="button" (click)="create()" [disabled]="!newName().trim()">Create</button>
        </div>
        @if (newKey()) {
          <p class="key">New API key (shown once): <code>{{ newKey() }}</code></p>
        }
        <ul>
          @for (user of users(); track user.id) {
            <li>
              <code>{{ user.id }}</code> {{ user.name }} <span class="hint">({{ user.role }}{{ user.disabled ? ', disabled' : '' }})</span>
              <button type="button" (click)="rotate(user)">Rotate key</button>
              @if (!user.disabled) { <button type="button" (click)="disable(user)">Disable</button> }
            </li>
          } @empty {
            <li class="hint">No accounts yet.</li>
          }
        </ul>
      }
    </details>
  `,
  styles: [`
    .users { margin-top: 0.6rem; }
    .tools { display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap; }
    .hint { color: #667; font-size: 0.85rem; }
    .key { background: #fff4d6; padding: 0.4rem; }
    li { margin: 0.2rem 0; }
  `],
})
export class UsersPanel {
  protected readonly users = signal<UserAccount[]>([]);
  protected readonly loaded = signal(false);
  protected readonly status = signal('');
  protected readonly newName = signal('');
  protected readonly newRole = signal('annotator');
  protected readonly newKey = signal('');

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  protected onName(event: Event): void {
    this.newName.set((event.target as HTMLInputElement).value);
  }

  protected onRole(event: Event): void {
    this.newRole.set((event.target as HTMLSelectElement).value);
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
            this.loaded.set(true);
            this.status.set(`${users.length} account(s)`);
          },
          error: () => this.status.set('accounts unavailable (admin only)'),
        });
      },
      error: () => this.status.set('identity unavailable'),
    });
  }

  protected create(): void {
    const name = this.newName().trim();
    if (!name) return;
    this.api.createUser(name, this.newRole()).subscribe({
      next: result => {
        this.users.update(users => [...users, result.account]);
        this.newKey.set(result.apiKey);
        this.newName.set('');
        this.status.set(`created ${result.account.name}`);
      },
      error: () => this.status.set('create failed (duplicate name or admin required)'),
    });
  }

  protected rotate(user: UserAccount): void {
    this.api.rotateUser(user.id).subscribe({
      next: result => {
        this.newKey.set(result.apiKey);
        this.status.set(`rotated key for ${user.name}`);
      },
      error: () => this.status.set('rotation failed'),
    });
  }

  protected disable(user: UserAccount): void {
    this.api.disableUser(user.id).subscribe({
      next: () => {
        this.users.update(users => users.map(u => u.id === user.id ? { ...u, disabled: true } : u));
        this.status.set(`disabled ${user.name}`);
      },
      error: () => this.status.set('disable failed'),
    });
  }
}
