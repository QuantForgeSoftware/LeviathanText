import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { ApiClient } from './api-client';
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
    expect(anyComponent.dirty()).toBe(true);
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
