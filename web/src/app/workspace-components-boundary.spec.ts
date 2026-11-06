import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { defer, EMPTY, filter, of, Subject } from 'rxjs';
import { ApiClient } from './api-client';
import { GeometryEditor } from './geometry-editor';
import { SurfaceView } from './surface-view';
import { FragmentPanel } from './fragment-panel';
import { displayFixture, markGeometryImageLoaded } from './capture-display.fixture';
import { WORKSPACE_ACCESS } from './workspace-access-types';
import type { WorkspaceAccess, WorkspaceAccessState, WorkspaceRequestIntent } from './workspace-access-types';

const projectId = 'col-generated-auth-components', surfaceId = 'surf-generated-auth-components';
class ControlledAccess implements WorkspaceAccess {
  active = true;
  generation = 0;
  role = 'Annotator';
  listeners = new Set<(state: WorkspaceAccessState) => void>();
  state(): WorkspaceAccessState { return { status: this.active ? 'active' : 'suspended', generation: this.generation,
    reason: 'generated-control', principal: this.active ? { mode: 'account', accountId: 'generated-a',
      workspaceNamespace: '0c33d377-1f2b-4e77-830c-926b9941fbc4', role: this.role } : null }; }
  isActive(): boolean { return this.active; }
  capture(): WorkspaceRequestIntent | null { const generation = this.generation; return this.active
    ? { credential: 'generated-key-a', projectId, generation, current: () => this.active && generation === this.generation } : null; }
  subscribe(listener: (state: WorkspaceAccessState) => void): () => void { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; }
  owner() { return this.active ? { workspaceNamespace: '0c33d377-1f2b-4e77-830c-926b9941fbc4', accountId: 'generated-a' } : null; }
  projectId(): string | null { return this.active ? projectId : null; }
  retry(): void { }
  suspend(notify = true): void { this.active = false; this.generation++; if (notify) for (const listener of this.listeners) listener(this.state()); }
}
/** Explicit transport double: no hidden production fallback, API, source image or model. */
function scopedMock(api: any): any {
  api.forWorkspaceRequest = (intent: WorkspaceRequestIntent) => new Proxy(api, { get: (target, property) => {
    const value = target[property];
    return typeof value !== 'function' ? value : (...args: unknown[]) => defer(() => intent.current()
      ? value.apply(target, args).pipe(filter(() => intent.current())) : EMPTY);
  } });
  return api;
}
function project() {
  return { id: projectId, name: 'generated protected collection', objects: [{ id: 'obj-generated', surfaces: [{
    id: surfaceId, kind: 'page', captures: [], regions: [{ id: 'reg-generated', frameRef: 'cap1', readingOrderIndex: 0 }],
    lines: [{ id: 'ln-generated', regionId: 'reg-generated', frameRef: 'cap1',
      polygon: [{ x: 10, y: 10 }, { x: 20, y: 10 }, { x: 20, y: 20 }, { x: 10, y: 20 }],
      baseline: [{ x: 10, y: 19 }, { x: 20, y: 19 }] }],
  }] }] };
}
describe('Workspace component action and presence boundaries', () => {
  afterEach(() => TestBed.resetTestingModule());
  function geometry() {
    const access = new ControlledAccess(), save = new Subject<unknown>();
    const api = scopedMock({ loadProject: () => of(project()), loadProjectFresh: () => of(project()),
      geometryHistory: () => of([]), unplacedLines: () => of([]), updateCaptureLineGeometry: jasmine.createSpy().and.returnValue(save) });
    TestBed.configureTestingModule({ imports: [GeometryEditor], providers: [{ provide: ApiClient, useValue: api }, { provide: WORKSPACE_ACCESS, useValue: access }] });
    const fixture = TestBed.createComponent(GeometryEditor);
    fixture.componentRef.setInput('projectId', projectId); fixture.componentRef.setInput('surfaceId', surfaceId);
    fixture.componentRef.setInput('display', displayFixture(projectId, surfaceId)); fixture.componentRef.setInput('imageUrl', 'blob:generated-only');
    fixture.detectChanges(); markGeometryImageLoaded(fixture);
    return { access, save, api, fixture, component: fixture.componentInstance as any };
  }
  it('retains geometry/undo when a direct action observes inactive access without a signal event', () => {
    const s = geometry(), original = s.component.lines()[0];
    const moved = { ...original, polygon: original.polygon.map((p: any) => ({ x: p.x + 30, y: p.y })) };
    s.component.lines.set([moved]); s.component.history.set([[original]]);
    s.access.suspend(false); // Equivalent direct-storage fence: imperative guard must read now.
    s.component.undoGeometry();
    expect(s.component.lines()[0].polygon[0].x).toBe(40);
    expect(s.component.history().length).toBe(1);
  });
  it('does not let a late pre-boundary geometry acknowledgement clear unresolved work', () => {
    const s = geometry(), original = s.component.lines()[0];
    s.component.lines.set([{ ...original, polygon: original.polygon.map((p: any) => ({ x: p.x + 30, y: p.y })) }]);
    s.component.selectedId.set(original.id); s.component.saveGeometry();
    expect(s.api.updateCaptureLineGeometry).toHaveBeenCalledTimes(1);
    s.access.suspend(); s.save.next({});
    expect(s.component.original().get(original.id).polygon[0].x).toBe(10);
    expect(s.component.hasNavigationWork()).toBeTrue();
  });
  it('refuses fragment requests without an explicit workspace capability', () => {
    const api = scopedMock({ surfaceFragments: jasmine.createSpy().and.returnValue(of([])) });
    TestBed.configureTestingModule({ imports: [FragmentPanel], providers: [{ provide: ApiClient, useValue: api }] });
    const fixture = TestBed.createComponent(FragmentPanel); fixture.componentRef.setInput('surfaceId', surfaceId); fixture.detectChanges();
    expect(api.surfaceFragments).not.toHaveBeenCalled();
  });
  it('does not replace a mounted fragment draft through an inactive direct handler', () => {
    const access = new ControlledAccess(), api = scopedMock({ surfaceFragments: () => of([]) });
    TestBed.configureTestingModule({ imports: [FragmentPanel], providers: [{ provide: ApiClient, useValue: api }, { provide: WORKSPACE_ACCESS, useValue: access }] });
    const fixture = TestBed.createComponent(FragmentPanel); fixture.componentRef.setInput('surfaceId', surfaceId); fixture.detectChanges();
    const component = fixture.componentInstance as any;
    component.onTm({ target: { value: 'generated old-owner draft' } }); access.suspend(false);
    component.onTm({ target: { value: 'replacement from inactive owner' } });
    expect(component.tm()).toBe('generated old-owner draft');
  });
  it('restores the actual fragment input DOM when inactive edits are rejected', () => {
    const access = new ControlledAccess(), api = scopedMock({ surfaceFragments: () => of([]) });
    TestBed.configureTestingModule({ imports: [FragmentPanel], providers: [{ provide: ApiClient, useValue: api }, { provide: WORKSPACE_ACCESS, useValue: access }] });
    const fixture = TestBed.createComponent(FragmentPanel); fixture.componentRef.setInput('surfaceId', surfaceId); fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'retained TM draft'; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    access.suspend(false); input.value = 'rejected owner text'; input.dispatchEvent(new Event('input'));
    expect(input.value).toBe('retained TM draft');
    access.active = true; fixture.detectChanges(); expect(input.value).toBe('retained TM draft');
  });
  it('retains geometry under a forced surface ABA and never dispatches into the changed scope', () => {
    const s = geometry(), original = s.component.lines()[0];
    s.component.lines.set([{ ...original, polygon: original.polygon.map((p: any) => ({ x: p.x + 30, y: p.y })) }]);
    s.fixture.componentRef.setInput('surfaceId', 'surf-other-generated'); s.fixture.detectChanges();
    s.fixture.componentRef.setInput('surfaceId', surfaceId); s.fixture.detectChanges();
    s.component.selectedId.set(original.id); s.component.saveGeometry();
    expect(s.component.inputQuarantined()).toBeTrue();
    expect(s.component.lines()[0].polygon[0].x).toBe(40);
    expect(s.component.hasNavigationWork()).toBeTrue(); expect(s.api.updateCaptureLineGeometry).not.toHaveBeenCalled();
  });
  it('never advertises retry for an unconfirmed structural submission', () => {
    const s = geometry(), display = s.component.display();
    s.component.newLineDraft.set({ display, projectId, surfaceId, polygon: [{x:30,y:30},{x:60,y:30},{x:60,y:60},{x:30,y:60}], baseline: [{x:30,y:59},{x:60,y:59}], expectedLineIds: ['ln-generated'], expectedRegionIds: ['reg-generated'] });
    expect(s.component.canRetryNewLine()).toBeTrue(); s.component.uncertainStructure.set(true);
    expect(s.component.canRetryNewLine()).toBeFalse(); s.component.retryNewLine();
    expect(s.component.newLineDraft()).not.toBeNull(); expect(s.api.updateCaptureLineGeometry).not.toHaveBeenCalled();
  });
  function surface(access: ControlledAccess) {
    const create = new Subject<any>();
    const api = scopedMock({ createSession: jasmine.createSpy().and.returnValue(create), touchPresence: jasmine.createSpy().and.returnValue(of({ editors: [] })),
      leavePresence: () => of({}), revokeSession: () => of({}), sessions: () => of([]), surfaceFragments: () => of([]),
      diplomaticExport: () => of({ text: 'generated secret source', lossReport: { format: 'text', contractVersion: 'fixture', entries: [] } }),
      reviewQueue: () => of([]), models: () => of([]), profileReport: () => of({ profile: 'fixture', version: '1', surfaces: [] }),
      loadProject: () => of(project()), loadProjectFresh: () => of(project()) });
    TestBed.configureTestingModule({ imports: [SurfaceView], providers: [{ provide: ApiClient, useValue: api }, { provide: WORKSPACE_ACCESS, useValue: access }] });
    const fixture = TestBed.createComponent(SurfaceView); fixture.componentRef.setInput('projectId', projectId); fixture.componentRef.setInput('surfaceId', surfaceId); fixture.detectChanges();
    return { fixture, api, create };
  }
  it('stops background presence and rejects the old session reply after suspension', fakeAsync(() => {
    const access = new ControlledAccess(), s = surface(access); access.suspend();
    s.create.next({ id: 'ses-from-old-owner', nodeId: 'generated-node' }); tick(20_001);
    expect(s.api.touchPresence).not.toHaveBeenCalled(); s.fixture.destroy();
  }));
  it('does not POST editing sessions for a reader just to render a surface', () => {
    const access = new ControlledAccess(); access.role = 'Reader'; const s = surface(access);
    expect(s.api.createSession).not.toHaveBeenCalled(); s.fixture.destroy();
  });
});
