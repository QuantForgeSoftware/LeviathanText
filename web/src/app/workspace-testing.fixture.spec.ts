/** Explicit TEST-ONLY workspace trust for existing component-logic fixtures.
 * Production has no missing-provider trust or mock-facade fallback. Transport
 * pinning and negative access behavior have separate real-client tests.
 */
import type { Provider } from '@angular/core';
import { ApiClient } from './api-client';
import { WorkspaceDraftStore } from './workspace-draft-store';
import { WORKSPACE_ACCESS } from './workspace-access-types';
import type {
  WorkspaceAccess, WorkspaceAccessState, WorkspaceDraftHandle, WorkspaceDraftScope,
  WorkspaceDraftSnapshot, WorkspaceOpaqueRecord, WorkspaceOwner, WorkspaceRequestIntent,
} from './workspace-access-types';

export const TEST_WORKSPACE_OWNER: Readonly<WorkspaceOwner> = Object.freeze({
  workspaceNamespace: '8c589ede-1389-4ab2-89cc-95f54292b99c', accountId: 'generated-owned-test-account',
});
export const TEST_WORKSPACE_PROJECT_ID = 'generated-owned-test-project';
export const TEST_WORKSPACE_SURFACE_ID = 'generated-owned-test-surface';
export interface WorkspaceTestOptions {
  access?: WorkspaceAccess;
  owner?: WorkspaceOwner;
  projectId?: string;
  credential?: string;
  role?: 'Admin' | 'Reviewer' | 'Reader' | 'Annotator';
}

export function provideWorkspaceTestAccess(options: WorkspaceTestOptions = {}): Provider {
  return { provide: WORKSPACE_ACCESS, deps: [ApiClient], useFactory: (api: ApiClient): WorkspaceAccess => {
    const facade = api as unknown as Record<string, unknown>;
    if (typeof facade['forWorkspaceRequest'] !== 'function')
      facade['forWorkspaceRequest'] = (_intent: WorkspaceRequestIntent) => api;
    if (typeof facade['lineFresh'] !== 'function' && typeof facade['line'] === 'function')
      facade['lineFresh'] = (...args: unknown[]) => Reflect.apply(facade['line'] as (...args: unknown[]) => unknown, api, args);
    if (options.access) return options.access;
    const owner = options.owner ?? TEST_WORKSPACE_OWNER;
    const projectId = options.projectId ?? TEST_WORKSPACE_PROJECT_ID;
    const state: WorkspaceAccessState = Object.freeze({ status: 'active', generation: 0,
      reason: 'Explicit generated component TEST fixture', principal: Object.freeze({ mode: 'account',
        accountId: owner.accountId, workspaceNamespace: owner.workspaceNamespace, role: options.role ?? 'Reviewer' }) });
    return {
      state: () => state, isActive: () => true,
      capture: () => ({ credential: options.credential ?? 'generated-test-credential', projectId,
        generation: state.generation, current: () => true }),
      subscribe: listener => { listener(state); return () => {}; },
      owner: () => ({ ...owner }), projectId: () => projectId, retry: () => {},
    };
  } };
}

export interface WorkspaceTestScopeOptions {
  owner?: WorkspaceOwner;
  projectId?: string;
  surfaceId?: string;
  layer?: string;
}
export function testDraftScope(lineId: string, options: WorkspaceTestScopeOptions = {}): WorkspaceDraftScope {
  return { ...(options.owner ?? TEST_WORKSPACE_OWNER), projectId: options.projectId ?? TEST_WORKSPACE_PROJECT_ID,
    surfaceId: options.surfaceId ?? TEST_WORKSPACE_SURFACE_ID, lineId, layer: options.layer ?? 'L1Diplomatic' };
}
export interface WorkspaceTestSeedOptions extends WorkspaceTestScopeOptions {
  baseRevisionId?: string | null;
  undo?: WorkspaceOpaqueRecord | null;
  unconfirmedWrites?: readonly WorkspaceOpaqueRecord[];
}
export function seedOwnedDraft(lineId: string, draftText: string, baseText: string,
  options: WorkspaceTestSeedOptions = {}): WorkspaceDraftHandle {
  const store = new WorkspaceDraftStore(localStorage);
  const scope = testDraftScope(lineId, options);
  const existing = store.load(scope);
  if (existing.status !== 'found' && existing.status !== 'missing')
    throw new Error(`Generated owned draft fixture cannot append to ${existing.status} recovery`);
  const result = store.save({ schemaVersion: 1, scope,
    base: { revisionId: options.baseRevisionId ?? null, text: baseText }, draftText, frame: null,
    undo: options.undo ?? null, geometry: null, readingOrder: null, unconfirmedWrites: options.unconfirmedWrites ?? [] },
    existing.status === 'found' ? existing.handle : undefined);
  if (!result.ok) throw new Error(`Generated owned draft fixture could not be saved: ${result.reason}`);
  return result.handle;
}
export function readOwnedDraft(lineId: string, options: WorkspaceTestScopeOptions = {}): WorkspaceDraftSnapshot | null {
  const result = new WorkspaceDraftStore(localStorage).load(testDraftScope(lineId, options));
  if (result.status === 'missing') return null;
  if (result.status !== 'found') throw new Error(`Generated owned draft fixture is ${result.status}`);
  return result.snapshot;
}
