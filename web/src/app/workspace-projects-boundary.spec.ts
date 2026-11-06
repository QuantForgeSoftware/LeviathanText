import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ProjectsPage } from './projects-page';
import { authInterceptor, setApiKey } from './auth';

describe('Workspace collection admission and ownerless bootstrap', () => {
  let http: HttpTestingController, fixture: ComponentFixture<ProjectsPage> | undefined;
  const local = { authenticated: false, name: 'local', role: 'admin', mode: 'local', accountId: null, workspaceNamespace: null };
  const account = (id: string) => ({ authenticated: true, name: 'equal-generated-name', role: 'Annotator', mode: 'account', accountId: id,
    workspaceNamespace: '6ed5c4e3-8bbb-41bd-9663-a96ee2672a23' });
  beforeEach(() => {
    localStorage.clear(); setApiKey(''); history.replaceState(null, '', location.pathname);
    TestBed.configureTestingModule({ imports: [ProjectsPage], providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { fixture?.destroy(); http.verify({ ignoreCancelled: true }); TestBed.resetTestingModule(); localStorage.clear(); fixture = undefined; });
  const create = () => { fixture = TestBed.createComponent(ProjectsPage); fixture.detectChanges(); return fixture.componentInstance as any; };
  it('permits an initial local retry when no private list or work was ever accepted', () => {
    const component = create(); http.expectOne('/api/v1/me').flush(local);
    http.expectOne('/api/v1/projects').error(new ProgressEvent('error'), { status: 0 });
    component.retryAccess(); http.expectOne('/api/v1/me').flush(local);
    const requests = http.match('/api/v1/projects'); expect(requests.length).toBe(1);
    requests.forEach(request => request.flush([{ id: 'col-generated', name: 'generated private collection' }]));
    fixture!.detectChanges(); expect(component.collectionsAvailable()).toBeTrue();
  });
  it('does not resume an anchored ownerless workspace after an invalid fresh principal', () => {
    const component = create(); http.expectOne('/api/v1/me').flush(local);
    http.expectOne('/api/v1/projects').flush([{ id: 'col-generated', name: 'generated original private collection' }]);
    component.retryAccess(); http.expectOne('/api/v1/me').flush({ authenticated: true, role: 'Reader', mode: 'account', name: 'invalid', accountId: null });
    component.retryAccess(); http.expectOne('/api/v1/me').flush(local);
    const requests = http.match('/api/v1/projects'); expect(requests.length).toBe(0); requests.forEach(request => request.flush([]));
    expect(component.collectionsAvailable()).toBeFalse();
  });
  it('rejects an older list reply after a newer invalid identity check', () => {
    const component = create(); http.expectOne('/api/v1/me').flush(local);
    const oldList = http.expectOne('/api/v1/projects');
    component.retryAccess(); http.expectOne('/api/v1/me').flush({ authenticated: true, role: 'Reader', mode: 'account', accountId: null });
    oldList.flush([{ id: 'col-old-response', name: 'must never become current private presentation' }]);
    fixture!.detectChanges(); expect(component.collectionsAvailable()).toBeFalse();
    expect(component.projects()).toEqual([]);
  });
  it('does not reactively retry its own coordinator publication after one key change', () => {
    setApiKey('generated-key-a'); const component = create();
    http.expectOne('/api/v1/me').flush(account('account-a'));
    http.expectOne('/api/v1/projects').flush([{ id: 'col-generated', name: 'generated current collection' }]);
    component.select('col-generated'); http.expectOne('/api/v1/me').flush(account('account-a'));
    http.expectOne('/api/v1/projects/col-generated').flush({ id: 'col-generated', name: 'generated current collection', objects: [] });
    http.match(request => request.url !== '/api/v1/me').forEach(request => request.flush([]));
    fixture!.detectChanges();
    const coordinator = component.workspaceAccess, actual = coordinator.retry.bind(coordinator); let calls = 0;
    spyOn(coordinator, 'retry').and.callFake(() => { calls++; if (calls <= 2) actual(); }); // Bound a real recursive-effect failure.
    setApiKey('generated-key-b'); fixture!.detectChanges();
    expect(calls).toBe(1);
    http.match('/api/v1/me').forEach(request => { if (!request.cancelled) request.flush(account('account-b')); });
  });
  it('requires fresh identity/list and synchronously conceals prior private collections on key change', () => {
    setApiKey('generated-key-a'); const component = create();
    http.expectNone('/api/v1/projects'); const me = http.expectOne('/api/v1/me'); expect(me.request.headers.get('X-Api-Key')).toBe('generated-key-a'); me.flush(account('account-a'));
    const list = http.expectOne('/api/v1/projects'); expect(list.request.headers.get('Cache-Control')).toContain('no-store'); list.flush([{ id: 'col-generated', name: 'generated secret collection A' }]);
    fixture!.detectChanges(); const protectedList = fixture!.nativeElement.querySelector('[data-workspace-collections]') as HTMLElement;
    expect(protectedList.hidden).toBeFalse(); setApiKey('generated-key-b');
    expect(protectedList.hidden).toBeTrue(); expect(protectedList.inert).toBeTrue(); fixture!.detectChanges();
    const next = http.expectOne('/api/v1/me'); expect(next.request.headers.get('X-Api-Key')).toBe('generated-key-b'); next.flush(account('account-b'));
    http.expectNone('/api/v1/projects'); component.select('col-generated'); expect(component.selectedId()).toBeNull(); expect(protectedList.hidden).toBeTrue();
  });
});
