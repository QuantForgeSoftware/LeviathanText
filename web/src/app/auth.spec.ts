import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiKey, authInterceptor, capturedApiKeyContext, eventsUrlWithKey, setApiKey } from './auth';

describe('workspace auth', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.verify();
    localStorage.clear();
  });

  it('adds X-Api-Key when configured and nothing otherwise', () => {
    http.get('/api/v1/projects').subscribe();
    backend.expectOne('/api/v1/projects').flush([]);

    setApiKey('secret-key');
    http.get('/api/v1/projects').subscribe();
    const request = backend.expectOne('/api/v1/projects');
    expect(request.request.headers.get('X-Api-Key')).toBe('secret-key');
    request.flush([]);

    expect(apiKey()).toBe('secret-key');
    expect(eventsUrlWithKey('/api/v1/jobs/j1/events')).toBe('/api/v1/jobs/j1/events?api_key=secret-key');
  });

  it('pins the captured request credential even when shared storage changes before dispatch', () => {
    setApiKey('generated-reader-a');
    const context = capturedApiKeyContext(apiKey());
    localStorage.setItem('leviathan:api-key', 'generated-reader-b');
    http.get('/api/v1/projects', { context, headers: { Authorization: 'Bearer generated-conflicting', 'X-Api-Key': 'generated-conflicting' } }).subscribe();
    const request = backend.expectOne('/api/v1/projects');
    expect(request.request.headers.get('X-Api-Key')).toBe('generated-reader-a');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush([]);
  });

  it('preserves an explicitly captured anonymous request after a key is configured', () => {
    const context = capturedApiKeyContext(''); setApiKey('generated-reader-b');
    http.get('/api/v1/projects', { context }).subscribe();
    const request = backend.expectOne('/api/v1/projects');
    expect(request.request.headers.has('X-Api-Key')).toBeFalse(); request.flush([]);
  });
});
