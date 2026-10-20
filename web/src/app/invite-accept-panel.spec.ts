import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from './api-client';
import { InviteAcceptPanel } from './invite-accept-panel';

describe('InviteAcceptPanel', () => {
  it('accepts a token and reports the account', () => {
    const calls: { token: string; name: string }[] = [];
    const stub = {
      acceptInvite: (token: string, name: string) => {
        calls.push({ token, name });
        return of({
          account: { id: 'usr-1', name, role: 'annotator', createdAt: '' },
          apiKey: 'invited-key', projectId: 'col-1', role: 'annotator',
        });
      },
    };
    TestBed.configureTestingModule({
      imports: [InviteAcceptPanel],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(InviteAcceptPanel);
    fixture.detectChanges();

    const token = fixture.nativeElement.querySelector('input[aria-label="Invitation token"]') as HTMLInputElement;
    const name = fixture.nativeElement.querySelector('input[aria-label="Invited account name"]') as HTMLInputElement;
    token.value = 'tok'; token.dispatchEvent(new Event('input'));
    name.value = 'anna'; name.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find(b => (b as HTMLButtonElement).textContent?.includes('Accept invitation')) as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(calls).toEqual([{ token: 'tok', name: 'anna' }]);
    expect(fixture.nativeElement.textContent).toContain('accepted as anna');
  });
});
