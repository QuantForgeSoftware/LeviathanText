import { DestroyRef, ElementRef, inject, signal, untracked } from '@angular/core';
import { ApiClient, ProjectPayload } from './api-client';
import { WORKSPACE_ACCESS } from './workspace-access-types';
import type { WorkspaceAccess, WorkspaceAccessState, WorkspaceRequestIntent } from './workspace-access-types';

/** Component-local presentation/lifetime fencing. Missing capability is always inactive. */
export class WorkspaceComponentScope {
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly epoch = signal(0);
  private alive = true;
  readonly access: WorkspaceAccess | null;
  constructor(onChange?: (state: WorkspaceAccessState) => void, provided?: WorkspaceAccess | null) {
    this.access = provided === undefined ? inject(WORKSPACE_ACCESS, { optional: true }) : provided;
    const unsubscribe = this.access?.subscribe(state => untracked(() => {
      this.epoch.update(value => value + 1);
      this.conceal(state.status !== 'active');
      onChange?.(state);
    }));
    const keydown = (event: KeyboardEvent) => {
      if (event.target instanceof Element && event.target.closest('[data-workspace-protected]') && !this.active()) {
        event.preventDefault(); event.stopImmediatePropagation();
      }
    };
    this.element.nativeElement.addEventListener('keydown', keydown, true);
    inject(DestroyRef).onDestroy(() => { this.alive = false; unsubscribe?.(); this.element.nativeElement.removeEventListener('keydown', keydown, true); });
  }
  /** Call imperatively for EVERY action; a computed-only guard can miss direct storage changes. */
  active(): boolean {
    this.epoch();
    const active = this.alive && this.access?.isActive() === true;
    if (!active) this.conceal(true);
    return active;
  }
  role(...roles: string[]): boolean {
    if (!this.active()) return false;
    const role = this.access?.state().principal?.role.toLowerCase();
    return role !== undefined && roles.map(value => value.toLowerCase()).includes(role);
  }
  mayEdit(): boolean { return this.role('annotator', 'reviewer', 'admin'); }
  concealNow(): void { this.conceal(true); }
  /** Rendering only. Bootstrap caller must already hold a fresh captured principal intent. */
  present(allowed: boolean): void { this.conceal(!allowed); }
  capture(projectId?: string, current: () => boolean = () => true): WorkspaceRequestIntent | null {
    if (!this.active()) return null;
    const captured = this.access?.capture();
    if (!captured || projectId !== undefined && captured.projectId !== projectId) return null;
    const epoch = this.epoch();
    return { ...captured, current: () => this.alive && epoch === this.epoch() && captured.current() && current() };
  }
  request(api: ApiClient, projectId?: string, current: () => boolean = () => true): ApiClient {
    const intent = this.capture(projectId, current);
    return api.forWorkspaceRequest(intent ?? { credential: '', current: () => false });
  }
  /** Synchronous visual/key/pointer concealment while mounted draft-bearing children survive. */
  private conceal(force: boolean): void {
    const host = this.element.nativeElement;
    const nodes = [...host.querySelectorAll<HTMLElement>('[data-workspace-protected]')].filter(node => {
      for (let parent = node.parentElement; parent && parent !== host; parent = parent.parentElement)
        if (parent.tagName.startsWith('APP-')) return false; // Each child owns its stricter local gate.
      return true;
    });
    if (host.hasAttribute('data-workspace-protected')) nodes.unshift(host);
    const role = this.access?.state().principal?.role.toLowerCase() ?? '';
    for (const node of nodes) {
      const roles = node.dataset['workspaceRole']?.split(',').map(value => value.trim().toLowerCase());
      const hidden = force || !!roles && !roles.includes(role);
      node.hidden = hidden; node.inert = hidden;
      if (hidden) node.setAttribute('aria-hidden', 'true'); else node.removeAttribute('aria-hidden');
    }
  }
}

/** A local identity gate composes with the principal capability without changing its owner. */
export class WorkspaceAccessGate implements WorkspaceAccess {
  private alive = true;
  private generation = 0;
  private readonly listeners = new Set<(state: WorkspaceAccessState) => void>();
  private readonly unsubscribe: (() => void) | undefined;
  private lastAllowed: boolean | undefined;
  private reading = false;
  private snapshot: WorkspaceAccessState = { status: 'suspended', generation: 0, reason: 'surface-identity-not-confirmed', principal: null };
  constructor(private readonly parent: WorkspaceAccess | null, private readonly allowed: () => boolean) {
    this.unsubscribe = parent?.subscribe(() => this.read(true));
  }
  private read(force = false): WorkspaceAccessState {
    if (this.reading) return this.snapshot;
    this.reading = true;
    try {
      const state = this.parent?.state();
      const active = this.alive && state?.status === 'active' && this.allowed();
      const changed = this.lastAllowed !== active;
      if (changed || force) this.generation++;
      this.lastAllowed = active;
      this.snapshot = { status: active ? 'active' : state?.status === 'active' ? 'quarantined' : state?.status ?? 'suspended',
        generation: this.generation, reason: active ? state!.reason : 'surface-identity-not-confirmed',
        principal: active ? state!.principal : null };
      if (changed || force) untracked(() => { for (const listener of [...this.listeners]) listener(this.snapshot); });
      return this.snapshot;
    } finally { this.reading = false; }
  }
  state(): WorkspaceAccessState { return this.read(); }
  isActive(): boolean { return this.state().status === 'active'; }
  capture(): WorkspaceRequestIntent | null {
    if (!this.isActive()) return null;
    const intent = this.parent?.capture(), generation = this.generation;
    let expired = false;
    return intent ? { ...intent, generation, current: () => {
      if (expired) return false;
      const active = this.isActive();
      if (!active || generation !== this.generation || !intent.current()) expired = true;
      return !expired;
    } } : null;
  }
  owner() { return this.isActive() ? this.parent?.owner() ?? null : null; }
  projectId(): string | null { return this.isActive() ? this.parent?.projectId() ?? null : null; }
  retry(): void { this.parent?.retry(); }
  subscribe(listener: (state: WorkspaceAccessState) => void): () => void {
    this.listeners.add(listener); return () => { this.listeners.delete(listener); };
  }
  refresh(): void { this.read(); }
  destroy(): void { this.alive = false; this.unsubscribe?.(); this.read(true); this.listeners.clear(); }
}

/** Recovery must never choose the first occurrence of an ambiguous native identity. */
export function uniqueWorkspaceProject(project: ProjectPayload, projectId: string): boolean {
  if (!project || project.id !== projectId || !Array.isArray(project.objects)) return false;
  const objects = new Set<string>(), surfaces = new Set<string>(), lines = new Set<string>();
  const regions = new Set<string>(), captures = new Set<string>();
  const add = (set: Set<string>, id: unknown) => typeof id === 'string' && id.length > 0 && id === id.trim()
    && !set.has(id) && !!set.add(id);
  for (const object of project.objects) {
    if (!object || !add(objects, object.id) || !Array.isArray(object.surfaces)) return false;
    for (const surface of object.surfaces) {
      if (!surface || !add(surfaces, surface.id) || !Array.isArray(surface.lines)
          || !Array.isArray(surface.regions) || !Array.isArray(surface.captures)) return false;
      if (surface.lines.some(line => !line || !add(lines, line.id))
          || surface.regions.some(region => !region || !add(regions, region.id))
          || surface.captures.some(capture => !capture || !add(captures, capture.id))) return false;
    }
  }
  return true;
}
