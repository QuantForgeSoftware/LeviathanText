import { WorkspaceAccessGate } from './workspace-component-scope';
import type { WorkspaceAccess, WorkspaceAccessState } from './workspace-access-types';

describe('WorkspaceAccessGate observed local identity boundary', () => {
  function parent(): WorkspaceAccess {
    const state: WorkspaceAccessState = { status: 'active', generation: 1, reason: 'generated-parent',
      principal: { mode: 'account', accountId: 'generated-a', workspaceNamespace: 'e96d2154-530e-46d6-a775-086f53acd72b', role: 'Annotator' } };
    return { state: () => state, isActive: () => true, capture: () => ({ credential: 'generated-key', projectId: 'col-generated', generation: 1, current: () => true }),
      owner: () => ({ accountId: 'generated-a', workspaceNamespace: 'e96d2154-530e-46d6-a775-086f53acd72b' }),
      projectId: () => 'col-generated', retry: () => {}, subscribe: () => () => {} };
  }
  it('never revives an intent that observed a false local gate before an ABA restoration', () => {
    let allowed = true; const gate = new WorkspaceAccessGate(parent(), () => allowed), intent = gate.capture()!;
    expect(intent.current()).toBeTrue(); allowed = false; expect(intent.current()).toBeFalse();
    allowed = true; expect(intent.current()).toBeFalse(); expect(gate.capture()!.current()).toBeTrue(); gate.destroy();
  });
  it('synchronously notifies children when state observes local loss without explicit refresh', () => {
    let allowed = true; const gate = new WorkspaceAccessGate(parent(), () => allowed); gate.capture();
    const changes: WorkspaceAccessState[] = []; gate.subscribe(state => changes.push(state));
    allowed = false; expect(gate.state().status).toBe('quarantined');
    expect(changes.some(state => state.status === 'quarantined')).toBeTrue();
    const lost = gate.state().generation; allowed = true; expect(gate.state().generation).toBeGreaterThan(lost); gate.destroy();
  });
});
