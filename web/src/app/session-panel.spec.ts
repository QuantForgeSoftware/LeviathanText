import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient, SessionView } from './api-client';
import { SessionPanel } from './session-panel';

describe('SessionPanel', () => {
  const session: SessionView = {
    id: 'ses-1',
    userId: 'usr-1',
    userName: 'anna',
    nodeId: 'node-a:123',
    surfaceId: 'surf-1',
    createdAt: '2026-11-02T21:00:00Z',
    lastSeenAt: '2026-11-02T21:01:00Z',
    expiresAt: '2026-11-02T22:00:00Z',
  };

  it('lists active sessions and revokes one', () => {
    const calls: string[] = [];
    let sessions: SessionView[] = [session];
    const stub = {
      sessions: (_surfaceId?: string) => of(sessions),
      revokeSession: (id: string) => {
        calls.push(id);
        sessions = [];
        return of({ revoked: true });
      },
    };
    TestBed.configureTestingModule({
      imports: [SessionPanel],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(SessionPanel);
    fixture.componentRef.setInput('surfaceId', 'surf-1');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('anna');
    expect(fixture.nativeElement.textContent).toContain('node-a:123');

    const button = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.trim() === 'Revoke') as HTMLButtonElement;
    button.click();
    fixture.detectChanges();

    expect(calls).toEqual(['ses-1']);
    expect(fixture.nativeElement.textContent).toContain('No active sessions');
  });
});
