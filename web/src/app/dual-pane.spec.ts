import { provideWorkspaceTestAccess } from './workspace-testing.fixture.spec';
import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiClient } from './api-client';
import { DualPane } from './dual-pane';

describe('DualPane (diff)', () => {
  it('highlights the changed middle between L1 and L2', () => {
    const stub = {
      line: () => of({ line: 'ln1', diplomatic: 'abc', direction: 'rtl', history: [{ id: 'rev-l1', text: 'abc', author: 'editor', at: '' }] }),
      lineLayerHistory: () => of([{ id: 'rev-l2', layer: 'L2Editorial', text: 'axc', author: 'editor', at: '' }]),
      annotations: () => of([{
        id: 'ann-b', kind: 'wordBoundary', anchoredRevisionId: 'rev-l1',
        range: { start: 1, end: 1 },
      }]),
    };
    TestBed.configureTestingModule({
      imports: [DualPane],
      providers: [provideWorkspaceTestAccess({}), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(DualPane);
    fixture.componentRef.setInput('lineId', 'ln1');
    fixture.componentRef.setInput('imageLink', '');
    fixture.detectChanges();

    const changed = Array.from(fixture.nativeElement.querySelectorAll('.changed'))
      .map((e: unknown) => (e as HTMLElement).textContent);
    expect(changed).toEqual(['b', 'x']);
    expect(fixture.nativeElement.textContent).toContain('prefix/suffix diff');
    expect(fixture.nativeElement.textContent).toContain('a·bc');
    expect(fixture.nativeElement.textContent).toContain('proposed word boundaries');
    expect((fixture.nativeElement.querySelector('.greek') as HTMLElement).getAttribute('dir')).toBe('rtl');
  });
});


describe('DualPane annotation coordinates', () => {
  it('refreshes the same line and ignores an older in-flight response', () => {
    const oldHistory = new Subject<unknown>();
    const newHistory = new Subject<unknown>();
    const history = jasmine.createSpy('history').and.returnValues(oldHistory, newHistory);
    TestBed.configureTestingModule({ imports: [DualPane], providers: [provideWorkspaceTestAccess({}), { provide: ApiClient, useValue: {
      line: () => of({ line: 'ln-refresh', diplomatic: 'αβ', history: [{ id: 'l1', text: 'αβ', author: '', at: '' }] }),
      lineLayerHistory: history, annotations: () => of([]),
    } }] });
    const fixture = TestBed.createComponent(DualPane);
    fixture.componentRef.setInput('lineId', 'ln-refresh');
    fixture.componentRef.setInput('imageLink', ''); fixture.detectChanges();
    fixture.componentRef.setInput('refreshToken', 1); fixture.detectChanges();
    newHistory.next([{ id: 'new-l2', text: 'αΩ', layer: 'L2Editorial', author: '', at: '' }]);
    oldHistory.next([{ id: 'old-l2', text: 'αδ', layer: 'L2Editorial', author: '', at: '' }]); fixture.detectChanges();
    expect(history.calls.count()).toBe(2);
    expect(fixture.nativeElement.querySelectorAll('.pane')[1].querySelector('.greek').textContent).toBe('αΩ');
    fixture.destroy();
    expect(newHistory.observed).toBeFalse();
  });

  it('shows boundaries only on their current L1 or L2 reading, regardless of response order', () => {
    const line = new Subject<unknown>();
    const editorial = new Subject<unknown>();
    TestBed.configureTestingModule({ imports: [DualPane], providers: [provideWorkspaceTestAccess({}), { provide: ApiClient, useValue: {
      line: () => line,
      lineLayerHistory: () => editorial,
      annotations: () => of([
        { kind: 'wordBoundary', anchoredRevisionId: 'rev-l1', range: { start: 1, end: 1 } },
        { kind: 'wordBoundary', anchoredRevisionId: 'rev-l2', range: { start: 2, end: 2 } },
        { kind: 'WordBoundary', anchoredRevisionId: 'rev-old', range: { start: 3, end: 3 } },
        { kind: 'wordBoundary', anchoredRevisionId: 'rev-old-l2', range: { start: 1, end: 1 } },
      ]),
    } }] });
    const fixture = TestBed.createComponent(DualPane);
    fixture.componentRef.setInput('lineId', 'ln-boundaries');
    fixture.componentRef.setInput('imageLink', '');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.boundary')).toBeNull();
    line.next({ line: 'ln-boundaries', diplomatic: 'αβγδ', history: [{ id: 'rev-l1', text: 'αβγδ', author: 'human', at: '' }] });
    editorial.next([{ id: 'rev-l2', layer: 'l2Editorial', text: 'αεβγδ', author: 'human', at: '' }]);
    fixture.detectChanges();
    const panes = fixture.nativeElement.querySelectorAll('.pane') as NodeListOf<HTMLElement>;
    expect(panes[0].querySelector('.boundary')?.textContent).toBe('α·βγδ');
    expect(panes[1].querySelector('.boundary')?.textContent).toBe('αε·βγδ');
  });

  it('reloads the displayed revision on a line switch and ignores the previous response', () => {
    const previousAnnotations = new Subject<unknown>();
    TestBed.configureTestingModule({ imports: [DualPane], providers: [provideWorkspaceTestAccess({}), { provide: ApiClient, useValue: {
      line: (line: string) => of({ line, diplomatic: line === 'ln-first' ? 'αβ' : 'γδ',
        history: [{ id: `${line}-revision`, text: line === 'ln-first' ? 'αβ' : 'γδ', author: 'human', at: '' }] }),
      lineLayerHistory: () => of([]),
      annotations: (line: string) => line === 'ln-first' ? previousAnnotations : of([
        { kind: 'wordBoundary', anchoredRevisionId: 'ln-second-revision', range: { start: 1, end: 1 } },
      ]),
    } }] });
    const fixture = TestBed.createComponent(DualPane);
    fixture.componentRef.setInput('lineId', 'ln-first');
    fixture.componentRef.setInput('imageLink', '');
    fixture.detectChanges();
    fixture.componentRef.setInput('lineId', 'ln-second');
    fixture.detectChanges();
    previousAnnotations.next([{ kind: 'wordBoundary', anchoredRevisionId: 'ln-first-revision', range: { start: 1, end: 1 } }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.boundary')?.textContent).toBe('γ·δ');
    expect(fixture.nativeElement.textContent).not.toContain('αβ');
  });
});
