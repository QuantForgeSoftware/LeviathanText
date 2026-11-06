import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { LineEditor } from './line-editor';
import { apiKey, authInterceptor, setApiKey } from './auth';
import { WORKSPACE_ACCESS } from './workspace-access-types';
import type { WorkspaceAccess, WorkspaceAccessState, WorkspaceRequestIntent } from './workspace-access-types';
import { TEST_WORKSPACE_OWNER, TEST_WORKSPACE_PROJECT_ID } from './workspace-testing.fixture.spec';

class CropAccess implements WorkspaceAccess {
  private readonly value = signal<WorkspaceAccessState>({ status: 'active', generation: 0, reason: 'generated verified crop scope',
    principal: { mode: 'account', ...TEST_WORKSPACE_OWNER, role: 'Reviewer' } });
  private listeners = new Set<(state: WorkspaceAccessState) => void>();
  state() { return this.value(); }
  isActive() { return this.state().status === 'active'; }
  owner() { return this.isActive() ? { ...TEST_WORKSPACE_OWNER } : null; }
  projectId() { return this.isActive() ? TEST_WORKSPACE_PROJECT_ID : null; }
  capture(): WorkspaceRequestIntent | null {
    if (!this.isActive()) return null;
    const generation = this.state().generation, credential = apiKey();
    return { credential, projectId: TEST_WORKSPACE_PROJECT_ID, generation,
      current: () => this.isActive() && generation === this.state().generation && credential === apiKey() };
  }
  subscribe(listener: (state: WorkspaceAccessState) => void) { this.listeners.add(listener); listener(this.state()); return () => this.listeners.delete(listener); }
  retry() {}
  transition(status: 'active' | 'suspended') {
    this.value.update(old => ({ ...old, status, generation: old.generation + 1 }));
    for (const listener of this.listeners) listener(this.state());
  }
}
const line = 'generated-private-crop-line';
const cropUrl = `/api/v1/lines/${line}/crop`;
const png = () => new Blob([Uint8Array.from(atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO2QNZkAAAAASUVORK5CYII='), value => value.charCodeAt(0))], { type: 'image/png' });

describe('line crop independent credential and blob lifetime boundary', () => {
  let backend: HttpTestingController;
  let parentPreview: string;
  let currentAccess: CropAccess;
  beforeEach(() => {
    localStorage.clear(); setApiKey('generated-crop-key-a'); parentPreview = URL.createObjectURL(png()); currentAccess = new CropAccess();
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), { provide: WORKSPACE_ACCESS, useValue: currentAccess }] });
    backend = TestBed.inject(HttpTestingController);
  });
  afterEach(() => {
    for (const request of backend.match(() => true)) if (!request.cancelled) request.flush({});
    backend.verify(); URL.revokeObjectURL(parentPreview); setApiKey(''); localStorage.clear();
  });
  function setup() {
    const access = currentAccess;
    const fixture = TestBed.createComponent(LineEditor); fixture.componentRef.setInput('lineId', line);
    fixture.componentRef.setInput('surfaceId', 'generated-crop-surface'); fixture.componentRef.setInput('authorId', 'generated');
    fixture.componentRef.setInput('imageUrl', parentPreview); fixture.componentRef.setInput('previewLink', parentPreview);
    fixture.detectChanges();
    const payload = { line, diplomatic: 'GENERATED SOURCE', history: [{ id: 'generated-crop-parent', text: 'GENERATED SOURCE', author: 'generated', at: '' }] };
    function metadata() {
      for (const request of backend.match(`/api/v1/lines/${line}`)) if (!request.cancelled) request.flush(payload);
      for (const request of backend.match(`/api/v1/lines/${line}/annotations`)) if (!request.cancelled) request.flush([]);
      fixture.detectChanges();
      for (const request of backend.match('/api/v1/providers')) if (!request.cancelled) request.flush({ providers: [] });
      fixture.detectChanges();
    }
    metadata();
    function crops(): TestRequest[] { return backend.match(request => request.url === cropUrl); }
    return { fixture, access, metadata, crops };
  }

  it('never includes the long-lived credential in the actual line image DOM URL', () => {
    const s = setup();
    for (const request of s.crops()) request.flush(png());
    s.fixture.detectChanges();
    const image = s.fixture.nativeElement.querySelector('img[alt="Line source excerpt"]') as HTMLImageElement | null;
    expect(image).not.toBeNull();
    expect(image?.getAttribute('src') ?? '').not.toContain('api_key=');
    expect(image?.getAttribute('src') ?? '').not.toContain('generated-crop-key-a');
  });

  it('fetches a captured authenticated Blob through the actual client and displays its owned object URL', () => {
    const created = spyOn(URL, 'createObjectURL').and.callThrough(); const s = setup(); const requests = s.crops();
    expect(requests.length).toBe(1);
    for (const request of requests) {
      expect(request.request.responseType).toBe('blob'); expect(request.request.headers.get('X-Api-Key')).toBe('generated-crop-key-a');
      expect(request.request.urlWithParams).not.toContain('api_key'); request.flush(png());
    }
    s.fixture.detectChanges(); const image = s.fixture.nativeElement.querySelector('img[alt="Line source excerpt"]') as HTMLImageElement | null;
    expect(created).toHaveBeenCalledTimes(1); expect(image?.getAttribute('src') ?? '').toMatch(/^blob:/);
    expect(image?.getAttribute('src')).not.toBe(parentPreview);
  });

  it('does not create an object URL for a crop response arriving after suspension', () => {
    const created = spyOn(URL, 'createObjectURL').and.callThrough(); const s = setup(); const requests = s.crops();
    expect(requests.length).toBe(1); s.access.transition('suspended');
    for (const request of requests) if (!request.cancelled) request.flush(png());
    s.fixture.detectChanges(); expect(created).not.toHaveBeenCalled();
  });

  for (const stop of ['suspend', 'destroy'] as const) it(`revokes only its own crop Blob on ${stop}`, () => {
    const revoked = spyOn(URL, 'revokeObjectURL').and.callThrough(); const s = setup(); const requests = s.crops();
    expect(requests.length).toBe(1); for (const request of requests) request.flush(png()); s.fixture.detectChanges();
    const image = s.fixture.nativeElement.querySelector('img[alt="Line source excerpt"]') as HTMLImageElement | null;
    const owned = image?.getAttribute('src') ?? '';
    if (stop === 'suspend') { s.access.transition('suspended'); s.fixture.detectChanges(); } else s.fixture.destroy();
    expect(owned).toMatch(/^blob:/); expect(revoked).toHaveBeenCalledWith(owned); expect(revoked).not.toHaveBeenCalledWith(parentPreview);
  });

  it('reloads the crop with captured rotated credentials even when the line ID stays equal', () => {
    const s = setup(); const first = s.crops(); expect(first.length).toBe(1);
    for (const request of first) request.flush(png()); s.fixture.detectChanges();
    s.access.transition('suspended'); setApiKey('generated-crop-key-b'); s.access.transition('active'); s.metadata();
    const next = s.crops(); expect(next.length).toBe(1);
    for (const request of next) { expect(request.request.headers.get('X-Api-Key')).toBe('generated-crop-key-b'); request.flush(png()); }
    s.fixture.detectChanges();
    expect((s.fixture.nativeElement.querySelector('img') as HTMLImageElement | null)?.getAttribute('src') ?? '').not.toContain('api_key');
    expect((s.fixture.nativeElement.querySelector('a') as HTMLAnchorElement | null)?.getAttribute('href')).toBe(parentPreview);
  });
});
