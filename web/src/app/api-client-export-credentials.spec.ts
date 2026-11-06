import { HttpClient, HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiClient } from './api-client';
import { ApiKeyStorageKey, authInterceptor, setApiKey } from './auth';
import { clearCache } from './offline-cache-storage';

/** Actual optional request contexts, composed with the real interceptor and generated HTTP only. */
describe('authenticated export request credential composition', () => {
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
    { name: 'tei', path: '/api/v1/projects/col-generated/surfaces/surf-generated/tei', method: 'GET', responseType: 'text',
      response: '<TEI>Generated</TEI>', send: (c, key) => c.tei('col-generated', 'surf-generated', key) },
    { name: 'editionHtml', path: '/api/v1/projects/col-generated/surfaces/surf-generated/edition.html', method: 'GET', responseType: 'text',
      response: '<p>Generated edition</p>', send: (c, key) => c.editionHtml('col-generated', 'surf-generated', key) },
    { name: 'wordBoundaryCsv', path: '/api/v1/projects/col-generated/word-boundaries', method: 'GET', responseType: 'text',
      response: 'line_id,start,end', send: (c, key) => c.wordBoundaryCsv('col-generated', key) },
    { name: 'searchablePdf', path: '/api/v1/projects/col-generated/surfaces/surf-generated/searchable-pdf', method: 'GET', responseType: 'blob',
      response: new Blob(['Generated PDF'], { type: 'application/pdf' }), send: (c, key) => c.searchablePdf('col-generated', 'surf-generated', key) },
    { name: 'trainingExport', path: '/api/v1/projects/col-generated/training-export', method: 'GET', responseType: 'json',
      response: { generated: true, lines: [] }, send: (c, key) => c.trainingExport('col-generated', key) },
    { name: 'projectBundle', path: '/api/v1/projects/col-generated/bundle', method: 'GET', responseType: 'blob',
      response: new Blob(['Generated ZIP'], { type: 'application/zip' }), send: (c, key) => c.projectBundle('col-generated', key) },
    { name: 'adjudicationCsv', path: '/api/v1/projects/col-generated/adjudication.csv', method: 'GET', responseType: 'text',
      response: 'line_id,generated', send: (c, key) => c.adjudicationCsv('col-generated', key) },
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
        expect(request.request.params.has('api_key')).toBeFalse();
        expect(request.request.urlWithParams).not.toContain('generated-reader-');
        expect(request.request.responseType).toBe(route.responseType);
        if (route.body) expect(request.request.body).toEqual(route.body);
        if (mode === 'captured anonymous') expect(request.request.headers.has('X-Api-Key')).toBeFalse();
        else expect(request.request.headers.get('X-Api-Key')).toBe(mode === 'captured A' ? 'generated-reader-a' : 'generated-reader-b');
        expect(request.request.headers.has('Authorization')).toBeFalse();
        request.flush(route.response);
        expect(await result).toEqual(route.response);
      });
    }
  }
});
