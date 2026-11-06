import { provideWorkspaceTestAccess } from './workspace-testing.fixture.spec';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient, RegistryEntry } from './api-client';
import { ModelRegistryPanel } from './model-registry-panel';

describe('ModelRegistryPanel', () => {
  const entry: RegistryEntry = {
    id: 'p01', name: 'P01 model', task: 'recognition',
    downloadUrl: 'https://example.test/p01.zip',
    sha256: 'a'.repeat(64), packageFormatVersion: 1,
  };

  it('loads a catalog and pulls a checksummed model', () => {
    const pulls: string[] = [];
    const stub = {
      registry: (_url: string) => of([entry]),
      pullRegistry: (_url: string, id: string) => {
        pulls.push(id);
        return of({ id, directory: '/models/p01', sha256: entry.sha256! });
      },
    };
    TestBed.configureTestingModule({
      imports: [ModelRegistryPanel],
      providers: [provideWorkspaceTestAccess({ role: 'Admin', projectId: 'col-1' }), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(ModelRegistryPanel);
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'https://example.test/index.json';
    input.dispatchEvent(new Event('input'));
    const load = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find(b => (b as HTMLButtonElement).textContent?.includes('Load catalog')) as HTMLButtonElement;
    load.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('P01 model');
    expect(fixture.nativeElement.textContent).toContain('checksummed');

    const pull = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find(b => (b as HTMLButtonElement).textContent?.trim() === 'Pull') as HTMLButtonElement;
    pull.click();
    fixture.detectChanges();

    expect(pulls).toEqual(['p01']);
    expect(fixture.nativeElement.textContent).toContain('pulled p01');
  });
});
