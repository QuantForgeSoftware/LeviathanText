import { TestBed } from '@angular/core/testing';
import { HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClient } from './api-client';
import { ApiKeyStorageKey, authInterceptor, setApiKey } from './auth';
import { cacheAuthorityRevision, ensureCacheAuthorityRevision, readCacheGeneration } from './offline-cache-storage';

describe('workspace per-intent transport', () => {
  let original: string | null;
  let active: boolean;
  let boundary: (() => void) | null;
  beforeEach(() => {
    original = localStorage.getItem(ApiKeyStorageKey); setApiKey('generated-current'); active = true; boundary = null;
    const schedulingBoundary: HttpInterceptorFn = (request, next) => {
      const action = boundary; boundary = null; action?.(); return next(request);
    };
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([schedulingBoundary, authInterceptor])), provideHttpClientTesting()] });
  });
  afterEach(() => { TestBed.inject(HttpTestingController).verify(); setApiKey(original ?? ''); });
  function scoped(credential = 'generated-captured') {
    return TestBed.inject(ApiClient).forWorkspaceRequest({ credential, current: () => active });
  }
  for (const credential of ['generated-captured', '']) {
    it(`pins ${credential ? 'captured' : 'anonymous'} mutations even when a scheduling interceptor changes the global key`, () => {
      boundary = () => localStorage.setItem(ApiKeyStorageKey, 'generated-other');
      scoped(credential).appendRevision('generated-line', 'L1Diplomatic', 'generated text', 'generated-author').subscribe();
      const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/lines/generated-line/revisions');
      expect(request.request.headers.get('X-Api-Key')).toBe(credential || null);
      expect(request.request.headers.has('Authorization')).toBeFalse();
      request.flush({ revisionId: 'generated-revision' });
    });
  }
  it('checks current access at subscription rather than trusting an earlier method call', () => {
    const request = scoped().appendRevision('generated-line', 'L1Diplomatic', 'sentinel', 'generated-author');
    active = false;
    let delivered = false;
    request.subscribe({ next: () => delivered = true, error: () => delivered = true });
    TestBed.inject(HttpTestingController).expectNone('/api/v1/lines/generated-line/revisions');
    expect(delivered).toBeFalse();
  });
  for (const failure of [false, true]) {
    it(`drops a late ${failure ? 'error' : 'success'} without clearing mounted state through the subscriber`, () => {
      let delivered = false;
      scoped().appendRevision('generated-line', 'L1Diplomatic', 'sentinel', 'generated-author')
        .subscribe({ next: () => delivered = true, error: () => delivered = true });
      const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/lines/generated-line/revisions');
      active = false;
      if (failure) request.flush({}, { status: 409, statusText: 'Generated conflict' });
      else request.flush({ revisionId: 'generated-revision' });
      expect(delivered).toBeFalse();
    });
  }
  for (const status of [401, 403, 404]) {
    it(`invalidates cache authority on a current ${status} refusal before any mutation completion callback`, () => {
      const authority = ensureCacheAuthorityRevision(); const generation = readCacheGeneration();
      let delivered = false;
      scoped().appendRevision('generated-line', 'L1Diplomatic', 'sentinel', 'generated-author')
        .subscribe({ next: () => delivered = true, error: () => delivered = true });
      TestBed.inject(HttpTestingController).expectOne('/api/v1/lines/generated-line/revisions')
        .flush({}, { status, statusText: 'Generated denial' });
      expect(readCacheGeneration()).toBeGreaterThan(generation);
      expect(cacheAuthorityRevision()).not.toBe(authority);
      expect(delivered).toBeFalse();
    });
  }
  it('does not fence unrelated authority because an already stale request later receives a denial', () => {
    const authority = ensureCacheAuthorityRevision(); const generation = readCacheGeneration();
    scoped().appendRevision('generated-line', 'L1Diplomatic', 'sentinel', 'generated-author').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/lines/generated-line/revisions');
    active = false; request.flush({}, { status: 403, statusText: 'Generated stale denial' });
    expect(readCacheGeneration()).toBe(generation); expect(cacheAuthorityRevision()).toBe(authority);
  });
  it('still propagates an active ordinary conflict so the mounted editor can reconcile it', () => {
    let status = 0;
    scoped().appendRevision('generated-line', 'L1Diplomatic', 'sentinel', 'generated-author').subscribe({ error: error => status = error.status });
    TestBed.inject(HttpTestingController).expectOne('/api/v1/lines/generated-line/revisions')
      .flush({}, { status: 409, statusText: 'Generated conflict' });
    expect(status).toBe(409);
  });
});
