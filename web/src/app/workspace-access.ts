import { signal, untracked } from '@angular/core';
import { take, Subscription } from 'rxjs';
import { persistedAccountFromFreshProfile, persistedWorkspaceOwnerFromFreshProfile } from './principal-profile';
import type { PrincipalProfile, WorkspaceOwner } from './principal-profile';
import type { WorkspaceAccess, WorkspaceAccessOptions, WorkspaceAccessState, WorkspaceBoundaryReason,
  WorkspacePrincipalDescriptor, WorkspaceRequestIntent, WorkspaceScopeSnapshot } from './workspace-access-types';

let boundaryEpoch = 0;
let boundaryNotificationDepth = 0;
const boundaries = new Set<(reason: WorkspaceBoundaryReason) => void>();
/** Global credential/authority boundary observation only; grants no workspace access. */
export function subscribeWorkspaceBoundary(listener: (reason: WorkspaceBoundaryReason) => void): () => void {
  let active = true;
  const guarded = (reason: WorkspaceBoundaryReason) => { if (active) listener(reason); };
  boundaries.add(guarded);
  return () => { active = false; boundaries.delete(guarded); };
}
/** Call synchronously BEFORE key/cache mutation. This only suspends, never probes. */
export function notifyWorkspaceBoundary(reason: WorkspaceBoundaryReason = 'authority-change'): void {
  boundaryEpoch++;
  const failures: unknown[] = [];
  boundaryNotificationDepth++;
  try {
    for (const listener of [...boundaries]) {
      try { listener(reason); } catch (error) { failures.push(error); }
    }
  } finally { boundaryNotificationDepth--; }
  // A faulty presentation listener cannot prevent other workspaces being fenced.
  if (failures.length === 1) throw failures[0];
  if (failures.length > 1) throw new AggregateError(failures, 'Workspace suspension listeners failed.');
}
function equalScope(a: WorkspaceScopeSnapshot | null, b: WorkspaceScopeSnapshot | null): boolean {
  if (a === null || b === null) return a === b;
  return a.credential === b.credential
    && a.localGeneration === b.localGeneration && a.authorityRevision === b.authorityRevision;
}
function sameOwner(a: WorkspaceOwner | null, b: WorkspaceOwner | null): boolean {
  return a !== null && b !== null && a.accountId === b.accountId && a.workspaceNamespace === b.workspaceNamespace;
}
function descriptor(profile: PrincipalProfile): WorkspacePrincipalDescriptor | null {
  if (typeof profile !== 'object' || profile === null || typeof profile.role !== 'string'
      || profile.role.length === 0 || profile.role.length > 128) return null;
  if (profile.mode === 'local' && profile.authenticated === false)
    return Object.freeze({ mode: 'local', accountId: null, workspaceNamespace: null, role: profile.role });
  if (profile.mode === 'bootstrap' && profile.authenticated === true)
    return Object.freeze({ mode: 'bootstrap', accountId: null, workspaceNamespace: null, role: profile.role });
  const accountId = persistedAccountFromFreshProfile(profile);
  if (accountId === null) return null;
  const owner = persistedWorkspaceOwnerFromFreshProfile(profile);
  if (profile.workspaceNamespace !== null && profile.workspaceNamespace !== undefined && owner === null) return null;
  return Object.freeze({ mode: 'account', accountId, workspaceNamespace: owner?.workspaceNamespace ?? null, role: profile.role });
}

/** One coordinator per preserved mounted workspace. A different identity needs a
 * separate fresh workspace; this object never reassigns its original draft owner. */
export class WorkspaceAccessCoordinator<TProject = unknown> implements WorkspaceAccess {
  private readonly value = signal<WorkspaceAccessState>({ status: 'suspended', generation: 0, reason: 'not-confirmed', principal: null });
  private readonly listeners = new Set<(state: WorkspaceAccessState) => void>();
  private readonly reads = new Set<Subscription>();
  private readonly notifications: { state: WorkspaceAccessState; listeners: ((state: WorkspaceAccessState) => void)[] }[] = [];
  private drainingNotifications = false;
  private observed: WorkspaceScopeSnapshot | null;
  private observedEpoch = boundaryEpoch;
  private selected: string | null = null;
  private anchor: { principal: WorkspacePrincipalDescriptor; owner: WorkspaceOwner | null; scope: WorkspaceScopeSnapshot; epoch: number; continuityLost: boolean } | null = null;
  private activeOwner: WorkspaceOwner | null = null;
  private requestVersion = 0;
  private destroyed = false;
  private readonly onBoundary = (reason: WorkspaceBoundaryReason) => this.suspend(reason);
  private readonly onStorage = (event: StorageEvent) => {
    if (event.key === null || event.key === 'leviathan:api-key' || event.key === 'leviathan:cache-authority-revision')
      this.suspend('storage-change'); // Includes ABA even when the final key equals the old key.
  };
  constructor(private readonly options: WorkspaceAccessOptions<TProject>) {
    this.observed = this.readScope();
    boundaries.add(this.onBoundary);
    globalThis.addEventListener?.('storage', this.onStorage);
  }
  state(): WorkspaceAccessState { this.sync(); return this.value(); }
  isActive(): boolean { return this.state().status === 'active'; }
  owner(): WorkspaceOwner | null { return this.isActive() && this.activeOwner ? { ...this.activeOwner } : null; }
  projectId(): string | null { return this.isActive() ? this.selected : null; }
  subscribe(listener: (state: WorkspaceAccessState) => void): () => void {
    if (this.destroyed) return () => undefined;
    this.sync(); this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }
  capture(): WorkspaceRequestIntent | null {
    if (!this.isActive() || this.selected === null || this.observed === null) return null;
    const scope = { ...this.observed }, epoch = boundaryEpoch, generation = this.value().generation;
    const projectId = this.selected;
    return Object.freeze({ credential: scope.credential, projectId, generation,
      current: () => !this.destroyed && this.isActive() && this.value().generation === generation
        && this.selected === projectId && epoch === boundaryEpoch && equalScope(scope, this.readScope()) });
  }
  /** Fresh network admission; caller still owns text/geometry reconciliation. */
  open(projectId: string): void {
    if (this.destroyed || boundaryNotificationDepth > 0 || this.drainingNotifications) return;
    if (!projectId || projectId !== projectId.trim()) {
      this.requestVersion++; this.selected = null; this.activeOwner = null; this.loseUnownedContinuity();
      try { this.publish('denied', 'invalid-project'); } finally { this.cancelReads(); } return;
    }
    this.sync(); this.selected = projectId; this.probe();
  }
  retry(): void { if (!this.destroyed && this.selected !== null) { this.sync(); this.probe(); } }
  notifyBoundary(reason: WorkspaceBoundaryReason = 'authority-change'): void { this.suspend(reason); }
  destroy(): void {
    if (this.destroyed) return;
    this.destroyed = true; this.requestVersion++; this.activeOwner = null;
    boundaries.delete(this.onBoundary); globalThis.removeEventListener?.('storage', this.onStorage);
    try { this.publish('suspended', 'destroyed'); }
    finally { try { this.cancelReads(); } finally { this.listeners.clear(); } }
  }
  private readScope(): WorkspaceScopeSnapshot | null {
    try {
      const scope = this.options.readScope();
      if (!scope || typeof scope.credential !== 'string' || !Number.isSafeInteger(scope.localGeneration)
        || scope.localGeneration < 0 || !(scope.authorityRevision === null || typeof scope.authorityRevision === 'string')) return null;
      return { ...scope };
    } catch { return null; }
  }
  private sync(): void {
    if (this.destroyed) return;
    const latest = this.readScope();
    if (this.observedEpoch !== boundaryEpoch || !equalScope(latest, this.observed)) this.suspend('authority-change');
  }
  private suspend(reason: WorkspaceBoundaryReason): void {
    if (this.destroyed) return;
    this.observed = this.readScope(); this.observedEpoch = boundaryEpoch;
    this.loseUnownedContinuity();
    this.requestVersion++; this.activeOwner = null;
    const denied = reason === 'authorization-denied' || this.value().status === 'denied';
    try { this.publish(denied ? 'denied' : 'suspended', reason); } finally { this.cancelReads(); }
  }
  private loseUnownedContinuity(): void { if (this.anchor !== null) this.anchor.continuityLost = true; }
  private canContinue(owner: WorkspaceOwner | null, principal: WorkspacePrincipalDescriptor, scope: WorkspaceScopeSnapshot, epoch: number): boolean {
    if (this.anchor === null || sameOwner(this.anchor.owner, owner)) return true;
    // Initial ownerless sessions may navigate normally only while the original
    // witness remains unchanged. This grants no durable owner or post-boundary resume.
    return this.anchor.owner === null && owner === null && !this.anchor.continuityLost
      && this.anchor.epoch === epoch && equalScope(this.anchor.scope, scope)
      && this.anchor.principal.mode === principal.mode && this.anchor.principal.accountId === principal.accountId
      && this.anchor.principal.workspaceNamespace === principal.workspaceNamespace;
  }
  private cancelReads(): void {
    const reads = [...this.reads]; this.reads.clear(); const failures: unknown[] = [];
    for (const read of reads) try { read.unsubscribe(); } catch (error) { failures.push(error); }
    if (failures.length === 1) throw failures[0];
    if (failures.length > 1) throw new AggregateError(failures, 'Workspace read cancellation failed.');
  }
  private track(read: Subscription): void { if (!read.closed) this.reads.add(read); }
  private publish(status: WorkspaceAccessState['status'], reason: string, principal: WorkspacePrincipalDescriptor | null = null): void {
    const state = Object.freeze({ status, generation: this.value().generation + 1, reason, principal });
    untracked(() => this.value.set(state));
    this.notifications.push({ state, listeners: [...this.listeners] });
    if (this.drainingNotifications) return;
    this.drainingNotifications = true;
    const failures: unknown[] = [];
    try {
      while (this.notifications.length) {
        const next = this.notifications.shift()!;
        for (const listener of next.listeners) try { listener(next.state); } catch (error) { failures.push(error); }
      }
    } finally { this.drainingNotifications = false; }
    if (failures.length === 1) throw failures[0];
    if (failures.length > 1) throw new AggregateError(failures, 'Workspace state listeners failed.');
  }
  private probe(): void {
    // Observers may suspend/clean up synchronously, but must retry only after
    // the credential mutation and current notification drain have completed.
    if (this.destroyed || boundaryNotificationDepth > 0 || this.drainingNotifications) return;
    const version = ++this.requestVersion; this.activeOwner = null;
    try { this.publish('suspended', 'checking-access'); } finally { this.cancelReads(); }
    if (this.destroyed || version !== this.requestVersion) return;
    const projectId = this.selected, scope = this.readScope(), epoch = boundaryEpoch;
    if (projectId === null || scope === null) { this.loseUnownedContinuity(); this.publish('suspended', 'scope-unavailable'); return; }
    this.observed = { ...scope }; this.observedEpoch = epoch;
    const current = () => !this.destroyed && version === this.requestVersion && epoch === boundaryEpoch
      && projectId === this.selected && equalScope(scope, this.readScope());
    const fail = (error: unknown) => {
      if (!current()) { this.sync(); return; }
      const status = typeof error === 'object' && error !== null && 'status' in error ? (error as { status: unknown }).status : null;
      this.requestVersion++; this.activeOwner = null; this.loseUnownedContinuity();
      const denied = status === 401 || status === 403 || status === 404;
      try { this.publish(denied ? 'denied' : 'suspended', denied ? 'authorization-denied' : 'fresh-access-unavailable'); }
      finally { try { this.cancelReads(); } finally { if (denied) this.options.onDenied?.(); } } // Includes status0; never cache fallback.
    };
    try {
      let sawProfile = false;
      this.track(this.options.api.me(scope.credential).pipe(take(1)).subscribe({
        next: profile => {
          sawProfile = true; if (!current()) { this.sync(); return; }
          const principal = descriptor(profile), owner = persistedWorkspaceOwnerFromFreshProfile(profile);
          if (principal === null) { this.loseUnownedContinuity(); this.publish('quarantined', 'principal-unverified'); return; }
          if (!this.canContinue(owner, principal, scope, epoch)) {
            this.loseUnownedContinuity();
            this.publish('quarantined', this.anchor?.owner == null || owner === null ? 'owner-continuity-unavailable' : 'different-owner'); return;
          }
          let sawProject = false;
          try {
            this.track(this.options.api.loadProjectFresh(projectId, scope.credential).pipe(take(1)).subscribe({
              next: project => {
                sawProject = true; if (!current()) { this.sync(); return; }
                // Explicit null/undefined cannot be an affirmative membership result.
                if (typeof project !== 'object' || project === null || Object.getOwnPropertyDescriptor(project, 'id')?.value !== projectId) { this.loseUnownedContinuity(); this.publish('denied', 'project-unavailable'); return; }
                this.anchor ??= { principal, owner: owner ? { ...owner } : null, scope: { ...scope }, epoch, continuityLost: false };
                this.activeOwner = owner ? { ...owner } : null;
                const candidate: WorkspaceAccessState = { status: 'active', generation: this.value().generation + 1, reason: 'fresh-access-confirmed', principal };
                try { this.options.onConfirmed?.(project, candidate); }
                catch { this.activeOwner = null; this.loseUnownedContinuity(); this.publish('quarantined', 'reconciliation-failed'); return; }
                if (!current()) { this.activeOwner = null; this.sync(); return; }
                this.publish('active', 'fresh-access-confirmed', principal);
              }, error: fail,
              complete: () => { if (!sawProject && current()) { this.loseUnownedContinuity(); this.publish('suspended', 'fresh-project-empty'); } },
            }));
          } catch (error) { fail(error); }
        }, error: fail,
        complete: () => { if (!sawProfile && current()) { this.loseUnownedContinuity(); this.publish('suspended', 'fresh-profile-empty'); } },
      }));
    } catch (error) { fail(error); }
  }
}
