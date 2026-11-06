import { persistedWorkspaceOwnerFromFreshProfile } from './principal-profile';

describe('explicit persisted workspace owner', () => {
  const namespace = '9da6ed1b-167c-4ecf-982b-39e45dd0629d';
  const account = { authenticated: true, name: 'generated', role: 'Reader', mode: 'account', accountId: 'usr-generated-a', workspaceNamespace: namespace };
  it('requires both stable store namespace and account rather than a credential or display name', () => {
    expect(persistedWorkspaceOwnerFromFreshProfile(account)).toEqual({ workspaceNamespace: namespace, accountId: 'usr-generated-a' });
    expect(persistedWorkspaceOwnerFromFreshProfile({ ...account, name: 'changed', role: 'Reviewer' }))
      .toEqual(persistedWorkspaceOwnerFromFreshProfile(account));
  });
  it('treats a changed namespace as a different owner even with the same account ID', () => {
    expect(persistedWorkspaceOwnerFromFreshProfile({ ...account, workspaceNamespace: '601f6a24-564e-477e-a470-be20e7c498a0' }))
      .not.toEqual(persistedWorkspaceOwnerFromFreshProfile(account));
  });
  it('refuses missing, malformed and empty namespaces without inheriting a previous value', () => {
    for (const value of [undefined, null, '', 'generated-key', namespace.toUpperCase(), ` ${namespace} `, '00000000-0000-0000-0000-000000000000'])
      expect(persistedWorkspaceOwnerFromFreshProfile({ ...account, workspaceNamespace: value })).toBeNull();
  });
  it('never turns bootstrap/local or a legacy response into a durable owner', () => {
    for (const profile of [{ ...account, mode: 'bootstrap' }, { ...account, mode: 'local' }, { ...account, mode: undefined }, null])
      expect(persistedWorkspaceOwnerFromFreshProfile(profile)).toBeNull();
  });
  it('distinguishes same-name accounts with common project access', () => {
    expect(persistedWorkspaceOwnerFromFreshProfile({ ...account, accountId: 'usr-generated-b' }))
      .not.toEqual(persistedWorkspaceOwnerFromFreshProfile(account));
  });
});
