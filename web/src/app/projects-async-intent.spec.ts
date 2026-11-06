import { TEST_WORKSPACE_OWNER } from './workspace-testing.fixture.spec';
const freshProfile = { authenticated: true, mode: 'account', ...TEST_WORKSPACE_OWNER, name: 'Generated reviewer', role: 'Reviewer' };
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Subject, ReplaySubject, of } from 'rxjs';
import { controlledWorkspaceApi } from './workspace-controlled-api.fixture.spec';
import { ApiClient } from './api-client';
import { ProjectsPage } from './projects-page';
import { ApiKeyStorageKey, setApiKey } from './auth';
import { CacheAuthorityRevisionStorageKey, clearCache } from './offline-cache-storage';

/** Generated callback scheduling cases. Child editor ownership is deliberately outside this suite. */
describe('ProjectsPage asynchronous intent', () => {
  let page: any;
  let fixture: ComponentFixture<ProjectsPage>;
  let list: Subject<any>;
  let statistics: Subject<any>[];
  let reviews: Subject<any>[];
  let loads: Subject<any>[];
  let searches: Subject<any>[];
  let imports: Subject<any>[];
  let pdf: Subject<Blob>;
  let html: Subject<string>;
  let create: Subject<any>;
  let originalKey: string | null;
  const hit = (text: string) => ({ totalHits: 1, truncated: false, hits: [{ surfaceId: 's', lineId: 'l', revisionId: 'r', layer: 'L1Diplomatic', snippet: text, match: { start: 0, end: 1 } }] });
  const input = (value: string) => ({ target: { value } }) as unknown as Event;
  function deferredFile() {
    let resolve!: (value: string) => void;
    let reject!: (reason: unknown) => void;
    const promise = new Promise<string>((yes, no) => { resolve = yes; reject = no; });
    return { resolve, reject, event: { target: { files: [{ text: () => promise }] } } as unknown as Event };
  }
  async function settle() { await Promise.resolve(); await Promise.resolve(); }
  beforeEach(() => {
    originalKey = localStorage.getItem('leviathan:api-key');
    setApiKey('generated-A'); clearCache();
    window.history.replaceState(null, '', '/');
    TestBed.configureTestingModule({ imports: [ProjectsPage], providers: [provideHttpClient(), provideHttpClientTesting()] });
    list = new ReplaySubject(1); statistics = []; reviews = []; loads = []; searches = []; imports = [];
    pdf = new Subject(); html = new Subject(); create = new Subject();
    spyOn(ApiClient.prototype, 'me').and.returnValue(of(freshProfile as any));
    spyOn(ApiClient.prototype, 'listProjectsFresh').and.returnValue(list);
    spyOn(ApiClient.prototype, 'stats').and.callFake(() => { const s = new Subject<any>(); statistics.push(s); return s; });
    spyOn(ApiClient.prototype, 'reviewSummary').and.callFake(() => { const s = new Subject<any>(); reviews.push(s); return s; });
    spyOn(ApiClient.prototype, 'loadProjectFresh').and.callFake(id => { const s = new ReplaySubject<any>(1); loads.push(s); s.next({ id, name: 'initial ' + id, objects: [] }); return s; });
    spyOn(ApiClient.prototype, 'search').and.callFake(() => { const s = new Subject<any>(); searches.push(s); return s; });
    spyOn(ApiClient.prototype, 'importLineText').and.callFake(() => { const s = new Subject<any>(); imports.push(s); return s; });
    spyOn(ApiClient.prototype, 'projectSearchablePdf').and.returnValue(pdf);
    spyOn(ApiClient.prototype, 'projectEditionHtml').and.returnValue(html);
    spyOn(ApiClient.prototype, 'createProject').and.returnValue(create);
    // Endpoint prototype spies bypass HttpClient. Reproduce only the public scoped
    // transport's admission/response fence using its ACTUAL captured intent; the
    // coordinator and fresh identity checks remain real. Real HTTP has separate tests.
    spyOn(ApiClient.prototype, 'forWorkspaceRequest').and.callFake(function(this: ApiClient, intent) { return controlledWorkspaceApi(this, intent); });
    // No detectChanges: these are controller callback tests, not child editor mocks or cache tests.
    fixture = TestBed.createComponent(ProjectsPage);
    page = fixture.componentInstance;
    list.next([{ id: 'A', name: 'generated A' }, { id: 'B', name: 'generated B' }]);
    page.select('A');
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    setApiKey(originalKey ?? ''); clearCache();
    window.history.replaceState(null, '', '/');
  });

  it('does not install old statistics, review or payload after selecting another collection', () => {
    (ApiClient.prototype.loadProjectFresh as jasmine.Spy).and.callFake(() => { const s = new Subject<any>(); loads.push(s); return s; });
    page.select('B');
    loads[1].next({ id: 'B', name: 'current', objects: [] });
    statistics[1].next({ lines: 2 }); reviews[1].next({ needsAdjudication: 2 });
    statistics[0].next({ lines: 900 }); reviews[0].next({ needsAdjudication: 900 });
    loads[0].next({ id: 'A', name: 'stale', objects: [] });
    expect(page.stats().lines).toBe(2); expect(page.review().needsAdjudication).toBe(2);
    expect(page.payloads()['A'].name).toBe('initial A'); expect(page.payloads()['B'].name).toBe('current');
  });
  it('rejects original A callbacks after selection A to B to A, including stale errors', () => {
    page.select('B'); page.select('A');
    statistics[2].next({ lines: 3 }); reviews[2].next({ needsAdjudication: 3 });
    statistics[0].error(new Error('old')); reviews[0].error(new Error('old'));
    loads[0].next({ id: 'A', name: 'old A', objects: [] });
    expect(page.stats().lines).toBe(3); expect(page.review().needsAdjudication).toBe(3);
    expect(page.payloads()['A'].name).toBe('initial A');
  });
  it('keeps the newest search result when old success and error callbacks arrive', () => {
    page.query.set('alpha'); page.runSearch(); page.runSearch(); page.runSearch();
    searches[2].next(hit('newest')); searches[0].next(hit('old')); searches[1].error(new Error('old'));
    expect(page.searchHits()[0].snippet).toBe('newest'); expect(page.searchStatus()).toBe('1 hit(s)');
  });
  for (const change of ['query', 'mode', 'selection']) {
    it(`clears hits and invalidates an outstanding search on ${change} change`, () => {
      page.query.set('alpha'); page.runSearch(); searches[0].next(hit('visible'));
      page.runSearch();
      if (change === 'query') page.onQuery(input('beta'));
      else if (change === 'mode') page.onMode(input('base'));
      else page.select('B');
      expect(page.searchHits()).toEqual([]); expect(page.searchStatus()).toBe('');
      searches[1].next(hit('late')); expect(page.searchHits()).toEqual([]);
    });
  }
  for (const change of ['selection', 'layer', 'credentials', 'shared authority ABA', 'destroy']) {
    it(`does not submit deferred CSV after ${change} change`, async () => {
      const file = deferredFile(); page.onCsvFile(file.event);
      if (change === 'selection') { page.select('B'); page.select('A'); }
      else if (change === 'layer') { page.onImportLayer(input('L2Editorial')); page.onImportLayer(input('L1Diplomatic')); }
      else if (change === 'credentials') setApiKey('generated-B');
      else if (change === 'shared authority ABA') {
        // Model another updated tab completing A→B→A before queued storage events.
        localStorage.setItem(ApiKeyStorageKey, 'generated-B');
        localStorage.setItem(CacheAuthorityRevisionStorageKey, 'b'.repeat(32));
        localStorage.setItem(ApiKeyStorageKey, 'generated-A');
      }
      else fixture.destroy();
      file.resolve('line_id,text\nl,generated'); await settle();
      expect(ApiClient.prototype.importLineText).not.toHaveBeenCalled();
    });
  }
  it('submits only the latest selected file with its captured project, layer and credential', async () => {
    const old = deferredFile(); const current = deferredFile();
    page.onCsvFile(old.event); page.onCsvFile(current.event);
    current.resolve('current'); old.resolve('old'); await settle();
    expect(ApiClient.prototype.importLineText).toHaveBeenCalledOnceWith('A', 'current', 'L1Diplomatic', 'generated-A');
  });
  it('ignores an already submitted CSV acknowledgement after navigation without claiming rollback', async () => {
    const file = deferredFile(); page.onCsvFile(file.event); file.resolve('sent'); await settle();
    expect(imports.length).toBe(1); page.select('B');
    imports[0].next({ applied: 1, skipped: 0, rows: [] });
    expect(page.selectedId()).toBe('B'); expect(page.importStatus()).toBe('');
    expect(ApiClient.prototype.loadProjectFresh).toHaveBeenCalledTimes(2);
  });
  it('refreshes the current collection and retains successful import feedback', async () => {
    const file = deferredFile(); page.onCsvFile(file.event); file.resolve('current'); await settle();
    imports[0].next({ applied: 2, skipped: 1, rows: [] });
    expect(page.selectedId()).toBe('A'); expect(page.importStatus()).toBe('applied 2, skipped 1');
    expect(ApiClient.prototype.loadProjectFresh).toHaveBeenCalledTimes(2);
  });
  it('reports a current CSV read failure and ignores a stale read failure', async () => {
    const file = deferredFile(); page.onCsvFile(file.event); file.reject(new Error('read')); await settle();
    expect(page.importStatus()).toBe('CSV file could not be read');
    const old = deferredFile(); page.onCsvFile(old.event); page.select('B'); old.reject(new Error('late')); await settle();
    expect(page.importStatus()).toBe('');
  });
  for (const change of ['selection', 'credentials', 'destroy']) {
    it(`does not download an export after ${change} change`, () => {
      const urls = spyOn(URL, 'createObjectURL').and.returnValue('blob:generated');
      const click = spyOn(HTMLAnchorElement.prototype, 'click');
      page.downloadProjectPdf(); page.downloadEdition();
      if (change === 'selection') { page.select('B'); page.select('A'); }
      else if (change === 'credentials') setApiKey('generated-B');
      else fixture.destroy();
      pdf.next(new Blob(['generated'])); html.next('<p>generated</p>');
      expect(urls).not.toHaveBeenCalled(); expect(click).not.toHaveBeenCalled();
    });
  }
  it('downloads current exports using captured request credentials', () => {
    spyOn(URL, 'createObjectURL').and.returnValue('blob:generated');
    const revoke = spyOn(URL, 'revokeObjectURL'); const click = spyOn(HTMLAnchorElement.prototype, 'click');
    page.downloadProjectPdf(); page.downloadEdition();
    pdf.next(new Blob(['generated'])); html.next('<p>generated</p>');
    expect(ApiClient.prototype.projectSearchablePdf).toHaveBeenCalledOnceWith('A', 'generated-A');
    expect(ApiClient.prototype.projectEditionHtml).toHaveBeenCalledOnceWith('A', 'generated-A');
    expect(click).toHaveBeenCalledTimes(2); expect(revoke).toHaveBeenCalledTimes(2);
  });
  it('suppresses list/deep-link and create callbacks after destruction', () => {
    page.create(); fixture.destroy();
    window.history.replaceState(null, '', '/?project=B');
    list.next([{ id: 'B', name: 'late' }]); create.next({ id: 'created-late' });
    expect(page.projects().map((p: any) => p.id)).toEqual(['A', 'B']);
    expect(page.selectedId()).toBe('A'); expect(page.createdId()).toBe('');
  });
});
