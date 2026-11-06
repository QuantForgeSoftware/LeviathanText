import { provideWorkspaceTestAccess } from './workspace-testing.fixture.spec';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient, NativeFragmentView } from './api-client';
import { FragmentPanel } from './fragment-panel';

describe('FragmentPanel', () => {
  it('lists and creates a fragment containing the current surface', () => {
    const calls: string[] = [];
    let fragments: NativeFragmentView[] = [{
      id: 'frag-1', externalRefs: { TM: '12345' }, metadata: {}, surfaceIds: ['surf-1'],
    }];
    const stub = {
      surfaceFragments: () => of(fragments),
      createSurfaceFragment: (_surface: string, refs: Record<string, string>) => {
        calls.push(JSON.stringify(refs));
        fragments = [...fragments, {
          id: 'frag-2', externalRefs: refs, metadata: {}, surfaceIds: ['surf-1'],
        }];
        return of({ id: 'frag-2', surfaceIds: ['surf-1'] });
      },
    };
    TestBed.configureTestingModule({
      imports: [FragmentPanel],
      providers: [provideWorkspaceTestAccess({}), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(FragmentPanel);
    fixture.componentRef.setInput('surfaceId', 'surf-1');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('frag-1');
    expect(fixture.nativeElement.textContent).toContain('TM=12345');

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = '67890';
    input.dispatchEvent(new Event('input'));
    (Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.trim() === 'Create fragment here') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(calls).toEqual([JSON.stringify({ TM: '67890' })]);
    expect(fixture.nativeElement.textContent).toContain('frag-2');
  });
});
