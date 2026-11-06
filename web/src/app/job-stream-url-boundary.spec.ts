import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClient } from './api-client';
import { JobsPanel } from './jobs-panel';
import { setApiKey } from './auth';

describe('Independent job stream credential URL boundary', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    localStorage.clear(); setApiKey('generated-job-stream-secret-a');
    TestBed.configureTestingModule({ imports: [JobsPanel], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); setApiKey(''); localStorage.clear(); });
  it('never embeds the generated long-lived credential in the complete stream URL', () => {
    const url = TestBed.inject(ApiClient).jobEventsUrl('job-generated-stream');
    expect(url).toBe('/api/v1/jobs/job-generated-stream/events');
    expect(url).not.toContain('api_key='); expect(url).not.toContain('generated-job-stream-secret-a');
  });
  it('does not open an unheadered native EventSource carrying a secret URL from the actual panel', () => {
    spyOn(globalThis, 'fetch').and.resolveTo(new Response(null, { status: 204 }));
    const source = { close: jasmine.createSpy('close'), addEventListener: jasmine.createSpy('listen'), onerror: null, readyState: 1 };
    const constructed = spyOn(window, 'EventSource').and.returnValue(source as unknown as EventSource);
    const fixture = TestBed.createComponent(JobsPanel); fixture.detectChanges();
    http.expectOne('/api/v1/jobs').flush([{ id: 'job-generated-stream', type: 'noop', status: 'Running', retriesUsed: 0 }]);
    fixture.detectChanges();
    const watch = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>).find(button => button.textContent?.trim() === 'Watch');
    expect(watch).toBeDefined(); watch!.click(); fixture.detectChanges();
    expect(constructed).not.toHaveBeenCalled();
    for (const call of constructed.calls.all()) {
      expect(String(call.args[0])).not.toContain('api_key='); expect(String(call.args[0])).not.toContain('generated-job-stream-secret-a');
    }
    fixture.destroy();
  });
});
