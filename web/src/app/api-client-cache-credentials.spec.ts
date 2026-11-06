import { HttpClient, HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiClient } from './api-client';
import { ApiKeyStorageKey, authInterceptor, setApiKey } from './auth';
import { CacheCredentialChangedError, clearCache, offlineMode, readCache } from './offline-cache';

/** Composed generated controls: real public client, cache, and auth interceptor; no network. */
describe('ApiClient cached-read credential composition', () => {
  const routes: ReadonlyArray<{
    name: string;
    url: string;
    cacheKey: string;
    value: object;
    read: (client: ApiClient) => Observable<unknown>;
  }> = [
    { name: 'listProjects', url: '/api/v1/projects', cacheKey: 'projects',
      value: [{ id: 'col-generated', name: 'Generated collection' }], read: client => client.listProjects() },
    { name: 'loadProject', url: '/api/v1/projects/col-generated', cacheKey: 'project:col-generated',
      value: { id: 'col-generated', name: 'Generated collection', objects: [] }, read: client => client.loadProject('col-generated') },
    { name: 'line', url: '/api/v1/lines/ln-generated', cacheKey: 'line:ln-generated',
      value: { id: 'ln-generated', diplomatic: 'Generated source' }, read: client => client.line('ln-generated') },
  ];
  let client: ApiClient;
  let backend: HttpTestingController;
  let beforeAuth: (() => void) | null;
  const observe = (source: Observable<unknown>) => firstValueFrom(source).then(
    value => ({ emitted: true as const, value, error: undefined }),
    error => ({ emitted: false as const, value: undefined, error }),
  );

  beforeEach(() => {
    localStorage.clear(); clearCache(); setApiKey('generated-reader-a'); beforeAuth = null;
    const schedulingBoundary: HttpInterceptorFn = (request, next) => {
      const action = beforeAuth; beforeAuth = null; action?.();
      return next(request);
    };
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([schedulingBoundary, authInterceptor])), provideHttpClientTesting(),
    ] });
    client = new ApiClient(TestBed.inject(HttpClient));
    backend = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { backend.verify(); setApiKey(''); clearCache(); localStorage.clear(); });

  for (const route of routes) {
    it(`${route.name} pins A on its actual HTTP request if storage changes before auth dispatch`, async () => {
      beforeAuth = () => localStorage.setItem(ApiKeyStorageKey, 'generated-reader-b');
      const result = observe(route.read(client));
      const request = backend.expectOne(route.url);
      expect(request.request.headers.get('X-Api-Key')).toBe('generated-reader-a');
      expect(request.request.headers.has('Authorization')).toBeFalse();
      request.flush(route.value);
      const reply = await result;
      expect(reply.emitted).toBeFalse(); expect(reply.error).toEqual(jasmine.any(CacheCredentialChangedError));
      expect(readCache(route.cacheKey)).toBeNull();
    });

    it(`${route.name} captures the real credential at subscription, not construction`, async () => {
      const read = route.read(client); setApiKey('generated-reader-b');
      const result = observe(read); const request = backend.expectOne(route.url);
      expect(request.request.headers.get('X-Api-Key')).toBe('generated-reader-b');
      request.flush(route.value);
      expect((await result).value).toEqual(route.value);
      expect(readCache(route.cacheKey)).toEqual(route.value);
    });

    it(`${route.name} keeps captured anonymous HTTP anonymous after a key appears`, async () => {
      setApiKey(''); beforeAuth = () => localStorage.setItem(ApiKeyStorageKey, 'generated-reader-b');
      const result = observe(route.read(client)); const request = backend.expectOne(route.url);
      expect(request.request.headers.has('X-Api-Key')).toBeFalse();
      expect(request.request.headers.has('Authorization')).toBeFalse();
      request.flush(route.value);
      expect((await result).emitted).toBeFalse(); expect(readCache(route.cacheKey)).toBeNull();
    });

    it(`${route.name} retains same-credential offline fallback after a real successful HTTP read`, async () => {
      const initial = observe(route.read(client)); backend.expectOne(route.url).flush(route.value);
      expect((await initial).value).toEqual(route.value);
      const fallback = observe(route.read(client));
      backend.expectOne(route.url).error(new ProgressEvent('error'), { status: 0, statusText: 'Generated transport failure' });
      expect((await fallback).value).toEqual(route.value); expect(offlineMode()).toBeTrue();
    });

    it(`${route.name} cannot use A's response as B's transport-offline fallback before storage events`, async () => {
      const initial = observe(route.read(client)); backend.expectOne(route.url).flush(route.value);
      expect((await initial).value).toEqual(route.value);
      localStorage.setItem(ApiKeyStorageKey, 'generated-reader-b');
      const fallback = observe(route.read(client)); const request = backend.expectOne(route.url);
      expect(request.request.headers.get('X-Api-Key')).toBe('generated-reader-b');
      request.error(new ProgressEvent('error'), { status: 0, statusText: 'Generated transport failure' });
      expect((await fallback).emitted).toBeFalse(); expect(offlineMode()).toBeFalse();
    });
  }
});
