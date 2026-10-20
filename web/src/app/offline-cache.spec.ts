import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { cachedRequest, clearCache, offlineMode, readCache, writeCache } from './offline-cache';

describe('offline cache', () => {
  beforeEach(() => {
    localStorage.clear();
    offlineMode.set(false);
  });
  afterEach(() => localStorage.clear());

  it('writes successful reads and falls back on transport errors', () => {
    cachedRequest('projects', of([{ id: 'p1' }])).subscribe();
    expect(readCache<{ id: string }[]>('projects')).toEqual([{ id: 'p1' }]);
    expect(offlineMode()).toBe(false);

    let result: unknown = null;
    cachedRequest('projects', throwError(() => new Error('offline')))
      .subscribe(value => (result = value));
    expect(result).toEqual([{ id: 'p1' }]);
    expect(offlineMode()).toBe(true);
  });

  it('does not invent data when nothing is cached', () => {
    let failed = false;
    cachedRequest('missing', throwError(() => new Error('offline'))).subscribe({ error: () => (failed = true) });
    expect(failed).toBe(true);
    expect(offlineMode()).toBe(false);
  });

  it('clears only cache keys', () => {
    writeCache('a', 1);
    localStorage.setItem('leviathan:api-key', 'secret');
    clearCache();
    expect(readCache('a')).toBeNull();
    expect(localStorage.getItem('leviathan:api-key')).toBe('secret');
  });
});
