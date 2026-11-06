import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { AuthPanel } from './auth-panel';
import { ApiClient } from './api-client';
import { apiKey, ApiKeyStorageKey, setApiKey } from './auth';
import { clearCache, cacheAuthorityRevision, CacheAuthorityRevisionStorageKey } from './offline-cache-storage';
import type { PrincipalProfile } from './principal-profile';

describe('AuthPanel independent identity request boundary', () => {
  beforeEach(() => { localStorage.clear(); setApiKey('generated-auth-a'); });
  afterEach(() => { setApiKey(''); localStorage.clear(); });
  const profile = (name: string): PrincipalProfile => ({ authenticated: true, name, role: 'Annotator', mode: 'account',
    accountId: `generated-${name}`, workspaceNamespace: '63b47b01-39c2-4cc1-92b4-3a0d3b198e16' });
  function setup() {
    const requests: Array<{ credential: string | undefined; response: Subject<PrincipalProfile> }> = [];
    const api = { me: (credential?: string) => { const response = new Subject<PrincipalProfile>(); requests.push({ credential, response }); return response; } };
    TestBed.configureTestingModule({ imports: [AuthPanel], providers: [{ provide: ApiClient, useValue: api }] });
    const fixture = TestBed.createComponent(AuthPanel); fixture.detectChanges();
    const component = fixture.componentInstance as unknown as { identity(): string };
    const click = (label: string) => {
      const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
        .find(item => item.textContent?.trim() === label)!; button.click(); fixture.detectChanges();
    };
    const save = (key: string) => { const input = fixture.nativeElement.querySelector('input[type=password]') as HTMLInputElement;
      input.value = key; input.dispatchEvent(new Event('input')); click('Save'); };
    return { fixture, component, requests, click, save };
  }

  it('clears the prior account identity immediately when a different credential is saved', () => {
    const s = setup(); s.requests[0].response.next(profile('PrivateAccountA')); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).toContain('PrivateAccountA');
    s.save('generated-auth-b');
    expect(s.fixture.nativeElement.textContent).not.toContain('PrivateAccountA');
    expect(s.requests.at(-1)?.credential).toBe('generated-auth-b');
  });

  it('does not let an older A identity response replace confirmed B', () => {
    const s = setup(); s.save('generated-auth-b');
    s.requests[1].response.next(profile('PrivateAccountB')); s.fixture.detectChanges();
    s.requests[0].response.next(profile('PrivateAccountA')); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).toContain('PrivateAccountB');
    expect(s.fixture.nativeElement.textContent).not.toContain('PrivateAccountA');
  });

  it('clears identity on key removal and ignores a response started before clear', () => {
    const s = setup(); s.click('Clear');
    s.requests[0].response.next(profile('PrivateAccountA')); s.fixture.detectChanges();
    expect(s.component.identity()).not.toContain('PrivateAccountA');
    expect(s.fixture.nativeElement.textContent).not.toContain('PrivateAccountA');
  });

  it('does not update a destroyed panel from its outstanding identity request', () => {
    const s = setup(); const before = s.component.identity(); s.fixture.destroy();
    s.requests[0].response.next(profile('PrivateAccountA'));
    expect(s.component.identity()).toBe(before);
  });

  it('fences visible identity and probes captured B after an external credential storage event', () => {
    const s = setup(); s.requests[0].response.next(profile('PrivateAccountA')); s.fixture.detectChanges();
    localStorage.setItem(ApiKeyStorageKey, 'generated-auth-b');
    globalThis.dispatchEvent(new StorageEvent('storage', { key: ApiKeyStorageKey, oldValue: 'generated-auth-a', newValue: 'generated-auth-b' }));
    s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).not.toContain('PrivateAccountA');
    expect(s.requests.length).toBe(2); expect(s.requests.at(-1)?.credential).toBe('generated-auth-b');
  });

  it('does not claim a local-only server merely because no key is configured', () => {
    setApiKey(''); const s = setup();
    expect(s.fixture.nativeElement.textContent).not.toContain('local-only server');
    expect(s.requests.length).toBe(1); expect(s.requests[0]?.credential).toBe('');
  });

  it('shows only a current captured account profile as the positive identity control', () => {
    const s = setup(); expect(s.requests[0].credential).toBe('generated-auth-a');
    s.requests[0].response.next(profile('ConfirmedCurrentAccount')); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).toContain('ConfirmedCurrentAccount (Annotator)');
  });

  it('confirms local mode only from the current fresh anonymous server profile', () => {
    setApiKey(''); const s = setup();
    expect(s.requests[0].credential).toBe('');
    expect(s.fixture.nativeElement.textContent).not.toContain('local server confirmed');
    s.requests[0].response.next({ authenticated: false, name: 'local', role: 'admin', mode: 'local', accountId: null, workspaceNamespace: null });
    s.fixture.detectChanges(); expect(s.fixture.nativeElement.textContent).toContain('local server confirmed');
  });

  it('does not report saved when credential storage rejects the new key', () => {
    const s = setup(); const original = Storage.prototype.setItem;
    const writes = spyOn(Storage.prototype, 'setItem').and.callFake(function(this: Storage, key: string, value: string) {
      if (key === ApiKeyStorageKey) throw new DOMException('generated denied credential write', 'QuotaExceededError');
      original.call(this, key, value);
    });
    s.save('generated-auth-b');
    expect(apiKey()).toBe('generated-auth-a');
    expect(s.fixture.nativeElement.textContent).toContain('Credential change could not be saved.');
    expect(s.requests.at(-1)?.credential).toBe('generated-auth-a');
    writes.and.callThrough();
  });

  it('does not report cleared when credential storage rejects removal', () => {
    const s = setup(); const original = Storage.prototype.removeItem;
    const removals = spyOn(Storage.prototype, 'removeItem').and.callFake(function(this: Storage, key: string) {
      if (key === ApiKeyStorageKey) throw new DOMException('generated denied credential removal', 'SecurityError');
      original.call(this, key);
    });
    s.click('Clear'); expect(apiKey()).toBe('generated-auth-a');
    expect(s.fixture.nativeElement.textContent).toContain('Credential change could not be saved.');
    expect(s.requests.at(-1)?.credential).toBe('generated-auth-a');
    removals.and.callThrough();
  });

  it('detects a direct same-tab key mutation on check without waiting for a storage event', () => {
    const s = setup(); s.requests[0].response.next(profile('PrivateAccountA')); s.fixture.detectChanges();
    localStorage.setItem(ApiKeyStorageKey, 'generated-auth-b'); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).not.toContain('PrivateAccountA');
    expect(s.requests.length).toBe(2); expect(s.requests[1].credential).toBe('generated-auth-b');
    s.requests[0].response.next(profile('OldAccountAgain')); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).not.toContain('OldAccountAgain');
  });

  it('fences an authority ABA by generation even when its final stored ticket is equal', () => {
    const s = setup(); const initialTicket = cacheAuthorityRevision()!;
    s.requests[0].response.next(profile('PrivateAccountA')); s.fixture.detectChanges();
    clearCache(); localStorage.setItem(CacheAuthorityRevisionStorageKey, initialTicket); s.fixture.detectChanges();
    expect(s.requests.length).toBe(2);
    expect(s.fixture.nativeElement.textContent).not.toContain('PrivateAccountA');
    s.requests[0].response.next(profile('OldAuthorityResponse')); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).not.toContain('OldAuthorityResponse');
  });

});
