/**
 * Geometry editor (todo Phase 2 gate: geometry/order correction; manual layout when
 * automatic layout fails). Renders the original capture with polygon/baseline overlays
 * in the image's natural coordinate frame; drags translate the selected line; region
 * order is editable with up/down; saves go through the geometry API.
 */
import { Component, input, output, signal, OnInit } from '@angular/core';
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
        <button type="button" (click)="zoomBy(-0.25)">−</button>
        <button type="button" (click)="zoom.set(1)">Fit</button>
        <button type="button" (click)="zoomBy(0.25)">+</button>
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
        <button type="button" (click)="saveGeometry()" [disabled]="!dirty()">Save geometry</button>
        <button type="button" (click)="undoGeometry()" [disabled]="history().length === 0">Undo edit</button>
        <button type="button" (click)="splitSelected()" [disabled]="!selectedId()">Split line</button>
        <button type="button" (click)="mergeSelectedWithNext()" [disabled]="!selectedId()">Merge with next</button>
        <button type="button" (click)="deleteSelected()" [disabled]="!selectedId()">Delete line</button>
        <button type="button" (click)="reset()" [disabled]="!dirty()">Reset</button>
        <span class="hint">{{ dirty() ? 'unsaved geometry' : 'saved' }}</span>
        @if (actionStatus()) { <span class="hint">{{ actionStatus() }}</span> }
      </div>
      }

      @if (!readOnly()) {
      <h4>Region order</h4>
      <ol>
        @for (region of regions(); track region.id) {
          <li>
            <code>{{ region.id }}</code>
            <button type="button" (click)="move(region.id, -1)">↑</button>
            <button type="button" (click)="move(region.id, 1)">↓</button>
          </li>
        }
      </ol>
      <button type="button" (click)="saveOrder()" [disabled]="!orderDirty()">Save reading order</button>

      <h4>Line order <small>(region {{ activeRegionId() }})</small></h4>
      <ol>
        @for (line of orderedLines(); track line.id) {
          <li>
            <code>{{ line.id }}</code>
            <button type="button" (click)="moveLine(line.id, -1)">↑</button>
            <button type="button" (click)="moveLine(line.id, 1)">↓</button>
          </li>
        } @empty {
          <li>No lines in this region.</li>
        }
      </ol>
      <button type="button" (click)="saveLineOrder()" [disabled]="!lineOrderDirty()">Save line order</button>

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
    .tools { margin-top: 0.5rem; display: flex; gap: 0.75rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class GeometryEditor implements OnInit {
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
  protected readonly dirty = signal(false);
  protected readonly orderDirty = signal(false);
  protected readonly lineOrderDirty = signal(false);
  protected readonly mode = signal<'move' | 'draw'>('move');
  protected readonly drawRect = signal<{ x: number; y: number; width: number; height: number } | null>(null);
  protected readonly actionStatus = signal('');
  protected readonly zoom = signal(1);
  protected readonly historyEdits = signal<{
    id: string; kind: string; at: string; author: string; lineId?: string; regionId?: string;
  }[]>([]);
  private drawStart: { x: number; y: number } | null = null;
  protected readonly lineOrder = signal<string[]>([]);
  protected readonly history = signal<GeometryLine[][]>([]);
  private pendingSnapshot: GeometryLine[] | null = null;
  private readonly original = signal<GeometryLine[]>([]);
  private readonly originalOrder = signal<string[]>([]);
  private drag: DragState | null = null;

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnInit(): void {
    this.api.loadProject(this.projectId()).subscribe(payload => this.loadFrom(payload));
  }

  private loadHistory(): void {
    this.api.geometryHistory(this.surfaceId()).subscribe({
      next: edits => this.historyEdits.set(edits),
      error: () => this.historyEdits.set([]),
    });
  }

  private loadFrom(payload: ProjectPayload): void {
    const surface = payload.objects.flatMap(o => o.surfaces).find(s => s.id === this.surfaceId());
    if (!surface) return;
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
    this.original.set(lines.map(l => ({ ...l, polygon: l.polygon.map(p => ({ ...p })), baseline: l.baseline.map(p => ({ ...p })) })));
    this.regions.set(regions);
    this.originalOrder.set(regions.map(r => r.id));
    const firstRegion = regions[0]?.id;
    this.lineOrder.set(lines.filter(l => l.regionId === firstRegion).map(l => l.id));
    this.dirty.set(false);
    this.orderDirty.set(false);
    this.lineOrderDirty.set(false);
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
    if (this.mode() !== 'draw' || event.target !== event.currentTarget) return;
    const point = this.dragFor(event);
    this.drawStart = { x: point.startX, y: point.startY };
    this.drawRect.set({ x: point.startX, y: point.startY, width: 0, height: 0 });
  }

  protected onDown(event: PointerEvent, line: GeometryLine): void {
    if (this.mode() === 'draw') return;
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
    if (this.mode() === 'draw') return;
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

  private dragFor(event: PointerEvent): { startX: number; startY: number } {
    const svg = (event.target as Element).closest('svg')!;
    const rect = svg.getBoundingClientRect();
    return {
      startX: ((event.clientX - rect.left) / rect.width) * this.width(),
      startY: ((event.clientY - rect.top) / rect.height) * this.height(),
    };
  }

  protected onMove(event: PointerEvent): void {
    if (this.readOnly()) return;
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
      const b = this.bbox(this.drag!.original);
      const opposite = [
        { x: b.right, y: b.bottom }, { x: b.left, y: b.bottom },
        { x: b.left, y: b.top }, { x: b.left, y: b.top },
      ][this.drag!.corner];
      const moving = this.bboxCorners(this.drag!.original)[this.drag!.corner];
      const nx = Math.min(Math.max(moving.x + dx, opposite.x + 4), this.width() - 1);
      const ny = Math.min(Math.max(moving.y + dy, opposite.y + 4), this.height() - 1);
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
    this.dirty.set(true);
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
    this.api.addLine(this.surfaceId(),
      [
        { x: rect.x, y: rect.y }, { x: right, y: rect.y },
        { x: right, y: bottom }, { x: rect.x, y: bottom },
      ],
      [{ x: rect.x, y: bottom }, { x: right, y: bottom }])
      .subscribe({
        next: () => {
          this.actionStatus.set('line added');
          this.reload();
        },
        error: () => this.actionStatus.set('could not add line'),
      });
  }

  protected splitSelected(): void {
    const line = this.lines().find(l => l.id === this.selectedId());
    if (!line || line.polygon.length === 0) return;
    const ys = line.polygon.map(p => p.y);
    const middle = (Math.min(...ys) + Math.max(...ys)) / 2;
    this.api.splitLine(line.id, middle).subscribe({
      next: () => {
        this.actionStatus.set('line split');
        this.selectedId.set(null);
        this.reload();
      },
      error: () => this.actionStatus.set('split refused (line has transcribed text)'),
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
    this.api.mergeLines(selected, next.id).subscribe({
      next: () => {
        this.actionStatus.set('lines merged');
        this.selectedId.set(null);
        this.reload();
      },
      error: () => this.actionStatus.set('merge refused (transcribed text or different region)'),
    });
  }

  protected deleteSelected(): void {
    const id = this.selectedId();
    if (!id) return;
    this.api.deleteLine(id).subscribe({
      next: () => {
        this.actionStatus.set('line deleted');
        this.selectedId.set(null);
        this.reload();
      },
      error: () => this.actionStatus.set('deletion refused (line has transcribed text)'),
    });
  }

  private reload(): void {
    this.api.loadProject(this.projectId()).subscribe(payload => this.loadFrom(payload));
  }

  protected undoGeometry(): void {
    const stack = this.history();
    if (stack.length === 0) return;
    this.lines.set(stack[stack.length - 1].map(l => this.clone(l)));
    this.history.set(stack.slice(0, -1));
    this.dirty.set(true);
  }

  private snapshot(): GeometryLine[] {
    return this.lines().map(l => this.clone(l));
  }

  protected saveGeometry(): void {
    const line = this.lines().find(l => l.id === this.selectedId());
    if (!line) return;
    this.api.updateLineGeometry(line.id, line.polygon, line.baseline).subscribe({
      next: () => {
        this.original.set(this.lines().map(l => ({ ...l })));
        this.dirty.set(false);
      },
      error: () => this.dirty.set(true),
    });
  }

  protected reset(): void {
    this.lines.set(this.original().map(l => this.clone(l)));
    this.history.set([]);
    this.dirty.set(false);
  }

  protected move(regionId: string, delta: number): void {
    const list = [...this.regions()];
    const index = list.findIndex(r => r.id === regionId);
    const target = index + delta;
    if (index < 0 || target < 0 || target >= list.length) return;
    [list[index], list[target]] = [list[target], list[index]];
    this.regions.set(list);
    this.orderDirty.set(list.map(r => r.id).join('|') !== this.originalOrder().join('|'));
  }

  protected saveOrder(): void {
    this.api.updateReadingOrder(this.surfaceId(), this.regions().map(r => r.id)).subscribe({
      next: () => {
        this.originalOrder.set(this.regions().map(r => r.id));
        this.orderDirty.set(false);
      },
      error: () => this.orderDirty.set(true),
    });
  }

  protected activeRegionId(): string {
    return this.regions()[0]?.id ?? '';
  }

  protected orderedLines(): { id: string }[] {
    const ids = this.lineOrder();
    const byId = new Map(this.lines().map(l => [l.id, l]));
    return ids.filter(id => byId.has(id)).map(id => ({ id }));
  }

  protected moveLine(lineId: string, delta: number): void {
    const list = [...this.lineOrder()];
    const index = list.indexOf(lineId);
    const target = index + delta;
    if (index < 0 || target < 0 || target >= list.length) return;
    [list[index], list[target]] = [list[target], list[index]];
    this.lineOrder.set(list);
    this.lineOrderDirty.set(true);
  }

  protected saveLineOrder(): void {
    this.api.updateLineOrder(this.activeRegionId(), this.lineOrder()).subscribe({
      next: () => this.lineOrderDirty.set(false),
      error: () => this.lineOrderDirty.set(true),
    });
  }
}
