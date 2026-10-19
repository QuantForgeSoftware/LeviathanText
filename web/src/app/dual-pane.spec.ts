import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from './api-client';
import { DualPane } from './dual-pane';

describe('DualPane (diff)', () => {
  it('highlights the changed middle between L1 and L2', () => {
    const stub = {
      line: () => of({ line: 'ln1', diplomatic: 'abc', history: [] }),
      lineLayerHistory: () => of([{ id: 'rev-l2', layer: 'L2Editorial', text: 'axc', author: 'editor', at: '' }]),
    };
    TestBed.configureTestingModule({
      imports: [DualPane],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(DualPane);
    fixture.componentRef.setInput('lineId', 'ln1');
    fixture.componentRef.setInput('imageLink', '');
    fixture.detectChanges();

    const changed = Array.from(fixture.nativeElement.querySelectorAll('.changed'))
      .map((e: unknown) => (e as HTMLElement).textContent);
    expect(changed).toEqual(['b', 'x']);
    expect(fixture.nativeElement.textContent).toContain('prefix/suffix diff');
  });
});
