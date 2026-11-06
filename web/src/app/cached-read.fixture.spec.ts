import { Observable, tap } from 'rxjs';
import { ApiClient } from './api-client';

/** Observe the real public cached read, including its asynchronous owner binding.
 * HTTP is still flushed/asserted by the caller; neither auth nor cache is replaced. */
export function observeCachedApiReads(): {
  projects(flush: () => void): Promise<void>;
  project(id: string, flush: () => void): Promise<void>;
} {
  let pending: { scope: string; resolve: () => void; reject: (error: unknown) => void } | null = null;
  function track<T>(scope: string, source: Observable<T>): Observable<T> {
    return source.pipe(tap({
      next: () => {
        if (pending?.scope !== scope) return;
        const observation = pending; pending = null; observation.resolve();
      },
      error: error => {
        if (pending?.scope !== scope) return;
        const observation = pending; pending = null; observation.reject(error);
      },
    }));
  }
  const list = ApiClient.prototype.listProjects;
  spyOn(ApiClient.prototype, 'listProjects').and.callFake(function(this: ApiClient) {
    return track('projects', list.call(this));
  });
  const load = ApiClient.prototype.loadProject;
  spyOn(ApiClient.prototype, 'loadProject').and.callFake(function(this: ApiClient, id: string) {
    return track(`project:${id}`, load.call(this, id));
  });
  function after(scope: string, flush: () => void): Promise<void> {
    if (pending !== null) throw new Error('A cached-read fixture observation is already pending.');
    return new Promise<void>((resolve, reject) => {
      pending = { scope, resolve, reject };
      try { flush(); }
      catch (error) { pending = null; reject(error); }
    });
  }
  return { projects: flush => after('projects', flush), project: (id, flush) => after(`project:${id}`, flush) };
}
