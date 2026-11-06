import { InjectionToken } from '@angular/core';
import type { Observable } from 'rxjs';
import type { PrincipalProfile, WorkspaceOwner } from './principal-profile';
export type { WorkspaceOwner } from './principal-profile';

export type WorkspaceBoundaryReason = 'credential-change' | 'authority-change' | 'storage-change' | 'authorization-denied';
export type WorkspaceAccessStatus = 'active' | 'suspended' | 'denied' | 'quarantined';
export interface WorkspacePrincipalDescriptor {
  readonly mode: 'account' | 'local' | 'bootstrap';
  readonly accountId: string | null;
  readonly workspaceNamespace: string | null;
  readonly role: string;
}
export interface WorkspaceAccessState {
  readonly status: WorkspaceAccessStatus;
  readonly generation: number;
  readonly reason: string;
  readonly principal: WorkspacePrincipalDescriptor | null;
}
/** Transient request material. Never serialize this as draft ownership. */
export interface WorkspaceRequestIntent {
  readonly credential: string;
  readonly projectId: string;
  readonly generation: number;
  current(): boolean;
}
export interface WorkspaceAccess {
  /** Synchronously fences direct storage/key/authority changes before returning. */
  state(): WorkspaceAccessState;
  isActive(): boolean;
  /** Null while inactive; a caller must not dispatch a protected request then. */
  capture(): WorkspaceRequestIntent | null;
  subscribe(listener: (state: WorkspaceAccessState) => void): () => void;
  owner(): WorkspaceOwner | null;
  projectId(): string | null;
  retry(): void;
}
/** No factory/default: missing injection does not grant standalone trust. */
export const WORKSPACE_ACCESS = new InjectionToken<WorkspaceAccess>('WORKSPACE_ACCESS');
export interface WorkspaceScopeSnapshot {
  readonly credential: string;
  readonly localGeneration: number;
  readonly authorityRevision: string | null;
}
export interface WorkspaceAccessApi<TProject> {
  me(credential: string): Observable<PrincipalProfile>;
  loadProjectFresh(projectId: string, credential: string): Observable<TProject>;
}
export interface WorkspaceAccessOptions<TProject> {
  readonly api: WorkspaceAccessApi<TProject>;
  readonly readScope: () => WorkspaceScopeSnapshot;
  /** Does not acknowledge a newer text/geometry parent. Caller reconciles true bases. */
  readonly onConfirmed?: (project: TProject, state: WorkspaceAccessState) => void;
  /** Current probe denial hook; root supplies clearCache to fence shared authority. */
  readonly onDenied?: () => void;
}

export type WorkspaceJson = null | boolean | number | string | readonly WorkspaceJson[]
  | { readonly [key: string]: WorkspaceJson };
/** Component-owned schema; validated plain JSON is preserved without reinterpretation. */
export interface WorkspaceOpaqueRecord {
  readonly version: 1;
  readonly kind: string;
  readonly data: WorkspaceJson;
}
export interface WorkspaceDraftScope extends WorkspaceOwner {
  readonly projectId: string;
  readonly surfaceId: string;
  readonly lineId: string;
  readonly layer: string;
}
export interface WorkspaceDraftFrame {
  readonly captureId: string;
  readonly frameRef: string;
  readonly checksum: { readonly algorithm: string; readonly digest: string } | null;
  readonly width: number | null;
  readonly height: number | null;
}
export interface WorkspaceDraftSnapshot {
  readonly schemaVersion: 1;
  readonly scope: WorkspaceDraftScope;
  /** An actual loaded ancestor, including known empty text; never guessed on recovery. */
  readonly base: { readonly revisionId: string | null; readonly text: string };
  readonly draftText: string;
  readonly frame: WorkspaceDraftFrame | null;
  readonly undo: WorkspaceOpaqueRecord | null;
  readonly geometry: WorkspaceOpaqueRecord | null;
  readonly readingOrder: WorkspaceOpaqueRecord | null;
  /** Cancelled HTTP does not imply rollback. Keep every unresolved submitted write. */
  readonly unconfirmedWrites: readonly WorkspaceOpaqueRecord[];
}
/** Exact immutable prior bytes enable append-only linear continuation without overwrite. */
export interface WorkspaceDraftHandle {
  readonly storageKey: string;
  readonly recordId: string;
  readonly raw: string;
}
export type WorkspaceDraftSaveResult =
  | { readonly ok: true; readonly handle: WorkspaceDraftHandle }
  | { readonly ok: false; readonly reason: string };
export type WorkspaceDraftLoadResult =
  | { readonly status: 'missing' }
  | { readonly status: 'found'; readonly snapshot: WorkspaceDraftSnapshot; readonly handle: WorkspaceDraftHandle }
  | { readonly status: 'ambiguous'; readonly recordIds: readonly string[] }
  | { readonly status: 'invalid' | 'unavailable'; readonly reason: string };
export interface WorkspaceDraftStorage {
  readonly length: number;
  key(index: number): string | null;
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}
