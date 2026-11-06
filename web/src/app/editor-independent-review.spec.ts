import { provideWorkspaceTestAccess, TEST_WORKSPACE_SURFACE_ID } from './workspace-testing.fixture.spec';
import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiClient } from './api-client';
import { LineEditor } from './line-editor';

describe('LineEditor independent same-line annotation review', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function setup() {
    const annotation = {
      id: 'ann-retracted', kind: 'AlternativeReading', anchoredRevisionId: 'rev-current',
      range: { start: 1, end: 2 }, alternativeText: 'γ',
    };
    const olderAnnotations = new Subject<typeof annotation[]>();
    const newestAnnotations = new Subject<typeof annotation[]>();
    const reviewed = new Subject<unknown>();
    const retracted = new Subject<unknown>();
    const api = {
      line: () => of({ line: 'ln-review', diplomatic: 'αβ', history: [
        { id: 'rev-current', text: 'αβ', author: 'generated', at: '' },
      ] }),
      annotations: jasmine.createSpy('annotations').and.returnValues(of([annotation]), olderAnnotations, newestAnnotations),
      providers: () => of({ providers: [] }),
      recordReview: () => reviewed,
      retractAnnotation: jasmine.createSpy('retractAnnotation').and.returnValue(retracted),
      appendRevision: jasmine.createSpy('appendRevision').and.returnValue(of({ revisionId: 'l2' })),
    };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: api }] });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
    fixture.componentRef.setInput('lineId', 'ln-review'); fixture.componentRef.setInput('authorId', 'generated');
    fixture.detectChanges();
    const button = (label: string) => Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(item => item.textContent?.trim() === label)!;
    const click = (label: string) => { button(label).click(); fixture.detectChanges(); };
    return { fixture, api, annotation, olderAnnotations, newestAnnotations, reviewed, retracted, click, button };
  }

  it('does not resurrect a retracted alternative when an older same-line list arrives last', () => {
    const s = setup();
    s.click('Accept'); s.reviewed.next({}); s.fixture.detectChanges();
    s.click('Retract'); s.retracted.next({});
    s.newestAnnotations.next([]); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).toContain('No annotations on this line.');
    s.olderAnnotations.next([s.annotation]); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).toContain('No annotations on this line.');
    expect(s.button('Accept as L2')).toBeUndefined();
    expect(s.api.appendRevision).not.toHaveBeenCalled();
  });

  it('does not erase a newer annotation list when an older same-line read fails last', () => {
    const s = setup();
    s.click('Accept'); s.reviewed.next({}); s.fixture.detectChanges();
    s.click('Retract'); s.retracted.next({});
    const newAnnotation = { ...s.annotation, id: 'ann-newer', alternativeText: 'δ' };
    s.newestAnnotations.next([newAnnotation]); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).toContain('δ');
    s.olderAnnotations.error({ status: 503 }); s.fixture.detectChanges();
    expect(s.fixture.nativeElement.textContent).toContain('δ');
    expect(s.fixture.nativeElement.textContent).not.toContain('No annotations on this line.');
  });
});
