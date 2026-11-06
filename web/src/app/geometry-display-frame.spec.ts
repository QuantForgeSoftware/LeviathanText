import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiClient, CaptureDisplayDescriptor } from './api-client';
import { GeometryEditor } from './geometry-editor';
import { displayFixture } from './capture-display.fixture';
import { provideWorkspaceTestAccess } from './workspace-testing.fixture.spec';

describe('GeometryEditor verified raw display frame', () => {
  function setup(regionMode: 'mixed' | 'other-only' | 'none' = 'mixed', delayReloads = false) {
    const first = { id: 'ln-first', regionId: 'reg-first', frameRef: 'cap1', polygon: [{ x: 100, y: 100 }, { x: 200, y: 100 }, { x: 200, y: 200 }, { x: 100, y: 200 }], baseline: [{ x: 100, y: 190 }, { x: 200, y: 190 }] };
    const other = { ...first, id: 'ln-other', regionId: 'reg-other', frameRef: 'cap2' };
    const payload = { id: 'col1', name: 'generated', objects: [{ id: 'obj1', surfaces: [{ id: 'surf1', kind: 'page', captures: [{ id: 'cap1' }, { id: 'cap2' }], lines: [first, other], regions: [{ id: 'reg-first', frameRef: 'cap1', readingOrderIndex: 0 }, { id: 'reg-other', frameRef: 'cap2', readingOrderIndex: 1 }] }] }] };
    if (regionMode !== 'mixed') {
      payload.objects[0].surfaces[0].regions = regionMode === 'none' ? [] : [payload.objects[0].surfaces[0].regions[1]];
      payload.objects[0].surfaces[0].lines = regionMode === 'none' ? [] : [other];
    }
    let loadCount = 0;
    const pendingReads: Subject<typeof payload>[] = [];
    const api = {
      loadProject: () => of(payload),
      loadProjectFresh: () => {
        if (++loadCount > 1 && delayReloads) { const reply = new Subject<typeof payload>(); pendingReads.push(reply); return reply; }
        return of(payload);
      }, geometryHistory: () => of([]), unplacedLines: () => of([]),
      updateCaptureLineGeometry: jasmine.createSpy('geometry').and.returnValue(of({})), addCaptureLine: jasmine.createSpy('addCaptureLine').and.returnValue(of({})),
      updateCaptureReadingOrder: jasmine.createSpy('order').and.returnValue(of({})), updateCaptureLineOrder: jasmine.createSpy('lineOrder').and.returnValue(of({})),
    };
    TestBed.configureTestingModule({ imports: [GeometryEditor], providers: [{ provide: ApiClient, useValue: api }, provideWorkspaceTestAccess({ projectId: 'col1' })] });
    const fixture = TestBed.createComponent(GeometryEditor);
    const descriptor = displayFixture('col1', 'surf1', { sourceWidth: 3001, sourceHeight: 2000, displayWidth: 2048, displayHeight: 1364, scaleX: 2048 / 3001, scaleY: 1364 / 2000 });
    fixture.componentRef.setInput('projectId', 'col1'); fixture.componentRef.setInput('surfaceId', 'surf1');
    const loadImage = (display: CaptureDisplayDescriptor = descriptor, url = 'blob:generated-raw') => {
      fixture.componentRef.setInput('display', display); fixture.componentRef.setInput('imageUrl', url); fixture.detectChanges();
      const image = fixture.nativeElement.querySelector('img') as HTMLImageElement;
      if (image) {
        Object.defineProperty(image, 'naturalWidth', { configurable: true, value: display.displayWidth });
        Object.defineProperty(image, 'naturalHeight', { configurable: true, value: display.displayHeight });
        image.dispatchEvent(new Event('load')); fixture.detectChanges();
      }
      return image;
    };
    const image = loadImage(); const component = fixture.componentInstance as any;
    return { fixture, component, api, descriptor, first, other, payload, loadImage, image, pendingReads };
  }

  it('uses raw source viewBox and independent rounded-axis mapping rather than preview natural dimensions', () => {
    const s = setup(); const svg = s.fixture.nativeElement.querySelector('svg') as SVGSVGElement;
    expect(svg.getAttribute('viewBox')).toBe('0 0 3001 2000'); expect(svg.getAttribute('preserveAspectRatio')).toBe('none');
    expect(s.component.width()).toBe(3001); expect(s.component.height()).toBe(2000);
    expect(s.component.lines().map((line: { id: string }) => line.id)).toEqual(['ln-first']);
    expect(s.fixture.nativeElement.querySelector('[data-id="ln-other"]')).toBeNull();
    expect(Array.from(s.fixture.nativeElement.querySelectorAll('h4') as NodeListOf<HTMLElement>).some(heading => heading.textContent === 'Region order')).toBeTrue();
    const matrix = new DOMMatrix([s.descriptor.scaleX, 0, 0, s.descriptor.scaleY, 17, 29]);
    spyOn(svg, 'getScreenCTM').and.returnValue(matrix);
    const pointer = (x: number, y: number) => ({ target: { closest: () => svg }, clientX: x * s.descriptor.scaleX + 17, clientY: y * s.descriptor.scaleY + 29 }) as unknown as PointerEvent;
    s.component.onDown(pointer(100, 100), s.component.lines()[0]); s.component.onMove(pointer(111.5, 109.25)); s.component.onUp();
    expect(s.component.lines()[0].polygon[0].x).toBeCloseTo(111.5, 8); expect(s.component.lines()[0].polygon[0].y).toBeCloseTo(109.25, 8);
    expect(s.first.polygon[0]).toEqual({ x: 100, y: 100 });
  });

  it('creates and edits compatible secondary geometry with complete global-order expectations', () => {
    const s = setup(); s.loadImage({ ...s.descriptor, captureId: 'cap2', sourceFrameRef: 'cap2' }, 'blob:generated-secondary');
    expect(s.component.lines().map((line: { id: string }) => line.id)).toEqual(['ln-other']);
    s.component.createLine({ x: 10, y: 20, width: 30, height: 40 });
    expect(s.api.addCaptureLine.calls.mostRecent().args[0].captureId).toBe('cap2');
    expect(s.api.addCaptureLine.calls.mostRecent().args[5]).toBe('reg-other');
    const line = s.component.lines()[0]; const draft = { ...line, polygon: line.polygon.map((point: { x: number; y: number }) => ({ ...point, x: point.x + 5 })) };
    s.component.lines.set([draft]); s.component.selectedId.set(line.id); s.component.saveGeometry();
    expect(s.api.updateCaptureLineGeometry.calls.mostRecent().args[0].captureId).toBe('cap2');
    expect(s.api.updateCaptureLineGeometry.calls.mostRecent().args[1]).toBe('ln-other');
    s.component.saveOrder(); s.component.saveLineOrder(); expect(s.api.updateCaptureReadingOrder).not.toHaveBeenCalled(); expect(s.api.updateCaptureLineOrder).not.toHaveBeenCalled();
  });

  it('preserves drafts across same-frame preview refresh and allows explicit discard when preview authorization fails', () => {
    const s = setup(); const changed = { ...s.component.lines()[0], polygon: s.first.polygon.map(point => ({ ...point, x: point.x + 5 })) };
    s.component.lines.set([changed]); s.fixture.detectChanges();
    s.fixture.componentRef.setInput('display', null); s.fixture.componentRef.setInput('imageUrl', ''); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.querySelector('svg')).toBeNull(); expect(s.component.lines()[0].polygon[0].x).toBe(105);
    expect(s.component.width()).toBe(3001); expect(s.component.height()).toBe(2000);
    s.loadImage({ ...s.descriptor }, 'blob:generated-refreshed'); expect(s.component.lines()[0].polygon[0].x).toBe(105);
    s.fixture.componentRef.setInput('display', null); s.fixture.componentRef.setInput('imageUrl', ''); s.fixture.detectChanges();
    const discard = Array.from(s.fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>).find(button => button.textContent?.includes('Discard geometry'))!;
    expect(discard.disabled).toBeFalse(); discard.click(); s.fixture.detectChanges(); expect(s.component.dirty()).toBeFalse();
    expect(s.component.lines()[0].polygon[0]).toEqual({ x: 100, y: 100 });
  });

  it('requests a new primary-frame region when only secondary regions exist', () => {
    const s = setup('other-only'); s.component.createLine({ x: 10, y: 20, width: 30, height: 40 });
    expect(s.api.addCaptureLine.calls.mostRecent().args[0].captureId).toBe('cap1');
    expect(s.api.addCaptureLine.calls.mostRecent().args[5]).toBeUndefined();
    expect(s.api.addCaptureLine.calls.mostRecent().args[4]).toEqual(['reg-other']);
  });

  it('passes an explicit compatible region and supports a primary capture with no lines or regions', () => {
    const s = setup(); s.component.createLine({ x: 10, y: 20, width: 30, height: 40 });
    expect(s.api.addCaptureLine.calls.mostRecent().args[5]).toBe('reg-first');
    expect(s.api.addCaptureLine.calls.mostRecent().args[3]).toEqual(['ln-first', 'ln-other']);
    expect(s.api.addCaptureLine.calls.mostRecent().args[4]).toEqual(['reg-first', 'reg-other']);
    s.fixture.destroy(); TestBed.resetTestingModule();
    const empty = setup('none'); empty.component.createLine({ x: 10, y: 20, width: 30, height: 40 });
    expect(empty.api.addCaptureLine).toHaveBeenCalled(); expect(empty.api.addCaptureLine.calls.mostRecent().args[5]).toBeUndefined();
  });

  it('retains a refused secondary box and blocks retry until its structural receipt is reconciled', () => {
    const s = setup(); s.loadImage({ ...s.descriptor, captureId: 'cap2', sourceFrameRef: 'cap2' }, 'blob:secondary');
    const reply = new Subject<{ lineId: string }>(); s.api.addCaptureLine.and.returnValue(reply);
    s.component.createLine({ x: 10.25, y: 20.5, width: 30, height: 40 });
    expect(s.component.changingStructure()).toBeTrue();
    reply.error({ status: 409 }); s.fixture.detectChanges();
    expect(s.component.newLineDraft().polygon[0]).toEqual({ x: 10.25, y: 20.5 });
    expect(s.component.dirty()).toBeTrue(); expect(s.component.hasNavigationWork()).toBeTrue();
    const retained = structuredClone(s.component.newLineDraft());
    const pending = structuredClone(s.component.unresolvedWrites);
    expect(pending.length).toBe(1); expect(pending[0].kind).toBe('addCaptureLine');
    expect(pending[0].draft).toEqual(retained);
    expect(s.component.canRetryNewLine()).toBeFalse();
    s.component.retryNewLine(); expect(s.api.addCaptureLine).toHaveBeenCalledTimes(1);
    expect(s.component.newLineDraft()).toEqual(retained); expect(s.component.unresolvedWrites).toEqual(pending);
    expect(s.fixture.nativeElement.querySelector('[data-new-line-draft]')).not.toBeNull();
    s.loadImage({ ...s.descriptor, captureId: 'cap2', sourceFrameRef: 'cap2', derivativeId: 'd'.repeat(64) }, 'blob:changed');
    expect(s.component.canRetryNewLine()).toBeFalse();
    s.component.retryNewLine(); expect(s.api.addCaptureLine).toHaveBeenCalledTimes(1);
    expect(s.component.newLineDraft()).toEqual(retained); expect(s.component.unresolvedWrites).toEqual(pending);
    s.component.discardNewLine(); expect(s.component.newLineDraft()).toEqual(retained); expect(s.component.dirty()).toBeTrue();
    expect(s.component.unresolvedWrites).toEqual(pending); expect(s.component.hasNavigationWork()).toBeTrue();
  });

  it('retains exact source coordinates and structural expectations without duplicating an unconfirmed add', () => {
    const s = setup(); const reply = new Subject<{ lineId: string }>(); s.api.addCaptureLine.and.returnValue(reply);
    s.component.createLine({ x: 1.5, y: 2.25, width: 30, height: 40 }); reply.error({ status: 503 });
    const originalArgs = s.api.addCaptureLine.calls.mostRecent().args;
    s.api.addCaptureLine.and.returnValue(of({ lineId: 'new' })); s.component.retryNewLine();
    expect(s.api.addCaptureLine).toHaveBeenCalledTimes(1);
    expect(s.api.addCaptureLine.calls.mostRecent().args).toEqual(originalArgs);
    const retained = s.component.newLineDraft();
    expect(retained.display).toEqual(originalArgs[0]); expect(retained.polygon).toEqual(originalArgs[1]);
    expect(retained.baseline).toEqual(originalArgs[2]); expect(retained.expectedLineIds).toEqual(originalArgs[3]);
    expect(retained.expectedRegionIds).toEqual(originalArgs[4]); expect(retained.regionId).toEqual(originalArgs[5]);
    expect(s.component.dirty()).toBeTrue(); expect(s.component.hasNavigationWork()).toBeTrue();
  });

  it('sends full mixed-frame order snapshots and preserves off-frame slots after a successful line reorder', () => {
    const s = setup(); const surface = s.payload.objects[0].surfaces[0];
    surface.regions.push({ id: 'reg-third', frameRef: 'cap1', readingOrderIndex: 2 });
    const offFrame = { ...s.other, id: 'ln-off-frame', regionId: 'reg-first' };
    const next = { ...s.first, id: 'ln-next' };
    surface.lines = [s.first, s.other, offFrame, next]; s.component.reload(); s.fixture.detectChanges();
    s.component.move('reg-first', 1); s.component.saveOrder();
    expect(s.api.updateCaptureReadingOrder.calls.mostRecent().args).toEqual([
      s.descriptor, ['reg-third', 'reg-first'], ['reg-first', 'reg-other', 'reg-third'],
    ]);
    s.component.moveLine('ln-first', 1); s.component.saveLineOrder();
    expect(s.api.updateCaptureLineOrder.calls.mostRecent().args).toEqual([
      s.descriptor, 'reg-first', ['ln-next', 'ln-first'], ['ln-first', 'ln-off-frame', 'ln-next'],
    ]);
    s.component.selectOrderRegion('reg-third'); s.component.selectOrderRegion('reg-first');
    expect(s.component.lineOrder()).toEqual(['ln-next', 'ln-first']);
    s.component.createLine({ x: 1, y: 2, width: 30, height: 40 });
    expect(s.api.addCaptureLine.calls.mostRecent().args[3]).toEqual(['ln-next', 'ln-other', 'ln-off-frame', 'ln-first']);
  });

  it('keeps a refused mixed-frame ordering draft and original full expectations', () => {
    const s = setup(); const surface = s.payload.objects[0].surfaces[0];
    surface.regions.push({ id: 'reg-third', frameRef: 'cap1', readingOrderIndex: 2 });
    s.component.reload(); const reply = new Subject<unknown>(); s.api.updateCaptureReadingOrder.and.returnValue(reply);
    s.component.move('reg-first', 1); s.component.saveOrder(); reply.error({ status: 409 });
    expect(s.component.orderDirty()).toBeTrue(); expect(s.component.hasNavigationWork()).toBeTrue();
    s.api.updateCaptureReadingOrder.and.returnValue(of({})); s.component.saveOrder();
    expect(s.api.updateCaptureReadingOrder.calls.mostRecent().args[2]).toEqual(['reg-first', 'reg-other', 'reg-third']);
    expect(s.component.orderDirty()).toBeFalse();
  });

  it('ignores an old-frame geometry read after switching frames and creating a new draft', () => {
    const s = setup('mixed', true); s.component.reload();
    s.loadImage({ ...s.descriptor, captureId: 'cap2', sourceFrameRef: 'cap2' }, 'blob:generated-next');
    expect(s.pendingReads.length).toBe(2); s.pendingReads[1].next(s.payload); s.fixture.detectChanges();
    const line = s.component.lines()[0]; s.component.lines.set([{ ...line, polygon: line.polygon.map((point: { x: number; y: number }) => ({ ...point, x: point.x + 5 })) }]);
    s.pendingReads[0].next(s.payload); s.fixture.detectChanges();
    expect(s.component.lines()[0].id).toBe('ln-other'); expect(s.component.lines()[0].polygon[0].x).toBe(105); expect(s.component.dirty()).toBeTrue();
  });

  it('adopts a changed frame after explicitly discarding a blocked draft without needing a second descriptor', () => {
    const s = setup(); const line = s.component.lines()[0];
    s.component.lines.set([{ ...line, polygon: line.polygon.map((point: { x: number; y: number }) => ({ ...point, x: point.x + 5 })) }]);
    s.fixture.detectChanges();
    s.loadImage({ ...s.descriptor, captureId: 'cap2', sourceFrameRef: 'cap2' }, 'blob:generated-frame-changed');
    expect(s.component.frameVerified()).toBeFalse(); expect(s.component.lines()[0].polygon[0].x).toBe(105);
    s.component.reset(); s.fixture.detectChanges();
    // The changed-source raster is withheld until adoption; its load needs no second descriptor request.
    const adoptedImage = s.fixture.nativeElement.querySelector('img') as HTMLImageElement;
    const adoptedDisplay = s.component.display() as CaptureDisplayDescriptor;
    Object.defineProperty(adoptedImage, 'naturalWidth', { configurable: true, value: adoptedDisplay.displayWidth });
    Object.defineProperty(adoptedImage, 'naturalHeight', { configurable: true, value: adoptedDisplay.displayHeight });
    adoptedImage.dispatchEvent(new Event('load')); s.fixture.detectChanges();
    expect(s.component.frameVerified()).toBeTrue(); expect(s.component.lines()[0].id).toBe('ln-other');
    expect(s.fixture.nativeElement.querySelector('svg')).not.toBeNull();
  });

  it('hides overlays after decode/dimension failures without replacing known source dimensions by1×1', () => {
    const s = setup(); let failures = 0; s.fixture.componentInstance.imageFailed.subscribe(() => failures++);
    Object.defineProperty(s.image, 'naturalWidth', { configurable: true, value: 0 }); s.image.dispatchEvent(new Event('load')); s.fixture.detectChanges();
    expect(failures).toBe(1); expect(s.fixture.nativeElement.querySelector('svg')).toBeNull();
    expect(s.component.width()).toBe(3001); expect(s.component.height()).toBe(2000); expect(s.component.locked()).toBeTrue();
  });
});
