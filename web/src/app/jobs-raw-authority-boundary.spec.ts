import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiClient } from './api-client';
import { apiKey, ApiKeyStorageKey, cacheCredentialGeneration, setApiKey } from './auth';
import { cacheAuthorityRevision } from './offline-cache-storage';
import { JobsPanel, JOB_STREAM_FACTORY } from './jobs-panel';
import { HeaderJobStreamOptions } from './header-job-stream';
import { WorkspaceAccessCoordinator } from './workspace-access';
import { TEST_WORKSPACE_OWNER } from './workspace-testing.fixture.spec';

/** Controlled component-observable proof. Real HTTP dispatch/header capture is
 * covered separately by captured-jobs-transport.spec.ts, not simulated here. */
describe('Raw jobs reads and shared workspace authority', () => {
  const job = { id: 'job-generated-raw-boundary', type: 'noop', status: 'Running', retriesUsed: 0 };
  let lists: Subject<unknown[]>[];
  let snapshots: Subject<unknown>[];
  let streams: HeaderJobStreamOptions[];
  const workspaces: { destroy(): void }[] = [];

  beforeEach(() => {
    localStorage.clear();
    setApiKey('generated-raw-reader-a');
    lists = []; snapshots = []; streams = [];
    const api = {
      jobs: jasmine.createSpy('jobs').and.callFake(() => {
        const result = new Subject<unknown[]>(); lists.push(result); return result;
      }),
      jobSnapshot: jasmine.createSpy('snapshot').and.callFake(() => {
        const result = new Subject<unknown>(); snapshots.push(result); return result;
      }),
      jobEventsUrl: (id: string) => `/api/v1/jobs/${id}/events`,
      createJob: () => of({ id: job.id }),
    };
    TestBed.configureTestingModule({ imports: [JobsPanel], providers: [
      { provide: ApiClient, useValue: api },
      { provide: JOB_STREAM_FACTORY, useValue: (_url: string, _key: string, options: HeaderJobStreamOptions) => {
        streams.push(options);
        return { close: jasmine.createSpy('close'), readyState: 'open', lastEventId: '', settled: Promise.resolve() };
      } },
    ] });
  });

  afterEach(() => {
    for (const workspace of workspaces.splice(0)) workspace.destroy();
    setApiKey(''); localStorage.clear();
  });

  function workspace() {
    const access = new WorkspaceAccessCoordinator({ api: {
      me: () => of({ authenticated: true, mode: 'account', ...TEST_WORKSPACE_OWNER,
        name: 'Generated current reader', role: 'Reader' } as any),
      loadProjectFresh: () => of({ id: 'col-generated-raw-boundary', name: 'Generated workspace', objects: [] }),
    }, readScope: () => ({ credential: apiKey(), localGeneration: cacheCredentialGeneration(),
      authorityRevision: cacheAuthorityRevision() }) });
    workspaces.push(access); access.open('col-generated-raw-boundary');
    expect(access.isActive()).withContext('fresh admission before the raw denial').toBeTrue();
    return access;
  }

  function panel() {
    const fixture = TestBed.createComponent(JobsPanel); fixture.detectChanges();
    return { fixture, page: fixture.componentInstance as any };
  }

  function startSnapshot(page: any, list: Subject<unknown[]>) {
    list.next([job]); list.complete();
    page.watch(job.id);
    streams.at(-1)!.onEvent({ type: 'job.resync-required', data: '{"reason":"history_gap"}', id: '' });
    expect(snapshots.length).toBe(1);
    return snapshots[0];
  }

  for (const route of ['list', 'snapshot'] as const) {
    for (const status of [401, 403]) {
      it(`synchronously suspends another active workspace on CURRENT ${route} ${status}`, () => {
        const { fixture, page } = panel();
        const read = route === 'snapshot' ? startSnapshot(page, lists[0]) : lists[0];
        const other = workspace();
        const ticket = cacheAuthorityRevision();
        let suspendedDuringNotification = false;
        const unsubscribe = other.subscribe(state => {
          if (state.status !== 'active') suspendedDuringNotification = true;
        });
        try {
          read.error({ status });
          expect(suspendedDuringNotification).withContext('suspension before Subject.error returns').toBeTrue();
          expect(other.isActive()).toBeFalse();
          expect(cacheAuthorityRevision()).not.toBe(ticket);
          expect(page.jobs()).toEqual([]);
          expect(lists.length).withContext('no automatic pre-mutation authority read').toBe(1);
        } finally { unsubscribe(); fixture.destroy(); }
      });
    }
  }

  it('never fences B for delayed A list401 and snapshot403 errors without a storage notification', () => {
    const first = panel();
    const second = panel();
    const oldList = lists[0];
    const oldSnapshot = startSnapshot(second.page, lists[1]);
    // Keep A subscriptions genuinely active: changing raw storage deliberately
    // bypasses the normal synchronous boundary, so each callback must check scope.
    localStorage.setItem(ApiKeyStorageKey, 'generated-raw-reader-b');
    const other = workspace();
    const ticket = cacheAuthorityRevision();
    try {
      oldList.error({ status: 401 }); oldSnapshot.error({ status: 403 });
      expect(other.isActive()).toBeTrue();
      expect(cacheAuthorityRevision()).toBe(ticket);
      expect(lists.length).toBe(2);
    } finally { first.fixture.destroy(); second.fixture.destroy(); }
  });
});
