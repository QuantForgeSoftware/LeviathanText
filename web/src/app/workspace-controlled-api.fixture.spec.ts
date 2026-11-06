/** TEST ONLY: prototype endpoint spies bypass HttpClient's workspace handler.
 * Preserve that handler's captured-intent admission/emission fence for generated
 * callback scheduling fixtures. This is not an HTTP or credential-pinning proof.
 */
import { defer, filter, catchError, EMPTY, throwError, Observable } from 'rxjs';
import type { ApiClient } from './api-client';

export function controlledWorkspaceApi(api: ApiClient, intent: { credential: string; current(): boolean }): ApiClient {
  const pureUrlHelpers = new Set(['lineCropUrl', 'jobEventsUrl', 'adjudicationUrl', 'captureDisplayPath']);
  return new Proxy(api, { get(target, key) {
    const member = Reflect.get(target, key);
    if (typeof member !== 'function') return member;
    if (typeof key === 'string' && pureUrlHelpers.has(key))
      return (...args: unknown[]) => Reflect.apply(member, api, args);
    return (...args: unknown[]) => defer(() => {
      if (!intent.current()) return EMPTY;
      const source = Reflect.apply(member, api, args) as Observable<unknown>;
      return source.pipe(filter(() => intent.current()),
        catchError(error => intent.current() ? throwError(() => error) : EMPTY));
    });
  } }) as ApiClient;
}
