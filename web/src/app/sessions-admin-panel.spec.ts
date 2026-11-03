import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient, SessionView } from './api-client';
import { SessionsAdminPanel } from './sessions-admin-panel';

describe('SessionsAdminPanel', () => {
  it('lists visible sessions and revokes one', () => {
    const calls: string[] = [];
    let sessions: SessionView[] = [{
      id: 'ses-admin', userId: 'usr-1', userName: 'anna', nodeId: 'node-a:1',
      surfaceId: 'surf-1', createdAt: '2026-11-02T21:00:00Z',
      lastSeenAt: '2026-11-02T21:01:00Z', expiresAt: '2026-11-02T22:00:00Z',
    }];
    const stub = {
      sessions: () => of(sessions),
      revokeSession: (id: string) => {
        calls.push(id);
        sessions = [];
        return of({ revoked: true });
      },
    };
    TestBed.configureTestingModule({
      imports: [SessionsAdminPanel],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(SessionsAdminPanel);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('anna');
    expect(fixture.nativeElement.textContent).toContain('surf-1');

    const button = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.trim() === 'Revoke') as HTMLButtonElement;
    button.click();
    fixture.detectChanges();

    expect(calls).toEqual(['ses-admin']);
    expect(fixture.nativeElement.textContent).toContain('No active sessions');
  });
});
