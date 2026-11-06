import { firstValueFrom } from 'rxjs';
import { ApiClient } from './api-client';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ProjectsPage } from './projects-page';
import { TEST_WORKSPACE_OWNER } from './workspace-testing.fixture.spec';
import { CacheAuthorityRevisionStorageKey, clearCache } from './offline-cache-storage';
import { ApiKeyStorageKey, setApiKey } from './auth';

describe('ProjectsPage initial cache authority', () => {
  it('renders its fresh-authorized collection read when no authority ticket existed before construction', async () => {
    const original = localStorage.getItem(ApiKeyStorageKey);
    setApiKey('generated-bootstrap-reader'); clearCache();
    localStorage.removeItem(CacheAuthorityRevisionStorageKey);
    window.history.replaceState(null, '', '/');
    TestBed.configureTestingModule({ imports: [ProjectsPage], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(ProjectsPage);
    const backend = TestBed.inject(HttpTestingController);
    try {
      backend.expectOne('/api/v1/me').flush({ authenticated: true, mode: 'account', ...TEST_WORKSPACE_OWNER, name: 'Generated reviewer', role: 'Reviewer' });
      const list = backend.expectOne('/api/v1/projects');
      expect(list.request.headers.get('Cache-Control')).toBe('no-cache, no-store');
      list.flush([{ id: 'generated', name: 'Generated collection' }]);
      expect((fixture.componentInstance as any).projects()).toEqual([{ id: 'generated', name: 'Generated collection' }]);
      expect(localStorage.getItem(CacheAuthorityRevisionStorageKey)).toMatch(/^[a-f0-9]{32}$/);
      backend.verify();
    } finally {
      fixture.destroy(); setApiKey(original ?? ''); clearCache();
      window.history.replaceState(null, '', '/');
    }
  });
  it('never treats a previously cached collection list as current authorization after a fresh refusal', async () => {
    const original = localStorage.getItem(ApiKeyStorageKey);
    setApiKey('generated-bootstrap-reader'); clearCache(); window.history.replaceState(null, '', '/');
    TestBed.configureTestingModule({ imports: [ProjectsPage], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const backend = TestBed.inject(HttpTestingController);
    const cached = firstValueFrom(TestBed.inject(ApiClient).listProjects());
    backend.expectOne('/api/v1/projects').flush([{ id: 'retained-old', name: 'Cached collection' }]);
    await cached;
    const fixture = TestBed.createComponent(ProjectsPage);
    try {
      backend.expectOne('/api/v1/me').flush({}, { status: 403, statusText: 'Forbidden' });
      expect((fixture.componentInstance as any).projects()).toEqual([]);
      backend.expectNone('/api/v1/projects');
      backend.verify();
    } finally {
      fixture.destroy(); setApiKey(original ?? ''); clearCache(); window.history.replaceState(null, '', '/');
    }
  });

});
