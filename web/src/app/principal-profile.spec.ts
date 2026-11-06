import { persistedAccountFromFreshProfile } from './principal-profile';

describe('persisted principal discriminator (proposed source)', () => {
  it('accepts exact persisted account identity rather than name or role', () => {
    const profile = { authenticated: true, name: 'same-name', role: 'Reader', mode: 'account' };
    expect(persistedAccountFromFreshProfile({ ...profile, accountId: 'usr-generated-a' })).toBe('usr-generated-a');
    expect(persistedAccountFromFreshProfile({ ...profile, accountId: 'usr-generated-b' })).toBe('usr-generated-b');
  });
  it('never treats local or bootstrap synthetic identity as a persisted account', () => {
    expect(persistedAccountFromFreshProfile({ authenticated: false, name: 'local', role: 'admin', mode: 'local', accountId: null })).toBeNull();
    expect(persistedAccountFromFreshProfile({ authenticated: true, name: 'bootstrap-admin', role: 'Admin', mode: 'bootstrap', accountId: 'usr-bootstrap' })).toBeNull();
  });
  it('quarantines missing, contradictory and unknown discriminator fields', () => {
    for (const profile of [null, {}, { authenticated: true, name: 'legacy', role: 'Admin' },
      { authenticated: true, name: 'x', role: 'Reader', mode: 'future', accountId: 'usr-generated' },
      { authenticated: false, name: 'x', role: 'Reader', mode: 'account', accountId: 'usr-generated' },
      { authenticated: true, name: 'x', role: 'Reader', mode: 'account', accountId: null },
      { authenticated: true, name: 'x', role: 'Reader', mode: 'account', accountId: ' ' }])
      expect(persistedAccountFromFreshProfile(profile)).toBeNull();
  });
});
