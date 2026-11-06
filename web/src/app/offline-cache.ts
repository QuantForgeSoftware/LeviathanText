/** Network-first response cache, bound to the actual request credential and authority revision. */
import { Observable, catchError, defer, from, mergeMap, of, throwError } from 'rxjs';
import { apiKey } from './auth';
import { cacheAuthorityRevision, clearCache, ensureCacheAuthorityRevision, offlineMode,
  readCache, readCacheGeneration, ReadCacheScope, writeCache } from './offline-cache-storage';
export { clearCache, offlineMode, readCache, writeCache } from './offline-cache-storage';

export class CacheCredentialChangedError extends Error {
  constructor() { super('Credentials or cached authority changed while the read was pending.'); }
}

async function ownerDigest(credential: string): Promise<string | null> {
  try {
    const bytes = new TextEncoder().encode(`LeviathanText response cache v3\0${credential}`);
    const hash = await globalThis.crypto.subtle.digest('SHA-256', bytes);
    return Array.from(new Uint8Array(hash), byte => byte.toString(16).padStart(2, '0')).join('');
  } catch { return null; } // No cryptographic binding means no persistent HTTP caching.
}

/** HTTP factories must pin the supplied credential on their actual request context. */
export function cachedRequest<T>(key: string, source: Observable<T> | ((credential: string) => Observable<T>), requestCurrent?: () => boolean): Observable<T> {
  return defer(() => {
    if (requestCurrent && !requestCurrent()) return throwError(() => new CacheCredentialChangedError());
    const credential = apiKey(); // Active request snapshot only; never a persisted key/history.
    const authority = ensureCacheAuthorityRevision();
    const generation = readCacheGeneration();
    const current = () => (requestCurrent?.() ?? true)
      && generation === readCacheGeneration() && credential === apiKey()
      && authority === cacheAuthorityRevision();
    const scope = ownerDigest(credential).then(owner => owner && authority
      ? { ownerSha256: owner, authorityRevision: authority } satisfies ReadCacheScope : null);
    const request = typeof source === 'function' ? source(credential) : source;
    return request.pipe(
      mergeMap(value => from(scope).pipe(mergeMap(binding => {
        if (!current()) return throwError(() => new CacheCredentialChangedError());
        if (binding) writeCache(key, value, binding);
        if (!current()) return throwError(() => new CacheCredentialChangedError());
        offlineMode.set(false);
        return of(value);
      }))),
      catchError(error => {
        if (!current()) return throwError(() => new CacheCredentialChangedError());
        const status: unknown = error?.status;
        // HTTP responses remain authoritative; legacy non-HTTP observable errors
        // retain fallback semantics only inside the same credential/authority scope.
        if (status !== undefined && status !== 0) {
          if (status === 401 || status === 403 || status === 404) clearCache();
          offlineMode.set(false);
          return throwError(() => error);
        }
        // Fence authoritative denials above immediately; digest latency must never
        // leave an older request able to refill revoked authority.
        return from(scope).pipe(mergeMap(binding => {
          if (!current()) return throwError(() => new CacheCredentialChangedError());
          const cached = binding ? readCache<T>(key, binding) : null;
          if (!current()) return throwError(() => new CacheCredentialChangedError());
          if (cached === null) return throwError(() => error);
          offlineMode.set(true);
          return of(cached);
        }));
      }),
    );
  });
}
