/**
 * Geometry editor (todo Phase 2 gate: geometry/order correction; manual layout when
 * automatic layout fails). Renders the original capture with polygon/baseline overlays
 * in the image's natural coordinate frame; drags translate the selected line; region
 * order is editable with up/down; saves go through the geometry API.
 */
import { Component, input, signal, OnInit } from '@angular/core';
import { ApiClient, GeometryLine, GeometryPoint, GeometryRegion, ProjectPayload } from './api-client';

interface DragState {
  lineId: string;
  startX: number;
  startY: number;
  original: GeometryLine;
}

@Component({
  selector: 'app-geometry-editor',
  standalone: true,
  template: `
    <section>
      <h3>Geometry <small>(image frame: {{ width() }}×{{ height() }})</small></h3>
      <p class="hint">Click a line, drag to move it, then save. Region order controls reading order.</p>

      <div class="canvas">
        <img [src]="imageUrl()" (load)="onImage($event)" alt="surface capture" />
        <svg
          [attr.viewBox]="'0 0 ' + width() + ' ' + height()"
          preserveAspectRatio="xMidYMid meet"
          (pointermove)="onMove($event)"
          (pointerup)="onUp()"
          (pointerleave)="onUp()">
          @for (line of lines(); track line.id) {
            <g [attr.data-id]="line.id">
              <polygon
                [attr.points]="polygonPoints(line)"
                [attr.class]="line.id === selectedId() ? 'sel' : ''"
                (pointerdown)="onDown($event, line)" />
              @if (line.baseline.length > 1) {
                <polyline [attr.points]="baselinePoints(line)" class="baseline" />
              }
            </g>
          }
        </svg>
      </div>

      <div class="tools">
        <button type="button" (click)="saveGeometry()" [disabled]="!dirty()">Save geometry</button>
        <button type="button" (click)="reset()" [disabled]="!dirty()">Reset</button>
        <span class="hint">{{ dirty() ? 'unsaved geometry' : 'saved' }}</span>
      </div>

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
    </section>
  `,
  styles: [`
    .canvas { position: relative; max-width: 100%; }
    .canvas img { width: 100%; display: block; }
    .canvas svg { position: absolute; inset: 0; width: 100%; height: 100%; }
    polygon { fill: rgba(40, 120, 220, 0.15); stroke: #2878dc; stroke-width: 2; cursor: move; }
    polygon.sel { fill: rgba(220, 120, 40, 0.25); stroke: #dc7828; }
    .baseline { fill: none; stroke: #dc3c28; stroke-width: 2; pointer-events: none; }
    .tools { margin-top: 0.5rem; display: flex; gap: 0.75rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class GeometryEditor implements OnInit {
  projectId = input.required<string>();
  surfaceId = input.required<string>();
  imageUrl = input.required<string>();

  protected readonly lines = signal<GeometryLine[]>([]);
  protected readonly regions = signal<GeometryRegion[]>([]);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly width = signal(1);
  protected readonly height = signal(1);
  protected readonly dirty = signal(false);
  protected readonly orderDirty = signal(false);
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
    this.dirty.set(false);
    this.orderDirty.set(false);
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

  protected onDown(event: PointerEvent, line: GeometryLine): void {
    (event.target as Element).setPointerCapture?.(event.pointerId);
    this.selectedId.set(line.id);
    const svg = (event.target as Element).closest('svg')!;
    const rect = svg.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * this.width();
    const y = ((event.clientY - rect.top) / rect.height) * this.height();
    this.drag = { lineId: line.id, startX: x, startY: y, original: { ...line, polygon: line.polygon.map(p => ({ ...p })), baseline: line.baseline.map(p => ({ ...p })) } };
  }

  protected onMove(event: PointerEvent): void {
    if (!this.drag) return;
    const svg = (event.target as Element).closest('svg');
    if (!svg) return;
    const rect = svg.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * this.width();
    const y = ((event.clientY - rect.top) / rect.height) * this.height();
    const dx = x - this.drag.startX;
    const dy = y - this.drag.startY;

    this.lines.update(lines => lines.map(l => l.id === this.drag!.lineId
      ? {
          ...l,
          polygon: this.drag!.original.polygon.map(p => ({ x: p.x + dx, y: p.y + dy })),
          baseline: this.drag!.original.baseline.map(p => ({ x: p.x + dx, y: p.y + dy })),
        }
      : l));
    this.dirty.set(true);
  }

  protected onUp(): void {
    this.drag = null;
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
    this.lines.set(this.original().map(l => ({ ...l, polygon: l.polygon.map(p => ({ ...p })), baseline: l.baseline.map(p => ({ ...p })) })));
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
}
