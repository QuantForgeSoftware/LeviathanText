import { provideWorkspaceTestAccess } from './workspace-testing.fixture.spec';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { ApiClient } from './api-client';
import { ProposalPanel } from './proposal-panel';

const advisory = () => ({
  id: 'prop-review', request: { targetRevisionId: 'rev-source', range: { start: 0, end: 2 }, task: 'translation' },
  proposedText: '  λόγος — translation  ', providerId: 'test-provider', modelId: 'test-model', status: 'proposed',
});

function button(fixture: ComponentFixture<ProposalPanel>, label: string): HTMLButtonElement | undefined {
  return Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
    .find(item => item.textContent?.includes(label));
}

function setup() {
  const response = new Subject<ReturnType<typeof advisory>>();
  const addAnnotation = jasmine.createSpy('addAnnotation').and.returnValue(of({ annotationId: 'ann-saved' }));
  const propose = jasmine.createSpy('propose').and.returnValue(response);
  TestBed.configureTestingModule({ imports: [ProposalPanel], providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: {
    providers: () => of({ providers: [{ configRef: 'test', settings: { modelId: 'test-model' } }] }),
    propose, addAnnotation,
  } }] });
  const fixture = TestBed.createComponent(ProposalPanel);
  fixture.componentRef.setInput('lineId', 'ln-source');
  fixture.componentRef.setInput('revisionId', 'rev-source');
  fixture.componentRef.setInput('rangeStart', 0);
  fixture.componentRef.setInput('rangeEnd', 2);
  fixture.detectChanges();
  const task = Array.from(fixture.nativeElement.querySelectorAll('select') as NodeListOf<HTMLSelectElement>)
    .find(select => select.querySelector('option[value="translation"]'))!;
  task.value = 'translation';
  task.dispatchEvent(new Event('change'));
  button(fixture, 'Propose')!.click();
  return { fixture, response, addAnnotation, propose };
}

describe('ProposalPanel advisory source anchors', () => {
  it('saves the original revision and range even if the selection changes, preserving provider output', () => {
    const { fixture, response, addAnnotation } = setup();
    response.next(advisory());
    fixture.componentRef.setInput('rangeStart', 1);
    fixture.componentRef.setInput('rangeEnd', 3);
    fixture.detectChanges();
    button(fixture, 'Save as advisory')!.click();
    expect(addAnnotation.calls.mostRecent().args).toEqual(['ln-source', {
      kind: 'Translation', anchoredRevisionId: 'rev-source', range: { start: 0, end: 2 },
      alternativeText: advisory().proposedText, description: 'Translation · test-provider/test-model',
    }]);
    fixture.detectChanges();
    expect(button(fixture, 'Save as advisory')).toBeUndefined();
  });

  for (const input of ['lineId', 'revisionId']) {
    it(`discards an in-flight response after ${input} changes`, () => {
      const { fixture, response, addAnnotation } = setup();
      fixture.componentRef.setInput(input, 'different-target');
      fixture.detectChanges();
      response.next(advisory());
      fixture.detectChanges();
      expect(button(fixture, 'Save as advisory')).toBeUndefined();
      expect(addAnnotation).not.toHaveBeenCalled();
    });

    it(`clears an already displayed proposal after ${input} changes`, () => {
      const { fixture, response, addAnnotation } = setup();
      response.next(advisory());
      fixture.detectChanges();
      expect(button(fixture, 'Save as advisory')).toBeDefined();
      fixture.componentRef.setInput(input, 'different-target');
      fixture.detectChanges();
      expect(button(fixture, 'Save as advisory')).toBeUndefined();
      expect(addAnnotation).not.toHaveBeenCalled();
    });
  }

  it('rejects a response whose target revision differs from the requested source', () => {
    const { fixture, response, addAnnotation } = setup();
    response.next({ ...advisory(), request: { ...advisory().request, targetRevisionId: 'rev-other' } });
    fixture.detectChanges();
    expect(button(fixture, 'Save as advisory')).toBeUndefined();
    expect(addAnnotation).not.toHaveBeenCalled();
  });

  it('rejects a response whose range differs from the requested source', () => {
    const { fixture, response, addAnnotation } = setup();
    response.next({ ...advisory(), request: { ...advisory().request, range: { start: 0, end: 1 } } });
    fixture.detectChanges();
    expect(button(fixture, 'Save as advisory')).toBeUndefined();
    expect(addAnnotation).not.toHaveBeenCalled();
  });

  it('clears a proposal rejected by the server as stale rather than rebinding it', () => {
    const { fixture, response, addAnnotation } = setup();
    addAnnotation.and.returnValue(throwError(() => ({ status: 409, error: { code: 'staleAnnotationAnchor' } })));
    response.next(advisory());
    fixture.detectChanges();
    button(fixture, 'Save as advisory')!.click();
    fixture.detectChanges();
    expect(button(fixture, 'Save as advisory')).toBeUndefined();
    expect(fixture.nativeElement.textContent).toContain('new proposal');
    expect(addAnnotation).toHaveBeenCalledTimes(1);
  });
});
