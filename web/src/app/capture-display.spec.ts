import { Component, input, output } from '@angular/core';
import { By } from '@angular/platform-browser';
import { WorkspaceAccessCoordinator } from './workspace-access';
import { WORKSPACE_ACCESS } from './workspace-access-types';
import { TEST_WORKSPACE_OWNER } from './workspace-testing.fixture.spec';
import { controlledWorkspaceApi } from './workspace-controlled-api.fixture.spec';
import { cacheAuthorityRevision } from './offline-cache-storage';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of, Subject } from 'rxjs';
import { ApiClient, CaptureDisplayDescriptor } from './api-client';
import { ApiKeyStorageKey, authInterceptor, setApiKey, apiKey, cacheCredentialGeneration } from './auth';
import { SurfaceView, validCaptureDisplay } from './surface-view';
import { GeometryEditor } from './geometry-editor';
import { displayFixture } from './capture-display.fixture';

describe('Authenticated capture display transport', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()] });
  });
  afterEach(() => { TestBed.inject(HttpTestingController).verify(); setApiKey(''); });

  it('fetches uncached descriptor and identity-bound PNG with headers, never credentials in URLs', () => {
    setApiKey('generated-display-key');
    const api = TestBed.inject(ApiClient); const http = TestBed.inject(HttpTestingController);
    const base = '/api/v1/projects/col1/surfaces/surf1/captures/cap1/display';
    api.captureDisplay('col1', 'surf1', 'cap1').subscribe();
    const metadata = http.expectOne(base);
    expect(metadata.request.headers.get('X-Api-Key')).toBe('generated-display-key');
    metadata.flush(displayFixture());
    let received: Blob | undefined;
    api.captureDisplayImage('col1', 'surf1', 'cap1', 'b'.repeat(64)).subscribe(blob => received = blob);
    const raster = http.expectOne(base + '/image');
    expect(raster.request.responseType).toBe('blob');
    expect(raster.request.headers.get('If-Match')).toBe('"' + 'b'.repeat(64) + '"');
    expect(raster.request.headers.get('X-Api-Key')).toBe('generated-display-key');
    expect(raster.request.urlWithParams).not.toContain('api_key');
    const blob = new Blob(['generated'], { type: 'image/png' }); raster.flush(blob);
    expect(received).toBe(blob);
  });

  it('refuses invalid identity, orientation and rounded-axis metadata before raster use', () => {
    expect(validCaptureDisplay(displayFixture(), 'col1', 'surf1', 'cap1')).toBeTrue();
    for (const change of [{ captureId: 'other' }, { sourceWidth: 0 }, { scaleX: 0 }, { scaleY: NaN }, { orientation: 'rotated' }, { derivativeId: 'not-a-digest' }]) {
      expect(validCaptureDisplay({ ...displayFixture(), ...change } as CaptureDisplayDescriptor, 'col1', 'surf1', 'cap1')).toBeFalse();
    }
    expect(validCaptureDisplay(displayFixture('col1', 'surf1', { sourceWidth: 3001, sourceHeight: 2000, displayWidth: 2048, displayHeight: 1364, scaleX: 2048 / 3001, scaleY: 1364 / 2000 }), 'col1', 'surf1', 'cap1')).toBeTrue();
  });

  it('pins secondary-capture creation to the preview and preserves all raw surface IDs with header authentication', () => {
    setApiKey('generated-geometry-key');
    const api = TestBed.inject(ApiClient); const http = TestBed.inject(HttpTestingController);
    const display = displayFixture('col1', 'surf1', { captureId: 'cap2', sourceFrameRef: 'der-frame2' });
    const polygon = [{ x: 1.25, y: 2.5 }, { x: 30, y: 2.5 }, { x: 30, y: 40 }];
    api.addCaptureLine(display, polygon, [], ['ln-primary', 'ln-secondary'], ['reg-primary', 'reg-secondary'], 'reg-secondary').subscribe();
    const request = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap2/lines');
    expect(request.request.headers.get('X-Api-Key')).toBe('generated-geometry-key');
    expect(request.request.headers.get('If-Match')).toBe('"' + display.derivativeId + '"');
    expect(request.request.urlWithParams).not.toContain('api_key');
    expect(request.request.body).toEqual({ polygon, baseline: [], expectedLineIds: ['ln-primary', 'ln-secondary'],
      expectedRegionIds: ['reg-primary', 'reg-secondary'], regionId: 'reg-secondary' });
    request.flush({ lineId: 'ln-created' });
  });
});

@Component({ selector: 'app-geometry-editor', standalone: true, template: '' })
class CaptureLifecycleGeometryProbe {
  projectId = input(''); surfaceId = input(''); imageUrl = input(''); readOnly = input(true);
  structureBlocked = input(false); display = input<CaptureDisplayDescriptor | null>(null);
  captureSwitchBlocked = output<boolean>(); imageFailed = output<void>(); lineSelected = output<string>();
}

describe('SurfaceView capture preview lifecycle', () => {
  const accesses: WorkspaceAccessCoordinator<any>[] = [];
  beforeEach(() => localStorage.clear());
  afterEach(() => { for (const access of accesses.splice(0)) access.destroy(); setApiKey(''); });

  function setup(remote = false) {
    setApiKey('generated-initial-key');
    const metadata: { id: string; reply: Subject<CaptureDisplayDescriptor> }[] = [];
    const images: { id: string; digest: string; reply: Subject<Blob> }[] = [];
    const fresh: Subject<any>[] = [];
    const payload = { id: 'col1', name: 'generated', objects: [{ id: 'obj1', surfaces: [{
      id: 'surf1', kind: 'page', lines: [], regions: [], captures: [
        { id: 'cap1', imageLocation: remote ? 'https://public.invalid/first.jpg' : '/private/generated-first.png' },
        { id: 'cap2', imageLocation: '/private/generated-second.png' },
      ],
    }] }] };
    let initialIdentity = true;
    const api = {
      createSession: () => of({ id: 'session', nodeId: 'node' }), touchPresence: () => of({ editor: 'generated', editors: [] }),
      leavePresence: () => of({}), revokeSession: () => of({}), sessions: () => of([]), surfaceFragments: () => of([]),
      diplomaticExport: () => of({ text: '', lossReport: { format: 'text', contractVersion: 'fixture', entries: [] } }),
      reviewQueue: () => of([]), models: () => of([]), profileReport: () => of({ profile: 'fixture', version: '1', surfaces: [] }),
      geometryHistory: () => of([]), unplacedLines: () => of([]),
      loadProjectFresh: jasmine.createSpy('fresh').and.callFake(() => { if (initialIdentity) { initialIdentity = false; return of(payload); } const reply = new Subject<any>(); fresh.push(reply); return reply; }),
      forWorkspaceRequest: (intent: { credential: string; current(): boolean }): ApiClient => controlledWorkspaceApi(api as unknown as ApiClient, intent),
      captureDisplay: jasmine.createSpy('descriptor').and.callFake((_p: string, _s: string, id: string) => { const reply = new Subject<CaptureDisplayDescriptor>(); metadata.push({ id, reply }); return reply; }),
      captureDisplayImage: jasmine.createSpy('image').and.callFake((_p: string, _s: string, id: string, digest: string) => { const reply = new Subject<Blob>(); images.push({ id, digest, reply }); return reply; }),
    };
    const profiles: Subject<any>[] = [], authorizedProjects: Subject<any>[] = [];
    let initialAdmission = true;
    const profile = { authenticated: true, mode: 'account', ...TEST_WORKSPACE_OWNER, name: 'Generated reviewer', role: 'Reviewer' };
    const access = new WorkspaceAccessCoordinator({ api: {
      me: () => { if (initialAdmission) return of(profile as any); const reply = new Subject<any>(); profiles.push(reply); return reply; },
      loadProjectFresh: () => { if (initialAdmission) return of(payload); const reply = new Subject<any>(); authorizedProjects.push(reply); return reply; },
    }, readScope: () => ({ credential: apiKey(), localGeneration: cacheCredentialGeneration(), authorityRevision: cacheAuthorityRevision() }) });
    accesses.push(access); access.open('col1'); initialAdmission = false;
    TestBed.configureTestingModule({ imports: [SurfaceView], providers: [{ provide: WORKSPACE_ACCESS, useValue: access }, { provide: ApiClient, useValue: api }] });
    // Isolate only the parent lifecycle; independent GeometryEditor tests cover raster/SVG geometry.
    TestBed.overrideComponent(SurfaceView, { remove: { imports: [GeometryEditor] }, add: { imports: [CaptureLifecycleGeometryProbe] } });
    const create = spyOn(URL, 'createObjectURL').and.returnValues('blob:generated-one', 'blob:generated-two', 'blob:generated-three');
    const revoke = spyOn(URL, 'revokeObjectURL');
    const fixture = TestBed.createComponent(SurfaceView);
    fixture.componentRef.setInput('projectId', 'col1'); fixture.componentRef.setInput('surfaceId', 'surf1'); fixture.detectChanges();
    const component = fixture.componentInstance as any;
    const switchTo = (index: number) => { component.onCapture({ target: { value: String(index) } }); fixture.detectChanges(); };
    const finish = (request = metadata.length - 1) => {
      const item = metadata[request]; item.reply.next(displayFixture('col1', 'surf1', { captureId: item.id, sourceFrameRef: item.id }));
      images[images.length - 1].reply.next(new Blob(['generated'], { type: 'image/png' })); fixture.detectChanges();
    };
    const confirmAccess = () => {
      const before = metadata.length;
      access.retry(); expect(access.isActive()).toBeFalse();
      profiles[profiles.length - 1].next(profile); expect(metadata.length).toBe(before);
      authorizedProjects[authorizedProjects.length - 1].next(payload); fixture.detectChanges();
      expect(metadata.length).toBe(before);
      // Same-owner principal confirmation does not replace the surface identity read.
      fresh[fresh.length - 1].next(payload); fixture.detectChanges();
    };
    return { fixture, component, access, confirmAccess, profiles, authorizedProjects, metadata, images, fresh, payload, api, create, revoke, switchTo, finish };
  }

  it('loads a capture-only surface by ID without treating filesystem metadata as an image URL', () => {
    const s = setup(); expect(s.metadata[0].id).toBe('cap1'); expect(s.component.imageLink()).toBe('');
    s.finish(); expect(s.component.imageLink()).toBe('blob:generated-one');
    expect(s.images[0].digest).toBe('b'.repeat(64)); expect(s.component.captureDisplay().sourceFrameRef).toBe('cap1');
    expect(s.fixture.nativeElement.innerHTML).not.toContain('src="/private/');
  });

  it('cancels stale capture replies and revokes only the owned previous object URL', () => {
    const s = setup(); s.finish(); s.switchTo(1);
    expect(s.revoke).toHaveBeenCalledWith('blob:generated-one'); expect(s.component.imageLink()).toBe('');
    expect(s.metadata[0].reply.observed).toBeFalse(); expect(s.images[0].reply.observed).toBeFalse();
    s.metadata[0].reply.next(displayFixture()); expect(s.images.length).toBe(1);
    s.finish(); expect(s.component.imageLink()).toBe('blob:generated-two');
    s.fixture.destroy(); expect(s.revoke).toHaveBeenCalledWith('blob:generated-two');
  });

  it('clears previews on same-tab and other-tab key changes before fresh authenticated retrieval', () => {
    const s = setup(); s.finish(); setApiKey('generated-new-key'); s.fixture.detectChanges();
    expect(s.component.imageLink()).toBe(''); expect(s.revoke).toHaveBeenCalledWith('blob:generated-one'); expect(s.metadata.length).toBe(1);
    s.confirmAccess(); expect(s.metadata.length).toBe(2);
    s.finish(); globalThis.dispatchEvent(new StorageEvent('storage', { key: ApiKeyStorageKey })); s.fixture.detectChanges();
    expect(s.component.imageLink()).toBe(''); expect(s.revoke).toHaveBeenCalledWith('blob:generated-two'); expect(s.metadata.length).toBe(2);
    s.confirmAccess(); expect(s.metadata.length).toBe(3);
  });

  it('keeps failed or changed previews empty and cancels pending work on destroy', () => {
    const s = setup(); s.metadata[0].reply.next(displayFixture()); s.images[0].reply.error({ status: 412 }); s.fixture.detectChanges();
    expect(s.component.imageLink()).toBe(''); expect(s.component.captureStatus()).toContain('changed');
    s.component.retryPreview(); s.fixture.detectChanges(); s.finish(); s.component.onPreviewImageFailed();
    expect(s.component.imageLink()).toBe(''); expect(s.component.captureDisplay()).toBeNull(); expect(s.revoke).toHaveBeenCalledWith('blob:generated-one');
    s.component.retryPreview(); s.fixture.detectChanges(); const pending = s.metadata[s.metadata.length - 1].reply;
    s.fixture.destroy(); expect(pending.observed).toBeFalse();
  });

  it('does not issue an image request for invalid metadata or bypass a pending geometry draft', () => {
    const s = setup(); s.metadata[0].reply.next(displayFixture('col-other')); s.fixture.detectChanges();
    expect(s.images.length).toBe(0); expect(s.component.imageLink()).toBe('');
    s.fixture.debugElement.query(By.directive(CaptureLifecycleGeometryProbe)).componentInstance.captureSwitchBlocked.emit(true); s.switchTo(1); expect(s.component.captureIndex()).toBe(0);
  });

  it('preserves remote direct view only after fresh project authorization and without credential-bearing HTTP image requests', () => {
    const s = setup(true); expect(s.component.imageLink()).toBe(''); expect(s.metadata.length).toBe(0);
    s.fresh[0].next(s.payload); s.fixture.detectChanges(); expect(s.component.imageLink()).toBe('https://public.invalid/first.jpg');
    expect(s.api.captureDisplayImage).not.toHaveBeenCalled(); expect(s.create).not.toHaveBeenCalled(); expect(s.component.captureDisplay()).toBeNull();
    setApiKey('generated-other-key'); s.fixture.detectChanges(); expect(s.component.imageLink()).toBe('');
    s.confirmAccess();
    s.fresh[s.fresh.length - 1].error({ status: 403 }); s.fixture.detectChanges(); expect(s.component.imageLink()).toBe('');
  });
});
