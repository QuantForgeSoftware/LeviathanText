import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from './api-client';
import { apiKey, setApiKey, cacheCredentialGeneration } from './auth';
import { clearCache, cacheAuthorityRevision, CacheAuthorityRevisionStorageKey } from './offline-cache-storage';
import { notifyWorkspaceBoundary, subscribeWorkspaceBoundary, WorkspaceAccessCoordinator } from './workspace-access';
import { JobsPanel, JOB_STREAM_FACTORY } from './jobs-panel';
import { TEST_WORKSPACE_OWNER } from './workspace-testing.fixture.spec';
import { HeaderJobStreamOptions, openHeaderJobStream } from './header-job-stream';

describe('Header job stream global boundaries', () => {
  beforeEach(() => { localStorage.clear(); setApiKey('generated-original'); });
  afterEach(() => { setApiKey(''); localStorage.clear(); });
  function setup() {
    const streams: { options: HeaderJobStreamOptions; close: jasmine.Spy; key: string; abortedWithKey: string | null }[] = [];
    const api = { jobs: jasmine.createSpy('jobs').and.returnValue(of([])),
      jobEventsUrl: (id: string) => `/api/v1/jobs/${id}/events`, jobSnapshot: () => of({}), createJob: () => of({ id: 'job-generated' }) };
    TestBed.configureTestingModule({ imports: [JobsPanel], providers: [{ provide: ApiClient, useValue: api },
      { provide: JOB_STREAM_FACTORY, useValue: (_url: string, key: string, options: HeaderJobStreamOptions) => {
        const entry = { options, key, abortedWithKey: null as string | null, close: jasmine.createSpy('close') };
        entry.close.and.callFake(() => { entry.abortedWithKey = apiKey(); options.onEvent({ type: 'job.progress', data: 'reentrant old bytes', id: '2' }); });
        streams.push(entry); return { close: entry.close, readyState: 'open', lastEventId: '', settled: Promise.resolve() };
      } }] });
    const fixture = TestBed.createComponent(JobsPanel); fixture.detectChanges();
    const page = fixture.componentInstance as any; page.watch('job-generated');
    return { fixture, page, streams, api };
  }
  it('aborts synchronously before key mutation and never dispatches a pre-mutation refresh or reentrant old bytes', () => {
    const s = setup(); setApiKey('generated-next');
    expect(s.streams[0].close).toHaveBeenCalledTimes(1);
    expect(s.streams[0].abortedWithKey).toBe('generated-original');
    expect(s.api.jobs).toHaveBeenCalledTimes(1);
    expect(s.page.events()).toEqual([]); expect(s.page.watching()).toBeNull();
    s.fixture.detectChanges(); expect(s.api.jobs).toHaveBeenCalledTimes(2);
  });
  it('aborts on same-key global authority changes without automatic old-authority reads', () => {
    const s = setup(); clearCache();
    expect(s.streams[0].close).toHaveBeenCalledTimes(1); expect(s.api.jobs).toHaveBeenCalledTimes(1);
    expect(s.page.events()).toEqual([]); expect(s.page.watching()).toBeNull();
  });
  it('fences a silent authority-ticket mutation before a queued event can be displayed', () => {
    const s = setup(); localStorage.setItem(CacheAuthorityRevisionStorageKey, 'f'.repeat(32));
    s.streams[0].options.onEvent({ type: 'job.progress', data: 'stale ticket', id: '3' });
    expect(s.page.events()).toEqual([]); expect(s.streams[0].options.current()).toBeFalse();
  });
  it('unsubscribes the destroyed panel and ignores callbacks after close', () => {
    const s = setup(); s.fixture.destroy();
    expect(s.streams[0].close).toHaveBeenCalledTimes(1); notifyWorkspaceBoundary();
    expect(s.streams[0].close).toHaveBeenCalledTimes(1);
    s.streams[0].options.onEvent({ type: 'job.progress', data: 'late', id: '4' }); expect(s.page.events()).toEqual([]);
  });
  it('removes a boundary listener before an already captured notification reaches it', () => {
    let calls = 0; let unsubscribeSecond = () => {};
    const first = subscribeWorkspaceBoundary(() => unsubscribeSecond());
    unsubscribeSecond = subscribeWorkspaceBoundary(() => calls++);
    try { notifyWorkspaceBoundary(); notifyWorkspaceBoundary(); expect(calls).toBe(0); }
    finally { first(); unsubscribeSecond(); }
  });
  function otherWorkspace() {
    const access = new WorkspaceAccessCoordinator({ api: {
      me: () => of({ authenticated: true, mode: 'account', ...TEST_WORKSPACE_OWNER, name: 'Generated reviewer', role: 'Reviewer' } as any),
      loadProjectFresh: () => of({ id: 'col-other', name: 'Generated other workspace', objects: [] }),
    }, readScope: () => ({ credential: apiKey(), localGeneration: cacheCredentialGeneration(), authorityRevision: cacheAuthorityRevision() }) });
    access.open('col-other'); return access;
  }
  it('invalidates shared authority on a CURRENT stream authorization refusal without pre-mutation reads', () => {
    const s = setup(), other = otherWorkspace(), ticket = cacheAuthorityRevision();
    try {
      expect(other.isActive()).toBeTrue();
      s.streams[0].options.onState!({ state: 'closed', reason: 'authorization', status: 403, attempt: 0 });
      expect(other.isActive()).toBeFalse(); expect(cacheAuthorityRevision()).not.toBe(ticket);
      expect(s.api.jobs).toHaveBeenCalledTimes(1); expect(s.page.jobs()).toEqual([]);
    } finally { other.destroy(); }
  });
  it('never invalidates the new authority for a delayed OLD-owner authorization refusal', () => {
    const s = setup(); setApiKey('generated-next'); s.fixture.detectChanges();
    const other = otherWorkspace(), ticket = cacheAuthorityRevision();
    try {
      expect(other.isActive()).toBeTrue();
      s.streams[0].options.onState!({ state: 'closed', reason: 'authorization', status: 401, attempt: 0 });
      expect(cacheAuthorityRevision()).toBe(ticket); expect(other.isActive()).toBeTrue();
    } finally { other.destroy(); }
  });
  it('uses the actual fetch transport with headers and aborts it before changing the live key', async () => {
    let started!: () => void; const dispatched = new Promise<void>(resolve => started = resolve);
    let request!: RequestInit; let endpoint = ''; let keyAtAbort: string | null = null;
    const source = new ReadableStream<Uint8Array>({ cancel() {} });
    spyOn(globalThis, 'fetch').and.callFake((input, init) => {
      endpoint = String(input); request = init!; init!.signal!.addEventListener('abort', () => keyAtAbort = apiKey()); started();
      return Promise.resolve(new Response(source, { headers: { 'Content-Type': 'text/event-stream' } }));
    });
    const native = spyOn(window, 'EventSource');
    TestBed.configureTestingModule({ imports: [JobsPanel], providers: [{ provide: ApiClient, useValue: {
      jobs: () => of([]), jobEventsUrl: (id: string) => `/api/v1/jobs/${id}/events`,
    } }, { provide: JOB_STREAM_FACTORY, useValue: openHeaderJobStream }] });
    const fixture = TestBed.createComponent(JobsPanel); fixture.detectChanges();
    (fixture.componentInstance as any).watch('job-generated'); await dispatched;
    expect(endpoint).not.toContain('generated-original'); expect(new URL(endpoint).search).toBe('');
    expect(new Headers(request.headers).get('X-Api-Key')).toBe('generated-original'); expect(request.credentials).toBe('omit');
    setApiKey('generated-next'); expect(request.signal!.aborted).toBeTrue(); expect(keyAtAbort as string | null).toBe('generated-original');
    expect(native).not.toHaveBeenCalled(); fixture.destroy();
  });
});
