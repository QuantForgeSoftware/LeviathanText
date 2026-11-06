import { Subject, of } from 'rxjs';
import type { ApiClient } from './api-client';
import { controlledWorkspaceApi } from './workspace-controlled-api.fixture.spec';

describe('TEST workspace controlled transport', () => {
  it('defers endpoint invocation and refuses a stale subscription', () => {
    let current = true;
    const line = jasmine.createSpy('line').and.returnValue(of({}));
    const api = controlledWorkspaceApi({ line } as unknown as ApiClient, { credential: 'generated', current: () => current });
    const response = api.line('generated-line');
    expect(line).not.toHaveBeenCalled(); current = false; response.subscribe();
    expect(line).not.toHaveBeenCalled();
  });
  it('rejects late success and late errors without an unhandled error', () => {
    let current = true; const source = new Subject<unknown>(); const seen: unknown[] = [];
    const api = controlledWorkspaceApi({ line: () => source } as unknown as ApiClient, { credential: 'generated', current: () => current });
    api.line('generated-line').subscribe({ next: value => seen.push(value), error: error => seen.push(error) });
    current = false; source.next({ old: true }); source.error(new Error('old'));
    expect(seen).toEqual([]);
  });
  it('preserves current errors and exact arguments for controller error fixtures', () => {
    const source = new Subject<unknown>(); const error = { status: 422 }; let received: unknown;
    const line = jasmine.createSpy('line').and.returnValue(source);
    const api = controlledWorkspaceApi({ line } as unknown as ApiClient, { credential: 'generated', current: () => true });
    api.line('generated-line').subscribe({ error: value => received = value }); source.error(error);
    expect(line).toHaveBeenCalledOnceWith('generated-line'); expect(received).toBe(error);
  });
  it('preserves pure URL helper values synchronously', () => {
    const api = controlledWorkspaceApi({ lineCropUrl: (id: string) => '/generated/' + id } as unknown as ApiClient,
      { credential: 'generated', current: () => false });
    expect(api.lineCropUrl('line')).toBe('/generated/line');
  });
});
