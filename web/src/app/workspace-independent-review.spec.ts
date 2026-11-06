import { fakeAsync, flushMicrotasks, TestBed } from '@angular/core/testing';
import { HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClient } from './api-client';
import { apiKey, ApiKeyStorageKey, authInterceptor, setApiKey } from './auth';
import { clearCache, readCache, writeCache } from './offline-cache';
import { ensureCacheAuthorityRevision } from './offline-cache-storage';

/** Independent generated HTTP evidence: actual client/cache/interceptor, no mock facade. */
describe('workspace independent request-lifetime review', () => {
  let boundary: (() => void) | null;
  let backend: HttpTestingController;
  beforeEach(() => {
    localStorage.clear(); clearCache(); setApiKey('generated-independent-a'); boundary = null;
    const scheduling: HttpInterceptorFn = (request, next) => {
      const action = boundary; boundary = null; action?.(); return next(request);
    };
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([scheduling, authInterceptor])), provideHttpClientTesting(),
    ] });
    backend = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { backend.verify(); setApiKey(''); clearCache(); localStorage.clear(); });

  it('does not dispatch a mutation whose actual credential scope expired inside the interceptor chain', () => {
    const captured = apiKey();
    const api = TestBed.inject(ApiClient).forWorkspaceRequest({ credential: captured, current: () => apiKey() === captured });
    boundary = () => localStorage.setItem(ApiKeyStorageKey, 'generated-independent-b');
    const delivered = jasmine.createSpy('delivered');
    api.appendRevision('generated-lifetime-line', 'L1Diplomatic', 'generated mutation', 'generated-author')
      .subscribe({ next: delivered, error: delivered });
    const requests = backend.match('/api/v1/lines/generated-lifetime-line/revisions');
    // Drain any unexpected request after recording its presence; no unhandled
    // request or real network side effect obscures the actual before-failure.
    expect(requests.length).toBe(0);
    for (const request of requests) {
      expect(request.request.headers.get('X-Api-Key')).toBe(captured);
      request.flush({ revisionId: 'generated-unexpected' });
    }
    expect(delivered).not.toHaveBeenCalled();
  });

  it('does not dispatch an anonymous mutation after its workspace lifetime expires before auth', () => {
    setApiKey(''); let current = true;
    const api = TestBed.inject(ApiClient).forWorkspaceRequest({ credential: '', current: () => current });
    boundary = () => { current = false; };
    api.appendRevision('generated-anonymous-line', 'L1Diplomatic', 'generated mutation', 'generated-author').subscribe();
    const requests = backend.match('/api/v1/lines/generated-anonymous-line/revisions');
    expect(requests.length).toBe(0);
    for (const request of requests) { expect(request.request.headers.has('X-Api-Key')).toBeFalse(); request.flush({}); }
  });

  it('fences a cached success across its asynchronous digest continuation even with unchanged key and authority', fakeAsync(() => {
    let resolve!: (value: ArrayBuffer) => void;
    spyOn(globalThis.crypto.subtle, 'digest').and.returnValue(new Promise<ArrayBuffer>(done => resolve = done));
    let current = true;
    const api = TestBed.inject(ApiClient).forWorkspaceRequest({ credential: apiKey(), current: () => current });
    const delivered = jasmine.createSpy('delivered');
    api.line('generated-digest-line').subscribe({ next: delivered, error: () => {} });
    backend.expectOne('/api/v1/lines/generated-digest-line').flush({ line: 'generated-digest-line', diplomatic: 'private old workspace', history: [] });
    current = false; // Project/access lifetime changed; credential/cache authority deliberately unchanged.
    resolve(new Uint8Array(32).buffer); flushMicrotasks();
    expect(delivered).not.toHaveBeenCalled();
    expect(readCache('line:generated-digest-line')).toBeNull();
  }));

  it('fences an offline fallback across its asynchronous digest continuation without deleting prior cache bytes', fakeAsync(() => {
    let resolve!: (value: ArrayBuffer) => void;
    spyOn(globalThis.crypto.subtle, 'digest').and.returnValue(new Promise<ArrayBuffer>(done => resolve = done));
    const authority = ensureCacheAuthorityRevision()!;
    const cached = { line: 'generated-offline-line', diplomatic: 'private cached workspace', history: [] };
    writeCache('line:generated-offline-line', cached, { ownerSha256: '0'.repeat(64), authorityRevision: authority });
    let current = true;
    const api = TestBed.inject(ApiClient).forWorkspaceRequest({ credential: apiKey(), current: () => current });
    const delivered = jasmine.createSpy('delivered');
    api.line('generated-offline-line').subscribe({ next: delivered, error: () => {} });
    backend.expectOne('/api/v1/lines/generated-offline-line').error(new ProgressEvent('generated-offline'));
    current = false; resolve(new Uint8Array(32).buffer); flushMicrotasks();
    expect(delivered).not.toHaveBeenCalled();
    expect(readCache('line:generated-offline-line')).toEqual(cached);
  }));
});

import { of, Subject } from 'rxjs';
import { LineEditor } from './line-editor';
import type { WorkspaceAccess, WorkspaceAccessState, WorkspaceOpaqueRecord, WorkspaceRequestIntent } from './workspace-access-types';
import { provideWorkspaceTestAccess, TEST_WORKSPACE_OWNER, TEST_WORKSPACE_PROJECT_ID, TEST_WORKSPACE_SURFACE_ID,
  seedOwnedDraft, readOwnedDraft } from './workspace-testing.fixture.spec';

class ReviewAccess implements WorkspaceAccess {
  private active = true;
  private generation = 0;
  private listeners = new Set<(state: WorkspaceAccessState) => void>();
  state(): WorkspaceAccessState { return { status: this.active ? 'active' : 'suspended', generation: this.generation,
    reason: 'generated independent review', principal: { mode: 'account', ...TEST_WORKSPACE_OWNER, role: 'Annotator' } }; }
  isActive(): boolean { return this.active; }
  owner() { return this.active ? { ...TEST_WORKSPACE_OWNER } : null; }
  projectId(): string | null { return this.active ? TEST_WORKSPACE_PROJECT_ID : null; }
  capture(): WorkspaceRequestIntent | null {
    if (!this.active) return null;
    const generation = this.generation;
    return { credential: 'generated-independent-owner', projectId: TEST_WORKSPACE_PROJECT_ID, generation,
      current: () => this.active && generation === this.generation };
  }
  subscribe(listener: (state: WorkspaceAccessState) => void): () => void {
    this.listeners.add(listener); listener(this.state()); return () => this.listeners.delete(listener);
  }
  retry(): void {}
  setActive(active: boolean): void { this.active = active; this.generation++; for (const listener of this.listeners) listener(this.state()); }
}

describe('workspace independent recovered-write review', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());
  function setup() {
    const lineId = 'generated-independent-recovery';
    const unresolved: WorkspaceOpaqueRecord = { version: 1, kind: 'line-editor-write', data: {
      submitted: 'SUBMITTED', parentRevisionId: 'rev-original-base', acknowledged: false,
      requestId: 'generated-original-request-identity',
    } };
    seedOwnedDraft(lineId, 'SUBMITTED', 'ORIGINAL BASE', { baseRevisionId: 'rev-original-base',
      undo: { version: 1, kind: 'line-editor-undo', data: [{ text: 'ORIGINAL BASE', revisionId: 'rev-original-base' }] },
      unconfirmedWrites: [unresolved] });
    const access = new ReviewAccess();
    const payload = { line: lineId, diplomatic: 'SUBMITTED', history: [{ id: 'rev-newer-server', text: 'SUBMITTED', author: 'generated', at: '' }] };
    const api = { line: () => of(payload), lineFresh: jasmine.createSpy('lineFresh').and.returnValue(of(payload)),
      providers: () => of({ providers: [] }), annotations: () => of([]), appendRevision: jasmine.createSpy('appendRevision') };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [
      provideWorkspaceTestAccess({ access }), { provide: ApiClient, useValue: api },
    ] });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', lineId); fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
    fixture.componentRef.setInput('authorId', 'generated'); fixture.detectChanges();
    return { fixture, access, api, lineId, original: readOwnedDraft(lineId)! };
  }

  it('does not replace an unknown recovered write ancestor or request identity merely because access resumes', () => {
    const s = setup();
    expect(s.original.base).toEqual({ text: 'ORIGINAL BASE', revisionId: 'rev-original-base' });
    const freshReadsBefore = s.api.lineFresh.calls.count();
    s.access.setActive(false); s.fixture.detectChanges();
    s.access.setActive(true); s.fixture.detectChanges();
    expect(s.api.lineFresh).toHaveBeenCalledTimes(freshReadsBefore + 1);
    const latest = readOwnedDraft(s.lineId)!;
    expect(latest.base).toEqual(s.original.base);
    expect(latest.undo).toEqual(s.original.undo);
    expect(latest.unconfirmedWrites).toEqual(s.original.unconfirmedWrites);
    expect(s.api.appendRevision).not.toHaveBeenCalled();
  });

  it('preserves the actual recovered submitted-request record when later text is typed', () => {
    const s = setup();
    const input = s.fixture.nativeElement.querySelector('input.greek') as HTMLInputElement;
    input.value = 'SUBMITTED LATER'; input.dispatchEvent(new Event('input')); s.fixture.detectChanges();
    const latest = readOwnedDraft(s.lineId)!;
    expect(latest.draftText).toBe('SUBMITTED LATER');
    expect(latest.base).toEqual(s.original.base);
    expect(latest.unconfirmedWrites).toEqual(s.original.unconfirmedWrites);
    expect(s.api.appendRevision).not.toHaveBeenCalled();
  });
});

describe('workspace independent line surface-scope review', () => {
  beforeEach(() => localStorage.clear()); afterEach(() => localStorage.clear());
  const payload = (text: string) => ({ line: 'generated-same-line', diplomatic: text,
    history: [{ id: 'generated-surface-base', text, author: 'generated', at: '' }] });
  function setup(line: () => import('rxjs').Observable<ReturnType<typeof payload>>) {
    const access = new ReviewAccess();
    const api = { line: jasmine.createSpy('line').and.callFake(line), lineFresh: jasmine.createSpy('lineFresh').and.callFake(line),
      providers: () => of({ providers: [] }), annotations: () => of([]),
      appendRevision: jasmine.createSpy('appendRevision').and.returnValue(new Subject()) };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [
      provideWorkspaceTestAccess({ access }), { provide: ApiClient, useValue: api },
    ] });
    const fixture = TestBed.createComponent(LineEditor); fixture.componentRef.setInput('lineId', 'generated-same-line');
    fixture.componentRef.setInput('surfaceId', 'generated-surface-a'); fixture.componentRef.setInput('authorId', 'generated');
    fixture.detectChanges(); return { fixture, api };
  }

  it('does not submit the old surface draft after only the surface input changes', () => {
    const s = setup(() => of(payload('SURFACE A SOURCE')));
    const input = s.fixture.nativeElement.querySelector('input.greek') as HTMLInputElement;
    input.value = 'SURFACE A DRAFT'; input.dispatchEvent(new Event('input')); s.fixture.detectChanges();
    const oldRecord = readOwnedDraft('generated-same-line', { surfaceId: 'generated-surface-a' });
    s.fixture.componentRef.setInput('surfaceId', 'generated-surface-b'); s.fixture.detectChanges();
    const component = s.fixture.componentInstance as unknown as { save(): void };
    component.save();
    expect(s.api.appendRevision).not.toHaveBeenCalled();
    expect(readOwnedDraft('generated-same-line', { surfaceId: 'generated-surface-a' })).toEqual(oldRecord);
    expect(readOwnedDraft('generated-same-line', { surfaceId: 'generated-surface-b' })?.draftText).not.toBe('SURFACE A DRAFT');
  });

  it('does not revive an old initial response after surface A to B to A with the same line ID', () => {
    const original = new Subject<ReturnType<typeof payload>>(); const later = new Subject<ReturnType<typeof payload>>();
    let reads = 0; const s = setup(() => reads++ === 0 ? original : later);
    s.fixture.componentRef.setInput('surfaceId', 'generated-surface-b'); s.fixture.detectChanges();
    s.fixture.componentRef.setInput('surfaceId', 'generated-surface-a'); s.fixture.detectChanges();
    original.next(payload('OLD INITIAL A')); s.fixture.detectChanges();
    const component = s.fixture.componentInstance as unknown as { payload(): { diplomatic?: string } | null };
    expect(component.payload()?.diplomatic).not.toBe('OLD INITIAL A');
    expect(s.api.appendRevision).not.toHaveBeenCalled();
  });
});

describe('workspace independent active unknown-write receipt', () => {
  beforeEach(() => localStorage.clear()); afterEach(() => localStorage.clear());
  for (const failureStatus of [0, 500]) it(`retains submitted identity and blocks retry after uncertain status ${failureStatus}`, () => {
    const lineId = 'generated-network-receipt-line';
    const saved = new Subject<{ revisionId: string }>();
    const api = { line: () => of({ line: lineId, diplomatic: 'BASE', history: [
      { id: 'generated-network-base', text: 'BASE', author: 'generated', at: '' },
    ] }), providers: () => of({ providers: [] }), annotations: () => of([]),
      appendRevision: jasmine.createSpy('appendRevision').and.returnValue(saved) };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [
      provideWorkspaceTestAccess(), { provide: ApiClient, useValue: api },
    ] });
    const fixture = TestBed.createComponent(LineEditor); fixture.componentRef.setInput('lineId', lineId);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID); fixture.componentRef.setInput('authorId', 'generated'); fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input.greek') as HTMLInputElement;
    input.value = 'SUBMITTED WITHOUT RECEIPT'; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    const save = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.trim() === 'Save revision')!;
    save.click(); fixture.detectChanges();
    const pending = readOwnedDraft(lineId)!; expect(pending.unconfirmedWrites.length).toBe(1);
    saved.error({ status: failureStatus, statusText: 'Generated uncertain failure: commit outcome unknown' }); fixture.detectChanges();
    const after = readOwnedDraft(lineId)!;
    expect(after.unconfirmedWrites).toEqual(pending.unconfirmedWrites);
    expect(after.base).toEqual(pending.base); expect(after.draftText).toBe('SUBMITTED WITHOUT RECEIPT');
    expect(save.disabled).toBeTrue(); save.click(); expect(api.appendRevision).toHaveBeenCalledTimes(1);
  });
});

describe('workspace independent actual cached-ack composition', () => {
  beforeEach(() => { localStorage.clear(); clearCache(); setApiKey('generated-independent-owner'); });
  afterEach(() => { TestBed.inject(HttpTestingController).verify(); setApiKey(''); clearCache(); localStorage.clear(); });
  it('never replaces an acknowledged submitted value with offline cached pre-save text', fakeAsync(() => {
    spyOn(globalThis.crypto.subtle, 'digest').and.returnValue(Promise.resolve(new Uint8Array(32).buffer));
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [
      provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(),
      provideWorkspaceTestAccess({ access: new ReviewAccess() }),
    ] });
    TestBed.overrideComponent(LineEditor, { set: { template: '' } });
    const backend = TestBed.inject(HttpTestingController);
    const lineId = 'generated-cached-ack-line';
    // A pre-existing legitimate old response remains a cache fallback hazard.
    // Initial loading is now fresh and must not be used to create this fixture.
    writeCache(`line:${lineId}`, { line: lineId, diplomatic: 'OLD CACHED BASE', history: [
      { id: 'generated-pre-save-revision', text: 'OLD CACHED BASE', author: 'generated', at: '' },
    ] }, { ownerSha256: '0'.repeat(64), authorityRevision: ensureCacheAuthorityRevision()! });
    const fixture = TestBed.createComponent(LineEditor); fixture.componentRef.setInput('lineId', lineId);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID); fixture.componentRef.setInput('authorId', 'generated'); fixture.detectChanges();
    backend.expectOne(`/api/v1/lines/${lineId}`).flush({ line: lineId, diplomatic: 'OLD CACHED BASE', history: [
      { id: 'generated-pre-save-revision', text: 'OLD CACHED BASE', author: 'generated', at: '' },
    ] });
    flushMicrotasks(); backend.expectOne(`/api/v1/lines/${lineId}/annotations`).flush([]); fixture.detectChanges();
    expect(readCache<{ diplomatic: string }>(`line:${lineId}`)?.diplomatic).toBe('OLD CACHED BASE');
    const component = fixture.componentInstance as unknown as { onEdit(event: Event): void; save(): void; text(): string };
    const input = document.createElement('input'); input.value = 'ACKNOWLEDGED SUBMISSION';
    component.onEdit({ target: input } as unknown as Event); component.save();
    backend.expectOne(`/api/v1/lines/${lineId}/revisions`).flush({ revisionId: 'generated-real-ack' });
    backend.expectOne(`/api/v1/lines/${lineId}`).error(new ProgressEvent('generated-refresh-network-loss'));
    flushMicrotasks(); fixture.detectChanges();
    // A buggy cached refresh may issue annotations after adopting OLD BASE.
    for (const pending of backend.match(`/api/v1/lines/${lineId}/annotations`)) pending.flush([]);
    expect(component.text()).toBe('ACKNOWLEDGED SUBMISSION');
    expect(readOwnedDraft(lineId)?.draftText).toBe('ACKNOWLEDGED SUBMISSION');
  }));
});

// Correct-review is also a submitted write, not a read-only review receipt.
describe('workspace independent correction-review uncertainty', () => {
  beforeEach(() => localStorage.clear()); afterEach(() => localStorage.clear());
  for (const status of [0, 500]) it(`preserves correction submission when its receipt is missing at status ${status}`, () => {
    const lineId = 'generated-uncertain-correction'; const reviewed = new Subject<unknown>();
    const api = { line: () => of({ line: lineId, diplomatic: 'BASE', history: [
      { id: 'generated-correction-base', text: 'BASE', author: 'generated', at: '' },
    ] }), providers: () => of({ providers: [] }), annotations: () => of([]),
      recordReview: jasmine.createSpy('recordReview').and.returnValue(reviewed), appendRevision: jasmine.createSpy('appendRevision') };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: api }] });
    const fixture = TestBed.createComponent(LineEditor); fixture.componentRef.setInput('lineId', lineId);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID); fixture.componentRef.setInput('authorId', 'generated'); fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input.greek') as HTMLInputElement;
    input.value = 'CORRECTION WITHOUT RECEIPT'; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(item => item.textContent?.trim() === 'Correct with current text')!;
    button.click(); fixture.detectChanges(); const before = readOwnedDraft(lineId)!;
    expect(before.unconfirmedWrites.length).toBe(1); reviewed.error({ status }); fixture.detectChanges();
    expect(readOwnedDraft(lineId)?.unconfirmedWrites).toEqual(before.unconfirmedWrites);
    expect(readOwnedDraft(lineId)?.base).toEqual(before.base);
    expect(button.disabled).toBeTrue(); button.click(); expect(api.recordReview).toHaveBeenCalledTimes(1);
  });
});

describe('workspace independent acknowledged pending ancestor', () => {
  beforeEach(() => localStorage.clear()); afterEach(() => localStorage.clear());
  it('retains the actual loaded base while a merged acknowledgement awaits its fresh text', () => {
    const lineId = 'generated-merged-ack-base';
    const acknowledgement = new Subject<{ revisionId: string; merged: boolean }>();
    const fresh = new Subject<{ line: string; diplomatic: string; history: never[] }>();
    const api = { line: () => of({ line: lineId, diplomatic: 'ACTUAL LOADED BASE', history: [
      { id: 'generated-loaded-parent', text: 'ACTUAL LOADED BASE', author: 'generated', at: '' },
    ] }), providers: () => of({ providers: [] }), annotations: () => of([]),
      appendRevision: () => acknowledgement };
    let reads = 0;
    const initial = api.line;
    const read = () => reads++ === 0 ? initial() : fresh;
    const facade = { ...api, line: read, lineFresh: read };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: facade }] });
    const fixture = TestBed.createComponent(LineEditor); fixture.componentRef.setInput('lineId', lineId);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID); fixture.componentRef.setInput('authorId', 'generated'); fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input.greek') as HTMLInputElement;
    input.value = 'CLIENT SUBMITTED VALUE'; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    const before = readOwnedDraft(lineId)!;
    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(item => item.textContent?.trim() === 'Save revision')!;
    button.click(); acknowledgement.next({ revisionId: 'generated-merged-server-revision', merged: true }); fixture.detectChanges();
    const waiting = readOwnedDraft(lineId)!;
    expect(waiting.base).toEqual(before.base);
    expect(waiting.draftText).toBe('CLIENT SUBMITTED VALUE');
    expect(waiting.unconfirmedWrites.length).toBe(1);
  });
});
