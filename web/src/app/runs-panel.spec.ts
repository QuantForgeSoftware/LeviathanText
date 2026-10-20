import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { RunsPanel } from './runs-panel';

describe('RunsPanel', () => {
  let http: HttpTestingController;
  const run = {
    id: 'run-1', kind: 'recognition', name: 'model-a', createdAt: '2026-10-20T00:00:00Z',
    config: { epochs: '10', seed: '7' }, metrics: { bestDevCer: 0.42 },
    history: [0.9, 0.7, 0.5, 0.42], artifacts: ['pkg'], notes: 'pilot',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RunsPanel],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists runs, shows details and renders a learning curve', () => {
    const fixture = TestBed.createComponent(RunsPanel);
    fixture.detectChanges();
    http.expectOne(r => r.url === '/api/v1/runs').flush([run]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('model-a');
    expect(fixture.nativeElement.textContent).toContain('bestDevCer=0.420');

    const details = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find(b => (b as HTMLButtonElement).textContent?.trim() === 'Details') as HTMLButtonElement;
    details.click();
    http.expectOne('/api/v1/runs/run-1').flush(run);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('polyline')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('epochs');
    expect(fixture.nativeElement.textContent).toContain('pilot');
  });
});
