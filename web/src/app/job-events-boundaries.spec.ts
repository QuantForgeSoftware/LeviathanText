import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiClient, JobSummary } from './api-client';
import { JobsPanel, JOB_STREAM_FACTORY } from './jobs-panel';
import { HeaderJobStreamOptions } from './header-job-stream';
import { ApiKeyStorageKey, setApiKey } from './auth';

/** TEST-only transport scheduling. The parser/fetch/header boundary is tested
 * independently; queued old callbacks deliberately still run after close. */
class OwnedEventSource {
  static instances: OwnedEventSource[] = [];
  readonly settled = Promise.resolve();
  readyState = 'open';
  lastEventId = '';
  closed = 0;
  constructor(readonly url: string, readonly options: HeaderJobStreamOptions) { OwnedEventSource.instances.push(this); }
  close(): void { this.closed++; this.readyState = 'closed'; }
  emit(kind: string, data = '{}'): void { this.options.onEvent({ type: kind, data, id: '' }); }
}

describe('JobsPanel event boundaries', () => {
  beforeEach(() => {
    setApiKey(''); OwnedEventSource.instances = [];
  });
  afterEach(() => setApiKey(''));

  function setup() {
    const snapshots: Subject<JobSummary>[] = [];
    const enqueued = new Subject<{ id: string }>();
    const api = {
      jobs: jasmine.createSpy('jobs').and.returnValue(of([])),
      jobEventsUrl: (id: string) => `/api/v1/jobs/${id}/events`,
      jobSnapshot: jasmine.createSpy('snapshot').and.callFake(() => { const reply = new Subject<JobSummary>(); snapshots.push(reply); return reply; }),
      createJob: jasmine.createSpy('enqueue').and.returnValue(enqueued),
    };
    TestBed.configureTestingModule({ imports: [JobsPanel], providers: [{ provide: ApiClient, useValue: api }, { provide: JOB_STREAM_FACTORY, useValue: (url: string, _credential: string, options: HeaderJobStreamOptions) => new OwnedEventSource(url, options) }] });
    const fixture = TestBed.createComponent(JobsPanel); fixture.detectChanges();
    const component = fixture.componentInstance as any;
    return { fixture, component, api, snapshots, enqueued };
  }

  it('caps displayed history and data while preserving latest events', () => {
    const s = setup(); s.component.watch('job-a'); const source = OwnedEventSource.instances[0];
    for (let i = 0; i < 1000; i++) source.emit('job.progress', `${i}:` + 'x'.repeat(1000));
    expect(s.component.events().length).toBe(200);
    expect(s.component.events()[0].data.startsWith('800:')).toBeTrue();
    expect(s.component.events()[199].data.startsWith('999:')).toBeTrue();
    expect(s.component.events().every((event: { data: string }) => event.data.length <= 200)).toBeTrue();
    expect(s.component.omittedEvents()).toBe(800);
  });

  it('ignores late old-job terminal callbacks without closing the new source', () => {
    const s = setup(); s.component.watch('job-a'); const old = OwnedEventSource.instances[0];
    s.component.watch('job-b'); const current = OwnedEventSource.instances[1];
    old.emit('job.completed', 'old terminal');
    expect(current.closed).toBe(0); expect(s.component.watching()).toBe('job-b'); expect(s.component.events()).toEqual([]);
    current.emit('job.progress', 'current'); expect(s.component.events()[0].data).toBe('current');
  });

  it('coalesces resync reads and keeps a watched job even when it falls outside the recent page', () => {
    const s = setup(); s.component.watch('job-a'); const source = OwnedEventSource.instances[0];
    source.emit('job.resync-required', '{"reason":"history_trimmed"}');
    source.emit('job.resync-required', '{"reason":"slow_consumer"}');
    expect(s.api.jobSnapshot).toHaveBeenCalledTimes(1);
    s.snapshots[0].next({ id: 'job-a', type: 'noop', status: 'running', retriesUsed: 0 }); s.snapshots[0].complete();
    expect(s.component.jobs()[0].id).toBe('job-a'); expect(source.closed).toBe(0);
    source.emit('job.resync-required'); expect(s.api.jobSnapshot).toHaveBeenCalledTimes(2);
  });

  it('prevents an old pending snapshot from replacing a later authoritative terminal refresh', () => {
    const s = setup(); s.component.watch('job-a'); const source = OwnedEventSource.instances[0];
    source.emit('job.resync-required');
    s.api.jobs.and.returnValue(of([{ id: 'job-a', type: 'noop', status: 'succeeded', retriesUsed: 0 }]));
    source.emit('job.completed');
    s.snapshots[0].next({ id: 'job-a', type: 'noop', status: 'pending', retriesUsed: 0 });
    expect(s.component.jobs()[0].status).toBe('succeeded'); expect(s.component.watching()).toBeNull();
  });

  it('prevents an older list response from replacing a later selected-job snapshot', () => {
    const s = setup(); s.component.watch('job-a'); const source = OwnedEventSource.instances[0];
    const olderList = new Subject<JobSummary[]>(); s.api.jobs.and.returnValue(olderList); s.component.refresh();
    source.emit('job.resync-required');
    s.snapshots[0].next({ id: 'job-a', type: 'noop', status: 'succeeded', retriesUsed: 0 });
    olderList.next([{ id: 'job-a', type: 'noop', status: 'pending', retriesUsed: 0 }]);
    expect(s.component.jobs()[0].status).toBe('succeeded');
  });

  it('refreshes an older watched job outside the recent page without growing the displayed list', () => {
    const s = setup(); s.component.watch('job-old');
    s.api.jobs.and.returnValue(of(Array.from({ length: 150 }, (_, i) => ({ id: `job-${i}`, type: 'noop', status: 'pending', retriesUsed: 0 }))));
    s.component.refresh(); expect(s.component.jobs().length).toBe(100);
    expect(s.api.jobSnapshot).toHaveBeenCalledWith('job-old', '', jasmine.any(Function));
    expect(s.api.jobSnapshot.calls.mostRecent().args[2]()).toBeTrue();
    s.snapshots[0].next({ id: 'job-old', type: 'noop', status: 'running', retriesUsed: 0 });
    expect(s.component.jobs().length).toBe(100); expect(s.component.jobs()[0].id).toBe('job-old');
  });

  it('stops capacity reconnect loops while retaining its authorized snapshot request', () => {
    const s = setup(); s.component.watch('job-a'); const source = OwnedEventSource.instances[0];
    source.emit('job.resync-required', '{"reason":"subscription_capacity"}');
    expect(source.closed).toBe(1); expect(s.component.watching()).toBeNull();
    s.snapshots[0].next({ id: 'job-a', type: 'noop', status: 'running', retriesUsed: 0 });
    expect(s.component.jobs()[0].status).toBe('running');
  });

  it('clears prior credential state and ignores old callbacks after same-tab and storage changes', () => {
    const s = setup(); s.component.watch('job-a'); const first = OwnedEventSource.instances[0]; first.emit('job.progress', 'old');
    setApiKey('generated-job-client-key'); s.fixture.detectChanges();
    expect(first.closed).toBe(1); expect(s.component.events()).toEqual([]); expect(s.component.watching()).toBeNull();
    first.emit('job.completed', 'late'); expect(s.component.events()).toEqual([]);
    s.component.watch('job-b'); const second = OwnedEventSource.instances[1];
    window.dispatchEvent(new StorageEvent('storage', { key: ApiKeyStorageKey }));
    expect(second.closed).toBe(1); expect(s.component.watching()).toBeNull();
  });

  it('does not let a delayed enqueue response replace a newer selected watch', () => {
    const s = setup(); s.component.enqueue(); s.component.enqueue(); expect(s.api.createJob).toHaveBeenCalledTimes(1);
    s.component.watch('job-b'); s.enqueued.next({ id: 'job-created' }); s.enqueued.complete();
    expect(s.component.watching()).toBe('job-b'); expect(OwnedEventSource.instances.length).toBe(1);
    expect(s.component.enqueuing()).toBeFalse();
  });

  it('clears selected history after a forbidden snapshot and disposes sources on destruction', () => {
    const s = setup(); s.component.watch('job-a'); const source = OwnedEventSource.instances[0];
    source.emit('job.progress', 'authorized before revoke'); source.emit('job.resync-required');
    s.snapshots[0].error({ status: 403 }); expect(s.component.jobs()).toEqual([]); expect(s.component.events()).toEqual([]);
    expect(source.closed).toBe(1);
    s.component.watch('job-b'); const current = OwnedEventSource.instances[1]; s.fixture.destroy();
    expect(current.closed).toBe(1); current.emit('job.progress', 'late'); expect(s.component.events()).toEqual([]);
  });
});
