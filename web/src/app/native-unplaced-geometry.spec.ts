import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient, ProjectPayload } from './api-client';
import { displayFixture, markGeometryImageLoaded } from './capture-display.fixture';
import { GeometryEditor } from './geometry-editor';
import { provideWorkspaceTestAccess } from './workspace-testing.fixture.spec';

/** Generated native absence values; no image or witness labels. */
describe('Native unplaced geometry projection', () => {
  function setup() {
    const polygon = [{ x: 10, y: 20 }, { x: 40, y: 20 }, { x: 40, y: 50 }];
    const payload: ProjectPayload = { id: 'col1', name: 'generated absence', objects: [{ id: 'obj1', surfaces: [{
      id: 'surf1', kind: 'page', captures: [{ id: 'cap1' }],
      regions: [{ id: 'reg1', frameRef: 'cap1', readingOrderIndex: 0 }, { id: 'reg-unplaced', frameRef: null, readingOrderIndex: null }],
      lines: [
        { id: 'ln-placed', regionId: 'reg1', frameRef: 'cap1', polygon, baseline: [] },
        { id: 'ln-frame-unknown', regionId: null, frameRef: null, polygon, baseline: [] },
        { id: 'ln-region-unknown', regionId: null, frameRef: 'cap1', polygon, baseline: [] },
      ],
    }] }] };
    const before = JSON.stringify(payload);
    const api = { loadProjectFresh: () => of(payload), geometryHistory: () => of([]), unplacedLines: () => of([]),
      updateCaptureLineGeometry: jasmine.createSpy('update'), addCaptureLine: jasmine.createSpy('add'),
      updateCaptureReadingOrder: jasmine.createSpy('region order'), updateCaptureLineOrder: jasmine.createSpy('line order') };
    TestBed.configureTestingModule({ imports: [GeometryEditor], providers: [{ provide: ApiClient, useValue: api }, provideWorkspaceTestAccess({ projectId: 'col1' })] });
    const fixture = TestBed.createComponent(GeometryEditor);
    fixture.componentRef.setInput('projectId', 'col1'); fixture.componentRef.setInput('surfaceId', 'surf1');
    fixture.componentRef.setInput('display', displayFixture()); fixture.componentRef.setInput('imageUrl', 'blob:generated');
    fixture.detectChanges(); markGeometryImageLoaded(fixture);
    return { fixture, state: fixture.componentInstance as any, api, payload, before };
  }
  it('does not assign a capture to unknown-frame lines or regions, even when they carry coordinates', () => {
    const s = setup();
    expect(s.state.lines().map((line: { id: string }) => line.id)).toEqual(['ln-placed', 'ln-region-unknown']);
    expect(s.state.regions().map((region: { id: string }) => region.id)).toEqual(['reg1']);
    expect(s.state.mixedFrames()).toBeTrue(); expect(JSON.stringify(s.payload)).toBe(s.before);
    expect(s.api.updateCaptureLineGeometry).not.toHaveBeenCalled(); expect(s.api.addCaptureLine).not.toHaveBeenCalled();
  });
  it('preserves explicit missing region on a known-frame line without inheriting region order or mutating native data', () => {
    const s = setup();
    const line = s.state.lines().find((value: { id: string }) => value.id === 'ln-region-unknown');
    expect(line.regionId).toBeNull(); expect(line.frameRef).toBe('cap1');
    expect(s.state.lineOrder()).toEqual(['ln-placed']); expect(JSON.stringify(s.payload)).toBe(s.before);
    expect(s.api.updateCaptureReadingOrder).not.toHaveBeenCalled(); expect(s.api.updateCaptureLineOrder).not.toHaveBeenCalled();
  });
});
