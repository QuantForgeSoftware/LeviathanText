import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiClient, CaptureDisplayDescriptor } from './api-client';
import { GeometryEditor } from './geometry-editor';
import { displayFixture } from './capture-display.fixture';
import { provideWorkspaceTestAccess } from './workspace-testing.fixture.spec';

describe('capture-bound existing geometry source context', () => {
  function setup(twoLines = false, twoRegions = false) {
    const original = { id: 'ln-one', regionId: 'reg-one', frameRef: 'cap1',
      polygon: [{ x: 100, y: 100 }, { x: 200, y: 100 }, { x: 200, y: 140 }, { x: 100, y: 140 }],
      baseline: [{ x: 100, y: 135 }, { x: 200, y: 135 }] };
    const payload = { id: 'col1', name: 'generated only', objects: [{ id: 'obj1', surfaces: [{ id: 'surf1', kind: 'page',
      captures: [{ id: 'cap1' }], lines: [original], regions: [{ id: 'reg-one', frameRef: 'cap1', readingOrderIndex: 0 }] }] }] };
    if (twoLines) payload.objects[0].surfaces[0].lines.push({ ...original, id: 'ln-two', polygon: original.polygon.map(p => ({ x: p.x, y: p.y + 100 })), baseline: original.baseline.map(p => ({ x: p.x, y: p.y + 100 })) });
    if (twoRegions) payload.objects[0].surfaces[0].regions.push({ id: 'reg-two', frameRef: 'cap1', readingOrderIndex: 1 });
    const save = new Subject<unknown>();
    const api = { loadProjectFresh: jasmine.createSpy('fresh load').and.callFake(() => of(payload)), loadProject: jasmine.createSpy('load').and.callFake(() => of(payload)), geometryHistory: () => of([]), unplacedLines: () => of([]),
      updateLineGeometry: jasmine.createSpy('legacy geometry').and.returnValue(save),
      updateCaptureLineGeometry: jasmine.createSpy('capture geometry').and.returnValue(save),
      addCaptureLine: jasmine.createSpy('add').and.returnValue(of({})),
      updateCaptureReadingOrder: jasmine.createSpy('regions').and.returnValue(of({})), updateCaptureLineOrder: jasmine.createSpy('lines').and.returnValue(of({})) };
    TestBed.configureTestingModule({ imports: [GeometryEditor], providers: [{ provide: ApiClient, useValue: api }, provideWorkspaceTestAccess({ projectId: 'col1' })] });
    const fixture = TestBed.createComponent(GeometryEditor);
    fixture.componentRef.setInput('projectId', 'col1'); fixture.componentRef.setInput('surfaceId', 'surf1');
    const descriptor = displayFixture('col1', 'surf1', { sourceWidth: 400, sourceHeight: 300, displayWidth: 400, displayHeight: 300, scaleX: 1, scaleY: 1 });
    const display = (value: CaptureDisplayDescriptor | null, url = 'blob:generated-source') => {
      fixture.componentRef.setInput('display', value); fixture.componentRef.setInput('imageUrl', value ? url : ''); fixture.detectChanges();
      if (value && fixture.nativeElement.querySelector('img')) {
        const image = fixture.nativeElement.querySelector('img') as HTMLImageElement;
        Object.defineProperty(image, 'naturalWidth', { configurable: true, value: value.displayWidth });
        Object.defineProperty(image, 'naturalHeight', { configurable: true, value: value.displayHeight });
        image.dispatchEvent(new Event('load')); fixture.detectChanges();
      }
    };
    display(descriptor);
    const component = fixture.componentInstance as any;
    const move = (dx: number, index = 0) => {
      const svg = fixture.nativeElement.querySelector('svg') as SVGSVGElement;
      if (!jasmine.isSpy(svg.getScreenCTM)) spyOn(svg, 'getScreenCTM').and.returnValue(new DOMMatrix());
      const pointer = (x: number, y: number) => ({ target: { closest: () => svg }, clientX: x, clientY: y }) as unknown as PointerEvent;
      component.onDown(pointer(100, 100), component.lines()[index]); component.onMove(pointer(100 + dx, 100)); component.onUp(); fixture.detectChanges();
    };
    return { fixture, component, descriptor, display, move, api, save, original, payload };
  }

  it('saves an existing draft with its capture identity and exact expected source geometry', () => {
    const s = setup(); s.move(10); s.component.saveGeometry();
    expect(s.api.updateLineGeometry).not.toHaveBeenCalled();
    expect(s.api.updateCaptureLineGeometry).toHaveBeenCalledTimes(1);
    const args = s.api.updateCaptureLineGeometry.calls.mostRecent().args;
    expect(args[0].derivativeId).toBe(s.descriptor.derivativeId); expect(args[1]).toBe('ln-one');
    expect(args[2][0]).toEqual({ x: 110, y: 100 });
    expect(args[4]).toEqual({ polygon: s.original.polygon, baseline: s.original.baseline });
  });

  it('retains old draft dimensions and blocks rebinding when same-frame source identity changes', () => {
    const s = setup(); const initialFreshReads = s.api.loadProjectFresh.calls.count(); s.move(10);
    const changed = { ...s.descriptor, derivativeId: 'e'.repeat(64), sourceWidth: 800, sourceHeight: 600, scaleX: .5, scaleY: .5 };
    s.display(changed, 'blob:generated-replaced');
    expect(s.component.width()).toBe(400); expect(s.component.height()).toBe(300);
    expect(s.component.lines()[0].polygon[0].x).toBe(110); expect(s.component.dirty()).toBeTrue(); expect(s.component.locked()).toBeTrue();
    s.component.saveGeometry(); expect(s.api.updateCaptureLineGeometry).not.toHaveBeenCalled(); expect(s.api.updateLineGeometry).not.toHaveBeenCalled();
    s.component.reset(); s.fixture.detectChanges();
    expect(s.component.width()).toBe(800); expect(s.component.height()).toBe(600); expect(s.component.dirty()).toBeFalse();
    expect(s.api.loadProjectFresh).toHaveBeenCalledTimes(initialFreshReads + 1);
  });

  it('same-identity authorization refresh preserves the draft and restores scoped saving', () => {
    const s = setup(); s.move(10); s.display(null); expect(s.component.locked()).toBeTrue();
    s.component.saveGeometry(); expect(s.api.updateCaptureLineGeometry).not.toHaveBeenCalled(); expect(s.api.updateLineGeometry).not.toHaveBeenCalled();
    s.display({ ...s.descriptor }, 'blob:generated-reauthorized');
    expect(s.component.lines()[0].polygon[0].x).toBe(110); expect(s.component.dirty()).toBeTrue();
    s.component.saveGeometry(); expect(s.api.updateCaptureLineGeometry).toHaveBeenCalledTimes(1);
    expect(s.api.updateCaptureLineGeometry.calls.mostRecent().args[0].derivativeId).toBe(s.descriptor.derivativeId);
  });

  it('an idle changed source drops old undo history before adopting its new frame dimensions', () => {
    const s = setup(); s.move(10); s.component.saveGeometry(); s.save.next({}); s.fixture.detectChanges();
    expect(s.component.dirty()).toBeFalse(); expect(s.component.history().length).toBe(1);
    s.display({ ...s.descriptor, derivativeId: 'f'.repeat(64), sourceWidth: 800, sourceHeight: 600, scaleX: .5, scaleY: .5 }, 'blob:generated-new-idle');
    expect(s.component.width()).toBe(800); expect(s.component.history()).toEqual([]);
  });

  it('keeps a no-op local draft blocked without treating equal geometry as rollback', () => {
    const s = setup(); s.move(10); s.component.saveGeometry(); s.move(-10); s.save.error({ status: 0 }); s.fixture.detectChanges();
    expect(s.component.lines()[0].polygon[0].x).toBe(100); expect(s.component.dirty()).toBeTrue();
    expect(s.component.selectedDirty()).toBeTrue(); expect(s.component.hasNavigationWork()).toBeTrue();
    s.component.saveGeometry(); expect(s.api.updateCaptureLineGeometry).toHaveBeenCalledTimes(1);
    const packet = s.component.unresolvedWrites[0];
    expect(packet.line.polygon[0].x).toBe(110); expect(packet.expected.polygon[0].x).toBe(100);
    expect(packet.display.derivativeId).toBe(s.descriptor.derivativeId);
    expect(s.component.dirty()).toBeTrue(); expect(s.component.hasNavigationWork()).toBeTrue();
  });

  it('requires a successful current read before discarding a definitively refused save', () => {
    const s = setup(); s.move(10); s.component.saveGeometry(); s.move(-10); s.save.error({ status: 409 });
    const failed = new Subject<typeof s.payload>(); s.api.loadProjectFresh.and.returnValue(failed);
    s.component.reset(); expect(s.component.discarding()).toBeTrue(); expect(s.component.hasNavigationWork()).toBeTrue();
    failed.error({ status: 403 }); s.fixture.detectChanges();
    expect(s.component.discarding()).toBeFalse(); expect(s.component.dirty()).toBeTrue(); expect(s.component.lines()[0].polygon[0].x).toBe(100);
    expect(s.component.actionStatus()).toContain('retained');
    const fresh = new Subject<typeof s.payload>(); s.api.loadProjectFresh.and.returnValue(fresh); s.component.reset();
    const serverLine = { ...s.original, polygon: s.original.polygon.map(p => ({ x: p.x + 10, y: p.y })) };
    fresh.next({ ...s.payload, objects: [{ ...s.payload.objects[0], surfaces: [{ ...s.payload.objects[0].surfaces[0], lines: [serverLine] }] }] });
    s.fixture.detectChanges(); expect(s.component.dirty()).toBeFalse(); expect(s.component.lines()[0].polygon[0].x).toBe(110);
    expect(s.component.history()).toEqual([]); expect(s.component.hasNavigationWork()).toBeFalse();
  });

  it('acknowledges the submitted geometry while retaining a newer draft in the same source context', () => {
    const s = setup(); s.move(10); s.component.saveGeometry(); s.move(-10); s.save.next({}); s.fixture.detectChanges();
    expect(s.component.lines()[0].polygon[0].x).toBe(100); expect(s.component.dirty()).toBeTrue();
    const retry = new Subject<unknown>(); s.api.updateCaptureLineGeometry.and.returnValue(retry); s.component.saveGeometry();
    const args = s.api.updateCaptureLineGeometry.calls.mostRecent().args;
    expect(args[0].derivativeId).toBe(s.descriptor.derivativeId); expect(args[4].polygon[0].x).toBe(110); expect(args[2][0].x).toBe(100);
  });

  it('does not clear another line uncertainty when a different line save succeeds', () => {
    const s = setup(true); s.move(10); s.component.saveGeometry(); s.move(-10); s.save.error({ status: 409 });
    const other = new Subject<unknown>(); s.api.updateCaptureLineGeometry.and.returnValue(other); s.move(10, 1); s.component.saveGeometry(); other.next({});
    expect(s.api.updateCaptureLineGeometry).toHaveBeenCalledTimes(2);
    expect(s.component.dirty()).toBeTrue(); expect(s.component.hasNavigationWork()).toBeTrue();
    s.component.selectedId.set('ln-one'); expect(s.component.selectedDirty()).toBeTrue();
  });

  it('does not discard on a fresh reply for a source identity that changed while the read was pending', () => {
    const s = setup(); s.move(10); s.component.saveGeometry(); s.save.error({ status: 409 });
    const fresh = new Subject<typeof s.payload>(); s.api.loadProjectFresh.and.returnValue(fresh); s.component.reset(); expect(s.component.discarding()).toBeTrue();
    s.display({ ...s.descriptor, derivativeId: 'e'.repeat(64), sourceWidth: 800, sourceHeight: 600 }, 'blob:changed-during-discard');
    fresh.next(s.payload); s.fixture.detectChanges();
    expect(s.component.dirty()).toBeTrue(); expect(s.component.width()).toBe(400); expect(s.component.discarding()).toBeFalse();
    expect(s.component.actionStatus()).toContain('retained'); expect(s.component.hasNavigationWork()).toBeTrue();
  });


  it('does not discard after a same-source authorization observation replaces the pending read context', () => {
    const s = setup(); s.move(10); s.component.saveGeometry(); s.save.error({ status: 409 });
    const fresh = new Subject<typeof s.payload>(); s.api.loadProjectFresh.and.returnValue(fresh); s.component.reset(); expect(s.component.discarding()).toBeTrue();
    s.display({ ...s.descriptor }, 'blob:equivalent-after-auth'); fresh.next(s.payload); s.fixture.detectChanges();
    expect(s.component.dirty()).toBeTrue(); expect(s.component.discarding()).toBeFalse();
    expect(s.component.lines()[0].polygon[0].x).toBe(110); expect(s.component.actionStatus()).toContain('retained');
  });


  it('ends a partial move on preview refresh so restored-source hover cannot move the old drag', () => {
    const s = setup(); const svg = { getScreenCTM: () => new DOMMatrix() };
    const pointer = (x: number, y: number) => ({ target: { closest: () => svg }, clientX: x, clientY: y }) as unknown as PointerEvent;
    s.component.onDown(pointer(100, 100), s.component.lines()[0]); s.component.onMove(pointer(110, 100));
    s.display(null); s.display({ ...s.descriptor }, 'blob:restored-after-drag');
    s.component.onMove(pointer(140, 100));
    expect(s.component.lines()[0].polygon[0].x).toBe(110); expect(s.component.history().length).toBe(1);
    expect(s.component.dirty()).toBeTrue(); expect(s.component.hasNavigationWork()).toBeTrue();
    expect(s.api.updateCaptureLineGeometry).not.toHaveBeenCalled();
  });

  it('retains a usable interrupted draw as an unsent box in its original source identity', () => {
    const s = setup(); const svg = { closest: () => svg, getScreenCTM: () => new DOMMatrix() };
    const pointer = (x: number, y: number) => ({ target: svg, currentTarget: svg, clientX: x, clientY: y }) as unknown as PointerEvent;
    s.component.setMode('draw'); s.component.onSvgDown(pointer(30, 30)); s.component.onMove(pointer(60, 70));
    s.display(null);
    expect(s.component.newLineDraft()).not.toBeNull();
    expect(s.component.newLineDraft()?.polygon[0]).toEqual({ x: 30, y: 30 });
    expect(s.component.newLineDraft()?.display.derivativeId).toBe(s.descriptor.derivativeId);
    expect(s.component.dirty()).toBeTrue(); expect(s.component.hasNavigationWork()).toBeTrue();
    expect(s.api.addCaptureLine).not.toHaveBeenCalled();
  });


  it('lets other existing drafts be confirmed before explicitly retrying an interrupted new box', () => {
    const s = setup(true); s.move(10); s.move(20, 1);
    const svg = { closest: () => svg, getScreenCTM: () => new DOMMatrix() };
    const pointer = (x: number, y: number) => ({ target: svg, currentTarget: svg, clientX: x, clientY: y }) as unknown as PointerEvent;
    s.component.setMode('draw'); s.component.onSvgDown(pointer(30, 30)); s.component.onMove(pointer(60, 70));
    s.display(null); s.display({ ...s.descriptor }, 'blob:restored-coexisting-drafts');
    expect(s.api.addCaptureLine).not.toHaveBeenCalled(); expect(s.component.canRetryNewLine()).toBeFalse();
    s.api.updateCaptureLineGeometry.and.returnValue(of({}));
    s.component.onDown(pointer(120, 120), s.component.lines()[0]); s.component.saveGeometry();
    expect(s.component.newLineDraft()).not.toBeNull(); expect(s.component.canRetryNewLine()).toBeFalse();
    s.component.onDown(pointer(120, 220), s.component.lines()[1]); s.component.saveGeometry();
    expect(s.api.updateCaptureLineGeometry).toHaveBeenCalledTimes(2); expect(s.api.addCaptureLine).not.toHaveBeenCalled();
    expect(s.component.newLineDraft()).not.toBeNull(); expect(s.component.canRetryNewLine()).toBeTrue();
    s.component.retryNewLine(); expect(s.api.addCaptureLine).toHaveBeenCalledTimes(1);
  });

  it('cancels only an empty interrupted draw without leaving navigation blocked', () => {
    const s = setup(); const svg = { closest: () => svg, getScreenCTM: () => new DOMMatrix() };
    const pointer = { target: svg, currentTarget: svg, clientX: 30, clientY: 30 } as unknown as PointerEvent;
    s.component.setMode('draw'); s.component.onSvgDown(pointer); s.display(null);
    expect(s.component.newLineDraft()).toBeNull(); expect(s.component.hasNavigationWork()).toBeFalse();
    expect(s.api.addCaptureLine).not.toHaveBeenCalled();
  });

  for (const status of [0, 500]) {
    it(`does not replay or discard a geometry request with unknown status ${status}`, () => {
      const s = setup(); s.move(10); s.component.saveGeometry(); s.save.error({ status });
      const reads = s.api.loadProjectFresh.calls.count();
      s.component.saveGeometry(); s.component.reset(); s.fixture.detectChanges();
      expect(s.api.updateCaptureLineGeometry).toHaveBeenCalledTimes(1);
      expect(s.api.loadProjectFresh.calls.count()).toBe(reads);
      expect(s.component.lines()[0].polygon[0].x).toBe(110);
      expect(s.component.hasNavigationWork()).toBeTrue();
      expect(s.component.actionStatus()).toContain('unconfirmed');
    });
  }

  it('keeps an unknown region-order outcome blocked through a fresh-read attempt', () => {
    const s = setup(false, true); const pending = new Subject<unknown>();
    s.api.updateCaptureReadingOrder.and.returnValue(pending); s.component.move('reg-one', 1);
    s.component.saveOrder(); pending.error({ status: 503 });
    const submitted = s.component.regions().map((region: { id: string }) => region.id);
    const reads = s.api.loadProjectFresh.calls.count();
    s.component.saveOrder(); s.component.reset();
    expect(s.api.updateCaptureReadingOrder).toHaveBeenCalledTimes(1);
    expect(s.api.loadProjectFresh.calls.count()).toBe(reads);
    expect(s.component.regions().map((region: { id: string }) => region.id)).toEqual(submitted);
    expect(s.component.hasNavigationWork()).toBeTrue();
  });

  it('keeps an unknown line-order outcome blocked through region selection and reset', () => {
    const s = setup(true, true); const pending = new Subject<unknown>();
    s.api.updateCaptureLineOrder.and.returnValue(pending); s.component.moveLine('ln-one', 1);
    s.component.saveLineOrder(); pending.error({ status: 0 });
    const reads = s.api.loadProjectFresh.calls.count();
    s.component.selectOrderRegion('reg-two'); s.component.saveLineOrder(); s.component.reset();
    expect(s.api.updateCaptureLineOrder).toHaveBeenCalledTimes(1);
    expect(s.api.loadProjectFresh.calls.count()).toBe(reads);
    expect(s.component.activeRegionId()).toBe('reg-one');
    expect(s.component.hasNavigationWork()).toBeTrue();
  });

  it('retains the exact new-box packet after an unknown structural outcome', () => {
    const s = setup(); const pending = new Subject<unknown>();
    s.api.addCaptureLine.and.returnValue(pending);
    const draft = { display: s.descriptor, polygon: [{ x: 10, y: 10 }, { x: 30, y: 10 }, { x: 30, y: 30 }],
      baseline: [{ x: 10, y: 25 }, { x: 30, y: 25 }], expectedLineIds: ['ln-one'], expectedRegionIds: ['reg-one'] };
    s.component.newLineDraft.set(draft); s.component.changingStructure.set(true); s.component.submitNewLine(draft);
    pending.error({ status: 500 }); const reads = s.api.loadProjectFresh.calls.count();
    s.component.discardNewLine(); s.component.reset(); s.component.retryNewLine();
    expect(s.component.newLineDraft()).toEqual(draft);
    expect(s.api.addCaptureLine).toHaveBeenCalledTimes(1);
    expect(s.api.loadProjectFresh.calls.count()).toBe(reads);
    expect(s.component.hasNavigationWork()).toBeTrue();
  });

  it('keeps a known precommit conflict discardable after a current read', () => {
    const s = setup(); s.move(10); s.component.saveGeometry(); s.save.error({ status: 409 });
    const reads = s.api.loadProjectFresh.calls.count(); s.component.reset();
    expect(s.api.loadProjectFresh.calls.count()).toBe(reads + 1);
    expect(s.component.dirty()).toBeFalse(); expect(s.component.hasNavigationWork()).toBeFalse();
  });

  it('does not interleave geometry and order publications while a request is pending', () => {
    const s = setup(true, true); s.move(10); s.component.saveGeometry();
    s.component.move('reg-one', 1); s.component.saveOrder();
    s.component.moveLine('ln-one', 1); s.component.saveLineOrder();
    expect(s.api.updateCaptureLineGeometry).toHaveBeenCalledTimes(1);
    expect(s.api.updateCaptureReadingOrder).not.toHaveBeenCalled();
    expect(s.api.updateCaptureLineOrder).not.toHaveBeenCalled();
    s.save.next({}); s.save.complete(); s.component.saveOrder();
    expect(s.api.updateCaptureReadingOrder).toHaveBeenCalledTimes(1);
  });

  it('shows a refused structural draft as awaiting fresh-read reconciliation', () => {
    const s = setup(); const pending = new Subject<unknown>(); s.api.addCaptureLine.and.returnValue(pending);
    const draft = { display: s.descriptor, polygon: [{ x: 10, y: 10 }, { x: 30, y: 10 }, { x: 30, y: 30 }],
      baseline: [{ x: 10, y: 25 }, { x: 30, y: 25 }], expectedLineIds: ['ln-one'], expectedRegionIds: ['reg-one'] };
    s.component.newLineDraft.set(draft); s.component.changingStructure.set(true); s.component.submitNewLine(draft);
    pending.error({ status: 409 }); s.fixture.detectChanges();
    const discard = Array.from(s.fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.trim() === 'Discard new line box');
    expect(discard?.disabled).toBeTrue(); expect(s.component.unknownOutcome()).toBeFalse();
    s.component.reset(); expect(s.component.newLineDraft()).toBeNull(); expect(s.component.hasNavigationWork()).toBeFalse();
  });

});
