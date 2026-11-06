import { TestBed } from '@angular/core/testing';
import { HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClient } from './api-client';
import { ApiKeyStorageKey, authInterceptor, setApiKey } from './auth';

// Generated transport controls; identity/access response-generation fencing remains a caller responsibility.
describe('principal network-only transport', () => {
  let beforeAuth: (() => void) | null = null;
  let original: string | null;
  beforeEach(() => {
    original = localStorage.getItem(ApiKeyStorageKey); setApiKey('generated-current'); beforeAuth = null;
    const schedulingBoundary: HttpInterceptorFn = (request, next) => {
      const action = beforeAuth; beforeAuth = null; action?.(); return next(request);
    };
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([schedulingBoundary, authInterceptor])), provideHttpClientTesting()] });
  });
  afterEach(() => { TestBed.inject(HttpTestingController).verify(); setApiKey(original ?? ''); });
  it('performs each /me request without read-cache fallback or stored profile authority', () => {
    const api = TestBed.inject(ApiClient); const http = TestBed.inject(HttpTestingController);
    let result: unknown;
    api.me().subscribe(value => result = value);
    const first = http.expectOne('/api/v1/me');
    expect(first.request.headers.get('Cache-Control')).toBe('no-cache, no-store');
    expect(first.request.headers.get('Pragma')).toBe('no-cache');
    first.flush({ authenticated: true, name: 'generated', role: 'Reader', mode: 'account', accountId: 'usr-generated-a' });
    expect(result).toEqual({ authenticated: true, name: 'generated', role: 'Reader', mode: 'account', accountId: 'usr-generated-a' });
    let denied = false; result = undefined;
    api.me().subscribe({ next: value => result = value, error: () => denied = true });
    http.expectOne('/api/v1/me').flush({}, { status: 401, statusText: 'Generated denial' });
    expect(denied).toBeTrue(); expect(result).toBeUndefined();
  });
  for (const credential of ['generated-captured', '']) {
    it(`pins an explicitly captured ${credential ? 'account' : 'anonymous'} credential on the actual fresh request`, () => {
      const api = TestBed.inject(ApiClient); const http = TestBed.inject(HttpTestingController);
      beforeAuth = () => localStorage.setItem(ApiKeyStorageKey, 'generated-other');
      api.me(credential).subscribe();
      const request = http.expectOne('/api/v1/me');
      expect(request.request.headers.get('X-Api-Key')).toBe(credential || null);
      expect(request.request.headers.has('Authorization')).toBeFalse();
      expect(request.request.headers.get('Cache-Control')).toBe('no-cache, no-store');
      request.flush({ authenticated: false, name: 'generated', role: 'Reader' });
    });
  }

});
