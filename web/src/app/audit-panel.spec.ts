import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from './api-client';
import { AuditPanel } from './audit-panel';

describe('AuditPanel', () => {
  it('loads recent admin audit events', () => {
    const stub = {
      me: () => of({ authenticated: true, name: 'admin', role: 'admin' }),
      audit: () => of([{
        id: 1, at: '2026-10-20T00:00:00Z', userName: 'anna', role: 'annotator',
        action: 'denied', method: 'POST', path: '/api/v1/projects/col-1', status: 403,
      }]),
    };
    TestBed.configureTestingModule({
      imports: [AuditPanel],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(AuditPanel);
    fixture.detectChanges();

    (Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find(b => (b as HTMLButtonElement).textContent?.includes('Load recent events')) as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('anna');
    expect(fixture.nativeElement.textContent).toContain('/api/v1/projects/col-1');
    expect(fixture.nativeElement.textContent).toContain('403');
  });
});
