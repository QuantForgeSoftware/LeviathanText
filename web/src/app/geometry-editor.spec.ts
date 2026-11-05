import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { ApiClient, GeometryLine } from './api-client';
import { GeometryEditor } from './geometry-editor';

describe('GeometryEditor', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GeometryEditor],
      providers: [provideHttpClient(), provideHttpClientTesting()],
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
    const line = { id: 'ln1', regionId: 'reg1', polygon: [{ x: 10, y: 10 }, { x: 20, y: 10 }, { x: 20, y: 20 }, { x: 10, y: 20 }], baseline: [{ x: 10, y: 19 }, { x: 20, y: 19 }] };
    const stub = {
      loadProject: () => of({
        id: 'col1', name: 'p',
        objects: [{ id: 'obj1', surfaces: [{ id: 'surf1', kind: 'page', lines: [line], regions: [{ id: 'reg1', readingOrderIndex: 0 }], captures: [] }] }],
      }),
      updateLineGeometry: () => of({}),
      updateReadingOrder: () => of({}),
      updateLineOrder: () => of({}),
      geometryHistory: () => of([]),
      unplacedLines: () => of([]),
    };
    TestBed.configureTestingModule({
      imports: [GeometryEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(GeometryEditor);
    fixture.componentRef.setInput('projectId', 'col1');
    fixture.componentRef.setInput('surfaceId', 'surf1');
    fixture.componentRef.setInput('imageUrl', '');
    fixture.detectChanges();

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
    const line = { id: 'ln1', regionId: 'reg1', polygon: [{ x: 10, y: 10 }, { x: 20, y: 10 }, { x: 20, y: 20 }, { x: 10, y: 20 }], baseline: [{ x: 10, y: 19 }, { x: 20, y: 19 }] };
    const stub = {
      loadProject: () => of({
        id: 'col1', name: 'p',
        objects: [{ id: 'obj1', surfaces: [{ id: 'surf1', kind: 'page', lines: [line], regions: [{ id: 'reg1', readingOrderIndex: 0 }], captures: [] }] }],
      }),
      geometryHistory: () => of([]),
      unplacedLines: () => of([]),
    };
    TestBed.configureTestingModule({
      imports: [GeometryEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(GeometryEditor);
    fixture.componentRef.setInput('projectId', 'col1');
    fixture.componentRef.setInput('surfaceId', 'surf1');
    fixture.componentRef.setInput('imageUrl', '');
    fixture.componentRef.setInput('readOnly', true);
    fixture.detectChanges();

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
    id: 'ln1', regionId: 'reg1',
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

  function setup(initial: GeometryLine[] = [first, second], regions = [{ id: 'reg1', readingOrderIndex: 0 }]) {
    TestBed.configureTestingModule({
      imports: [GeometryEditor],
      providers: [provideHttpClient(), provideHttpClientTesting()],
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
      regions: { (): { id: string; readingOrderIndex: number }[]; set(value: { id: string; readingOrderIndex: number }[]): void };
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
    function load(projectId: string, surfaceId: string, lines: GeometryLine[]) {
      fixture.componentRef.setInput('projectId', projectId);
      fixture.componentRef.setInput('surfaceId', surfaceId);
      fixture.componentRef.setInput('imageUrl', '');
      fixture.detectChanges();
      http.expectOne(`/api/v1/projects/${projectId}`).flush({
        id: projectId, name: 'test',
        objects: [{ id: 'obj1', surfaces: [{
          id: surfaceId, kind: 'page', lines, regions, captures: [],
        }] }],
      });
      http.expectOne(`/api/v1/surfaces/${surfaceId}/geometry-history`).flush([]);
      http.expectOne(`/api/v1/surfaces/${surfaceId}/unplaced-lines`).flush([]);
    }
    load('col1', 'surf1', initial);
    return { fixture, component, http, load };
  }

  it('submits the exact loaded base and preserves the draft on 409', () => {
    const { component, http } = setup();
    const draft = moved(first, 5);
    component.lines.set([draft, second]);
    component.selectedId.set(first.id);
    component.saveGeometry();

    const request = http.expectOne('/api/v1/lines/ln1/geometry');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ ...points(draft), expectedGeometry: points(first) });
    request.flush({ code: 'revisionConflict' }, { status: 409, statusText: 'Conflict' });

    expect(component.lines()[0]).toEqual(draft);
    expect(component.dirty()).toBe(true);
    expect(component.actionStatus()).toContain('draft');
    http.verify();
  });

  it('acknowledges only the submitted line state and keeps concurrent local drafts', () => {
    const { component, http } = setup();
    const submitted = moved(first, 5);
    const later = moved(first, 8);
    const other = moved(second, 3);
    component.lines.set([submitted, other]);
    component.selectedId.set(first.id);
    component.saveGeometry();
    const firstSave = http.expectOne('/api/v1/lines/ln1/geometry');

    component.lines.set([later, other]);
    component.selectedId.set(second.id);
    firstSave.flush({ lineId: first.id, ...points(submitted) });

    expect(component.lines()).toEqual([later, other]);
    expect(component.dirty()).toBe(true);
    component.saveGeometry();
    const secondSave = http.expectOne('/api/v1/lines/ln2/geometry');
    expect(secondSave.request.body.expectedGeometry).toEqual(points(second));
    secondSave.flush({ lineId: second.id, ...points(other) });

    expect(component.dirty()).toBe(true);
    component.selectedId.set(first.id);
    component.saveGeometry();
    const retry = http.expectOne('/api/v1/lines/ln1/geometry');
    expect(retry.request.body.expectedGeometry).toEqual(points(submitted));
    retry.flush({ lineId: first.id, ...points(later) });
    expect(component.dirty()).toBe(false);
    http.verify();
  });

  it('ignores a save reply after leaving and returning to the same surface', () => {
    const { component, http, load } = setup();
    const submitted = moved(first, 5);
    component.lines.set([submitted, second]);
    component.selectedId.set(first.id);
    component.saveGeometry();
    const delayed = http.expectOne('/api/v1/lines/ln1/geometry');

    load('col2', 'surf2', [second]);
    const reloaded = moved(first, 20);
    load('col1', 'surf1', [reloaded, second]);
    delayed.flush({ lineId: first.id, ...points(submitted) });

    expect(component.lines()).toEqual([reloaded, second]);
    expect(component.dirty()).toBe(false);
    expect(component.actionStatus()).toBe('');
    http.verify();
  });

  it('keeps a drawn box when another editor already placed the line', () => {
    const unplaced: GeometryLine = { ...first, polygon: [], baseline: [] };
    const { component, http } = setup([unplaced]);
    component.selectUnplaced(first.id);
    component.createLine({ x: 12, y: 15, width: 10, height: 10 });

    const request = http.expectOne('/api/v1/lines/ln1/geometry');
    expect(request.request.body.expectedGeometry).toEqual(points(unplaced));
    request.flush({ code: 'revisionConflict' }, { status: 409, statusText: 'Conflict' });

    expect(component.lines()[0].polygon[0]).toEqual({ x: 12, y: 15 });
    expect(component.dirty()).toBe(true);
    expect(component.actionStatus()).toContain('draft');
    http.verify();
  });

  for (const action of ['add', 'split', 'merge', 'delete'] as const) {
    it(`ignores a delayed ${action} response after changing surfaces`, () => {
      const { component, http, load } = setup();
      component.selectedId.set(first.id);
      const requestUrl = action === 'add' ? '/api/v1/surfaces/surf1/lines'
        : action === 'split' ? '/api/v1/lines/ln1/split'
        : action === 'merge' ? '/api/v1/lines/ln1/merge' : '/api/v1/lines/ln1';
      if (action === 'add') component.createLine({ x: 1, y: 2, width: 10, height: 10 });
      if (action === 'split') component.splitSelected();
      if (action === 'merge') component.mergeSelectedWithNext();
      if (action === 'delete') component.deleteSelected();
      const delayed = http.expectOne(requestUrl);

      load('col2', 'surf2', [first, second]);
      const draft = moved(first, 9);
      component.lines.set([draft, second]);
      delayed.flush({ lineId: first.id });

      http.expectNone('/api/v1/projects/col2');
      expect(component.lines()[0]).toEqual(draft);
      expect(component.dirty()).toBe(true);
      expect(component.actionStatus()).toBe('');
      http.verify();
    });
  }

  it('keeps line-order edits made while a save is pending', () => {
    const { component, http } = setup();
    component.moveLine(first.id, 1);
    component.saveLineOrder();
    const save = http.expectOne('/api/v1/regions/reg1/line-order');
    expect(save.request.body.lineIds).toEqual([second.id, first.id]);
    component.moveLine(first.id, -1);
    save.flush({});

    expect(component.lineOrderDirty()).toBe(true);
    component.saveLineOrder();
    const next = http.expectOne('/api/v1/regions/reg1/line-order');
    expect(next.request.body.lineIds).toEqual([first.id, second.id]);
    next.flush({});
    expect(component.lineOrderDirty()).toBe(false);
    http.verify();
  });

  it('keeps region-order edits made while a save is pending', () => {
    const { component, http } = setup();
    component.regions.set([{ id: 'reg1', readingOrderIndex: 0 }, { id: 'reg2', readingOrderIndex: 1 }]);
    component.move('reg1', 1);
    component.saveOrder();
    const save = http.expectOne('/api/v1/surfaces/surf1/reading-order');
    expect(save.request.body.regionIds).toEqual(['reg2', 'reg1']);
    component.move('reg1', -1);
    save.flush({});

    expect(component.orderDirty()).toBe(true);
    component.saveOrder();
    const next = http.expectOne('/api/v1/surfaces/surf1/reading-order');
    expect(next.request.body.regionIds).toEqual(['reg1', 'reg2']);
    next.flush({});
    expect(component.orderDirty()).toBe(false);
    http.verify();
  });

  it('keeps line-order writes attached to their loaded region after region reordering', () => {
    const { component, http } = setup([first, second], [
      { id: 'reg1', readingOrderIndex: 0 }, { id: 'reg2', readingOrderIndex: 1 },
    ]);
    component.move('reg1', 1);
    component.saveOrder();
    http.expectOne('/api/v1/surfaces/surf1/reading-order').flush({});

    component.moveLine(first.id, 1);
    component.saveLineOrder();

    const order = http.expectOne('/api/v1/regions/reg1/line-order');
    expect(order.request.body.lineIds).toEqual([second.id, first.id]);
    order.flush({});
    http.verify();
  });

  it('requires saving or resetting local geometry before a structural edit', () => {
    const { component, http } = setup();
    const draft = moved(first, 5);
    component.lines.set([draft, second]);
    component.selectedId.set(first.id);

    component.deleteSelected();

    http.expectNone('/api/v1/lines/ln1');
    expect(component.lines()[0]).toEqual(draft);
    expect(component.actionStatus()).toContain('draft');
    http.verify();
  });

  it('holds geometry edits while a structural request is pending and releases them on failure', () => {
    const { component, http } = setup();
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

  it('resizes all four handles while leaving their opposite corners fixed', () => {
    const { component, http } = setup();
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

  it('clamps resize at the image edges and before crossing the opposite corner', () => {
    const { component, http } = setup();
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
