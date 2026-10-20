/** Public invitation acceptance: token + name -> account/key, then store the key locally. */
import { Component, signal } from '@angular/core';
import { ApiClient } from './api-client';
import { setApiKey } from './auth';
import { clearCache } from './offline-cache';

@Component({
  selector: 'app-invite-accept-panel',
  standalone: true,
  template: `
    <details class="invite">
      <summary>Have an invitation?</summary>
      <div class="tools">
        <input [value]="token()" (input)="onToken($event)" placeholder="invitation token" aria-label="Invitation token" />
        <input [value]="name()" (input)="onName($event)" placeholder="account name" aria-label="Invited account name" />
        <button type="button" (click)="accept()" [disabled]="!token().trim() || !name().trim()">Accept invitation</button>
        <span class="hint" role="status">{{ status() }}</span>
      </div>
    </details>
  `,
  styles: [`
    .invite { margin-top: 0.6rem; }
    .tools { display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap; }
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class InviteAcceptPanel {
  protected readonly token = signal('');
  protected readonly name = signal('');
  protected readonly status = signal('');

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  protected onToken(event: Event): void {
    this.token.set((event.target as HTMLInputElement).value);
  }

  protected onName(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
  }

  protected accept(): void {
    this.api.acceptInvite(this.token().trim(), this.name().trim()).subscribe({
      next: result => {
        setApiKey(result.apiKey);
        clearCache();
        this.token.set('');
        this.status.set(`accepted as ${result.account.name} (${result.role}); key saved — reload`);
      },
      error: () => this.status.set('invitation is invalid, expired or already used'),
    });
  }
}
