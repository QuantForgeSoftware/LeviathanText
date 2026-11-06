/** API key entry for shared deployments (local-only mode when empty). */
import { Component, OnInit, OnDestroy, DoCheck, signal } from '@angular/core';
import { apiKey, hasApiKey, setApiKey, cacheCredentialGeneration, ApiKeyStorageKey } from './auth';
import { ApiClient } from './api-client';
import { cacheAuthorityRevision, ensureCacheAuthorityRevision, CacheAuthorityRevisionStorageKey } from './offline-cache-storage';
import { Subscription } from 'rxjs';
import { authorId, setAuthorId } from './identity';

@Component({
  selector: 'app-auth-panel',
  standalone: true,
  template: `
    <section>
      <h2>API key</h2>
      <p class="hint">{{ hasKey() ? 'Key configured' : 'No key configured' }} — {{ identity() }}</p>
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
export class AuthPanel implements OnInit, OnDestroy, DoCheck {
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

  private destroyed = false;
  private identityVersion = 0;
  private identityRequest: Subscription | null = null;
  private observed: { credential: string; generation: number; authority: string | null } | null = null;
  private readonly onStorage = (event: StorageEvent) => {
    if (event.key === null || event.key === ApiKeyStorageKey || event.key === CacheAuthorityRevisionStorageKey)
      this.loadIdentity();
  };

  ngOnInit(): void {
    globalThis.addEventListener?.('storage', this.onStorage);
    this.loadIdentity();
  }

  ngDoCheck(): void {
    if (!this.destroyed && this.observed && (this.observed.credential !== apiKey()
      || this.observed.generation !== cacheCredentialGeneration()
      || this.observed.authority !== cacheAuthorityRevision())) this.loadIdentity();
  }

  ngOnDestroy(): void {
    this.destroyed = true; this.identityVersion++;
    this.identityRequest?.unsubscribe(); this.identityRequest = null;
    globalThis.removeEventListener?.('storage', this.onStorage);
  }

  private loadIdentity(): void {
    if (this.destroyed) return;
    const credential = apiKey(), generation = cacheCredentialGeneration(), authority = ensureCacheAuthorityRevision();
    const version = ++this.identityVersion;
    this.observed = { credential, generation, authority };
    this.identityRequest?.unsubscribe(); this.identityRequest = null;
    this.hasKey.set(credential.length > 0);
    this.identity.set('checking identity…');
    const current = () => !this.destroyed && version === this.identityVersion
      && credential === apiKey() && generation === cacheCredentialGeneration() && authority === cacheAuthorityRevision();
    this.identityRequest = this.api.me(credential).subscribe({
      next: me => {
        if (!current()) return;
        this.identity.set(me.authenticated ? `${me.name} (${me.role})`
          : me.mode === 'local' ? 'local server confirmed' : 'identity not confirmed');
      },
      error: () => { if (current()) this.identity.set('identity not confirmed'); },
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
    const requested = this.draft().trim();
    setApiKey(requested);
    this.draft.set('');
    this.status.set(apiKey() === requested ? 'saved; checking current credentials' : 'Credential change could not be saved.');
    this.loadIdentity();
  }

  protected clear(): void {
    setApiKey('');
    this.status.set(apiKey() === '' ? 'cleared; checking server access' : 'Credential change could not be saved.');
    this.loadIdentity();
  }
}
