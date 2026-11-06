import { provideWorkspaceTestAccess } from './workspace-testing.fixture.spec';
import { controlledWorkspaceApi } from './workspace-controlled-api.fixture.spec';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Subject, of } from 'rxjs';
import { ApiClient } from './api-client';
import { SurfaceView } from './surface-view';
import { ApiKeyStorageKey, setApiKey } from './auth';
import { CacheAuthorityRevisionStorageKey, clearCache } from './offline-cache-storage';

/** Controller exports with real Angular input lifecycle; no child editor/model/data route. */
describe('SurfaceView authenticated export intent', () => {
  const routes: ReadonlyArray<{
    method: string; api: string; value: unknown; filename: string; args: string[];
    mime: string; expectedText?: string; expectedBytes?: number[];
  }> = [
    { method: 'downloadTei', api: 'tei', value: '<TEI>Generated</TEI>', filename: 'surf-A.tei.xml', args: ['col-A', 'surf-A', 'generated-A'],
      mime: 'application/xml', expectedText: '<TEI>Generated</TEI>' },
    { method: 'downloadEditionHtml', api: 'editionHtml', value: '<p>Generated</p>', filename: 'surf-A-edition.html', args: ['col-A', 'surf-A', 'generated-A'],
      mime: 'text/html', expectedText: '<p>Generated</p>' },
    { method: 'downloadWordBoundaries', api: 'wordBoundaryCsv', value: 'line_id,start,end\nln-generated,0,1', filename: 'surf-A-word-boundaries.csv', args: ['col-A', 'generated-A'],
      mime: 'text/csv', expectedText: 'line_id,start,end\nln-generated,0,1' },
    { method: 'downloadSearchablePdf', api: 'searchablePdf', value: new Blob([new Uint8Array([37, 80, 68, 70, 0, 255, 13, 10])], { type: 'application/pdf' }), filename: 'surf-A.pdf', args: ['col-A', 'surf-A', 'generated-A'],
      mime: 'application/pdf', expectedBytes: [37, 80, 68, 70, 0, 255, 13, 10] },
    { method: 'downloadTraining', api: 'trainingExport', value: { generated: true, lines: [] }, filename: 'col-A-training.json', args: ['col-A', 'generated-A'],
      mime: 'application/json', expectedText: '{\n  "generated": true,\n  "lines": []\n}' },
    { method: 'downloadBundle', api: 'projectBundle', value: new Blob([new Uint8Array([80, 75, 3, 4, 0, 255, 1, 128])], { type: 'application/zip' }), filename: 'col-A.zip', args: ['col-A', 'generated-A'],
      mime: 'application/zip', expectedBytes: [80, 75, 3, 4, 0, 255, 1, 128] },
  ];
  let fixture: ComponentFixture<SurfaceView>;
  let component: any;
  let response: Subject<any>;
  let apiSpies: Record<string, jasmine.Spy>;
  let createUrl: jasmine.Spy;
  let revokeUrl: jasmine.Spy;
  let click: jasmine.Spy;
  let downloads: string[];
  beforeEach(() => {
    localStorage.clear(); setApiKey('generated-A'); clearCache();
    TestBed.configureTestingModule({ imports: [SurfaceView], providers: [provideHttpClient(), provideHttpClientTesting(), provideWorkspaceTestAccess({ projectId: 'col-A', credential: 'generated-A' })] });
    // Preserve actual ngOnChanges/OnDestroy. Only unrelated initialization/children are suppressed.
    TestBed.overrideComponent(SurfaceView, { set: { template: '', imports: [] } });
    // Keep the real identity initialization while suppressing only child rendering.
    spyOn(ApiClient.prototype, 'forWorkspaceRequest').and.callFake(function(this: ApiClient, intent) {
      return controlledWorkspaceApi(this, intent);
    });
    spyOn(ApiClient.prototype, 'diplomaticExport').and.returnValue(of({ text: '', lossReport: { format: 'text', contractVersion: 'generated', entries: [] } }));
    spyOn(ApiClient.prototype, 'reviewQueue').and.returnValue(of([]));
    spyOn(ApiClient.prototype, 'models').and.returnValue(of([]));
    spyOn(ApiClient.prototype, 'profileReport').and.returnValue(of({ profile: 'generated', version: '1', surfaces: [] } as any));
    spyOn(ApiClient.prototype, 'loadProjectFresh').and.callFake(projectId => of({ id: projectId, name: 'generated', objects: [{ id: 'generated-object', surfaces: [{
      id: component.surfaceId(), kind: 'page', lines: [], regions: [], captures: [],
    }] }] } as any));
    spyOn(ApiClient.prototype, 'createSession').and.returnValue(of({ id: 'generated-session', nodeId: 'generated-node' } as any));
    spyOn(ApiClient.prototype, 'touchPresence').and.returnValue(of({ editor: 'generated', editors: [] } as any));
    spyOn(ApiClient.prototype, 'revokeSession').and.returnValue(of({} as any));
    spyOn(ApiClient.prototype, 'leavePresence').and.returnValue(of({ editors: [] }));
    response = new Subject<any>(); apiSpies = {};
    apiSpies['tei'] = spyOn(ApiClient.prototype, 'tei').and.returnValue(response);
    apiSpies['editionHtml'] = spyOn(ApiClient.prototype, 'editionHtml').and.returnValue(response);
    apiSpies['wordBoundaryCsv'] = spyOn(ApiClient.prototype, 'wordBoundaryCsv').and.returnValue(response);
    apiSpies['searchablePdf'] = spyOn(ApiClient.prototype, 'searchablePdf').and.returnValue(response);
    apiSpies['trainingExport'] = spyOn(ApiClient.prototype, 'trainingExport').and.returnValue(response);
    apiSpies['projectBundle'] = spyOn(ApiClient.prototype, 'projectBundle').and.returnValue(response);
    downloads = [];
    createUrl = spyOn(URL, 'createObjectURL').and.returnValue('blob:generated-export');
    revokeUrl = spyOn(URL, 'revokeObjectURL');
    click = spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function(this: HTMLAnchorElement) { downloads.push(this.download); });
    fixture = TestBed.createComponent(SurfaceView); component = fixture.componentInstance;
    fixture.componentRef.setInput('projectId', 'col-A'); fixture.componentRef.setInput('surfaceId', 'surf-A');
    fixture.detectChanges();
  });
  afterEach(() => { fixture.destroy(); TestBed.resetTestingModule(); setApiKey(''); clearCache(); localStorage.clear(); });
  function invoke(method: string): boolean {
    expect(typeof component[method]).toBe('function'); // Old bundle anchor has no authenticated action.
    if (typeof component[method] !== 'function') return false;
    component[method](); return true;
  }
  function setScope(project: string, surface: string): void {
    fixture.componentRef.setInput('projectId', project); fixture.componentRef.setInput('surfaceId', surface);
    fixture.detectChanges(); // Actual Angular ngOnChanges, not a simulated epoch assignment.
  }
  for (const route of routes) {
    it(`${route.method} downloads exact current bytes and MIME with captured arguments and filename`, async () => {
      if (!invoke(route.method)) return;
      expect(apiSpies[route.api]).toHaveBeenCalledOnceWith(...route.args);
      response.next(route.value);
      expect(downloads).toEqual([route.filename]); expect(createUrl).toHaveBeenCalledTimes(1);
      expect(revokeUrl).toHaveBeenCalledOnceWith('blob:generated-export');
      const blob = createUrl.calls.mostRecent().args[0] as Blob;
      expect(blob instanceof Blob).toBeTrue(); expect(blob.type).toBe(route.mime);
      if (route.expectedBytes) expect(Array.from(new Uint8Array(await blob.arrayBuffer()))).toEqual(route.expectedBytes);
      else expect(await blob.text()).toBe(route.expectedText!);
    });
    for (const change of ['project', 'surface', 'input ABA', 'credential', 'authority ABA', 'destroy']) {
      it(`${route.method} discards a pending success after ${change}`, () => {
        if (!invoke(route.method)) return;
        if (change === 'project') setScope('col-B', 'surf-A');
        else if (change === 'surface') setScope('col-A', 'surf-B');
        else if (change === 'input ABA') { setScope('col-B', 'surf-B'); setScope('col-A', 'surf-A'); }
        else if (change === 'credential') setApiKey('generated-B');
        else if (change === 'authority ABA') {
          const old = localStorage.getItem(CacheAuthorityRevisionStorageKey);
          localStorage.setItem(ApiKeyStorageKey, 'generated-B');
          localStorage.setItem(CacheAuthorityRevisionStorageKey, old === 'b'.repeat(32) ? 'c'.repeat(32) : 'b'.repeat(32));
          localStorage.setItem(ApiKeyStorageKey, 'generated-A');
        } else fixture.destroy();
        component.exportStatus.set('current status'); response.next(route.value);
        expect(createUrl).not.toHaveBeenCalled(); expect(click).not.toHaveBeenCalled();
        expect(revokeUrl).not.toHaveBeenCalled(); expect(component.exportStatus()).toBe('current status');
      });
    }
    it(`${route.method} reports a current HTTP error without downloading`, () => {
      if (!invoke(route.method)) return;
      response.error({ status: 500 }); // Ordinary current failure; authoritative access denials suspend through the real transport tests.
      expect(component.exportStatus()).toContain('failed'); expect(createUrl).not.toHaveBeenCalled();
    });
    it(`${route.method} ignores an obsolete HTTP error`, () => {
      if (!invoke(route.method)) return;
      setScope('col-B', 'surf-B'); component.exportStatus.set('new scope'); response.error({ status: 403 });
      expect(component.exportStatus()).toBe('new scope'); expect(createUrl).not.toHaveBeenCalled();
    });
  }
  it('does not dispatch any export after actual component destruction', () => {
    fixture.destroy();
    for (const route of routes) invoke(route.method);
    for (const spy of Object.values(apiSpies)) expect(spy).not.toHaveBeenCalled();
    expect(createUrl).not.toHaveBeenCalled();
  });
});
