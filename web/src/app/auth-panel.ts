/** API key entry for shared deployments (local-only mode when empty). */
import { Component, OnInit, signal } from '@angular/core';
import { apiKey, hasApiKey, setApiKey } from './auth';
import { ApiClient } from './api-client';
import { clearCache } from './offline-cache';
import { authorId, setAuthorId } from './identity';

@Component({
  selector: 'app-auth-panel',
  standalone: true,
  template: `
    <section>
      <h2>API key</h2>
      @if (hasKey()) {
        <p class="hint">Key configured — {{ identity() }}</p>
      } @else {
        <p class="hint">No key configured — local-only server.</p>
      }
      <div class="tools">
        <input [value]="draft()" (input)="onInput($event)" placeholder="paste API key" type="password" aria-label="API key" />
        <button type="button" (click)="save()">Save</button>
        <button type="button" (click)="clear()">Clear</button>
        <span class="hint">{{ status() }}</span>
      </div>
      <div class="tools">
        <label>Editor name
          <input [value]="author()" (input)="onAuthor($event)" placeholder="name recorded on revisions/reviews" />
        </label>
        <button type="button" (click)="saveAuthor()">Save name</button>
        <span class="hint">{{ authorStatus() }}</span>
      </div>
    </section>
  `,
  styles: [`
    .tools { display: flex; gap: 0.5rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class AuthPanel implements OnInit {
  protected readonly draft = signal('');
  protected readonly hasKey = signal(hasApiKey());
  protected readonly status = signal('');
  protected readonly identity = signal('checking identity…');
  protected readonly author = signal(authorId());
  protected readonly authorStatus = signal('');

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnInit(): void {
    if (this.hasKey()) this.loadIdentity();
  }

  private loadIdentity(): void {
    this.api.me().subscribe({
      next: (me) => this.identity.set(me.authenticated ? `${me.name} (${me.role})` : 'local-only'),
      error: () => this.identity.set('key rejected or server unreachable'),
    });
  }

  protected onAuthor(event: Event): void {
    this.author.set((event.target as HTMLInputElement).value);
  }

  protected saveAuthor(): void {
    setAuthorId(this.author());
    this.authorStatus.set('saved; used for revisions and reviews');
  }

  protected onInput(event: Event): void {
    this.draft.set((event.target as HTMLInputElement).value);
  }

  protected save(): void {
    setApiKey(this.draft());
    this.draft.set('');
    clearCache();
    this.hasKey.set(hasApiKey());
    this.status.set('saved; reload data to use the new credentials');
    if (this.hasKey()) this.loadIdentity();
  }

  protected clear(): void {
    setApiKey('');
    clearCache();
    this.hasKey.set(false);
    this.status.set('cleared');
  }
}
