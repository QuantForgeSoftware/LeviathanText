import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject, firstValueFrom, of, throwError } from 'rxjs';
import { setApiKey } from './auth';
import { cachedRequest, clearCache, offlineMode, readCache, writeCache } from './offline-cache';

/** Generated client authority controls; no network, collection data or provider. */
describe('offline cache authorization boundary', () => {
  const key = 'project:generated-private';
  const privateValue = { id: 'generated-private', text: 'reader A generated text' };
  const authorityKey = 'leviathan:cache-authority-revision';
  const offline = () => new HttpErrorResponse({ status: 0, statusText: 'generated transport failure' });
  const capture = <T>(source: Observable<T>): Promise<{ emitted: boolean; value?: unknown; error?: unknown }> =>
    firstValueFrom(source).then(value => ({ emitted: true, value }), error => ({ emitted: false, error }));
  const seed = (value = privateValue) => firstValueFrom(cachedRequest(key, of(value)));
  beforeEach(() => { localStorage.clear(); setApiKey('generated-reader-a'); offlineMode.set(false); });
  afterEach(() => { setApiKey(''); clearCache(); localStorage.clear(); });

  for (const status of [401, 403, 404, 500]) {
    it(`does not turn authoritative HTTP ${status} into a cached successful read`, async () => {
      await seed();
      const denied = new HttpErrorResponse({ status, statusText: 'generated refusal' });
      const result = await capture(cachedRequest(key, throwError(() => denied)));
      expect(result.emitted).toBeFalse(); expect(result.error).toBe(denied); expect(offlineMode()).toBeFalse();
    });
  }

  it('retains the supported same-credential transport-offline fallback', async () => {
    await seed(); const result = await capture(cachedRequest(key, throwError(offline)));
    expect(result.value).toEqual(privateValue); expect(offlineMode()).toBeTrue();
  });

  it('clears read-only caches through the shared setter without deleting drafts', async () => {
    await seed(); localStorage.setItem('leviathan:draft:generated-line', 'unsaved generated draft');
    setApiKey('generated-reader-b'); expect(readCache(key)).toBeNull();
    expect(localStorage.getItem('leviathan:draft:generated-line')).toBe('unsaved generated draft');
  });

  it('neither emits nor caches a successful response from an older credential', async () => {
    const source = new Subject<typeof privateValue>(); const pending = capture(cachedRequest(key, source));
    setApiKey('generated-reader-b'); source.next(privateValue); source.complete();
    expect((await pending).emitted).toBeFalse(); expect(readCache(key)).toBeNull(); expect(offlineMode()).toBeFalse();
  });

  it('rejects an old response after a credential A-to-B-to-A cycle', async () => {
    const source = new Subject<typeof privateValue>(); const pending = capture(cachedRequest(key, source));
    setApiKey('generated-reader-b'); setApiKey('generated-reader-a'); source.next(privateValue); source.complete();
    expect((await pending).emitted).toBeFalse(); expect(readCache(key)).toBeNull();
  });

  it('cannot refill the new credential cache from a late error and old fallback', async () => {
    await seed(); const source = new Subject<typeof privateValue>(); const pending = capture(cachedRequest(key, source));
    setApiKey('generated-reader-b'); source.error(offline());
    expect((await pending).emitted).toBeFalse(); expect(readCache(key)).toBeNull(); expect(offlineMode()).toBeFalse();
  });

  it('invalidates responses and caches on delivered cross-tab credential events', async () => {
    await seed(); const source = new Subject<typeof privateValue>(); const pending = capture(cachedRequest(key, source));
    localStorage.setItem('leviathan:api-key', 'generated-reader-b');
    window.dispatchEvent(new StorageEvent('storage', { key: 'leviathan:api-key' }));
    source.next(privateValue); source.complete();
    expect((await pending).emitted).toBeFalse(); expect(readCache(key)).toBeNull();
  });

  it('fences old replies and owner-mismatched fallback before a queued storage event runs', async () => {
    await seed(); const source = new Subject<typeof privateValue>(); const pending = capture(cachedRequest(key, source));
    localStorage.setItem('leviathan:api-key', 'generated-reader-b');
    source.next(privateValue); source.complete();
    expect((await pending).emitted).toBeFalse();
    expect((await capture(cachedRequest(key, throwError(offline)))).emitted).toBeFalse();
    window.dispatchEvent(new StorageEvent('storage', { key: 'leviathan:api-key' }));
  });

  it('fences a shared-ticket ABA cycle before either storage event is delivered', async () => {
    await seed(); const original = localStorage.getItem(authorityKey)!;
    const source = new Subject<typeof privateValue>(); const pending = capture(cachedRequest(key, source));
    // Another updated tab writes a fresh nonsecret authority revision before changing its key.
    localStorage.setItem(authorityKey, original === 'a'.repeat(32) ? 'b'.repeat(32) : 'a'.repeat(32));
    localStorage.setItem('leviathan:api-key', 'generated-reader-b');
    localStorage.setItem(authorityKey, 'c'.repeat(32)); localStorage.setItem('leviathan:api-key', 'generated-reader-a');
    source.next(privateValue); source.complete();
    expect((await pending).emitted).toBeFalse();
    expect((await capture(cachedRequest(key, throwError(offline)))).emitted).toBeFalse();
    window.dispatchEvent(new StorageEvent('storage', { key: authorityKey }));
  });

  it('does not let an older same-credential success refill authority after a denial', async () => {
    await seed(); const source = new Subject<typeof privateValue>(); const pending = capture(cachedRequest(key, source));
    const denied = new HttpErrorResponse({ status: 403 });
    expect((await capture(cachedRequest(key, throwError(() => denied)))).error).toBe(denied);
    source.next(privateValue); source.complete();
    expect((await pending).emitted).toBeFalse();
    expect((await capture(cachedRequest(key, throwError(offline)))).emitted).toBeFalse();
  });

  it('fences an authoritative denial immediately while that request digest is unresolved', async () => {
    let calls = 0; let release!: (value: ArrayBuffer) => void;
    spyOn(crypto.subtle, 'digest').and.callFake(() => {
      calls++;
      return calls === 3 ? new Promise<ArrayBuffer>(resolve => release = resolve) : Promise.resolve(new ArrayBuffer(32));
    });
    await seed(); const source = new Subject<typeof privateValue>(); const pending = capture(cachedRequest(key, source));
    const denied = new HttpErrorResponse({ status: 401 }); let observed = false;
    const denial = capture(cachedRequest(key, throwError(() => denied))).then(result => { observed = true; return result; });
    try {
      expect(readCache(key)).toBeNull(); // The fence cannot wait for the held third digest.
      expect((await denial).error).toBe(denied); expect(observed).toBeTrue();
      source.next(privateValue); source.complete(); expect((await pending).emitted).toBeFalse();
      expect((await capture(cachedRequest(key, throwError(offline)))).emitted).toBeFalse();
    } finally { release(new ArrayBuffer(32)); source.complete(); }
  });

  it('captures scope at subscription rather than observable construction', async () => {
    const value = { id: 'generated-reader-b-value' }; const request = cachedRequest(key, of(value));
    setApiKey('generated-reader-b');
    expect((await capture(request)).value).toEqual(value); expect(readCache(key)).toEqual(value);
  });

  it('does not let a legacy tab overwrite the new cache namespace', async () => {
    const value = { id: 'generated-current-reader-b', text: 'generated B text' };
    setApiKey('generated-reader-b'); await seed(value);
    localStorage.setItem('leviathan:cache:' + key, JSON.stringify(privateValue));
    expect(readCache(key)).toEqual(value);
    expect((await capture(cachedRequest(key, throwError(offline)))).value).toEqual(value);
  });

  it('does not consume legacy unbound cache or delete source drafts and credentials', () => {
    localStorage.setItem('leviathan:cache:' + key, JSON.stringify(privateValue));
    localStorage.setItem('leviathan:draft:generated-line', 'unsaved generated draft');
    expect(readCache(key)).toBeNull();
    expect(localStorage.getItem('leviathan:api-key')).toBe('generated-reader-a');
    expect(localStorage.getItem('leviathan:draft:generated-line')).toBe('unsaved generated draft');
  });

  it('serves current network data but cannot use persistent fallback without crypto binding', async () => {
    await seed(); spyOn(crypto.subtle, 'digest').and.returnValue(Promise.reject(new Error('generated crypto unavailable')));
    expect((await capture(cachedRequest(key, of(privateValue)))).value).toEqual(privateValue);
    expect((await capture(cachedRequest(key, throwError(offline)))).emitted).toBeFalse();
  });
});
