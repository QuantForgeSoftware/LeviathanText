import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiClient } from './api-client';
import { LineEditor } from './line-editor';
import { provideWorkspaceTestAccess, TEST_WORKSPACE_SURFACE_ID } from './workspace-testing.fixture.spec';

// Actual component/DOM and recording Subjects; no provider, model or server call.
describe('Manual form values after an older successful acknowledgement', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function setup(expansion: boolean) {
    const appended = new Subject<{ revisionId: string }>();
    const annotated = new Subject<{ annotationId: string }>();
    const api = {
      line: () => of({ line: 'ln-generated-form', diplomatic: 'ab', revisions: [],
        history: [{ id: 'rev-form-base', layer: 'L1Diplomatic', text: 'ab', author: 'generated', at: '' }] }),
      annotations: () => of([]), providers: () => of({ providers: [] }),
      appendRevision: jasmine.createSpy('appendRevision').and.returnValue(appended),
      addAnnotation: jasmine.createSpy('addAnnotation').and.returnValue(annotated),
    };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [
      provideWorkspaceTestAccess(), { provide: ApiClient, useValue: api },
    ] });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
    fixture.componentRef.setInput('lineId', 'ln-generated-form');
    fixture.componentRef.setInput('authorId', 'generated-reviewer');
    fixture.detectChanges();
    const form = () => fixture.nativeElement.querySelector('.annotation-form') as HTMLElement;
    const kind = form().querySelector('select') as HTMLSelectElement;
    kind.value = expansion ? 'Abbreviation' : 'AlternativeReading'; kind.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const numbers = form().querySelectorAll('input[type=number]');
    const set = (input: HTMLInputElement, value: string) => {
      input.value = value; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    };
    set(numbers[0] as HTMLInputElement, '0'); set(numbers[1] as HTMLInputElement, '1');
    const labelInput = (prefix: string) => {
      const label = Array.from(form().querySelectorAll('label')).find(node => node.textContent?.trim().startsWith(prefix));
      if (!label) throw new Error(`Missing actual form label: ${prefix}`);
      return label.querySelector('input') as HTMLInputElement;
    };
    const note = labelInput('Note');
    const value = labelInput(expansion ? 'Expansion' : 'Alternative');
    set(note, 'submitted-note'); set(value, expansion ? 'EXP' : 'X');
    const add = Array.from(form().querySelectorAll('button')).find(node => node.textContent?.trim() === 'Add')!;
    expect(add.disabled).toBeFalse(); add.click(); fixture.detectChanges();
    return { fixture, api, appended, annotated, note, value, set };
  }

  for (const expansion of [false, true]) {
    for (const editedAfterDispatch of [false, true]) {
      it(`${expansion ? 'expansion' : 'annotation'} ${editedAfterDispatch ? 'preserves newer values' : 'clears unchanged submitted values'} on old success`, () => {
        const s = setup(expansion);
        try {
          if (editedAfterDispatch) { s.set(s.note, 'newer-note'); s.set(s.value, 'NEW'); }
          if (expansion) {
            expect(s.api.appendRevision).toHaveBeenCalledOnceWith('ln-generated-form', 'L2Editorial', 'EXPb', 'generated-reviewer', undefined, true);
            expect(s.api.addAnnotation).not.toHaveBeenCalled();
            s.appended.next({ revisionId: 'rev-form-expansion' }); s.appended.complete(); s.fixture.detectChanges();
          }
          expect(s.api.addAnnotation).toHaveBeenCalledTimes(1);
          const submitted = s.api.addAnnotation.calls.mostRecent().args[1];
          expect(submitted.description).toBe('submitted-note');
          expect(submitted[expansion ? 'expansionText' : 'alternativeText']).toBe(expansion ? 'EXP' : 'X');
          if (expansion) expect(submitted.expansionRevisionId).toBe('rev-form-expansion');
          if (editedAfterDispatch) {
            expect(s.note.value).toBe('newer-note'); expect(s.value.value).toBe('NEW');
          }
          s.annotated.next({ annotationId: 'ann-generated-form' }); s.annotated.complete(); s.fixture.detectChanges();
          expect(s.note.value).withContext('an older acknowledgement cannot clear a newer note').toBe(editedAfterDispatch ? 'newer-note' : '');
          expect(s.value.value).withContext('an older acknowledgement cannot clear a newer alternative/expansion').toBe(editedAfterDispatch ? 'NEW' : '');
          expect((s.fixture.nativeElement.querySelector('input.greek') as HTMLInputElement).value).toBe('ab');
          expect(s.api.addAnnotation).toHaveBeenCalledTimes(1);
        } finally { s.fixture.destroy(); }
      });
    }
  }
});
