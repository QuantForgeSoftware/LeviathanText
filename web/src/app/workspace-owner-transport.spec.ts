import { TestBed } from '@angular/core/testing';
import { HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClient } from './api-client';
import { ApiKeyStorageKey, authInterceptor, setApiKey } from './auth';

describe('fresh workspace membership transport', () => {
  let original: string | null;
  let boundary: (() => void) | null;
  beforeEach(() => {
    original = localStorage.getItem(ApiKeyStorageKey); setApiKey('generated-current'); boundary = null;
    const schedulingBoundary: HttpInterceptorFn = (request, next) => {
      const action = boundary; boundary = null; action?.(); return next(request);
    };
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([schedulingBoundary, authInterceptor])), provideHttpClientTesting()] });
  });
  afterEach(() => { TestBed.inject(HttpTestingController).verify(); setApiKey(original ?? ''); });
  for (const credential of ['generated-captured', '']) {
    it(`pins the ${credential ? 'captured account' : 'captured anonymous'} membership check across an interceptor boundary`, () => {
      const api = TestBed.inject(ApiClient); const http = TestBed.inject(HttpTestingController);
      boundary = () => localStorage.setItem(ApiKeyStorageKey, 'generated-other');
      api.loadProjectFresh('generated/project', credential).subscribe();
      const request = http.expectOne('/api/v1/projects/generated%2Fproject');
      expect(request.request.headers.get('X-Api-Key')).toBe(credential || null);
      expect(request.request.headers.has('Authorization')).toBeFalse();
      expect(request.request.headers.get('Cache-Control')).toBe('no-cache, no-store');
      expect(request.request.headers.get('Pragma')).toBe('no-cache');
      request.flush({ id: 'generated/project', name: 'generated', objects: [] });
    });
  }
  it('preserves the current-credential caller and propagates a network refusal without cached membership', () => {
    const api = TestBed.inject(ApiClient); const http = TestBed.inject(HttpTestingController);
    let success = false; let failed = false;
    api.loadProjectFresh('generated-project').subscribe({ next: () => success = true, error: () => failed = true });
    const request = http.expectOne('/api/v1/projects/generated-project');
    expect(request.request.headers.get('X-Api-Key')).toBe('generated-current');
    request.error(new ProgressEvent('generated-offline'));
    expect(failed).toBeTrue(); expect(success).toBeFalse();
  });
});
