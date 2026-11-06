import { HttpClient, HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiClient } from './api-client';
import { ApiKeyStorageKey, authInterceptor, setApiKey } from './auth';
import { clearCache } from './offline-cache-storage';

/** Actual optional request contexts, composed with the real interceptor and generated HTTP only. */
describe('ProjectsPage request credential composition', () => {
  let client: ApiClient;
  let backend: HttpTestingController;
  let beforeAuth: (() => void) | null;
  const routes: ReadonlyArray<{
    name: string;
    path: string;
    method: string;
    responseType: 'json' | 'blob' | 'text';
    body?: object;
    response: object | Blob | string;
    send: (client: ApiClient, credential?: string) => Observable<unknown>;
  }> = [
    { name: 'createProject', path: '/api/v1/projects', method: 'POST', responseType: 'json',
      body: { name: 'Generated collection' }, response: { id: 'col-generated', name: 'Generated collection' },
      send: (c, key) => c.createProject('Generated collection', key) },
    { name: 'stats', path: '/api/v1/projects/col-generated/stats', method: 'GET', responseType: 'json',
      response: { lines: 1 }, send: (c, key) => c.stats('col-generated', key) },
    { name: 'reviewSummary', path: '/api/v1/projects/col-generated/review-summary', method: 'GET', responseType: 'json',
      response: { lines: [], needsAdjudication: 0 }, send: (c, key) => c.reviewSummary('col-generated', key) },
    { name: 'search', path: '/api/v1/projects/col-generated/search', method: 'GET', responseType: 'json',
      response: { totalHits: 0, truncated: false, hits: [] }, send: (c, key) => c.search('col-generated', 'generated query', 'literal', key) },
    { name: 'importLineText', path: '/api/v1/projects/col-generated/import-line-text', method: 'POST', responseType: 'json',
      body: { csvText: 'line_id,text\nln-generated,Generated source', layer: 'L1Diplomatic' },
      response: { applied: 1, skipped: 0, rows: [] },
      send: (c, key) => c.importLineText('col-generated', 'line_id,text\nln-generated,Generated source', 'L1Diplomatic', key) },
    { name: 'projectSearchablePdf', path: '/api/v1/projects/col-generated/searchable-pdf', method: 'GET', responseType: 'blob',
      response: new Blob(['Generated PDF fixture'], { type: 'application/pdf' }), send: (c, key) => c.projectSearchablePdf('col-generated', key) },
    { name: 'projectEditionHtml', path: '/api/v1/projects/col-generated/edition.html', method: 'GET', responseType: 'text',
      response: '<p>Generated edition</p>', send: (c, key) => c.projectEditionHtml('col-generated', key) },
  ];
  beforeEach(() => {
    localStorage.clear(); clearCache(); setApiKey('generated-reader-a'); beforeAuth = null;
    const schedulingBoundary: HttpInterceptorFn = (request, next) => {
      const action = beforeAuth; beforeAuth = null; action?.();
      return next(request);
    };
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([schedulingBoundary, authInterceptor])), provideHttpClientTesting(),
    ] });
    client = new ApiClient(TestBed.inject(HttpClient)); backend = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { backend.verify(); setApiKey(''); clearCache(); localStorage.clear(); });

  for (const route of routes) {
    for (const mode of ['captured A', 'captured anonymous', 'omitted credential'] as const) {
      it(`${route.name} preserves ${mode} behavior through a before-auth storage change`, async () => {
        const supplied = mode === 'captured A' ? 'generated-reader-a' : mode === 'captured anonymous' ? '' : undefined;
        beforeAuth = () => localStorage.setItem(ApiKeyStorageKey, 'generated-reader-b');
        const result = firstValueFrom(route.send(client, supplied));
        const request = backend.expectOne(r => r.url === route.path);
        expect(request.request.method).toBe(route.method);
        expect(request.request.responseType).toBe(route.responseType);
        if (route.body) expect(request.request.body).toEqual(route.body);
        if (route.name === 'search') {
          expect(request.request.params.get('q')).toBe('generated query');
          expect(request.request.params.get('mode')).toBe('literal');
        }
        if (mode === 'captured anonymous') expect(request.request.headers.has('X-Api-Key')).toBeFalse();
        else expect(request.request.headers.get('X-Api-Key')).toBe(mode === 'captured A' ? 'generated-reader-a' : 'generated-reader-b');
        expect(request.request.headers.has('Authorization')).toBeFalse();
        request.flush(route.response);
        expect(await result).toEqual(route.response);
      });
    }
  }
});
