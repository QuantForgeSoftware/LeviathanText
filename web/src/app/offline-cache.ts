/**
 * Local cache for read-only API data (todo §4: keep cached projects usable offline). Writes
 * happen on every successful read; on a transport error the last cached value is returned and
 * an offline flag is raised so the UI can say the data is stale. Mutations are never replayed
 * automatically — drafts and the revision API remain the authority.
 */
import { Observable, catchError, of, tap, throwError } from 'rxjs';
import { signal } from '@angular/core';

const Prefix = 'leviathan:cache:';
export const offlineMode = signal(false);

export function readCache<T>(key: string): T | null {
  try {
    const raw = globalThis.localStorage?.getItem(Prefix + key);
    return raw ? (JSON.parse(raw) as T) : null;
  } catch {
    return null;
  }
}

export function writeCache(key: string, value: unknown): void {
  try {
    globalThis.localStorage?.setItem(Prefix + key, JSON.stringify(value));
  } catch {
    /* quota/private mode: caching degrades to none */
  }
}

export function clearCache(): void {
  try {
    const storage = globalThis.localStorage;
    if (!storage) return;
    const keys: string[] = [];
    for (let i = 0; i < storage.length; i++) {
      const key = storage.key(i);
      if (key?.startsWith(Prefix)) keys.push(key);
    }
    for (const key of keys) storage.removeItem(key);
  } catch {
    /* ignore */
  }
  offlineMode.set(false);
}

export function cachedRequest<T>(key: string, source: Observable<T>): Observable<T> {
  return source.pipe(
    tap(value => {
      writeCache(key, value);
      offlineMode.set(false);
    }),
    catchError(error => {
      const cached = readCache<T>(key);
      if (cached === null) return throwError(() => error);
      offlineMode.set(true);
      return of(cached);
    }),
  );
}
