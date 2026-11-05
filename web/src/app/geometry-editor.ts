/**
 * Geometry editor (todo Phase 2 gate: geometry/order correction; manual layout when
 * automatic layout fails). Renders the original capture with polygon/baseline overlays
 * in the image's natural coordinate frame; drags translate the selected line; region
 * order is editable with up/down; saves go through the geometry API.
 */
import { Component, computed, DestroyRef, inject, input, OnChanges, output, signal, SimpleChanges } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiClient, GeometryLine, GeometryPoint, GeometryRegion, ProjectPayload } from './api-client';

interface DragState {
  lineId: string;
  startX: number;
  startY: number;
  original: GeometryLine;
  mode: 'move' | 'resize';
  corner: number; // 0=TL, 1=TR, 2=BR, 3=BL
}

@Component({
  selector: 'app-geometry-editor',
  standalone: true,
  template: `
    <section>
      <h3>Geometry <small>(image frame: {{ width() }}×{{ height() }})</small></h3>
      @if (readOnly()) {
        <p class="hint">Page view: click a line box to open it. Enable edit mode to move/draw/resize.</p>
      } @else {
        <p class="hint">Click a line, drag to move it, then save. Region order controls reading order.</p>
      }
      <p class="tools">
        <button type="button" aria-label="Zoom out" (click)="zoomBy(-0.25)">−</button>
        <button type="button" aria-label="Fit page" (click)="zoom.set(1)">Fit</button>
        <button type="button" aria-label="Zoom in" (click)="zoomBy(0.25)">+</button>
        <span class="hint">{{ (zoom() * 100).toFixed(0) }}%</span>
      </p>

      <div class="scroll">
      <div class="canvas" [style.width.%]="zoom() * 100">
        <img [src]="imageUrl()" (load)="onImage($event)" alt="surface capture" />
        <svg
          [attr.viewBox]="'0 0 ' + width() + ' ' + height()"
          preserveAspectRatio="xMidYMid meet"
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
          @for (line of lines(); track line.id) {
            <g [attr.data-id]="line.id">
              <polygon
                [attr.points]="polygonPoints(line)"
                [attr.class]="line.id === selectedId() ? 'sel' : ''"
                (pointerdown)="onDown($event, line)" />
              @if (line.baseline.length > 1) {
                <polyline [attr.points]="baselinePoints(line)" class="baseline" />
              }
              @if (line.id === selectedId() && !readOnly()) {
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
      </div>
      </div>

      @if (!readOnly()) {
      <div class="tools">
        <button type="button" (click)="setMode('move')" [disabled]="mode() === 'move'">Move / resize</button>
        <button type="button" (click)="setMode('draw')" [disabled]="mode() === 'draw'">Draw new line</button>
        <button type="button" (click)="saveGeometry()" [disabled]="!selectedDirty() || savingGeometry() || changingStructure()">Save geometry</button>
        <button type="button" (click)="undoGeometry()" [disabled]="history().length === 0 || changingStructure()">Undo edit</button>
        <button type="button" (click)="splitSelected()" [disabled]="!selectedId() || changingStructure()">Split line</button>
        <button type="button" (click)="mergeSelectedWithNext()" [disabled]="!selectedId() || changingStructure()">Merge with next</button>
        <button type="button" (click)="deleteSelected()" [disabled]="!selectedId() || changingStructure()">Delete line</button>
        <button type="button" (click)="reset()" [disabled]="!dirty() || changingStructure()">Reset</button>
        <span class="hint" role="status">{{ dirty() ? 'unsaved geometry' : 'saved' }} {{ actionStatus() }}</span>
      </div>
      }

      @if (!readOnly() && unplaced().length > 0) {
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

      @if (!readOnly()) {
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
      <button type="button" (click)="saveOrder()" [disabled]="!orderDirty() || savingOrder() || changingStructure()">Save reading order</button>

      <h4>Line order <small>(region {{ activeRegionId() }})</small></h4>
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
      <button type="button" (click)="saveLineOrder()" [disabled]="!lineOrderDirty() || savingLineOrder() || changingStructure()">Save line order</button>

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
  lineSelected = output<string>();

  protected readonly lines = signal<GeometryLine[]>([]);
  protected readonly regions = signal<GeometryRegion[]>([]);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly width = signal(1);
  protected readonly height = signal(1);
  protected readonly dirty = computed(() => this.lines().some(line => this.isChanged(line)));
  protected readonly selectedDirty = computed(() => {
    const selected = this.lines().find(line => line.id === this.selectedId());
    return selected !== undefined && this.isChanged(selected);
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
  private readonly destroyRef = inject(DestroyRef);

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['projectId'] && !changes['surfaceId']) return;
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
    this.reload();
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
      next: lines => { if (current()) this.unplaced.set(lines); },
      error: () => { if (current()) this.unplaced.set([]); },
    });
  }

  protected selectUnplaced(lineId: string): void {
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
    const lines = surface.lines.map(l => ({
      id: l.id,
      regionId: (l as { regionId?: string }).regionId ?? '',
      polygon: ((l as { polygon?: GeometryPoint[] }).polygon ?? []).map(p => ({ ...p })),
      baseline: ((l as { baseline?: GeometryPoint[] }).baseline ?? []).map(p => ({ ...p })),
    }));
    const regions = surface.regions.map(r => ({
      id: r.id,
      readingOrderIndex: (r as { readingOrderIndex?: number }).readingOrderIndex ?? null,
    })).sort((a, b) => (a.readingOrderIndex ?? 0) - (b.readingOrderIndex ?? 0));

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
    this.loadHistory();
  }

  protected onImage(event: Event): void {
    const img = event.target as HTMLImageElement;
    this.width.set(img.naturalWidth || 1);
    this.height.set(img.naturalHeight || 1);
  }

  protected polygonPoints(line: { polygon: GeometryPoint[] }): string {
    return line.polygon.map(p => `${p.x},${p.y}`).join(' ');
  }

  protected baselinePoints(line: { baseline: GeometryPoint[] }): string {
    return line.baseline.map(p => `${p.x},${p.y}`).join(' ');
  }

  protected zoomBy(delta: number): void {
    this.zoom.set(Math.min(4, Math.max(0.5, this.zoom() + delta)));
  }

  protected setMode(mode: 'move' | 'draw'): void {
    this.mode.set(mode);
    this.drawRect.set(null);
    this.drawStart = null;
  }

  protected onSvgDown(event: PointerEvent): void {
    if (this.readOnly() || this.changingStructure() || this.mode() !== 'draw' || event.target !== event.currentTarget) return;
    const point = this.dragFor(event);
    this.drawStart = { x: point.startX, y: point.startY };
    this.drawRect.set({ x: point.startX, y: point.startY, width: 0, height: 0 });
  }

  protected onDown(event: PointerEvent, line: GeometryLine): void {
    if (this.mode() === 'draw' || this.changingStructure()) return;
    this.selectedId.set(line.id);
    if (this.readOnly()) {
      this.lineSelected.emit(line.id);
      return;
    }
    (event.target as Element).setPointerCapture?.(event.pointerId);
    this.pendingSnapshot = this.snapshot();
    this.drag = { ...this.dragFor(event), lineId: line.id, original: this.clone(line), mode: 'move', corner: -1 };
  }

  protected onResizeDown(event: PointerEvent, line: GeometryLine, corner: number): void {
    if (this.mode() === 'draw' || this.changingStructure()) return;
    event.stopPropagation();
    (event.target as Element).setPointerCapture?.(event.pointerId);
    this.selectedId.set(line.id);
    this.pendingSnapshot = this.snapshot();
    this.drag = { ...this.dragFor(event), lineId: line.id, original: this.clone(line), mode: 'resize', corner };
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

  private dragFor(event: PointerEvent): { startX: number; startY: number } {
    const svg = (event.target as Element).closest('svg')!;
    const rect = svg.getBoundingClientRect();
    return {
      startX: ((event.clientX - rect.left) / rect.width) * this.width(),
      startY: ((event.clientY - rect.top) / rect.height) * this.height(),
    };
  }

  protected onMove(event: PointerEvent): void {
    if (this.readOnly() || this.changingStructure()) return;
    if (this.drawStart) {
      const point = this.dragFor(event);
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
    const rect = svg.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * this.width();
    const y = ((event.clientY - rect.top) / rect.height) * this.height();
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

  private createLine(rect: { x: number; y: number; width: number; height: number }): void {
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
      this.submitGeometry(draft);
      return;
    }

    if (!this.beginStructureChange()) return;
    const generation = this.loadGeneration;
    this.api.addLine(this.surfaceId(), polygon, baseline).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          if (generation !== this.loadGeneration) return;
          this.actionStatus.set('line added');
          this.reload();
        },
        error: () => this.structureError(generation, 'could not add line'),
      });
  }

  protected splitSelected(): void {
    const line = this.lines().find(l => l.id === this.selectedId());
    if (!line || line.polygon.length === 0) return;
    const ys = line.polygon.map(p => p.y);
    const middle = (Math.min(...ys) + Math.max(...ys)) / 2;
    if (!this.beginStructureChange()) return;
    const generation = this.loadGeneration;
    this.api.splitLine(line.id, middle).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.actionStatus.set('line split');
        this.selectedId.set(null);
        this.reload();
      },
      error: () => this.structureError(generation, 'split refused (line has transcribed text)'),
    });
  }

  protected mergeSelectedWithNext(): void {
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
    this.api.mergeLines(selected, next.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.actionStatus.set('lines merged');
        this.selectedId.set(null);
        this.reload();
      },
      error: () => this.structureError(generation, 'merge refused (transcribed text or different region)'),
    });
  }

  protected deleteSelected(): void {
    const id = this.selectedId();
    if (!id) return;
    if (!this.beginStructureChange()) return;
    const generation = this.loadGeneration;
    this.api.deleteLine(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.actionStatus.set('line deleted');
        this.selectedId.set(null);
        this.reload();
      },
      error: () => this.structureError(generation, 'deletion refused (line has transcribed text)'),
    });
  }

  private reload(): void {
    const generation = ++this.loadGeneration;
    this.savingGeometry.set(false);
    this.savingOrder.set(false);
    this.savingLineOrder.set(false);
    this.api.loadProject(this.projectId()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: payload => { if (generation === this.loadGeneration) this.loadFrom(payload); },
      error: () => this.structureError(generation, 'could not reload geometry'),
    });
  }

  private beginStructureChange(): boolean {
    if (this.readOnly() || this.changingStructure()) return false;
    if (this.dirty() || this.savingGeometry() || this.orderDirty() || this.lineOrderDirty()
      || this.savingOrder() || this.savingLineOrder()) {
      this.actionStatus.set('Save geometry and order drafts before adding, splitting, merging, or deleting a line.');
      return false;
    }
    this.changingStructure.set(true);
    this.drag = null;
    this.pendingSnapshot = null;
    return true;
  }

  private structureError(generation: number, message: string): void {
    if (generation !== this.loadGeneration) return;
    this.changingStructure.set(false);
    this.actionStatus.set(message);
  }

  protected undoGeometry(): void {
    if (this.changingStructure()) return;
    const stack = this.history();
    if (stack.length === 0) return;
    this.lines.set(stack[stack.length - 1].map(l => this.clone(l)));
    this.history.set(stack.slice(0, -1));
  }

  private snapshot(): GeometryLine[] {
    return this.lines().map(l => this.clone(l));
  }

  protected saveGeometry(): void {
    const line = this.lines().find(l => l.id === this.selectedId());
    if (line) this.submitGeometry(line);
  }

  private submitGeometry(line: GeometryLine): void {
    if (this.readOnly() || this.savingGeometry() || this.changingStructure() || !this.isChanged(line)) return;
    const original = this.original().get(line.id);
    if (!original) return;
    const submitted = this.clone(line);
    const { polygon, baseline } = this.clone(original);
    const expectedGeometry = { polygon, baseline };
    const generation = this.loadGeneration;
    const wasUnplaced = original.polygon.length === 0 && original.baseline.length === 0;
    this.savingGeometry.set(true);
    this.actionStatus.set('');
    this.api.updateLineGeometry(line.id, submitted.polygon, submitted.baseline, expectedGeometry)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.savingGeometry.set(false);
        this.original.update(lines => new Map(lines).set(submitted.id, submitted));
        if (wasUnplaced) {
          if (this.placingLineId() === submitted.id) this.placingLineId.set(null);
          this.actionStatus.set('text line placed');
          this.loadHistory();
        }
      },
      error: error => {
        if (generation !== this.loadGeneration) return;
        this.savingGeometry.set(false);
        this.actionStatus.set(error.status === 409
          ? 'Line geometry changed on the server. Your draft is still here.'
          : 'Could not save geometry. Your draft is still here.');
      },
    });
  }

  protected reset(): void {
    if (this.changingStructure()) return;
    this.lines.set([...this.original().values()].map(l => this.clone(l)));
    this.history.set([]);
  }

  protected move(regionId: string, delta: number): void {
    if (this.changingStructure()) return;
    const list = [...this.regions()];
    const index = list.findIndex(r => r.id === regionId);
    const target = index + delta;
    if (index < 0 || target < 0 || target >= list.length) return;
    [list[index], list[target]] = [list[target], list[index]];
    this.regions.set(list);
  }

  protected saveOrder(): void {
    if (this.readOnly() || this.savingOrder() || this.changingStructure() || !this.orderDirty()) return;
    const submitted = this.regions().map(region => region.id);
    const generation = this.loadGeneration;
    this.savingOrder.set(true);
    this.api.updateReadingOrder(this.surfaceId(), submitted).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.savingOrder.set(false);
        this.originalOrder.set(submitted);
      },
      error: () => { if (generation === this.loadGeneration) this.savingOrder.set(false); },
    });
  }

  protected activeRegionId(): string {
    return this.lineOrderRegionId();
  }

  protected orderedLines(): { id: string }[] {
    const ids = this.lineOrder();
    const byId = new Map(this.lines().map(l => [l.id, l]));
    return ids.filter(id => byId.has(id)).map(id => ({ id }));
  }

  protected moveLine(lineId: string, delta: number): void {
    if (this.changingStructure()) return;
    const list = [...this.lineOrder()];
    const index = list.indexOf(lineId);
    const target = index + delta;
    if (index < 0 || target < 0 || target >= list.length) return;
    [list[index], list[target]] = [list[target], list[index]];
    this.lineOrder.set(list);
  }

  protected saveLineOrder(): void {
    if (this.readOnly() || this.savingLineOrder() || this.changingStructure() || !this.lineOrderDirty()) return;
    const submitted = [...this.lineOrder()];
    const generation = this.loadGeneration;
    this.savingLineOrder.set(true);
    this.api.updateLineOrder(this.activeRegionId(), submitted).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (generation !== this.loadGeneration) return;
        this.savingLineOrder.set(false);
        this.originalLineOrder.set(submitted);
      },
      error: () => { if (generation === this.loadGeneration) this.savingLineOrder.set(false); },
    });
  }
}
