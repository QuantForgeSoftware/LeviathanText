import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from './api-client';
import { ProjectHealthPanel } from './project-health-panel';

describe('ProjectHealthPanel', () => {
  it('shows integrity/overlap findings and exports CSVs', () => {
    const calls: string[] = [];
    const stub = {
      projectIntegrity: () => of([{ code: 'duplicate-id', scope: 'ln-1', message: 'duplicate' }]),
      projectOverlap: () => of([{ kind: 'image-checksum', value: 'abc', entities: 'cap-1', note: 'same image' }]),
      integrityCsv: () => { calls.push('integrity'); return of('code,scope,message\n'); },
      overlapCsv: () => { calls.push('overlap'); return of('kind,value,entities,note\n'); },
    };
    TestBed.configureTestingModule({
      imports: [ProjectHealthPanel],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(ProjectHealthPanel);
    fixture.componentRef.setInput('projectId', 'col-1');
    fixture.detectChanges();

    const click = (label: string): void => {
      const button = Array.from(fixture.nativeElement.querySelectorAll('button'))
        .find((b: unknown) => (b as HTMLButtonElement).textContent?.trim() === label) as HTMLButtonElement;
      button.click();
      fixture.detectChanges();
    };

    click('Check project');
    expect(fixture.nativeElement.textContent).toContain('1 integrity issue(s), 1 overlap(s)');
    expect(fixture.nativeElement.textContent).toContain('duplicate-id');
    expect(fixture.nativeElement.textContent).toContain('image-checksum');

    click('Integrity CSV');
    click('Overlap CSV');
    expect(calls).toEqual(['integrity', 'overlap']);
  });
});
