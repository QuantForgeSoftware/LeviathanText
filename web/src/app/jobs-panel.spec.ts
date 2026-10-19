import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { JobsPanel } from './jobs-panel';

describe('JobsPanel', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JobsPanel],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists recent jobs with type and status from the API', () => {
    const fixture = TestBed.createComponent(JobsPanel);
    fixture.detectChanges();
    http.expectOne('/api/v1/jobs').flush([
      { id: 'job-1', type: 'layout.detect', status: 'Succeeded', retriesUsed: 0 },
      { id: 'job-2', type: 'import.dir', status: 'Running', retriesUsed: 1 },
    ]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('layout.detect');
    expect(text).toContain('import.dir');
    expect(text).toContain('Succeeded');
    expect(text).toContain('Running');
  });

  it('degrades to an empty list when the API is offline', () => {
    const fixture = TestBed.createComponent(JobsPanel);
    fixture.detectChanges();
    http.expectOne('/api/v1/jobs').error(new ProgressEvent('error'));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No jobs yet');
  });
});
