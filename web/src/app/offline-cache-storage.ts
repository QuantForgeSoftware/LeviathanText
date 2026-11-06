/** Read-only response storage; never a source-document or draft store. */
import { signal } from '@angular/core';
import { notifyWorkspaceBoundary } from './workspace-access';

const LegacyPrefix = 'leviathan:cache:';
const Prefix = `${LegacyPrefix}v3:`;
export const CacheAuthorityRevisionStorageKey = 'leviathan:cache-authority-revision';
export interface ReadCacheScope { ownerSha256: string; authorityRevision: string; }
let generation = 0;
let legacyPruned = false;
export const offlineMode = signal(false);
export function readCacheGeneration(): number { return generation; }
export function cacheAuthorityRevision(): string | null {
  try {
    const value = globalThis.localStorage?.getItem(CacheAuthorityRevisionStorageKey);
    return value && /^[a-f0-9]{32}$/.test(value) ? value : null;
  } catch { return null; }
}

export function ensureCacheAuthorityRevision(): string | null {
  const existing = cacheAuthorityRevision();
  if (existing) return existing;
  try {
    const words = globalThis.crypto.getRandomValues(new Uint8Array(16));
    const value = Array.from(words, byte => byte.toString(16).padStart(2, '0')).join('');
    globalThis.localStorage?.setItem(CacheAuthorityRevisionStorageKey, value);
    return cacheAuthorityRevision();
  } catch { return null; }
}

function pruneLegacyCache(): void {
  if (legacyPruned) return;
  const storage = globalThis.localStorage;
  if (!storage) return;
  const keys: string[] = [];
  for (let index = 0; index < storage.length; index++) {
    const key = storage.key(index);
    if (key?.startsWith(LegacyPrefix) && !key.startsWith(Prefix)) keys.push(key);
  }
  for (const key of keys) storage.removeItem(key);
  legacyPruned = true;
}

/** Unscoped access remains a storage utility; HTTP reads always supply their captured scope. */
export function readCache<T>(key: string, scope?: ReadCacheScope): T | null {
  try {
    pruneLegacyCache();
    const raw = globalThis.localStorage?.getItem(Prefix + key);
    if (!raw) return null;
    const entry = JSON.parse(raw);
    if (entry?.version !== 3 || !Object.prototype.hasOwnProperty.call(entry, 'value')) return null;
    if (scope && (entry.ownerSha256 !== scope.ownerSha256 || entry.authorityRevision !== scope.authorityRevision)) return null;
    return entry.value as T;
  } catch { return null; }
}

export function writeCache(key: string, value: unknown, scope?: ReadCacheScope): void {
  try {
    pruneLegacyCache();
    globalThis.localStorage?.setItem(Prefix + key, JSON.stringify({ version: 3,
      ownerSha256: scope?.ownerSha256 ?? null, authorityRevision: scope?.authorityRevision ?? null, value }));
  } catch { /* Caching is optional when storage is unavailable or full. */ }
}

/** Broadcast removal fences other tabs synchronously, before queued storage events run. */
export function clearCache(broadcast = true): void {
  generation++;
  try { notifyWorkspaceBoundary('authority-change'); }
  catch { /* All coordinator states were fenced before listener errors; still fence shared storage. */ }
  try {
    const storage = globalThis.localStorage;
    if (storage) {
      if (broadcast) storage.removeItem(CacheAuthorityRevisionStorageKey);
      const keys: string[] = [];
      for (let index = 0; index < storage.length; index++) {
        const key = storage.key(index);
        if (key?.startsWith(LegacyPrefix)) keys.push(key);
      }
      for (const key of keys) storage.removeItem(key);
      if (broadcast) ensureCacheAuthorityRevision();
    }
  } catch { /* Leave credentials, source documents and recovery drafts intact. */ }
  offlineMode.set(false);
}
