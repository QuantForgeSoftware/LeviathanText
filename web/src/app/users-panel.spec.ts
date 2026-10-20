import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from './api-client';
import { UsersPanel } from './users-panel';

describe('UsersPanel', () => {
  it('loads accounts, creates one and exposes the returned key once', () => {
    const created = { id: 'usr-1', name: 'anna', role: 'annotator', createdAt: '' };
    const stub = {
      me: () => of({ authenticated: true, name: 'admin', role: 'admin' }),
      users: () => of([created]),
      createUser: (_name: string, _role: string) => of({
        account: { id: 'usr-2', name: 'ben', role: 'reader', createdAt: '' },
        apiKey: 'one-time-key',
      }),
      rotateUser: () => of({ apiKey: 'rotated-key' }),
      disableUser: () => of(undefined),
    };
    TestBed.configureTestingModule({
      imports: [UsersPanel],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(UsersPanel);
    fixture.detectChanges();

    const load = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find(b => (b as HTMLButtonElement).textContent?.includes('Load accounts')) as HTMLButtonElement;
    load.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('anna');

    const name = fixture.nativeElement.querySelector('input[aria-label="Account name"]') as HTMLInputElement;
    name.value = 'ben';
    name.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    const create = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find(b => (b as HTMLButtonElement).textContent?.trim() === 'Create') as HTMLButtonElement;
    create.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('ben');
    expect(fixture.nativeElement.textContent).toContain('one-time-key');
  });
});
