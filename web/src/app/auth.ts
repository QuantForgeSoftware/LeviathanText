/**
 * Shared-deployment API key for the workspace. The key is kept in localStorage and attached
 * by an interceptor; SSE requests append it as a query token because EventSource cannot set
 * headers. Nothing is sent when no key is configured (local-only mode).
 */
import { HttpInterceptorFn } from '@angular/common/http';

const StorageKey = 'leviathan:api-key';

export function apiKey(): string {
  try {
    return globalThis.localStorage?.getItem(StorageKey) ?? '';
  } catch {
    return '';
  }
}

export function setApiKey(key: string): void {
  try {
    const trimmed = key.trim();
    if (trimmed) globalThis.localStorage?.setItem(StorageKey, trimmed);
    else globalThis.localStorage?.removeItem(StorageKey);
  } catch {
    /* storage unavailable: requests stay unauthenticated */
  }
}

export function hasApiKey(): boolean {
  return apiKey().length > 0;
}

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const key = apiKey();
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
