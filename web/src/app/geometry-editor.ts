/**
 * Geometry editor (todo Phase 2 gate: geometry/order correction; manual layout when
 * automatic layout fails). Renders a capture preview with polygon/baseline overlays
 * in the image's natural coordinate frame; drags translate the selected line; region
 * order is editable with up/down; saves go through the geometry API.
 */
import { Component, computed, DestroyRef, effect, inject, input, OnChanges, output, signal, SimpleChanges } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkspaceComponentScope } from './workspace-component-scope';
import type { WorkspaceAccessState } from './workspace-access-types';
import { ApiClient, CaptureDisplayDescriptor, GeometryLine, GeometryPoint, GeometryRegion, ProjectPayload } from './api-client';

interface DragState {
  lineId: string;
  startX: number;
  startY: number;
  original: GeometryLine;
  mode: 'move' | 'resize';
  corner: number; // 0=TL, 1=TR, 2=BR, 3=BL
}

interface NewLineDraft {
  display: CaptureDisplayDescriptor;
  polygon: GeometryPoint[];
  baseline: GeometryPoint[];
  expectedLineIds: string[];
  expectedRegionIds: string[];
  regionId?: string;
}

@Component({
  selector: 'app-geometry-editor',
  standalone: true,
  template: `
    @if (inputQuarantined()) { <p role="status">Geometry work is retained for its original surface. Restore that workspace before continuing.</p> }
    <section data-workspace-protected [hidden]="!workspaceAvailable()" [attr.inert]="!workspaceAvailable() ? '' : null">
      <h3>Geometry <small>(source frame: {{ width() }}×{{ height() }})</small></h3>
      @if (unknownOutcome()) { <p role="alert">A write outcome is unconfirmed. Your original-frame work is retained; saving, retrying and discarding remain blocked pending authoritative reconciliation.</p> }
      @if (workingDisplay(); as raster) {
        @if (raster.displayWidth !== raster.sourceWidth || raster.displayHeight !== raster.sourceHeight) {
          <p class="hint">Preview {{ raster.displayWidth }}×{{ raster.displayHeight }} pixels; image coordinates {{ raster.sourceWidth }}×{{ raster.sourceHeight }}.</p>
        }
      }
      @if (locked()) {
        <p class="hint">Page view: click a line box to open it. Enable edit mode to move/draw/resize.</p>
      } @else {
        <p class="hint">Click a line, drag to move it, then save.</p>
      }
      @if (locked() && actionStatus()) { <p class="hint" role="status">{{ actionStatus() }}</p> }
      <p class="tools">
        <button type="button" aria-label="Zoom out" (click)="zoomBy(-0.25)">−</button>
        <button type="button" aria-label="Fit page" (click)="fitZoom()">Fit</button>
        <button type="button" aria-label="Zoom in" (click)="zoomBy(0.25)">+</button>
        <span class="hint">{{ (zoom() * 100).toFixed(0) }}%</span>
      </p>

      <div class="scroll">
      <div class="canvas" [style.width.%]="zoom() * 100">
        @if (imageUrl() && (!display() || frameVerified())) {
          @for (url of [imageUrl()]; track url) {
            <img [src]="url" (load)="onImage($event)" (error)="onImageError($event)" alt="surface capture" />
          }
        }
        @if (frameVerified() && rasterLoaded()) {
        <svg
          [attr.viewBox]="'0 0 ' + width() + ' ' + height()"
          preserveAspectRatio="none"
          role="img"
          aria-label="Surface capture with editable line boxes"
          (pointerdown)="onSvgDown($event)"
          (pointermove)="onMove($event)"
          (pointerup)="onUp()"
          (pointerleave)="onUp()">
          @if (drawRect(); as rect) {
            <rect [attr.x]="rect.x" [attr.y]="rect.y" [attr.width]="rect.width" [attr.height]="rect.height"
                  class="draft" />
          }
          @if (newLineDraft(); as draft) {
            <polygon [attr.points]="polygonPoints(draft)" class="draft" data-new-line-draft="true" />
          }
          @for (line of lines(); track line.id) {
            <g [attr.data-id]="line.id">
              <polygon
                [attr.points]="polygonPoints(line)"
                [attr.class]="line.id === selectedId() ? 'sel' : ''"
                (pointerdown)="onDown($event, line)" />
              @if (line.baseline.length > 1) {
                <polyline [attr.points]="baselinePoints(line)" class="baseline" />
              }
              @if (line.id === selectedId() && !locked()) {
                @for (corner of bboxCorners(line); track $index) {
                  <circle
                    [attr.cx]="corner.x"
                    [attr.cy]="corner.y"
                    r="5"
                    class="handle"
                    (pointerdown)="onResizeDown($event, line, $index)" />
                }
              }
            </g>
          }
        </svg>
        }
      </div>
      </div>

      @if (locked() && (dirty() || orderDirty() || lineOrderDirty())) {
        <button type="button" (click)="reset()" [disabled]="unknownOutcome() || savingGeometry() || savingOrder() || savingLineOrder() || changingStructure() || discarding()">Discard geometry and order drafts</button>
      }
      @if (newLineDraft()) {
        <p class="tools">
          <span class="hint">Unsaved new line box retained in its original source frame.</span>
          <button type="button" (click)="retryNewLine()" [disabled]="!canRetryNewLine()">Retry new line</button>
          <button type="button" (click)="discardNewLine()" [disabled]="unknownOutcome() || uncertainStructure() || changingStructure()">Discard new line box</button>
        </p>
      }
      @if (!locked()) {
      <div class="tools">
        <button type="button" (click)="setMode('move')" [disabled]="mode() === 'move'">Move / resize</button>
        <button type="button" (click)="setMode('draw')" [disabled]="mode() === 'draw' || !canCreateNewLine()">Draw new line</button>
        <button type="button" (click)="saveGeometry()" [disabled]="unknownOutcome() || !selectedDirty() || savingGeometry() || changingStructure()">Save geometry</button>
        <button type="button" (click)="undoGeometry()" [disabled]="history().length === 0 || changingStructure()">Undo edit</button>
        <button type="button" (click)="splitSelected()" [disabled]="unknownOutcome() || !selectedId() || changingStructure()">Split line</button>
        <button type="button" (click)="mergeSelectedWithNext()" [disabled]="unknownOutcome() || !selectedId() || changingStructure()">Merge with next</button>
        <button type="button" (click)="deleteSelected()" [disabled]="unknownOutcome() || !selectedId() || changingStructure()">Delete line</button>
        <button type="button" (click)="reset()" [disabled]="unknownOutcome() || !dirty() || changingStructure()">Reset</button>
        <span class="hint" role="status">{{ dirty() ? 'unsaved geometry' : 'saved' }} {{ actionStatus() }}</span>
      </div>
      }

      @if (!locked() && !canCreateNewLine()) {
        <p class="hint">New lines require a current verified capture preview.</p>
      }
      @if (!locked() && mixedFrames()) {
        <p class="hint">Only geometry in this image frame is shown. Ordering replaces this frame's slots; other frames keep their positions.</p>
      }
      @if (!locked() && unplaced().length > 0) {
        <h4>Unplaced text lines ({{ unplaced().length }})</h4>
        <p class="hint">Select a line, choose “Draw new line”, then drag its box on the image.</p>
        <ol>
          @for (entry of unplaced(); track entry.lineId) {
            <li>
              <button type="button" class="link" (click)="selectUnplaced(entry.lineId)">
                {{ entry.referenceText || entry.lineId }}
              </button>
            </li>
          }
        </ol>
      }

      @if (!locked()) {
      <h4>Region order</h4>
      <ol>
        @for (region of regions(); track region.id) {
          <li>
            <code>{{ region.id }}</code>
            <button type="button" aria-label="Move region earlier" (click)="move(region.id, -1)">↑</button>
            <button type="button" aria-label="Move region later" (click)="move(region.id, 1)">↓</button>
          </li>
        }
      </ol>
      <button type="button" (click)="saveOrder()" [disabled]="unknownOutcome() || !orderDirty() || savingOrder() || changingStructure()">Save reading order</button>

      <h4>Line order <small>(region {{ activeRegionId() }})</small></h4>
      <label>Region
        <select [value]="activeRegionId()" (change)="onOrderRegion($event)"
          [disabled]="lineOrderDirty() || savingLineOrder() || changingStructure()">
          @for (region of regions(); track region.id) { <option [value]="region.id">{{ region.id }}</option> }
        </select>
      </label>
      <ol>
        @for (line of orderedLines(); track line.id) {
          <li>
            <code>{{ line.id }}</code>
            <button type="button" aria-label="Move line earlier" (click)="moveLine(line.id, -1)">↑</button>
            <button type="button" aria-label="Move line later" (click)="moveLine(line.id, 1)">↓</button>
          </li>
        } @empty {
          <li>No lines in this region.</li>
        }
      </ol>
      <button type="button" (click)="saveLineOrder()" [disabled]="unknownOutcome() || !lineOrderDirty() || savingLineOrder() || changingStructure()">Save line order</button>

      <details>
        <summary>Edit history ({{ historyEdits().length }})</summary>
        <ul>
          @for (edit of historyEdits().slice(-8).reverse(); track edit.id) {
            <li><b>{{ edit.kind }}</b> · {{ edit.author }} · {{ edit.at }}</li>
          } @empty {
            <li>No geometry edits recorded yet.</li>
          }
        </ul>
      </details>
      }
    </section>
  `,
  styles: [`
    .scroll { max-height: 70vh; overflow: auto; border: 1px solid #dde; }
    .canvas { position: relative; }
    .canvas img { width: 100%; display: block; }
    .canvas svg { position: absolute; inset: 0; width: 100%; height: 100%; }
    polygon { fill: rgba(40, 120, 220, 0.15); stroke: #2878dc; stroke-width: 2; cursor: move; }
    polygon.sel { fill: rgba(220, 120, 40, 0.25); stroke: #dc7828; }
    .baseline { fill: none; stroke: #dc3c28; stroke-width: 2; pointer-events: none; }
    .handle { fill: #fff; stroke: #dc7828; stroke-width: 2; cursor: nwse-resize; }
    .draft { fill: rgba(40, 160, 80, 0.2); stroke: #28a050; stroke-width: 2; stroke-dasharray: 5 3; pointer-events: none; }
    .link { background: none; border: none; color: #334; cursor: pointer; text-decoration: underline; padding: 0; text-align: left; }
    .tools { margin-top: 0.5rem; display: flex; gap: 0.75rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class GeometryEditor implements OnChanges {
  projectId = input.required<string>();
  surfaceId = input.required<string>();
  imageUrl = input.required<string>();
  readOnly = input(false);
  structureBlocked = input(false);
  display = input<CaptureDisplayDescriptor | null>(null);
  captureSwitchBlocked = output<boolean>();
  imageFailed = output<void>();
  protected readonly rasterLoaded = signal(false);
  protected readonly mixedFrames = signal(false);
  private readonly activeFrame = signal('');
  protected readonly workingDisplay = signal<CaptureDisplayDescriptor | null>(null);
  private readonly unconfirmedGeometry = signal(new Set<string>());
  protected readonly discarding = signal(false);
  private loadedSurfaceLineIds: string[] = [];
  private loadedSurfaceRegionIds: string[] = [];
  private loadedSurfaceRegionOrder: string[] = [];
  private loadedRegionLineIds = new Map<string, string[]>();
  protected readonly frameVerified = computed(() => {
    const display = this.display();
    const draft = this.newLineDraft();
    return !!display && display.orientation === 'raw' && display.projectId === this.projectId()
      && display.surfaceId === this.surfaceId() && display.sourceFrameRef === this.activeFrame()
      && this.sameDisplay(this.workingDisplay(), display)
      && (!draft || draft.display.derivativeId === display.derivativeId)
      && Number.isSafeInteger(display.sourceWidth) && display.sourceWidth > 0
      && Number.isSafeInteger(display.sourceHeight) && display.sourceHeight > 0;
  });
  protected readonly canCreateNewLine = computed(() => {
    const display = this.display();
    return !!display && this.frameVerified() && this.newLineDraft() === null;
  });
  protected readonly locked = computed(() => !this.workspaceCanEdit() || this.readOnly() || !this.frameVerified() || !this.rasterLoaded() || this.discarding());
  lineSelected = output<string>();

  protected readonly lines = signal<GeometryLine[]>([]);
  protected readonly regions = signal<GeometryRegion[]>([]);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly width = signal(0);
  protected readonly height = signal(0);
  protected readonly newLineDraft = signal<NewLineDraft | null>(null);
  protected readonly dirty = computed(() => this.newLineDraft() !== null || this.unconfirmedGeometry().size > 0 || this.lines().some(line => this.isChanged(line)));
  protected readonly canRetryNewLine = computed(() => {
    const draft = this.newLineDraft(); const display = this.display();
    return !!draft && !!display && !this.locked() && !this.changingStructure() && !this.unknownOutcome() && !this.uncertainStructure() && !this.structureBlocked()
      && !this.savingGeometry() && !this.savingOrder() && !this.savingLineOrder()
      && !this.lines().some(line => this.isChanged(line)) && !this.orderDirty() && !this.lineOrderDirty()
      && draft.display.derivativeId === display.derivativeId && draft.display.captureId === display.captureId
      && draft.display.sourceFrameRef === this.activeFrame();
  });
  protected readonly selectedDirty = computed(() => {
    const selected = this.lines().find(line => line.id === this.selectedId());
    return selected !== undefined && (this.isChanged(selected) || this.unconfirmedGeometry().has(selected.id));
  });
  protected readonly savingGeometry = signal(false);
  protected readonly changingStructure = signal(false);
  protected readonly orderDirty = computed(() => this.regions().map(region => region.id).join('|') !== this.originalOrder().join('|'));
  protected readonly lineOrderDirty = computed(() => this.lineOrder().join('|') !== this.originalLineOrder().join('|'));
  protected readonly savingOrder = signal(false);
  protected readonly savingLineOrder = signal(false);
  protected readonly mode = signal<'move' | 'draw'>('move');
  protected readonly drawRect = signal<{ x: number; y: number; width: number; height: number } | null>(null);
  protected readonly actionStatus = signal('');
  protected readonly zoom = signal(1);
  protected readonly unplaced = signal<{ lineId: string; order: number; referenceText: string }[]>([]);
  protected readonly placingLineId = signal<string | null>(null);
  protected readonly historyEdits = signal<{
    id: string; kind: string; at: string; author: string; lineId?: string; regionId?: string;
  }[]>([]);
  private drawStart: { x: number; y: number } | null = null;
  protected readonly lineOrder = signal<string[]>([]);
  private readonly lineOrderRegionId = signal('');
  protected readonly history = signal<GeometryLine[][]>([]);
  private pendingSnapshot: GeometryLine[] | null = null;
  private readonly original = signal(new Map<string, GeometryLine>());
  private readonly originalOrder = signal<string[]>([]);
  private readonly originalLineOrder = signal<string[]>([]);
  private drag: DragState | null = null;
  private loadGeneration = 0;
  private historyGeneration = 0;
  private discardGeneration = 0;
  private readonly destroyRef = inject(DestroyRef);

  private readonly baseApi: ApiClient;
  private readonly scope: WorkspaceComponentScope;
  private initialized = false;
  private scopeVersion = 0;
  protected readonly inputQuarantined = signal(false);
  protected readonly unknownOutcome = signal(false);
  private readonly uncertainOrder = signal(false);
  private readonly uncertainLineOrder = signal(false);
  protected readonly uncertainStructure = signal(false);
  private pendingGeometryWrite: { line: GeometryLine; expected: { polygon: GeometryPoint[]; baseline: GeometryPoint[] }; display: CaptureDisplayDescriptor } | null = null;
  private pendingStructureWrite: unknown = null;
  private pendingOrderWrite: unknown = null;
  private pendingLineOrderWrite: unknown = null;
  private readonly unresolvedWrites: unknown[] = [];
  protected workspaceAvailable(): boolean {
    const active = this.scope.active();
    if (this.inputQuarantined()) this.scope.concealNow();
    return active && !this.inputQuarantined();
  }
  protected workspaceCanEdit(): boolean { return this.workspaceAvailable() && this.scope.mayEdit(); }
  private get api(): ApiClient {
    const project = this.projectId(), surface = this.surfaceId(), version = this.scopeVersion;
    if (!this.workspaceAvailable()) return this.baseApi.forWorkspaceRequest({ credential: '', current: () => false });
    return this.scope.request(this.baseApi, project, () => this.projectId() === project && this.surfaceId() === surface && this.scopeVersion === version && !this.inputQuarantined());
  }
  private onWorkspaceState(state: WorkspaceAccessState): void {
    if (state.status !== 'active') {
      this.closeGestureForRefresh();
      if (this.pendingGeometryWrite || this.pendingOrderWrite || this.pendingLineOrderWrite || this.pendingStructureWrite) this.unknownOutcome.set(true);
      if (this.pendingGeometryWrite) {
        this.unresolvedWrites.push(this.pendingGeometryWrite);
        const id = this.pendingGeometryWrite.line.id;
        this.unconfirmedGeometry.update(ids => new Set(ids).add(id));
      }
      if (this.savingOrder()) { this.uncertainOrder.set(true); this.unresolvedWrites.push(this.pendingOrderWrite); }
      if (this.savingLineOrder()) { this.uncertainLineOrder.set(true); this.unresolvedWrites.push(this.pendingLineOrderWrite); }
      if (this.changingStructure()) { this.uncertainStructure.set(true); this.unresolvedWrites.push(this.pendingStructureWrite); }
      this.pendingStructureWrite = null;
      this.pendingGeometryWrite = null; this.pendingOrderWrite = null; this.pendingLineOrderWrite = null;
      this.loadGeneration++; this.historyGeneration++; this.discardGeneration++;
      this.savingGeometry.set(false); this.savingOrder.set(false); this.savingLineOrder.set(false);
      this.changingStructure.set(false); this.discarding.set(false); this.rasterLoaded.set(false);
      return;
    }
    if (!this.initialized || !this.workspaceAvailable()) return;
    if (!this.frameHasWork()) {
      const display = this.display();
      if (this.validDisplay(display)) this.adoptDisplay(display);
      this.reload();
    } else this.actionStatus.set('Access restored. Your original-frame drafts and unconfirmed writes are retained.');
  }

  constructor(api: ApiClient) {
    this.baseApi = api;
    this.scope = new WorkspaceComponentScope(state => this.onWorkspaceState(state));
    effect(() => this.captureSwitchBlocked.emit(this.dirty() || this.orderDirty() || this.lineOrderDirty()
      || this.savingGeometry() || this.savingOrder() || this.savingLineOrder() || this.changingStructure() || this.discarding() || this.drawRect() !== null || this.unknownOutcome() || this.uncertainOrder() || this.uncertainLineOrder() || this.uncertainStructure()));
  }

  ngOnChanges(changes: SimpleChanges): void {
    const scopeChangedWithWork = this.initialized && (!!changes['projectId'] || !!changes['surfaceId']) && this.frameHasWork();
    if (changes['projectId'] || changes['surfaceId']) this.scopeVersion++;
    this.initialized = true;
    if (scopeChangedWithWork) { this.inputQuarantined.set(true); this.scope.concealNow(); return; }
    if (!this.workspaceAvailable()) return;
    if (changes['imageUrl']) this.rasterLoaded.set(false);
    const display = this.display();
    const valid = this.validDisplay(display);
    const scopeChanged = !!changes['projectId'] || !!changes['surfaceId'];
    if (!scopeChanged && (changes['imageUrl'] || !valid || !this.sameDisplay(this.workingDisplay(), display)))
      this.closeGestureForRefresh();
    if (!scopeChanged && valid && !this.sameDisplay(this.workingDisplay(), display) && (this.frameHasWork() || this.drag !== null)) {
      this.actionStatus.set('The source image changed. Your draft remains in its original frame; explicitly discard or reconcile it before adopting the new source.');
      return;
    }
    if (scopeChanged) {
      this.discardGeneration++; this.discarding.set(false);
      this.adoptDisplay(valid ? display : null); this.clearFrameView(); this.reload();
    } else if (valid && !this.sameDisplay(this.workingDisplay(), display)) {
      this.adoptDisplay(display); this.clearFrameView(); this.reload();
    }
  }

  hasNavigationWork(): boolean { return this.frameHasWork(); }

  private frameHasWork(): boolean {
    return this.dirty() || this.orderDirty() || this.lineOrderDirty() || this.savingGeometry()
      || this.savingOrder() || this.savingLineOrder() || this.changingStructure() || this.discarding() || this.drawRect() !== null || this.unknownOutcome() || this.uncertainOrder() || this.uncertainLineOrder() || this.uncertainStructure();
  }

  private validDisplay(display: CaptureDisplayDescriptor | null): display is CaptureDisplayDescriptor {
    return !!display && display.projectId === this.projectId() && display.surfaceId === this.surfaceId()
      && display.orientation === 'raw' && Number.isSafeInteger(display.sourceWidth) && display.sourceWidth > 0
      && Number.isSafeInteger(display.sourceHeight) && display.sourceHeight > 0;
  }

  private sameDisplay(a: CaptureDisplayDescriptor | null, b: CaptureDisplayDescriptor | null): boolean {
    return !!a && !!b && a.projectId === b.projectId && a.surfaceId === b.surfaceId && a.captureId === b.captureId
      && a.sourceFrameRef === b.sourceFrameRef && a.sourceWidth === b.sourceWidth && a.sourceHeight === b.sourceHeight
      && a.derivativeId === b.derivativeId;
  }

  private adoptDisplay(display: CaptureDisplayDescriptor | null): void {
    this.workingDisplay.set(display ? { ...display, recordedSourceChecksum: { ...display.recordedSourceChecksum },
      observedSourceChecksum: { ...display.observedSourceChecksum }, displayChecksum: { ...display.displayChecksum } } : null);
    this.activeFrame.set(display?.sourceFrameRef ?? '');
    this.width.set(display?.sourceWidth ?? 0); this.height.set(display?.sourceHeight ?? 0);
  }

  private adoptCurrentDisplayIfIdle(): boolean {
    const display = this.display();
    if (!this.validDisplay(display) || this.sameDisplay(this.workingDisplay(), display) || this.frameHasWork() || this.drag) return false;
    this.adoptDisplay(display); this.clearFrameView(); this.reload(); return true;
  }

  private closeGestureForRefresh(): void {
    if (this.drag && this.pendingSnapshot && JSON.stringify(this.pendingSnapshot) !== JSON.stringify(this.snapshot()))
      this.history.update(stack => [...stack, this.pendingSnapshot!]);
    this.drag = null; this.pendingSnapshot = null;
    const rect = this.drawRect();
    this.drawStart = null; this.drawRect.set(null);
    if (rect && rect.width >= 4 && rect.height >= 4) this.createLine(rect, true);
  }

  private clearFrameView(): void {
    this.newLineDraft.set(null);
    this.unconfirmedGeometry.set(new Set());
    this.uncertainOrder.set(false); this.uncertainLineOrder.set(false); this.uncertainStructure.set(false); this.pendingStructureWrite = null; this.unresolvedWrites.length = 0;
    this.lines.set([]);
    this.original.set(new Map());
    this.regions.set([]);
    this.originalOrder.set([]);
    this.originalLineOrder.set([]);
    this.lineOrder.set([]);
    this.lineOrderRegionId.set('');
    this.selectedId.set(null);
    this.placingLineId.set(null);
    this.history.set([]);
    this.historyEdits.set([]);
    this.unplaced.set([]);
    this.pendingSnapshot = null;
    this.drag = null;
    this.actionStatus.set('');
    this.changingStructure.set(false);
    this.setMode('move');
  }

  private loadHistory(): void {
    const surfaceId = this.surfaceId();
    const generation = this.loadGeneration;
    const request = ++this.historyGeneration;
    const current = () => generation === this.loadGeneration && request === this.historyGeneration;
    this.api.geometryHistory(surfaceId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: edits => { if (current()) this.historyEdits.set(edits); },
      error: () => { if (current()) this.historyEdits.set([]); },
    });
    this.api.unplacedLines(surfaceId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: lines => { if (current()) this.unplaced.set(lines.filter(item => this.lines().some(line => line.id === item.lineId))); },
      error: () => { if (current()) this.unplaced.set([]); },
    });
  }

  protected selectUnplaced(lineId: string): void {
    if (!this.workspaceAvailable()) return;
    if (this.discarding()) return;
    this.placingLineId.set(lineId);
    this.actionStatus.set('draw a box for the selected text line');
    this.setMode('draw');
  }

  private loadFrom(payload: ProjectPayload): void {
    const surface = payload.objects.flatMap(o => o.surfaces).find(s => s.id === this.surfaceId());
    if (!surface) {
      this.changingStructure.set(false);
      this.actionStatus.set('surface geometry is unavailable');
      return;
    }
    this.loadedSurfaceLineIds = surface.lines.map(line => line.id);
    this.loadedSurfaceRegionIds = surface.regions.map(region => region.id);
    this.loadedSurfaceRegionOrder = surface.regions.map(region => ({ id: region.id, rank: region.readingOrderIndex ?? null }))
      .sort((a, b) => a.rank === null ? (b.rank === null ? 0 : -1) : b.rank === null ? 1 : a.rank - b.rank).map(region => region.id);
    this.loadedRegionLineIds = new Map(surface.regions.map(region => [region.id,
      surface.lines.filter(line => line.regionId === region.id).map(line => line.id)]));
    if (!this.activeFrame()) return;
    this.mixedFrames.set(surface.lines.some(line => line.frameRef !== this.activeFrame())
      || surface.regions.some(region => region.frameRef !== this.activeFrame()));
    const lines = surface.lines.filter(line => line.frameRef === this.activeFrame()).map(l => ({
      id: l.id,
      regionId: l.regionId,
      frameRef: l.frameRef,
      polygon: ((l as { polygon?: GeometryPoint[] }).polygon ?? []).map(p => ({ ...p })),
      baseline: ((l as { baseline?: GeometryPoint[] }).baseline ?? []).map(p => ({ ...p })),
    }));
    const regions = surface.regions.filter(region => region.frameRef === this.activeFrame()).map(r => ({
      id: r.id,
      frameRef: r.frameRef,
      readingOrderIndex: (r as { readingOrderIndex?: number }).readingOrderIndex ?? null,
    })).sort((a, b) => a.readingOrderIndex === null ? (b.readingOrderIndex === null ? 0 : -1)
      : b.readingOrderIndex === null ? 1 : a.readingOrderIndex - b.readingOrderIndex);

    this.lines.set(lines);
    this.original.set(new Map(lines.map(line => [line.id, this.clone(line)])));
    this.regions.set(regions);
    this.originalOrder.set(regions.map(r => r.id));
    const firstRegion = regions[0]?.id;
    this.lineOrderRegionId.set(firstRegion ?? '');
    this.lineOrder.set(lines.filter(l => l.regionId === firstRegion).map(l => l.id));
    this.originalLineOrder.set([...this.lineOrder()]);
    this.history.set([]);
    this.changingStructure.set(false);
    if (this.adoptCurrentDisplayIfIdle()) return;
    this.loadHistory();
  }

  protected onImage(event: Event): void {
    if (!this.workspaceAvailable()) return;
    const img = event.target as HTMLImageElement;
    if (img.getAttribute('src') !== this.imageUrl()) return;
    const display = this.display();
    if (!display) return; // An unverified remote image has no editable source frame.
    if (img.naturalWidth !== display.displayWidth || img.naturalHeight !== display.displayHeight) {
      this.rasterLoaded.set(false); this.imageFailed.emit(); return;
    }
    this.rasterLoaded.set(true);
  }

  protected onImageError(event: Event): void {
    if (!this.workspaceAvailable()) return;
    if ((event.target as HTMLImageElement).getAttribute('src') !== this.imageUrl()) return;
    this.rasterLoaded.set(false); this.imageFailed.emit();
  }

  protected polygonPoints(line: { polygon: GeometryPoint[] }): string {
    return line.polygon.map(p => `${p.x},${p.y}`).join(' ');
  }

  protected baselinePoints(line: { baseline: GeometryPoint[] }): string {
    return line.baseline.map(p => `${p.x},${p.y}`).join(' ');
  }

  protected fitZoom(): void { if (this.workspaceAvailable()) this.zoom.set(1); }
  protected onOrderRegion(event: Event): void {
    const target = event.target as HTMLSelectElement;
    if (!this.workspaceAvailable()) { target.value = this.activeRegionId(); return; }
    this.selectOrderRegion(target.value);
    target.value = this.activeRegionId();
  }

  protected zoomBy(delta: number): void {
    if (!this.workspaceAvailable()) return;
    this.zoom.set(Math.min(4, Math.max(0.5, this.zoom() + delta)));
  }

  protected setMode(mode: 'move' | 'draw'): void {
    if (!this.workspaceAvailable()) return;
    this.mode.set(mode);
    this.drawRect.set(null);
    this.drawStart = null;
  }

  protected onSvgDown(event: PointerEvent): void {
    if (!this.workspaceCanEdit()) return;
    if (this.locked() || this.changingStructure() || this.newLineDraft() || this.mode() !== 'draw' || event.target !== event.currentTarget) return;
    const point = this.dragFor(event);
    if (!point) return;
    this.drawStart = { x: point.startX, y: point.startY };
    this.drawRect.set({ x: point.startX, y: point.startY, width: 0, height: 0 });
  }

  protected onDown(event: PointerEvent, line: GeometryLine): void {
    if (!this.workspaceAvailable()) return;
    if (this.changingStructure()) return;
    if (this.newLineDraft()) {
      this.selectedId.set(line.id); // Resolve other retained geometry without discarding the unsent box.
      if (this.locked()) this.lineSelected.emit(line.id);
      return;
    }
    if (this.mode() === 'draw') return;
    this.selectedId.set(line.id);
    if (this.locked()) {
      this.lineSelected.emit(line.id);
      return;
    }
    (event.target as Element).setPointerCapture?.(event.pointerId);
    this.pendingSnapshot = this.snapshot();
    const point = this.dragFor(event); if (!point) return;
    this.drag = { ...point, lineId: line.id, original: this.clone(line), mode: 'move', corner: -1 };
  }

  protected onResizeDown(event: PointerEvent, line: GeometryLine, corner: number): void {
    if (!this.workspaceCanEdit()) return;
    if (this.locked() || this.mode() === 'draw' || this.changingStructure() || this.newLineDraft()) return;
    event.stopPropagation();
    (event.target as Element).setPointerCapture?.(event.pointerId);
    this.selectedId.set(line.id);
    this.pendingSnapshot = this.snapshot();
    const point = this.dragFor(event); if (!point) return;
    this.drag = { ...point, lineId: line.id, original: this.clone(line), mode: 'resize', corner };
  }

  protected bboxCorners(line: GeometryLine): GeometryPoint[] {
    const b = this.bbox(line);
    return [
      { x: b.left, y: b.top }, { x: b.right, y: b.top },
      { x: b.right, y: b.bottom }, { x: b.left, y: b.bottom },
    ];
  }

  private bbox(line: GeometryLine): { left: number; top: number; right: number; bottom: number } {
    const xs = line.polygon.map(p => p.x), ys = line.polygon.map(p => p.y);
    return { left: Math.min(...xs), top: Math.min(...ys), right: Math.max(...xs), bottom: Math.max(...ys) };
  }

  private clone(line: GeometryLine): GeometryLine {
    return { ...line, polygon: line.polygon.map(p => ({ ...p })), baseline: line.baseline.map(p => ({ ...p })) };
  }

  private isChanged(line: GeometryLine): boolean {
    const original = this.original().get(line.id);
    const equal = (a: GeometryPoint[], b: GeometryPoint[]) =>
      a.length === b.length && a.every((point, index) => point.x === b[index].x && point.y === b[index].y);
    return !original || !equal(line.polygon, original.polygon) || !equal(line.baseline, original.baseline);
  }

  private dragFor(event: PointerEvent): { startX: number; startY: number } | null {
    const svg = (event.target as Element).closest('svg');
    if (!svg) return null;
    // The inverse screen transform accounts for CSS zoom and independent preview axes.
    if (svg.getScreenCTM) {
      const matrix = svg.getScreenCTM();
      if (!matrix) return null;
      try {
        const point = new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse());
        return Number.isFinite(point.x) && Number.isFinite(point.y) ? { startX: point.x, startY: point.y } : null;
      } catch { return null; }
    }
    // Deterministic geometry-only callers may supply an axis-aligned rectangle.
    const rect = svg.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0 ? {
      startX: ((event.clientX - rect.left) / rect.width) * this.width(),
      startY: ((event.clientY - rect.top) / rect.height) * this.height(),
    } : null;
  }

  protected onMove(event: PointerEvent): void {
    if (!this.workspaceCanEdit()) return;
    if (this.locked() || this.changingStructure()) return;
    if (this.drawStart) {
      const point = this.dragFor(event);
      if (!point) return;
      const x = Math.min(this.drawStart.x, point.startX);
      const y = Math.min(this.drawStart.y, point.startY);
      this.drawRect.set({
        x, y,
        width: Math.abs(point.startX - this.drawStart.x),
        height: Math.abs(point.startY - this.drawStart.y),
      });
      return;
    }
    if (!this.drag) return;
    const svg = (event.target as Element).closest('svg');
    if (!svg) return;
    const point = this.dragFor(event); if (!point) return;
    const x = point.startX;
    const y = point.startY;
    const dx = x - this.drag.startX;
    const dy = y - this.drag.startY;

    this.lines.update(lines => lines.map(l => {
      if (l.id !== this.drag!.lineId) return l;
      if (this.drag!.mode === 'move')
      {
        return {
          ...l,
          polygon: this.drag!.original.polygon.map(p => ({ x: p.x + dx, y: p.y + dy })),
          baseline: this.drag!.original.baseline.map(p => ({ x: p.x + dx, y: p.y + dy })),
        };
      }

      // Resize: drag one corner; the opposite corner stays fixed.
      const corner = this.drag!.corner;
      const corners = this.bboxCorners(this.drag!.original);
      const moving = corners[corner];
      const opposite = corners[(corner + 2) % corners.length];
      const resizeAxis = (position: number, fixed: number, onLowSide: boolean, frameEnd: number) => {
        const limit = Math.max(0, Math.min(frameEnd, fixed + (onLowSide ? -4 : 4)));
        return onLowSide ? Math.max(0, Math.min(position, limit)) : Math.min(frameEnd, Math.max(position, limit));
      };
      const nx = resizeAxis(moving.x + dx, opposite.x, corner === 0 || corner === 3, this.width() - 1);
      const ny = resizeAxis(moving.y + dy, opposite.y, corner === 0 || corner === 1, this.height() - 1);
      const left = Math.min(opposite.x, nx), right = Math.max(opposite.x, nx);
      const top = Math.min(opposite.y, ny), bottom = Math.max(opposite.y, ny);
      return {
        ...l,
        polygon: [
          { x: left, y: top }, { x: right, y: top },
          { x: right, y: bottom }, { x: left, y: bottom },
        ],
        baseline: [{ x: left, y: bottom }, { x: right, y: bottom }],
      };
    }));
  }

  protected onUp(): void {
    if (!this.workspaceCanEdit()) return;
    if (this.drawStart) {
      const rect = this.drawRect();
      this.drawStart = null;
      this.drawRect.set(null);
      if (rect && rect.width >= 4 && rect.height >= 4) this.createLine(rect);
      return;
    }
    if (this.drag && this.pendingSnapshot
        && JSON.stringify(this.pendingSnapshot) !== JSON.stringify(this.snapshot()))
      this.history.update(stack => [...stack, this.pendingSnapshot!]);
    this.pendingSnapshot = null;
    this.drag = null;
  }

  private createLine(rect: { x: number; y: number; width: number; height: number }, retainOnly = false): void {
    if (!retainOnly && !this.workspaceCanEdit()) return;
    if (this.newLineDraft() || !retainOnly && (this.locked() || this.changingStructure() || this.discarding())) return;
    const right = rect.x + rect.width;
    const bottom = rect.y + rect.height;
    const polygon = [
      { x: rect.x, y: rect.y }, { x: right, y: rect.y },
      { x: right, y: bottom }, { x: rect.x, y: bottom },
    ];
    const baseline = [{ x: rect.x, y: bottom }, { x: right, y: bottom }];
    const placing = this.placingLineId();

    if (placing) {
      const line = this.lines().find(candidate => candidate.id === placing);
      if (!line) return;
      const draft = { ...line, polygon, baseline };
      this.lines.update(lines => lines.map(candidate => candidate.id === placing ? draft : candidate));
      this.selectedId.set(placing);
      if (retainOnly) this.actionStatus.set('The preview refreshed while drawing. Your placement box is retained in its original source frame and has not been submitted.');
      else this.submitGeometry(draft);
      return;
    }

    const regionId = this.regions().find(region => region.id === this.activeRegionId())?.id ?? this.regions()[0]?.id;
    const display = this.workingDisplay();
    if (!display || !retainOnly && !this.canCreateNewLine()) return;
    if (!retainOnly && !this.beginStructureChange()) return;
    const draft = { display, polygon, baseline, expectedLineIds: [...this.loadedSurfaceLineIds],
      expectedRegionIds: [...this.loadedSurfaceRegionIds], regionId };
    this.newLineDraft.set(draft);
    if (retainOnly) this.actionStatus.set('The preview refreshed while drawing. Your new line box is retained in its original source frame and has not been submitted.');
    else this.submitNewLine(draft);
  }

  protected retryNewLine(): void {
    if (!this.workspaceCanEdit()) return;
    if (this.unknownOutcome()) { this.actionStatus.set('A previous write is unconfirmed. Its submitted request and draft are retained pending authoritative reconciliation.'); return; }
    if (this.uncertainStructure()) { this.actionStatus.set('The refused structural request is retained. Load current geometry before discarding or retrying.'); return; }
    if (!this.canRetryNewLine() || this.structureBlocked()) return;
    this.changingStructure.set(true);
    this.submitNewLine(this.newLineDraft()!);
  }

  protected discardNewLine(): void {
    if (!this.workspaceAvailable()) return;
    if (this.changingStructure() || this.discarding() || this.unknownOutcome() || this.uncertainStructure()) return;
    this.newLineDraft.set(null);
    this.actionStatus.set('new line box discarded');
    this.adoptCurrentDisplayIfIdle();
  }

  private submitNewLine(draft: NewLineDraft): void {
    if (!this.workspaceCanEdit() || this.unknownOutcome()) return;
    const generation = this.loadGeneration;
    this.pendingStructureWrite = { kind: 'addCaptureLine', draft: JSON.parse(JSON.stringify(draft)) };
    this.api.addCaptureLine(draft.display, draft.polygon, draft.baseline, draft.expectedLineIds, draft.expectedRegionIds, draft.regionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          if (generation !== this.loadGeneration) return;
          this.pendingStructureWrite = null; this.uncertainStructure.set(false);
          this.newLineDraft.set(null);
          this.actionStatus.set('line added');
          this.reload();
        },
        error: error => this.structureError(generation, 'Could not confirm the new line. Your box is retained; check the current surface before retrying.', error),
      });
  }

  protected splitSelected(): void {
    if (!this.workspaceCanEdit()) return;
    const line = this.lines().find(l => l.id === this.selectedId());
    if (!line || line.polygon.length === 0) return;
    const ys = line.polygon.map(p => p.y);
    const middle = (Math.min(...ys) + Math.max(...ys)) / 2;
    if (!this.beginStructureChange()) return;
    const generation = this.loadGeneration;
    this.pendingStructureWrite = { kind: 'splitLine', line: this.clone(line), middle, projectId: this.projectId(), surfaceId: this.surfaceId() };
    this.api.splitLine(line.id, middle).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.pendingStructureWrite = null; this.uncertainStructure.set(false);
          this.actionStatus.set('line split');
        this.selectedId.set(null);
        this.reload();
      },
      error: error => this.structureError(generation, 'split refused (line has transcribed text)', error),
    });
  }

  protected mergeSelectedWithNext(): void {
    if (!this.workspaceCanEdit()) return;
    const selected = this.selectedId();
    if (!selected) return;
    const list = this.lines();
    const index = list.findIndex(l => l.id === selected);
    if (index < 0) return;
    const next = list.slice(index + 1).find(l => l.regionId === list[index].regionId);
    if (!next) {
      this.actionStatus.set('no following line in this region to merge');
      return;
    }
    if (!this.beginStructureChange()) return;
    const generation = this.loadGeneration;
    this.pendingStructureWrite = { kind: 'mergeLines', first: selected, second: next.id, projectId: this.projectId(), surfaceId: this.surfaceId() };
    this.api.mergeLines(selected, next.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.pendingStructureWrite = null; this.uncertainStructure.set(false);
          this.actionStatus.set('lines merged');
        this.selectedId.set(null);
        this.reload();
      },
      error: error => this.structureError(generation, 'merge refused (transcribed text or different region)', error),
    });
  }

  protected deleteSelected(): void {
    if (!this.workspaceCanEdit()) return;
    const id = this.selectedId();
    if (!id) return;
    if (!this.beginStructureChange()) return;
    const generation = this.loadGeneration;
    this.pendingStructureWrite = { kind: 'deleteLine', lineId: id, projectId: this.projectId(), surfaceId: this.surfaceId() };
    this.api.deleteLine(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.pendingStructureWrite = null; this.uncertainStructure.set(false);
          this.actionStatus.set('line deleted');
        this.selectedId.set(null);
        this.reload();
      },
      error: error => this.structureError(generation, 'deletion refused (line has transcribed text)', error),
    });
  }

  private reload(): void {
    const generation = ++this.loadGeneration;
    this.savingGeometry.set(false);
    this.savingOrder.set(false);
    this.savingLineOrder.set(false);
    this.api.loadProjectFresh(this.projectId()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: payload => { if (generation === this.loadGeneration) this.loadFrom(payload); },
      error: () => this.structureError(generation, 'could not reload geometry'),
    });
  }

  private beginStructureChange(): boolean {
    if (!this.workspaceCanEdit() || this.unknownOutcome() || this.structureBlocked() || this.uncertainStructure() || this.locked() || this.changingStructure()) return false;
    if (this.dirty() || this.drawStart !== null || this.savingGeometry() || this.orderDirty() || this.lineOrderDirty()
      || this.savingOrder() || this.savingLineOrder()) {
      this.actionStatus.set('Save geometry and order drafts before adding, splitting, merging, or deleting a line.');
      return false;
    }
    this.changingStructure.set(true);
    this.drag = null;
    this.pendingSnapshot = null;
    return true;
  }

  private retainUnknownOutcome(error: unknown): void {
    const status = typeof error === 'object' && error !== null && 'status' in error ? (error as { status: unknown }).status : undefined;
    // Only documented precommit route refusals prove non-publication. Reads,
    // cancellation, timeouts, parse failures and server errors cannot do so.
    if (![400, 409, 412, 422].includes(typeof status === 'number' ? status : 0)) this.unknownOutcome.set(true);
  }

  private structureError(generation: number, message: string, error?: unknown): void {
    if (generation !== this.loadGeneration) return;
    if (this.pendingStructureWrite) { this.retainUnknownOutcome(error); this.unresolvedWrites.push(this.pendingStructureWrite); this.uncertainStructure.set(true); this.pendingStructureWrite = null; }
    this.changingStructure.set(false);
    this.actionStatus.set(message);
  }

  protected undoGeometry(): void {
    if (!this.workspaceAvailable()) return;
    if (this.changingStructure() || this.discarding()) return;
    const stack = this.history();
    if (stack.length === 0) return;
    this.lines.set(stack[stack.length - 1].map(l => this.clone(l)));
    this.history.set(stack.slice(0, -1));
  }

  private snapshot(): GeometryLine[] {
    return this.lines().map(l => this.clone(l));
  }

  protected saveGeometry(): void {
    if (!this.workspaceCanEdit()) return;
    const line = this.lines().find(l => l.id === this.selectedId());
    if (line) this.submitGeometry(line);
  }

  private submitGeometry(line: GeometryLine): void {
    if (!this.workspaceCanEdit()) return;
    const display = this.workingDisplay();
    if (!display || this.unknownOutcome() || this.locked() || this.savingGeometry() || this.savingOrder() || this.savingLineOrder() || this.changingStructure()
      || !this.isChanged(line) && !this.unconfirmedGeometry().has(line.id)) return;
    const original = this.original().get(line.id);
    if (!original) return;
    const submitted = this.clone(line);
    const { polygon, baseline } = this.clone(original);
    const expectedGeometry = { polygon, baseline };
    const generation = this.loadGeneration;
    const wasUnplaced = original.polygon.length === 0 && original.baseline.length === 0;
    this.pendingGeometryWrite = { line: this.clone(submitted), expected: { polygon, baseline }, display: { ...display } };
    this.savingGeometry.set(true);
    this.actionStatus.set('');
    this.api.updateCaptureLineGeometry(display, line.id, submitted.polygon, submitted.baseline, expectedGeometry)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.savingGeometry.set(false); this.pendingGeometryWrite = null;
        this.original.update(lines => new Map(lines).set(submitted.id, submitted));
        this.unconfirmedGeometry.update(lines => { const next = new Set(lines); next.delete(submitted.id); return next; });
        if (wasUnplaced) {
          if (this.placingLineId() === submitted.id) this.placingLineId.set(null);
          this.actionStatus.set('text line placed');
          this.loadHistory();
        }
        this.adoptCurrentDisplayIfIdle();
      },
      error: error => {
        if (generation !== this.loadGeneration) return;
        this.retainUnknownOutcome(error);
        if (this.pendingGeometryWrite) this.unresolvedWrites.push(this.pendingGeometryWrite);
        this.pendingGeometryWrite = null; this.savingGeometry.set(false);
        this.unconfirmedGeometry.update(lines => new Set(lines).add(submitted.id));
        this.actionStatus.set(error?.status === 409
          ? 'Line geometry changed on the server. Your draft is still here.'
          : error?.status === 412 ? 'The source image changed. Your draft remains in its original frame; refresh the preview and explicitly discard or reconcile it.'
          : error?.status === 422 ? 'The image assignment or source bounds need correction. Your draft is retained; check the registered line and region before saving.'
          : 'The geometry save is unconfirmed. Your draft is retained; await authoritative reconciliation before retrying or discarding.');
      },
    });
  }

  protected reset(): void {
    if (!this.workspaceAvailable()) return;
    if (this.unknownOutcome()) { this.actionStatus.set('A write outcome is unconfirmed. Your draft and submitted request are retained; a fresh read cannot authorize retry or discard.'); return; }
    if (this.changingStructure() || this.savingGeometry() || this.savingOrder() || this.savingLineOrder() || this.discarding()) return;
    const display = this.display();
    if (this.unconfirmedGeometry().size > 0 || this.uncertainOrder() || this.uncertainLineOrder() || this.uncertainStructure() || this.validDisplay(display) && !this.sameDisplay(this.workingDisplay(), display)) {
      this.discardAfterFreshRead(); return;
    }
    this.newLineDraft.set(null);
    this.lines.set([...this.original().values()].map(l => this.clone(l)));
    this.history.set([]);
    const ranks = new Map(this.originalOrder().map((id, index) => [id, index]));
    this.regions.update(regions => [...regions].sort((a, b) => (ranks.get(a.id) ?? 0) - (ranks.get(b.id) ?? 0)));
    this.lineOrder.set([...this.originalLineOrder()]);
    this.drawStart = null; this.drawRect.set(null); this.drag = null; this.pendingSnapshot = null;
  }

  private discardAfterFreshRead(): void {
    const display = this.display();
    if (!this.validDisplay(display)) {
      this.actionStatus.set('Restore access to the capture preview before discarding an unconfirmed save. Your draft is retained.'); return;
    }
    const target = { ...display };
    const generation = ++this.discardGeneration;
    const load = this.loadGeneration;
    this.discarding.set(true);
    const current = () => generation === this.discardGeneration && load === this.loadGeneration;
    this.api.loadProjectFresh(target.projectId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: payload => {
        if (!current()) return;
        const surfaces = Array.isArray(payload?.objects)
          ? payload.objects.flatMap(object => Array.isArray(object?.surfaces) ? object.surfaces : []).filter(surface => surface?.id === target.surfaceId) : [];
        const surface = surfaces[0];
        if (this.display() !== display || !this.sameDisplay(target, this.display()) || target.projectId !== this.projectId() || target.surfaceId !== this.surfaceId()
          || payload.id !== target.projectId || surfaces.length !== 1 || !Array.isArray(surface?.lines)
          || !Array.isArray(surface?.regions) || !Array.isArray(surface?.captures)
          || surface.lines.some(line => !line || typeof line.id !== 'string' || !Array.isArray(line.polygon ?? []) || !Array.isArray(line.baseline ?? []))
          || surface.regions.some(region => !region || typeof region.id !== 'string')
          || surface.captures.filter(capture => capture?.id === target.captureId).length !== 1) {
          this.discarding.set(false);
          this.actionStatus.set('The current source or geometry could not be confirmed. Your draft is retained.'); return;
        }
        this.discarding.set(false);
        this.adoptDisplay(target); this.clearFrameView(); ++this.loadGeneration; this.loadFrom(payload);
        this.actionStatus.set('Drafts discarded after loading current geometry.');
      },
      error: () => {
        if (!current()) return;
        this.discarding.set(false);
        this.actionStatus.set('Could not load current geometry. Your draft and unconfirmed save are retained.');
      },
    });
  }

  protected move(regionId: string, delta: number): void {
    if (!this.workspaceAvailable()) return;
    if (this.changingStructure() || this.discarding()) return;
    const list = [...this.regions()];
    const index = list.findIndex(r => r.id === regionId);
    const target = index + delta;
    if (index < 0 || target < 0 || target >= list.length) return;
    [list[index], list[target]] = [list[target], list[index]];
    this.regions.set(list);
  }

  protected saveOrder(): void {
    if (!this.workspaceCanEdit()) return;
    const display = this.display();
    if (!display || this.unknownOutcome() || this.locked() || this.savingGeometry() || this.savingOrder() || this.savingLineOrder() || this.changingStructure() || this.newLineDraft() || !this.orderDirty() && !this.uncertainOrder()) return;
    const submitted = this.regions().map(region => region.id);
    const expected = [...this.originalOrder()];
    const expectedFull = [...this.loadedSurfaceRegionOrder];
    const generation = this.loadGeneration;
    this.pendingOrderWrite = { display: { ...display }, submitted: [...submitted], expected: [...expectedFull] };
    this.savingOrder.set(true);
    this.api.updateCaptureReadingOrder(display, submitted, expectedFull).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.savingOrder.set(false); this.pendingOrderWrite = null; this.uncertainOrder.set(false);
        this.originalOrder.set(submitted);
        this.loadedSurfaceRegionOrder = this.replaceSlots(expectedFull, expected, submitted);
        this.actionStatus.set('reading order saved');
      },
      error: (error: { status?: number }) => {
        if (generation !== this.loadGeneration) return;
        this.retainUnknownOutcome(error);
        if (this.pendingOrderWrite) this.unresolvedWrites.push(this.pendingOrderWrite);
        this.pendingOrderWrite = null; this.savingOrder.set(false); this.uncertainOrder.set(true);
        this.actionStatus.set(error?.status === 409
          ? 'Reading order changed elsewhere. Your draft is retained; reload and reconcile before saving.' : 'Reading order save failed; your draft is retained.');
      },
    });
  }

  protected activeRegionId(): string {
    return this.lineOrderRegionId();
  }

  protected selectOrderRegion(regionId: string): void {
    if (!this.workspaceAvailable()) return;
    if (this.unknownOutcome() || this.discarding() || this.lineOrderDirty() || this.savingLineOrder() || this.changingStructure()
      || !this.regions().some(region => region.id === regionId)) return;
    this.lineOrderRegionId.set(regionId);
    const lines = this.lines().filter(line => line.regionId === regionId).map(line => line.id);
    this.lineOrder.set(lines); this.originalLineOrder.set([...lines]);
  }

  private replaceSlots(full: string[], selected: string[], replacement: string[]): string[] {
    const targets = new Set(selected); let index = 0;
    return full.map(id => targets.has(id) ? replacement[index++] : id);
  }

  protected orderedLines(): { id: string }[] {
    const ids = this.lineOrder();
    const byId = new Map(this.lines().map(l => [l.id, l]));
    return ids.filter(id => byId.has(id)).map(id => ({ id }));
  }

  protected moveLine(lineId: string, delta: number): void {
    if (!this.workspaceAvailable()) return;
    if (this.changingStructure() || this.discarding()) return;
    const list = [...this.lineOrder()];
    const index = list.indexOf(lineId);
    const target = index + delta;
    if (index < 0 || target < 0 || target >= list.length) return;
    [list[index], list[target]] = [list[target], list[index]];
    this.lineOrder.set(list);
  }

  protected saveLineOrder(): void {
    if (!this.workspaceCanEdit()) return;
    const display = this.display();
    if (!display || this.unknownOutcome() || this.locked() || this.savingGeometry() || this.savingOrder() || this.savingLineOrder() || this.changingStructure() || this.newLineDraft() || !this.lineOrderDirty() && !this.uncertainLineOrder()) return;
    const submitted = [...this.lineOrder()];
    const expected = [...this.originalLineOrder()];
    const regionId = this.activeRegionId();
    const expectedFull = [...(this.loadedRegionLineIds.get(regionId) ?? [])];
    const generation = this.loadGeneration;
    this.pendingLineOrderWrite = { display: { ...display }, regionId, submitted: [...submitted], expected: [...expectedFull] };
    this.savingLineOrder.set(true);
    this.api.updateCaptureLineOrder(display, regionId, submitted, expectedFull).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.savingLineOrder.set(false); this.pendingLineOrderWrite = null; this.uncertainLineOrder.set(false);
        this.originalLineOrder.set(submitted);
        const full = this.replaceSlots(expectedFull, expected, submitted);
        this.loadedRegionLineIds.set(regionId, full);
        this.loadedSurfaceLineIds = this.replaceSlots(this.loadedSurfaceLineIds, expectedFull, full);
        this.lines.update(lines => {
          const byId = new Map(lines.map(line => [line.id, line]));
          const ids = this.replaceSlots(lines.map(line => line.id), expected, submitted);
          return ids.map(id => byId.get(id)!);
        });
        this.actionStatus.set('line order saved');
      },
      error: (error: { status?: number }) => {
        if (generation !== this.loadGeneration) return;
        this.retainUnknownOutcome(error);
        if (this.pendingLineOrderWrite) this.unresolvedWrites.push(this.pendingLineOrderWrite);
        this.pendingLineOrderWrite = null; this.savingLineOrder.set(false); this.uncertainLineOrder.set(true);
        this.actionStatus.set(error?.status === 409
          ? 'Line order changed elsewhere. Your draft is retained; reload and reconcile before saving.' : 'Line order save failed; your draft is retained.');
      },
    });
  }
}
