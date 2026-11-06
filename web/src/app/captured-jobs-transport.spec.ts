import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable, Subscription } from 'rxjs';
import { JobSummary } from './api-client';
import { ApiKeyStorageKey, authInterceptor, setApiKey } from './auth';
import { JOB_STREAM_FACTORY, JobsPanel } from './jobs-panel';
import { HeaderJobStreamOptions } from './header-job-stream';
import { cacheAuthorityRevision, ensureCacheAuthorityRevision, readCacheGeneration } from './offline-cache-storage';

describe('captured Jobs transport through real scheduled auth interception', () => {
  let fixture: ComponentFixture<JobsPanel>;
  let http: HttpTestingController;
  let original: string | null;
  let gate: string | null;
  let release: (() => void) | null;
  let streamOptions: HeaderJobStreamOptions | null;
  const row: JobSummary = { id: 'job-generated-transport', type: 'noop', status: 'Pending', retriesUsed: 0 };
  beforeEach(async () => {
    original = localStorage.getItem(ApiKeyStorageKey); setApiKey('generated-jobs-A');
    gate = null; release = null; streamOptions = null;
    const schedule: HttpInterceptorFn = (request, next) => {
      if (`${request.method} ${request.url}` !== gate) return next(request);
      gate = null;
      return new Observable(observer => {
        let live = true; let inner: Subscription | null = null;
        release = () => { if (live) inner = next(request).subscribe(observer); };
        return () => { live = false; inner?.unsubscribe(); };
      });
    };
    TestBed.configureTestingModule({ imports: [JobsPanel], providers: [
      provideHttpClient(withInterceptors([schedule, authInterceptor])), provideHttpClientTesting(),
      { provide: JOB_STREAM_FACTORY, useValue: (_url: string, _credential: string, options: HeaderJobStreamOptions) => {
        streamOptions = options; let resolve!: () => void;
        const settled = new Promise<void>(done => resolve = done);
        return { close: () => resolve(), readyState: 'open', lastEventId: '', settled };
      } },
    ] });
    fixture = TestBed.createComponent(JobsPanel); http = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); http.expectOne('/api/v1/jobs').flush([row]); fixture.detectChanges();
  });
  afterEach(() => { fixture.destroy(); http.verify(); setApiKey(original ?? ''); });
  function click(label: string): void {
    const buttons = [...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[];
    const button = buttons.find(candidate => candidate.textContent?.trim() === label);
    expect(button).withContext(label).toBeDefined(); button!.click();
  }
  function start(route: 'list' | 'snapshot' | 'create'): string {
    const url = route === 'snapshot' ? `/api/v1/jobs/${row.id}` : '/api/v1/jobs';
    gate = `${route === 'create' ? 'POST' : 'GET'} ${url}`;
    if (route === 'list') click('Refresh');
    else if (route === 'create') click('Enqueue noop job');
    else {
      click('Watch');
      streamOptions!.onEvent({ type: 'job.resync-required', data: '{"reason":"generated-gap"}', id: '' });
    }
    expect(release).withContext('actual component request reached preceding scheduling interceptor').not.toBeNull();
    return url;
  }
  function response(route: string): JobSummary[] | JobSummary | { id: string } { return route === 'list' ? [row] : route === 'create' ? { id: row.id } : row; }
  for (const route of ['list', 'snapshot', 'create'] as const) {
    it(`does not dispatch the old ${route} intent under a directly replaced credential`, () => {
      const url = start(route);
      // No storage event or component change detection: final auth must independently fence dispatch.
      localStorage.setItem(ApiKeyStorageKey, 'generated-jobs-B'); release!();
      const requests = http.match(request => request.url === url);
      const headers = requests.map(request => request.request.headers.get('X-Api-Key'));
      for (const request of requests) request.flush(response(route));
      expect(headers).withContext('actual backend headers from stale scheduled intent').toEqual([]);
    });
    it(`preserves current ${route} headers and payload through the same schedule`, () => {
      const url = start(route); release!();
      const request = http.expectOne(url);
      expect(request.request.headers.get('X-Api-Key')).toBe('generated-jobs-A');
      expect(request.request.urlWithParams).not.toContain('api_key');
      if (route === 'create') expect(request.request.body).toEqual({ type: 'noop', ownerId: 'workspace', idempotencyKey: null });
      request.flush(response(route));
      if (route === 'create') http.expectOne('/api/v1/jobs').flush([row]);
    });
  }
  for (const status of [401, 403]) {
    it(`ignores a stale actual ${status} response without revoking the replacement authority`, () => {
      click('Refresh'); const request = http.expectOne('/api/v1/jobs');
      const authority = ensureCacheAuthorityRevision();
      localStorage.setItem(ApiKeyStorageKey, 'generated-jobs-B');
      request.flush({}, { status, statusText: 'Generated stale denial' });
      expect(cacheAuthorityRevision()).toBe(authority);
      expect(localStorage.getItem(ApiKeyStorageKey)).toBe('generated-jobs-B');
    });
  }
  for (const status of [401, 403]) {
    it(`handles current enqueue ${status} with ${status === 401 ? 'global authentication' : 'local Reader-role'} refusal and no second dispatch`, () => {
      const authority = ensureCacheAuthorityRevision(); const generation = readCacheGeneration();
      click('Enqueue noop job');
      const request = http.expectOne('/api/v1/jobs');
      expect(request.request.method).toBe('POST');
      expect(request.request.headers.get('X-Api-Key')).toBe('generated-jobs-A');
      request.flush({}, { status, statusText: 'Generated current enqueue denial' });
      if (status === 401) {
        expect(readCacheGeneration()).toBeGreaterThan(generation);
        expect(cacheAuthorityRevision()).not.toBe(authority);
      } else {
        expect(readCacheGeneration()).toBe(generation);
        expect(cacheAuthorityRevision()).toBe(authority);
      }
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('tbody').textContent)
        .toContain(status === 401 ? 'No jobs yet.' : row.id);
      http.expectNone('/api/v1/jobs');
    });
  }

});
