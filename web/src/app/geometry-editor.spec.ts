import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { ApiClient, GeometryLine } from './api-client';
import { GeometryEditor } from './geometry-editor';
import { prepareGeometryDisplay, markGeometryImageLoaded } from './capture-display.fixture';
import { provideWorkspaceTestAccess } from './workspace-testing.fixture.spec';

describe('GeometryEditor', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GeometryEditor],
      providers: [provideWorkspaceTestAccess({ projectId: 'col1' }), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('serializes polygon and baseline coordinates in image-frame units', () => {
    const fixture = TestBed.createComponent(GeometryEditor);
    const component = fixture.componentInstance as unknown as {
      polygonPoints(line: { polygon: { x: number; y: number }[] }): string;
      baselinePoints(line: { baseline: { x: number; y: number }[] }): string;
    };
    expect(component.polygonPoints({ polygon: [{ x: 10.5, y: 20 }, { x: 30, y: 40.25 }] }))
      .toBe('10.5,20 30,40.25');
    expect(component.baselinePoints({ baseline: [{ x: 1, y: 2 }, { x: 3, y: 4 }] }))
      .toBe('1,2 3,4');
  });

  it('renders without project data (offline-safe)', () => {
    const fixture = TestBed.createComponent(GeometryEditor);
    fixture.componentRef.setInput('projectId', 'col-offline');
    fixture.componentRef.setInput('surfaceId', 'surf-offline');
    fixture.componentRef.setInput('imageUrl', '');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('h3')?.textContent).toContain('Geometry');
  });
});


describe('GeometryEditor (undo)', () => {
  it('restores the snapshot pushed before an edit', () => {
    const line = { id: 'ln1', regionId: 'reg1', frameRef: 'cap1', polygon: [{ x: 10, y: 10 }, { x: 20, y: 10 }, { x: 20, y: 20 }, { x: 10, y: 20 }], baseline: [{ x: 10, y: 19 }, { x: 20, y: 19 }] };
    const stub = {
      loadProjectFresh: () => of({
        id: 'col1', name: 'p',
        objects: [{ id: 'obj1', surfaces: [{ id: 'surf1', kind: 'page', lines: [line], regions: [{ id: 'reg1', frameRef: 'cap1', readingOrderIndex: 0 }], captures: [] }] }],
      }),
      updateLineGeometry: () => of({}),
      updateCaptureReadingOrder: () => of({}),
      updateCaptureLineOrder: () => of({}),
      geometryHistory: () => of([]),
      unplacedLines: () => of([]),
    };
    TestBed.configureTestingModule({
      imports: [GeometryEditor],
      providers: [provideWorkspaceTestAccess({ projectId: 'col1' }), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(GeometryEditor);
    fixture.componentRef.setInput('projectId', 'col1');
    fixture.componentRef.setInput('surfaceId', 'surf1');
    prepareGeometryDisplay(fixture);
    fixture.detectChanges();
    markGeometryImageLoaded(fixture);

    const anyComponent = fixture.componentInstance as unknown as {
      lines: { (): typeof line[]; set: (value: typeof line[]) => void };
      history: { set: (value: typeof line[][]) => void };
      undoGeometry: () => void;
      dirty: () => boolean;
    };
    const moved = { ...line, polygon: line.polygon.map(p => ({ x: p.x + 5, y: p.y })) };
    anyComponent.lines.set([moved]);
    anyComponent.history.set([[line]]);
    anyComponent.undoGeometry();

    expect(anyComponent.lines()[0].polygon[0]).toEqual({ x: 10, y: 10 });
    expect(anyComponent.dirty()).toBe(false);
  });
});


describe('GeometryEditor (read-only view)', () => {
  it('emits the line id on click and does not start a drag', () => {
    const line = { id: 'ln1', regionId: 'reg1', frameRef: 'cap1', polygon: [{ x: 10, y: 10 }, { x: 20, y: 10 }, { x: 20, y: 20 }, { x: 10, y: 20 }], baseline: [{ x: 10, y: 19 }, { x: 20, y: 19 }] };
    const stub = {
      loadProjectFresh: () => of({
        id: 'col1', name: 'p',
        objects: [{ id: 'obj1', surfaces: [{ id: 'surf1', kind: 'page', lines: [line], regions: [{ id: 'reg1', frameRef: 'cap1', readingOrderIndex: 0 }], captures: [] }] }],
      }),
      geometryHistory: () => of([]),
      unplacedLines: () => of([]),
    };
    TestBed.configureTestingModule({
      imports: [GeometryEditor],
      providers: [provideWorkspaceTestAccess({ projectId: 'col1' }), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(GeometryEditor);
    fixture.componentRef.setInput('projectId', 'col1');
    fixture.componentRef.setInput('surfaceId', 'surf1');
    prepareGeometryDisplay(fixture);
    fixture.componentRef.setInput('readOnly', true);
    fixture.detectChanges();
    markGeometryImageLoaded(fixture);

    const selected: string[] = [];
    fixture.componentInstance.lineSelected.subscribe(id => selected.push(id));
    const polygon = fixture.nativeElement.querySelector('polygon') as SVGPolygonElement;
    polygon.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }));

    expect(selected).toEqual(['ln1']);
    expect(fixture.nativeElement.querySelector('.handle')).toBeNull(); // no resize handles in view mode
  });
});

describe('GeometryEditor guarded geometry saves', () => {
  const first: GeometryLine = {
    id: 'ln1', regionId: 'reg1', frameRef: 'cap1',
    polygon: [{ x: 10, y: 10 }, { x: 20, y: 10 }, { x: 20, y: 20 }, { x: 10, y: 20 }],
    baseline: [{ x: 10, y: 19 }, { x: 20, y: 19 }],
  };
  const second: GeometryLine = { ...first, id: 'ln2' };
  const points = (line: GeometryLine) => ({ polygon: line.polygon, baseline: line.baseline });
  const moved = (line: GeometryLine, dx: number) => ({
    ...line,
    polygon: line.polygon.map(p => ({ x: p.x + dx, y: p.y })),
    baseline: line.baseline.map(p => ({ x: p.x + dx, y: p.y })),
  });

  async function setup(initial: GeometryLine[] = [first, second], regions: { id: string; readingOrderIndex: number | null }[] = [{ id: 'reg1', readingOrderIndex: 0 }]) {
    TestBed.configureTestingModule({
      imports: [GeometryEditor],
      providers: [provideWorkspaceTestAccess({ projectId: 'col1' }), provideHttpClient(), provideHttpClientTesting()],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(GeometryEditor);
    const component = fixture.componentInstance as unknown as {
      lines: { (): GeometryLine[]; set(value: GeometryLine[]): void };
      selectedId: { set(value: string): void };
      dirty: () => boolean;
      actionStatus: () => string;
      saveGeometry(): void;
      splitSelected(): void;
      deleteSelected(): void;
      mergeSelectedWithNext(): void;
      saveOrder(): void;
      saveLineOrder(): void;
      move(regionId: string, delta: number): void;
      moveLine(lineId: string, delta: number): void;
      lineOrderDirty(): boolean;
      regions: { (): { id: string; readingOrderIndex: number | null }[]; set(value: { id: string; readingOrderIndex: number | null }[]): void };
      orderDirty(): boolean;
      selectUnplaced(lineId: string): void;
      createLine(rect: { x: number; y: number; width: number; height: number }): void;
      width: { set(value: number): void };
      height: { set(value: number): void };
      onDown(event: PointerEvent, line: GeometryLine): void;
      onResizeDown(event: PointerEvent, line: GeometryLine, corner: number): void;
      onMove(event: PointerEvent): void;
      onUp(): void;
    };
    async function load(projectId: string, surfaceId: string, lines: GeometryLine[]) {
      fixture.componentRef.setInput('projectId', projectId);
      fixture.componentRef.setInput('surfaceId', surfaceId);
      prepareGeometryDisplay(fixture, projectId, surfaceId);
      fixture.detectChanges();
      if (projectId !== 'col1') { http.expectNone(`/api/v1/projects/${projectId}`); return; }
      http.expectOne(`/api/v1/projects/${projectId}`).flush({
        id: projectId, name: 'test',
        objects: [{ id: 'obj1', surfaces: [{
          id: surfaceId, kind: 'page', lines, regions: regions.map(region => ({ ...region, frameRef: 'cap1' })), captures: [{ id: 'cap1' }],
        }] }],
      });
      http.expectOne(`/api/v1/surfaces/${surfaceId}/geometry-history`).flush([]);
      http.expectOne(`/api/v1/surfaces/${surfaceId}/unplaced-lines`).flush([]);
      markGeometryImageLoaded(fixture);
    }
    await load('col1', 'surf1', initial);
    return { fixture, component, http, load };
  }

  it('submits the exact loaded base and preserves the draft on 409', async () => {
    const { component, http } = await setup();
    const draft = moved(first, 5);
    component.lines.set([draft, second]);
    component.selectedId.set(first.id);
    component.saveGeometry();

    const request = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/lines/ln1/geometry');
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('If-Match')).toBe('\"' + 'b'.repeat(64) + '\"');
    expect(request.request.body).toEqual({ ...points(draft), expectedGeometry: points(first) });
    request.flush({ code: 'revisionConflict' }, { status: 409, statusText: 'Conflict' });

    expect(component.lines()[0]).toEqual(draft);
    expect(component.dirty()).toBe(true);
    expect(component.actionStatus()).toContain('draft');
    http.verify();
  });

  it('acknowledges only the submitted line state and keeps concurrent local drafts', async () => {
    const { component, http } = await setup();
    const submitted = moved(first, 5);
    const later = moved(first, 8);
    const other = moved(second, 3);
    component.lines.set([submitted, other]);
    component.selectedId.set(first.id);
    component.saveGeometry();
    const firstSave = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/lines/ln1/geometry');

    component.lines.set([later, other]);
    component.selectedId.set(second.id);
    firstSave.flush({ lineId: first.id, ...points(submitted) });

    expect(component.lines()).toEqual([later, other]);
    expect(component.dirty()).toBe(true);
    component.saveGeometry();
    const secondSave = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/lines/ln2/geometry');
    expect(secondSave.request.body.expectedGeometry).toEqual(points(second));
    secondSave.flush({ lineId: second.id, ...points(other) });

    expect(component.dirty()).toBe(true);
    component.selectedId.set(first.id);
    component.saveGeometry();
    const retry = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/lines/ln1/geometry');
    expect(retry.request.body.expectedGeometry).toEqual(points(submitted));
    retry.flush({ lineId: first.id, ...points(later) });
    expect(component.dirty()).toBe(false);
    http.verify();
  });

  it('quarantines a pending save on attempted scope change and retains the old draft', async () => {
    const { component, http, load } = await setup();
    const submitted = moved(first, 5);
    component.lines.set([submitted, second]); component.selectedId.set(first.id); component.saveGeometry();
    const delayed = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/lines/ln1/geometry');
    await load('col2', 'surf2', [second]);
    expect(component.lines()).toEqual([submitted, second]);
    expect(component.dirty()).toBeTrue();
    component.saveGeometry(); http.expectNone('/api/v1/projects/col2');
    delayed.flush({ lineId: first.id, ...points(submitted) });
    expect(component.lines()).toEqual([submitted, second]);
    expect(component.dirty()).toBeTrue();
    http.verify();
  });

  it('allows clean navigation to another surface within the same authorized project', async () => {
    const { component, http, load } = await setup();
    await load('col1', 'surf-clean', [second]);
    expect(component.lines()).toEqual([second]);
    expect(component.dirty()).toBeFalse();
    http.verify();
  });

  it('keeps a drawn box when another editor already placed the line', async () => {
    const unplaced: GeometryLine = { ...first, polygon: [], baseline: [] };
    const { component, http } = await setup([unplaced]);
    component.selectUnplaced(first.id);
    component.createLine({ x: 12, y: 15, width: 10, height: 10 });

    const request = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/lines/ln1/geometry');
    expect(request.request.body.expectedGeometry).toEqual(points(unplaced));
    request.flush({ code: 'revisionConflict' }, { status: 409, statusText: 'Conflict' });

    expect(component.lines()[0].polygon[0]).toEqual({ x: 12, y: 15 });
    expect(component.dirty()).toBe(true);
    expect(component.actionStatus()).toContain('draft');
    http.verify();
  });

  for (const action of ['add', 'split', 'merge', 'delete'] as const) {
    it(`retains a pending ${action} packet and old frame after attempted scope change`, async () => {
      const { component, http, load } = await setup();
      component.selectedId.set(first.id);
      const requestUrl = action === 'add' ? '/api/v1/projects/col1/surfaces/surf1/captures/cap1/lines'
        : action === 'split' ? '/api/v1/lines/ln1/split'
        : action === 'merge' ? '/api/v1/lines/ln1/merge' : '/api/v1/lines/ln1';
      if (action === 'add') component.createLine({ x: 1, y: 2, width: 10, height: 10 });
      if (action === 'split') component.splitSelected();
      if (action === 'merge') component.mergeSelectedWithNext();
      if (action === 'delete') component.deleteSelected();
      const delayed = http.expectOne(requestUrl);

      await load('col2', 'surf2', [first, second]);
      const retained = component.lines();
      const pending = (component as unknown as { pendingStructureWrite: unknown }).pendingStructureWrite;
      delayed.flush({ lineId: first.id });

      http.expectNone('/api/v1/projects/col2');
      expect(component.lines()).toEqual(retained);
      expect((component as unknown as { pendingStructureWrite: unknown }).pendingStructureWrite).toEqual(pending);
      expect(pending).not.toBeNull();
      expect((component as unknown as { changingStructure(): boolean }).changingStructure()).toBeTrue();
      component.deleteSelected(); http.expectNone('/api/v1/lines/ln1');
      http.verify();
    });
  }

  it('keeps line-order edits made while a save is pending', async () => {
    const { component, http } = await setup();
    component.moveLine(first.id, 1);
    component.saveLineOrder();
    const save = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/regions/reg1/line-order');
    expect(save.request.body.lineIds).toEqual([second.id, first.id]);
    expect(save.request.body.expectedRegionLineIds).toEqual([first.id, second.id]);
    component.moveLine(first.id, -1);
    save.flush({});

    expect(component.lineOrderDirty()).toBe(true);
    component.saveLineOrder();
    const next = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/regions/reg1/line-order');
    expect(next.request.body.lineIds).toEqual([first.id, second.id]);
    expect(next.request.body.expectedRegionLineIds).toEqual([second.id, first.id]);
    next.flush({});
    expect(component.lineOrderDirty()).toBe(false);
    http.verify();
  });

  it('keeps region-order edits made while a save is pending', async () => {
    const { component, http } = await setup([first, second], [{ id: 'reg1', readingOrderIndex: 0 }, { id: 'reg2', readingOrderIndex: 1 }]);
    component.move('reg1', 1);
    component.saveOrder();
    const save = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/reading-order');
    expect(save.request.body.regionIds).toEqual(['reg2', 'reg1']);
    expect(save.request.body.expectedSurfaceRegionIds).toEqual(['reg1', 'reg2']);
    component.move('reg1', -1);
    save.flush({});

    expect(component.orderDirty()).toBe(true);
    component.saveOrder();
    const next = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/reading-order');
    expect(next.request.body.regionIds).toEqual(['reg1', 'reg2']);
    expect(next.request.body.expectedSurfaceRegionIds).toEqual(['reg2', 'reg1']);
    next.flush({});
    expect(component.orderDirty()).toBe(false);
    http.verify();
  });

  it('retains a stale line-order draft and its original snapshot after HTTP409', async () => {
    const { component, http } = await setup();
    component.moveLine(first.id, 1); component.saveLineOrder();
    const save = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/regions/reg1/line-order');
    expect(save.request.body.expectedRegionLineIds).toEqual([first.id, second.id]);
    save.flush({}, { status: 409, statusText: 'Conflict' });
    expect(component.lineOrderDirty()).toBeTrue();
    expect(component.actionStatus()).toContain('draft is retained; reload and reconcile');
    component.saveLineOrder();
    const retry = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/regions/reg1/line-order');
    expect(retry.request.body.expectedRegionLineIds).toEqual([first.id, second.id]);
    expect(retry.request.body.lineIds).toEqual([second.id, first.id]);
    retry.flush({}); http.verify();
  });

  it('retains a stale region-order draft and its original snapshot after HTTP409', async () => {
    const { component, http } = await setup([first, second], [{ id: 'reg1', readingOrderIndex: 0 }, { id: 'reg2', readingOrderIndex: 1 }]);
    component.move('reg1', 1); component.saveOrder();
    const save = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/reading-order');
    expect(save.request.body.expectedSurfaceRegionIds).toEqual(['reg1', 'reg2']);
    save.flush({}, { status: 409, statusText: 'Conflict' });
    expect(component.orderDirty()).toBeTrue();
    expect(component.actionStatus()).toContain('draft is retained; reload and reconcile');
    component.saveOrder();
    const retry = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/reading-order');
    expect(retry.request.body.expectedSurfaceRegionIds).toEqual(['reg1', 'reg2']);
    expect(retry.request.body.regionIds).toEqual(['reg2', 'reg1']);
    retry.flush({}); http.verify();
  });

  it('uses null-first region order and stable native ties in the expected snapshot', async () => {
    const { component, http } = await setup([first, second], [
      { id: 'reg1', readingOrderIndex: 0 },
      { id: 'reg-unset', readingOrderIndex: null },
      { id: 'reg-tied', readingOrderIndex: 0 },
    ]);
    expect(component.regions().map(region => region.id)).toEqual(['reg-unset', 'reg1', 'reg-tied']);
    component.move('reg1', 1); component.saveOrder();
    const save = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/reading-order');
    expect(save.request.body.expectedSurfaceRegionIds).toEqual(['reg-unset', 'reg1', 'reg-tied']);
    expect(save.request.body.regionIds).toEqual(['reg-unset', 'reg-tied', 'reg1']);
    save.flush({}); http.verify();
  });

  it('keeps line-order writes attached to their loaded region after region reordering', async () => {
    const { component, http } = await setup([first, second], [
      { id: 'reg1', readingOrderIndex: 0 }, { id: 'reg2', readingOrderIndex: 1 },
    ]);
    component.move('reg1', 1);
    component.saveOrder();
    http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/reading-order').flush({});

    component.moveLine(first.id, 1);
    component.saveLineOrder();

    const order = http.expectOne('/api/v1/projects/col1/surfaces/surf1/captures/cap1/regions/reg1/line-order');
    expect(order.request.body.lineIds).toEqual([second.id, first.id]);
    order.flush({});
    http.verify();
  });

  it('requires saving or resetting local geometry before a structural edit', async () => {
    const { component, http } = await setup();
    const draft = moved(first, 5);
    component.lines.set([draft, second]);
    component.selectedId.set(first.id);

    component.deleteSelected();

    http.expectNone('/api/v1/lines/ln1');
    expect(component.lines()[0]).toEqual(draft);
    expect(component.actionStatus()).toContain('draft');
    http.verify();
  });

  it('holds geometry edits while a structural request is pending and releases them on failure', async () => {
    const { component, http } = await setup();
    component.width.set(100);
    component.height.set(100);
    component.selectedId.set(first.id);
    component.deleteSelected();
    const deletion = http.expectOne('/api/v1/lines/ln1');

    component.onDown(pointer(10, 10), first);
    component.onMove(pointer(15, 10));
    component.onUp();
    expect(component.lines()[0]).toEqual(first);
    deletion.flush({}, { status: 409, statusText: 'Conflict' });

    component.onDown(pointer(10, 10), first);
    component.onMove(pointer(15, 10));
    component.onUp();
    expect(component.lines()[0]).toEqual(moved(first, 5));
    http.verify();
  });

  function pointer(x: number, y: number): PointerEvent {
    const target = { closest: () => ({ getBoundingClientRect: () => ({ left: 0, top: 0, width: 100, height: 100 }) }) };
    return { target, clientX: x, clientY: y, stopPropagation() {} } as unknown as PointerEvent;
  }

  it('resizes all four handles while leaving their opposite corners fixed', async () => {
    const { component, http } = await setup();
    component.width.set(100);
    component.height.set(100);
    const cases = [
      { end: [4, 3], bounds: [4, 3, 20, 20] },
      { end: [26, 3], bounds: [10, 3, 26, 20] },
      { end: [26, 27], bounds: [10, 10, 26, 27] },
      { end: [4, 27], bounds: [4, 10, 20, 27] },
    ];
    cases.forEach(({ end, bounds }, corner) => {
      component.lines.set([first, second]);
      const start = first.polygon[corner];
      component.onResizeDown(pointer(start.x, start.y), first, corner);
      component.onMove(pointer(end[0], end[1]));
      component.onUp();

      const [left, top, right, bottom] = bounds;
      const resized = component.lines()[0];
      expect(resized.polygon).withContext(`corner ${corner}`).toEqual([
        { x: left, y: top }, { x: right, y: top }, { x: right, y: bottom }, { x: left, y: bottom },
      ]);
      expect(resized.baseline).toEqual([{ x: left, y: bottom }, { x: right, y: bottom }]);
    });
    http.verify();
  });

  it('clamps resize at the image edges and before crossing the opposite corner', async () => {
    const { component, http } = await setup();
    component.width.set(100);
    component.height.set(100);
    component.onResizeDown(pointer(10, 10), first, 0);
    component.onMove(pointer(-20, 50));
    component.onUp();

    expect(component.lines()[0].polygon).toEqual([
      { x: 0, y: 16 }, { x: 20, y: 16 }, { x: 20, y: 20 }, { x: 0, y: 20 },
    ]);
    http.verify();
  });
});
