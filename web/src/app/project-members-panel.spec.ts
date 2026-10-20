import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from './api-client';
import { ProjectMembersPanel } from './project-members-panel';

describe('ProjectMembersPanel', () => {
  it('loads admin users/members and grants or revokes access', () => {
    const calls: string[] = [];
    const members = ['usr-a'];
    const stub = {
      me: () => of({ authenticated: true, name: 'admin', role: 'admin' }),
      users: () => of([
        { id: 'usr-a', name: 'anna', role: 'annotator', createdAt: '' },
        { id: 'usr-b', name: 'ben', role: 'annotator', createdAt: '' },
      ]),
      projectMembers: () => of(members),
      addProjectMember: (_project: string, userId: string) => {
        calls.push(`add:${userId}`);
        members.push(userId);
        return of([...members]);
      },
      removeProjectMember: (_project: string, userId: string) => {
        calls.push(`remove:${userId}`);
        members.splice(members.indexOf(userId), 1);
        return of(undefined);
      },
    };
    TestBed.configureTestingModule({
      imports: [ProjectMembersPanel],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(ProjectMembersPanel);
    fixture.componentRef.setInput('projectId', 'col-1');
    fixture.detectChanges();

    (Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.includes('Load members')) as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('anna');
    expect(fixture.nativeElement.textContent).toContain('1 member(s)');

    (Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.trim() === 'Add') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(calls).toContain('add:usr-b');
    expect(fixture.nativeElement.textContent).toContain('2 member(s)');
  });
});
