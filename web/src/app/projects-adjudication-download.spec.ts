import { controlledWorkspaceApi } from './workspace-controlled-api.fixture.spec';
import { TEST_WORKSPACE_OWNER } from './workspace-testing.fixture.spec';
const freshProfile = { authenticated: true, mode: 'account', ...TEST_WORKSPACE_OWNER, name: 'Generated reviewer', role: 'Reviewer' };
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Subject, of } from 'rxjs';
import { ProjectsPage } from './projects-page';
import { ApiClient } from './api-client';
import { ApiKeyStorageKey, setApiKey } from './auth';
import { clearCache } from './offline-cache-storage';

describe('ProjectsPage authenticated adjudication download', () => {
  let page: any;
  let source: Subject<string>;
  let original: string | null;
  beforeEach(() => {
    original = localStorage.getItem(ApiKeyStorageKey); setApiKey('generated-reader'); clearCache();
    window.history.replaceState(null, '', '/');
    TestBed.configureTestingModule({ imports: [ProjectsPage], providers: [provideHttpClient(), provideHttpClientTesting()] });
    spyOn(ApiClient.prototype, 'me').and.returnValue(of(freshProfile as any));
    spyOn(ApiClient.prototype, 'listProjectsFresh').and.returnValue(of([{ id: 'A', name: 'Generated' }, { id: 'B', name: 'Generated B' }]));
    spyOn(ApiClient.prototype, 'stats').and.returnValue(of({} as any));
    spyOn(ApiClient.prototype, 'reviewSummary').and.returnValue(of({} as any));
    spyOn(ApiClient.prototype, 'loadProjectFresh').and.callFake(id => of({ id, name: 'Generated', objects: [] } as any));
    spyOn(ApiClient.prototype, 'forWorkspaceRequest').and.callFake(function(this: ApiClient, intent) { return controlledWorkspaceApi(this, intent); });
    source = new Subject(); spyOn(ApiClient.prototype, 'adjudicationCsv').and.returnValue(source);
    page = TestBed.createComponent(ProjectsPage).componentInstance;
    page.select('A');
  });
  afterEach(() => { TestBed.resetTestingModule(); setApiKey(original ?? ''); clearCache(); window.history.replaceState(null, '', '/'); });
  it('uses captured authenticated HTTP and preserves CSV bytes and originating filename without a token URL', async () => {
    let content!: Blob; let anchor!: HTMLAnchorElement;
    const urls = spyOn(URL, 'createObjectURL').and.callFake(blob => { content = blob as Blob; return 'blob:generated'; });
    const revoke = spyOn(URL, 'revokeObjectURL');
    spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function(this: HTMLAnchorElement) { anchor = this; });
    page.downloadAdjudication(); source.next('line_id,text\r\nl,generated\r\n');
    expect(ApiClient.prototype.adjudicationCsv).toHaveBeenCalledOnceWith('A', 'generated-reader');
    expect(urls).toHaveBeenCalledTimes(1); expect(content.type).toBe('text/csv');
    expect(await content.text()).toBe('line_id,text\r\nl,generated\r\n');
    expect(anchor.download).toBe('A-adjudication.csv'); expect(anchor.href).toBe('blob:generated');
    expect(revoke).toHaveBeenCalledOnceWith('blob:generated');
  });
  it('does not download after a current authoritative HTTP refusal', () => {
    const urls = spyOn(URL, 'createObjectURL'); const click = spyOn(HTMLAnchorElement.prototype, 'click');
    page.downloadAdjudication(); source.error({ status: 403 });
    expect(urls).not.toHaveBeenCalled(); expect(click).not.toHaveBeenCalled();
    expect(page.editionStatus()).toBe('adjudication CSV export failed');
  });
  for (const change of ['selection ABA', 'credential', 'authority', 'destroy']) {
    it(`ignores late adjudication data after ${change}`, () => {
      const urls = spyOn(URL, 'createObjectURL'); const click = spyOn(HTMLAnchorElement.prototype, 'click');
      page.downloadAdjudication();
      if (change === 'selection ABA') { page.select('B'); page.select('A'); }
      else if (change === 'credential') setApiKey('generated-other');
      else if (change === 'authority') clearCache();
      else page.ngOnDestroy();
      source.next('line_id,text\nl,old');
      expect(urls).not.toHaveBeenCalled(); expect(click).not.toHaveBeenCalled();
      expect(page.editionStatus()).toBe('');
    });
  }
});
