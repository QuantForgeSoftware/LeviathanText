/** Additive fields are optional so old servers fail closed for continuity checks. */
export interface PrincipalProfile {
  authenticated: boolean;
  name: string;
  role: string;
  mode?: 'local' | 'bootstrap' | 'account';
  accountId?: string | null;
  workspaceNamespace?: string | null;
}

/** The caller must first obtain a noncached, current-request-scope /me response.
 * This only identifies a persisted principal; it grants no project access and
 * never transfers local/bootstrap/legacy draft ownership. */
export function persistedAccountFromFreshProfile(profile: unknown): string | null {
  if (typeof profile !== 'object' || profile === null) return null;
  const value = profile as Partial<PrincipalProfile>;
  return value.authenticated === true && value.mode === 'account'
    && typeof value.name === 'string' && typeof value.role === 'string'
    && typeof value.accountId === 'string' && value.accountId.length > 0
    && value.accountId === value.accountId.trim() ? value.accountId : null;
}

export interface WorkspaceOwner { workspaceNamespace: string; accountId: string; }

/** Ownership capability only after a current network response. It does not grant membership. */
export function persistedWorkspaceOwnerFromFreshProfile(profile: unknown): WorkspaceOwner | null {
  const accountId = persistedAccountFromFreshProfile(profile);
  if (accountId === null) return null;
  const namespace = (profile as PrincipalProfile).workspaceNamespace;
  if (typeof namespace !== 'string'
    || !/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/.test(namespace)
    || namespace === '00000000-0000-0000-0000-000000000000') return null;
  return { workspaceNamespace: namespace, accountId };
}
