/**
 * Shared-deployment API key for the workspace. The key is kept in localStorage and attached
 * by an interceptor. Job streams use a captured X-Api-Key fetch header, never a
 * credential URL. The legacy URL helper remains for compatibility with its other
 * callers. Nothing is sent when no key is configured (local-only mode).
 */
import { signal } from '@angular/core';
import { EMPTY } from 'rxjs';
import { HttpContext, HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { CacheAuthorityRevisionStorageKey, clearCache, readCacheGeneration } from './offline-cache-storage';

export const ApiKeyStorageKey = 'leviathan:api-key';
const StorageKey = ApiKeyStorageKey;
/** Change notification only; it retains no credential value or history. */
export const apiKeyRevision = signal(0);
/** Request-scope generation only; no credential value or history is retained. */
export function cacheCredentialGeneration(): number { return readCacheGeneration(); }

// Components already observe storage for their own reactive lifecycle. Keep this
// imperative cache generation separate so they do not receive duplicate revisions.
globalThis.addEventListener?.('storage', (event: StorageEvent) => {
  if (event.key === null || event.key === ApiKeyStorageKey || event.key === CacheAuthorityRevisionStorageKey)
    clearCache(false); // Do not rebroadcast a received authority notification.
});

const CapturedApiKey = new HttpContextToken<string | null>(() => null);
const CapturedRequestCurrent = new HttpContextToken<(() => boolean) | null>(() => null);
export function capturedApiKeyContext(credential: string, current?: () => boolean): HttpContext {
  return new HttpContext().set(CapturedApiKey, credential).set(CapturedRequestCurrent, current ?? null);
}

export function apiKey(): string {
  try {
    return globalThis.localStorage?.getItem(StorageKey) ?? '';
  } catch {
    return '';
  }
}

export function setApiKey(key: string): void {
  const previous = apiKey();
  try {
    const trimmed = key.trim();
    if (trimmed !== previous) clearCache(); // Fence requests before mutating shared credentials.
    if (trimmed) globalThis.localStorage?.setItem(StorageKey, trimmed);
    else globalThis.localStorage?.removeItem(StorageKey);
  } catch {
    /* storage unavailable: requests stay unauthenticated */
  }
  if (apiKey() !== previous) {
    apiKeyRevision.update(version => version + 1);
  }
}

export function hasApiKey(): boolean {
  return apiKey().length > 0;
}

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  // Recheck after any preceding scheduling interceptor, at auth dispatch itself.
  const current = request.context.get(CapturedRequestCurrent);
  if (current && !current()) return EMPTY;
  const captured = request.context.get(CapturedApiKey);
  const key = captured ?? apiKey();
  if (captured !== null) {
    let headers = request.headers.delete('X-Api-Key').delete('Authorization');
    if (key) headers = headers.set('X-Api-Key', key);
    return next(request.clone({ headers }));
  }
  return key
    ? next(request.clone({ setHeaders: { 'X-Api-Key': key } }))
    : next(request);
};

export function eventsUrlWithKey(url: string): string {
  const key = apiKey();
  if (!key) return url;
  const separator = url.includes('?') ? '&' : '?';
  return `${url}${separator}api_key=${encodeURIComponent(key)}`;
}
