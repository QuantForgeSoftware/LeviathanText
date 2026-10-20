import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ProjectsPage } from './projects-page';

describe('ProjectsPage (search)', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectsPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('selects a project, runs a search and renders hits', () => {
    const fixture = TestBed.createComponent(ProjectsPage);
    fixture.detectChanges();
    http.expectOne('/api/v1/projects').flush([{ id: 'col-1', name: 'demo' }]);
    fixture.detectChanges();

    const projectButton = fixture.nativeElement.querySelector('button.link') as HTMLButtonElement;
    projectButton.click();
    http.expectOne('/api/v1/projects/col-1/stats').flush({
      objects: 1, surfaces: 1, captures: 1, regions: 1, lines: 1,
      transcribedLines: 1, linesWithL0: 0, l0Revisions: 0, l2Revisions: 0,
      annotations: 0, flaggedLines: 0,
    });
    http.expectOne('/api/v1/projects/col-1/review-summary').flush({
      transcribedLines: 0, reviewedLines: 0, doubleReviewedLines: 0, disputedLines: 0,
      needsAdjudication: 0, lines: [],
    });
    http.expectOne('/api/v1/projects/col-1').flush({
      id: 'col-1', name: 'demo',
      objects: [{ id: 'obj-1', surfaces: [{ id: 'surf-1', kind: 'page', lines: [], regions: [], captures: [] }] }],
    });
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'νθρωπ';
    input.dispatchEvent(new Event('input'));
    const searchButton = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find(b => (b as HTMLButtonElement).textContent?.includes('Search')) as HTMLButtonElement;
    searchButton.click();

    http.expectOne(r => r.url === '/api/v1/projects/col-1/search' && r.params.get('q') === 'νθρωπ')
      .flush({
        query: 'νθρωπ', mode: 'literal', totalHits: 1, truncated: false,
        hits: [{
          surfaceId: 'surf-1', lineId: 'ln-1', revisionId: 'rev-1', layer: 'L1Diplomatic',
          snippet: '…ἄνθρωπος…', match: { start: 2, end: 7 },
        }],
      });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('ln-1');
    expect(text).toContain('ἄνθρωπος');
  });
});
