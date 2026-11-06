import { Component, input, OnDestroy, OnInit, output } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ProjectsPage } from './projects-page';
import { SurfaceView } from './surface-view';
import { ProjectHealthPanel } from './project-health-panel';
import { ProjectMembersPanel } from './project-members-panel';
import { SessionsAdminPanel } from './sessions-admin-panel';
import { ModelRegistryPanel } from './model-registry-panel';
import { TEST_WORKSPACE_OWNER } from './workspace-testing.fixture.spec';
const freshProfile = { authenticated: true, mode: 'account', ...TEST_WORKSPACE_OWNER, name: 'Generated reviewer', role: 'Reviewer' };
const collections = [{ id: 'col1', name: 'generated1' }, { id: 'col2', name: 'generated2' }];

@Component({ selector: 'app-surface-view', standalone: true, template: '<p>{{ mountedText }}</p><img [attr.data-preview]="mountedImage" />' })
class SurfaceMountProbe implements OnInit, OnDestroy {
  static destroyed: string[] = [];
  projectId = input.required<string>(); surfaceId = input.required<string>(); focusLineId = input(''); navigationBlocked = output<boolean>();
  mountedText = ''; mountedImage = '';
  ngOnInit() { this.mountedText = 'text-' + this.surfaceId(); this.mountedImage = 'image-' + this.surfaceId(); }
  ngOnDestroy() { SurfaceMountProbe.destroyed.push(this.mountedImage); }
}
@Component({ selector: 'app-project-health-panel, app-project-members-panel, app-sessions-admin-panel, app-model-registry-panel', standalone: true, template: '' })
class UnrelatedPanelProbe { projectId = input(''); }

describe('ProjectsPage capture-view navigation identity', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/'); SurfaceMountProbe.destroyed = [];
    TestBed.configureTestingModule({ imports: [ProjectsPage], providers: [provideHttpClient(), provideHttpClientTesting()] });
    TestBed.overrideComponent(ProjectsPage, { remove: { imports: [SurfaceView, ProjectHealthPanel, ProjectMembersPanel, SessionsAdminPanel, ModelRegistryPanel] }, add: { imports: [SurfaceMountProbe, UnrelatedPanelProbe] } });
  });
  afterEach(() => { TestBed.inject(HttpTestingController).verify(); window.history.replaceState(null, '', '/'); });
  async function setup() {
    const fixture = TestBed.createComponent(ProjectsPage); const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/v1/me').flush(freshProfile);
    http.expectOne('/api/v1/projects').flush(collections); fixture.detectChanges();
    const component = fixture.componentInstance as any; component.select('col1');
    http.expectOne('/api/v1/me').flush(freshProfile);
    http.expectOne('/api/v1/projects/col1').flush({ id: 'col1', name: 'generated1', objects: [{ id: 'obj1', surfaces: ['surf1', 'surf2'].map(id => ({ id, kind: 'page', lines: [], regions: [], captures: [] })) }] });
    http.expectOne('/api/v1/projects').flush(collections);
    http.expectOne('/api/v1/projects/col1/stats').flush({ surfaces: 2, lines: 0, transcribedLines: 0, annotations: 0, flaggedLines: 0 });
    http.expectOne('/api/v1/projects/col1/review-summary').flush({ transcribedLines: 0, reviewedLines: 0, doubleReviewedLines: 0, disputedLines: 0, needsAdjudication: 0, lines: [] });

    component.openSurface('surf1'); fixture.detectChanges();
    return { fixture, component, http, view: () => fixture.debugElement.query(By.directive(SurfaceMountProbe)).componentInstance as SurfaceMountProbe };
  }
  it('destroys the old surface view and renders fresh text/preview identity when navigating', async () => {
    const s = await setup(); const previous = s.view(); s.component.navigateSurface(1); s.fixture.detectChanges();
    expect(s.view()).not.toBe(previous); expect(s.fixture.nativeElement.textContent).toContain('text-surf2');
    expect(s.fixture.nativeElement.textContent).not.toContain('text-surf1');
    expect(s.fixture.nativeElement.querySelector('app-surface-view img').getAttribute('data-preview')).toBe('image-surf2');
    expect(SurfaceMountProbe.destroyed).toContain('image-surf1');
  });
  it('blocks rendered and programmatic collection/surface navigation until geometry drafts or writes release the view', async () => {
    const s = await setup(); const original = s.view(); original.navigationBlocked.emit(true); s.fixture.detectChanges();
    s.component.navigateSurface(1); s.component.openSurface('surf2'); s.component.select('col2'); s.fixture.detectChanges();
    expect(s.component.openSurfaceId()).toBe('surf1'); expect(s.component.selectedId()).toBe('col1'); expect(s.view()).toBe(original);
    expect((Array.from(s.fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>).find(button => button.textContent?.includes('Next surface'))!).disabled).toBeTrue();
    s.http.expectNone('/api/v1/projects/col2');
    original.navigationBlocked.emit(false); s.fixture.detectChanges(); s.component.navigateSurface(1); s.fixture.detectChanges(); expect(s.component.openSurfaceId()).toBe('surf2');
  });
});
