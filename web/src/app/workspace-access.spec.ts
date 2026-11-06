import { computed } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import { WorkspaceAccessCoordinator, notifyWorkspaceBoundary } from './workspace-access';
import type { PrincipalProfile } from './principal-profile';
import type { WorkspaceScopeSnapshot } from './workspace-access-types';

const NS = '11111111-1111-4111-8111-111111111111';
const OTHER_NS = '22222222-2222-4222-8222-222222222222';
function account(id = 'account-a', workspaceNamespace: string | null = NS, role = 'Annotator'): PrincipalProfile {
  return { authenticated: true, name: 'equal-generated-name', role, mode: 'account', accountId: id, workspaceNamespace };
}

describe('WorkspaceAccessCoordinator', () => {
  let scope: WorkspaceScopeSnapshot;
  let created: WorkspaceAccessCoordinator<{ id: string; revision?: number }>[];
  let profiles: { key: string; value: Subject<PrincipalProfile> }[];
  let projects: { id: string; key: string; value: Subject<{ id: string; revision?: number }> }[];
  beforeEach(() => { scope = { credential: 'generated-key-a', localGeneration: 0, authorityRevision: 'a'.repeat(32) }; created = []; profiles = []; projects = []; });
  afterEach(() => created.forEach(item => item.destroy()));
  function make(extra: { denied?: () => void; confirmed?: () => void } = {}) {
    const api = {
      me: (key: string) => { const value = new Subject<PrincipalProfile>(); profiles.push({ key, value }); return value; },
      loadProjectFresh: (id: string, key: string) => { const value = new Subject<{ id: string; revision?: number }>(); projects.push({ id, key, value }); return value; },
    };
    const coordinator = new WorkspaceAccessCoordinator({ api, readScope: () => scope, onDenied: extra.denied, onConfirmed: extra.confirmed });
    created.push(coordinator); return coordinator;
  }
  function complete(coordinator: WorkspaceAccessCoordinator<{ id: string; revision?: number }>, profile = account(), id = 'col-a') {
    coordinator.open(id); profiles.at(-1)!.value.next(profile); projects.at(-1)!.value.next({ id });
  }
  it('requires fresh profile then fresh exact-project access before any capture', () => {
    const c = make(); expect(c.capture()).toBeNull(); c.open('col-a'); expect(projects.length).toBe(0);
    profiles[0].value.next(account()); expect(c.capture()).toBeNull(); expect(projects[0]).toEqual(jasmine.objectContaining({ id: 'col-a', key: 'generated-key-a' }));
    projects[0].value.next({ id: 'col-a' }); expect(c.isActive()).toBeTrue(); expect(c.owner()).toEqual({ workspaceNamespace: NS, accountId: 'account-a' });
  });
  it('synchronously suspends before credential mutation and invalidates old intents', () => {
    const c = make(); complete(c); const intent = c.capture()!; let seen = false;
    c.subscribe(state => { if (state.status !== 'active') { seen = true; expect(scope.credential).toBe('generated-key-a'); expect(c.capture()).toBeNull(); } });
    notifyWorkspaceBoundary('credential-change'); expect(seen).toBeTrue(); expect(intent.current()).toBeFalse();
  });
  it('resumes rotated keys only after same namespace/account and new project confirmation', () => {
    const c = make(); complete(c); const old = c.capture()!;
    notifyWorkspaceBoundary('credential-change'); scope = { credential: 'generated-rotated', localGeneration: 1, authorityRevision: 'b'.repeat(32) };
    c.retry(); expect(profiles.at(-1)!.key).toBe('generated-rotated'); profiles.at(-1)!.value.next(account()); expect(c.isActive()).toBeFalse();
    projects.at(-1)!.value.next({ id: 'col-a', revision: 2 }); expect(c.isActive()).toBeTrue(); expect(old.current()).toBeFalse();
  });
  it('quarantines different equal-name accounts before probing their old project', () => {
    const c = make(); complete(c); const count = projects.length;
    notifyWorkspaceBoundary('credential-change'); scope = { ...scope, credential: 'generated-b' }; c.retry(); profiles.at(-1)!.value.next(account('account-b'));
    expect(c.state().status).toBe('quarantined'); expect(projects.length).toBe(count); expect(c.owner()).toBeNull(); expect(c.projectId()).toBeNull();
  });
  for (const ns of [OTHER_NS, null]) it(`does not resume the same account when namespace becomes ${ns ?? 'missing'}`, () => {
    const c = make(); complete(c); notifyWorkspaceBoundary(); c.retry(); profiles.at(-1)!.value.next(account('account-a', ns));
    expect(c.state().status).toBe('quarantined'); expect(c.capture()).toBeNull();
  });
  for (const mode of ['local', 'bootstrap'] as const) it(`allows a fresh ${mode} session but never resumes its ownerless drafts after a boundary`, () => {
    const c = make(); const profile: PrincipalProfile = { mode, authenticated: mode === 'bootstrap', name: 'generated', role: 'Admin' };
    complete(c, profile); expect(c.isActive()).toBeTrue(); expect(c.owner()).toBeNull();
    notifyWorkspaceBoundary('authority-change'); c.retry(); profiles.at(-1)!.value.next(profile);
    expect(c.state().status).toBe('quarantined'); expect(c.capture()).toBeNull();
  });
  it('permits fresh account access without a namespace but grants no durable ownership/continuity', () => {
    const c = make(); complete(c, account('account-a', null)); expect(c.isActive()).toBeTrue(); expect(c.owner()).toBeNull();
    notifyWorkspaceBoundary(); c.retry(); profiles.at(-1)!.value.next(account('account-a', null)); expect(c.state().status).toBe('quarantined');
  });
  it('fences direct key changes even when a presentation computed cached active', () => {
    const c = make(); complete(c); const presentation = computed(() => c.isActive()); expect(presentation()).toBeTrue();
    scope = { ...scope, credential: 'direct-generated-change' }; expect(presentation()).toBeTrue();
    expect(c.capture()).toBeNull(); expect(c.state().status).toBe('suspended'); expect(presentation()).toBeFalse();
  });
  it('fences shared-authority/local-generation changes before event dispatch', () => {
    const c = make(); complete(c); const old = c.capture()!; scope = { ...scope, authorityRevision: 'c'.repeat(32) };
    expect(old.current()).toBeFalse(); expect(c.isActive()).toBeFalse();
    c.retry(); profiles.at(-1)!.value.next(account()); projects.at(-1)!.value.next({ id: 'col-a' }); const second = c.capture()!;
    scope = { ...scope, localGeneration: 2 }; expect(second.current()).toBeFalse();
  });
  it('fences ABA storage events even when final credential equals the captured value', () => {
    const c = make(); complete(c); const old = c.capture()!;
    globalThis.dispatchEvent(new StorageEvent('storage', { key: 'leviathan:api-key', oldValue: 'generated-b', newValue: 'generated-key-a' }));
    expect(c.isActive()).toBeFalse(); expect(old.current()).toBeFalse();
  });
  it('rejects profile completion after its credential became stale without waiting for notification', () => {
    const c = make(); c.open('col-a'); scope = { ...scope, credential: 'generated-b' }; profiles[0].value.next(account());
    expect(projects.length).toBe(0); expect(c.isActive()).toBeFalse();
  });
  it('rejects project completion after its authority became stale', () => {
    const c = make(); c.open('col-a'); profiles[0].value.next(account()); scope = { ...scope, authorityRevision: 'd'.repeat(32) };
    projects[0].value.next({ id: 'col-a' }); expect(c.isActive()).toBeFalse();
  });
  it('rejects mismatched or absent project responses', () => {
    const c = make(); c.open('col-a'); profiles[0].value.next(account()); projects[0].value.next({ id: 'col-other' });
    expect(c.state().status).toBe('denied'); expect(c.capture()).toBeNull();
  });
  for (const status of [0, 401, 403, 404]) it(`never resumes from a fresh project error status ${status}`, () => {
    let denied = 0; const c = make({ denied: () => denied++ }); complete(c); c.retry(); profiles.at(-1)!.value.next(account());
    projects.at(-1)!.value.error({ status }); expect(c.isActive()).toBeFalse(); expect(c.state().status).toBe(status === 0 ? 'suspended' : 'denied'); expect(denied).toBe(status === 0 ? 0 : 1);
  });
  it('current denial hook fences another active coordinator and tolerates synchronous reentrancy', () => {
    const clear = () => { scope = { ...scope, localGeneration: scope.localGeneration + 1, authorityRevision: 'e'.repeat(32) }; notifyWorkspaceBoundary('authorization-denied'); };
    const a = make({ denied: clear }), b = make(); complete(a); complete(b); a.retry(); profiles.at(-1)!.value.error({ status: 403 });
    expect(a.state().status).toBe('denied'); expect(b.isActive()).toBeFalse(); expect(b.capture()).toBeNull();
  });
  it('stale denial never clears a newer authority or calls denial hooks', () => {
    let denied = 0; const c = make({ denied: () => denied++ }); c.open('col-a'); const stale = profiles[0].value;
    notifyWorkspaceBoundary(); scope = { ...scope, credential: 'generated-rotated' }; c.retry(); stale.error({ status: 403 });
    expect(denied).toBe(0); profiles.at(-1)!.value.next(account()); projects.at(-1)!.value.next({ id: 'col-a' }); expect(c.isActive()).toBeTrue();
  });
  it('updates fresh roles without assigning a new owner or parent acknowledgement', () => {
    let confirmed = 0; const c = make({ confirmed: () => confirmed++ }); complete(c); c.retry(); profiles.at(-1)!.value.next(account('account-a', NS, 'Reader')); projects.at(-1)!.value.next({ id: 'col-a' });
    expect(c.state().principal?.role).toBe('Reader'); expect(confirmed).toBe(2); expect(c.owner()?.accountId).toBe('account-a');
  });
  it('cannot activate after a reentrant boundary inside reconciliation', () => {
    const c = make({ confirmed: () => notifyWorkspaceBoundary('credential-change') }); complete(c); expect(c.isActive()).toBeFalse(); expect(c.capture()).toBeNull();
  });
  it('fails closed on reconciliation failure without clearing the mounted caller draft', () => {
    const mounted = { text: 'generated draft', undo: ['generated base'] };
    const c = make({ confirmed: () => { throw Error('generated reconciliation failure'); } }); complete(c);
    expect(c.state().status).toBe('quarantined'); expect(mounted).toEqual({ text: 'generated draft', undo: ['generated base'] });
  });
  it('ignores callbacks after destruction and makes captured intents unusable', () => {
    const c = make(); complete(c); const old = c.capture()!; c.destroy(); expect(old.current()).toBeFalse(); expect(c.capture()).toBeNull(); c.retry(); expect(c.isActive()).toBeFalse();
  });
  it('handles unavailable scope reads without recursive publication', () => {
    const api = { me: () => new Subject<PrincipalProfile>(), loadProjectFresh: () => new Subject<{ id: string }>() };
    const c = new WorkspaceAccessCoordinator({ api, readScope: () => { throw Error('generated storage failure'); } }); created.push(c);
    expect(c.state().status).toBe('suspended'); expect(c.capture()).toBeNull(); c.open('col-a'); expect(c.isActive()).toBeFalse();
  });
  it('fences all workspaces before rethrowing a faulty synchronous listener', () => {
    const a = make(), b = make(); complete(a); complete(b); const off = a.subscribe(() => { throw Error('generated listener failure'); });
    expect(() => notifyWorkspaceBoundary()).toThrow(); off(); expect(a.isActive()).toBeFalse(); expect(b.isActive()).toBeFalse();
  });
  it('prevents accidental mutation of a captured credential', () => {
    const c = make(); complete(c); const intent = c.capture()!; expect(Object.isFrozen(intent)).toBeTrue();
  });
  it('does not lose the admitted read when a synchronous state listener retries reentrantly', () => {
    const c = make(); let retried = false;
    c.subscribe(state => { if (state.reason === 'checking-access' && !retried) { retried = true; c.retry(); } });
    c.open('col-a'); expect(profiles.length).toBe(1); profiles[0].value.next(account());
    expect(projects.length).toBe(1); if (projects.length) projects[0].value.next({ id: 'col-a' });
    expect(c.isActive()).toBeTrue();
  });
  it('cannot start reauthorization synchronously inside a pre-mutation boundary callback', () => {
    const c = make(); complete(c); const before = profiles.length; let retried = false;
    c.subscribe(state => { if (state.status !== 'active' && !retried) { retried = true; c.retry(); } });
    notifyWorkspaceBoundary('credential-change'); expect(profiles.length).toBe(before); expect(c.isActive()).toBeFalse();
  });

  it('delivers reentrant state publications in monotonic FIFO order', () => {
    const c = make(); complete(c); let nested = false; const generations: number[] = [];
    c.subscribe(state => { if (state.reason === 'authority-change' && !nested) { nested = true; c.notifyBoundary('storage-change'); } });
    c.subscribe(state => { generations.push(state.generation); expect(c.capture()).toBeNull(); });
    c.notifyBoundary(); expect(generations.length).toBe(2); expect(generations[1]).toBeGreaterThan(generations[0]);
  });
  it('uses publication-time subscriber snapshots across unsubscribe/new-subscribe callbacks', () => {
    const c = make(); complete(c); let once = false, original = 0, added = 0;
    let off = () => {};
    c.subscribe(() => { if (!once) { once = true; off(); c.subscribe(() => added++); } });
    off = c.subscribe(() => original++); c.notifyBoundary(); expect(original).toBe(1); expect(added).toBe(0);
    c.notifyBoundary('storage-change'); expect(original).toBe(1); expect(added).toBe(1);
  });
  it('fences another workspace even when a read teardown throws during suspension', () => {
    const api = { me: () => new Observable<PrincipalProfile>(() => () => { throw Error('generated teardown failure'); }), loadProjectFresh: () => new Subject<{ id: string }>() };
    const a = new WorkspaceAccessCoordinator({ api, readScope: () => scope }); created.push(a); a.open('col-a');
    const b = make(); complete(b); expect(() => notifyWorkspaceBoundary()).toThrow(); expect(a.capture()).toBeNull(); expect(b.capture()).toBeNull();
  });

  for (const mode of ['local', 'bootstrap', 'account-without-namespace'] as const)
    it(`allows a second fresh project in an unchanged initial ${mode} session without inventing an owner`, () => {
      const c = make(); const profile: PrincipalProfile = mode === 'account-without-namespace' ? account('account-a', null)
        : { mode, authenticated: mode === 'bootstrap', name: 'generated initial session', role: 'Admin' };
      complete(c, profile); c.open('col-b'); profiles.at(-1)!.value.next(profile);
      expect(projects.length).toBe(2); if (projects.length === 2) projects.at(-1)!.value.next({ id: 'col-b' });
      expect(c.isActive()).toBeTrue(); expect(c.projectId()).toBe('col-b'); expect(c.owner()).toBeNull();
    });
  it('invalid project open cannot be overwritten by an earlier pending project response', () => {
    const c = make(); c.open('col-a'); profiles[0].value.next(account()); c.open('');
    projects[0].value.next({ id: 'col-a' }); expect(c.isActive()).toBeFalse(); expect(c.capture()).toBeNull();
  });

  for (const mode of ['local', 'bootstrap', 'account-without-namespace'] as const)
    for (const change of ['credential', 'generation', 'authority'] as const)
      it(`never continues initial ${mode} ownership after direct ${change} change`, () => {
        const c = make(); const profile: PrincipalProfile = mode === 'account-without-namespace' ? account('account-a', null)
          : { mode, authenticated: mode === 'bootstrap', name: 'generated', role: 'Admin' };
        complete(c, profile);
        scope = change === 'credential' ? { ...scope, credential: 'different-generated-key' }
          : change === 'generation' ? { ...scope, localGeneration: 1 } : { ...scope, authorityRevision: 'f'.repeat(32) };
        c.open('col-b'); profiles.at(-1)!.value.next(profile); expect(c.state().status).toBe('quarantined'); expect(c.capture()).toBeNull();
      });
  for (const status of [0, 403]) it(`does not restore ownerless continuity after a fresh access failure ${status}`, () => {
    const profile = account('account-a', null), c = make(); complete(c, profile); c.retry(); profiles.at(-1)!.value.next(profile);
    projects.at(-1)!.value.error({ status }); c.retry(); profiles.at(-1)!.value.next(profile); expect(c.state().status).toBe('quarantined');
  });
  it('does not attach an initial ownerless draft to a namespace that appears later', () => {
    const c = make(); complete(c, account('account-a', null)); c.retry(); profiles.at(-1)!.value.next(account()); expect(c.state().status).toBe('quarantined');
    c.retry(); profiles.at(-1)!.value.next(account('account-a', null)); expect(c.state().status).toBe('quarantined');
  });
  it('permanently quarantines ownerless continuity after invalid profile/project data', () => {
    const c = make(), profile = account('account-a', null); complete(c, profile); c.retry(); profiles.at(-1)!.value.next({ ...profile, mode: undefined });
    c.retry(); profiles.at(-1)!.value.next(profile); expect(c.state().status).toBe('quarantined');
    const other = make(); complete(other, profile); other.retry(); profiles.at(-1)!.value.next(profile); projects.at(-1)!.value.next({ id: 'wrong-generated-project' });
    other.retry(); profiles.at(-1)!.value.next(profile); expect(other.state().status).toBe('quarantined');
  });
  it('rejects malformed namespace values instead of silently treating them as absent', () => {
    const c = make(); c.open('col-a'); profiles.at(-1)!.value.next(account('account-a', 'malformed-generated-namespace')); expect(c.state().status).toBe('quarantined'); expect(projects.length).toBe(0);
  });

});
