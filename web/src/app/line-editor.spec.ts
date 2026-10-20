import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from './api-client';
import { LineEditor } from './line-editor';

describe('LineEditor (crash recovery)', () => {
  const lineId = 'ln-draft-test';

  function setup(serverText: string) {
    const stub = {
      line: () => of({ line: lineId, diplomatic: serverText, history: [] }),
      annotations: () => of([]),
      appendRevision: () => of({ revisionId: 'rev-new' }),
      addAnnotation: () => of({ annotationId: 'ann-1' }),
    };
    TestBed.configureTestingModule({
      imports: [LineEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', lineId);
    fixture.componentRef.setInput('authorId', 'tester');
    return fixture;
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('restores an unsaved draft that differs from the server text', () => {
    localStorage.setItem(`leviathan:draft:${lineId}`, 'draft text');
    const fixture = setup('server text');
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('draft text');
    expect(fixture.nativeElement.textContent).toContain('Unsaved draft restored');
  });

  it('ignores a stale draft identical to the server text', () => {
    localStorage.setItem(`leviathan:draft:${lineId}`, 'same text');
    const fixture = setup('same text');
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('same text');
    expect(fixture.nativeElement.textContent).not.toContain('Unsaved draft restored');
  });

  it('uses the server text when no draft exists', () => {
    const fixture = setup('server only');
    fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('server only');
  });
});

describe('LineEditor (alternatives)', () => {
  it('composes an L2 revision from an alternative span without touching L1', () => {
    const calls: { layer: string; text: string; parent?: string }[] = [];
    const stub = {
      line: () => of({ line: 'ln-alt', diplomatic: 'abd', history: [{ id: 'rev-1', author: 'x', text: 'abd' }] }),
      annotations: () => of([{
        id: 'ann-1', kind: 'AlternativeReading', anchoredRevisionId: 'rev-1',
        range: { start: 1, end: 2 }, alternativeText: 'X',
      }]),
      appendRevision: (_line: string, layer: string, text: string, _author: string, parent?: string) => {
        calls.push({ layer, text, parent });
        return of({ revisionId: 'rev-2' });
      },
      addAnnotation: () => of({}),
      providers: () => of({ providers: [] }),
    };
    TestBed.configureTestingModule({
      imports: [LineEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', 'ln-alt');
    fixture.componentRef.setInput('authorId', 'tester');
    fixture.detectChanges();

    const button = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.includes('Accept as L2')) as HTMLButtonElement;
    expect(button).toBeTruthy();
    button.click();

    expect(calls).toEqual([{ layer: 'L2Editorial', text: 'aXd', parent: 'rev-1' }]);
  });
});

describe('LineEditor (L0 copy)', () => {
  it('copies model output into the editor but never saves it automatically', () => {
    let saved = 0;
    const stub = {
      line: () => of({
        line: 'ln-l0', diplomatic: 'abc',
        history: [{ id: 'rev-l1', layer: 'L1Diplomatic', text: 'abc', author: 'human', at: '' }],
        revisions: [
          { id: 'rev-l1', layer: 'L1Diplomatic', text: 'abc', author: 'human', at: '' },
          { id: 'rev-l0', layer: 'L0RawRecognition', text: 'model read', author: 'model:tiny', at: '' },
        ],
      }),
      annotations: () => of([]),
      appendRevision: () => { saved++; return of({}); },
      addAnnotation: () => of({}),
      providers: () => of({ providers: [] }),
    };
    TestBed.configureTestingModule({
      imports: [LineEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', 'ln-l0');
    fixture.componentRef.setInput('authorId', 'tester');
    fixture.detectChanges();

    const button = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.includes('Copy latest L0')) as HTMLButtonElement;
    expect(button).toBeTruthy();
    button.click();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('model read');
    expect(saved).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('unsaved');
  });
});

describe('LineEditor (review ledger)', () => {
  it('records an accept decision with the reviewer identity and refreshes', () => {
    const calls: { reviewer: string; outcome: string; correctionText?: string | null }[] = [];
    let lineLoads = 0;
    const stub = {
      line: () => {
        lineLoads++;
        return of({
          line: 'ln-review', diplomatic: 'αβγ',
          history: [{ id: 'rev-1', layer: 'L1Diplomatic', text: 'αβγ', author: 'human', at: '' }],
          revisions: [{ id: 'rev-1', layer: 'L1Diplomatic', text: 'αβγ', author: 'human', at: '' }],
          reviews: [{ id: 'rv-1', reviewer: 'reviewer-a', outcome: 'accept', at: '', revisionId: 'rev-1' }],
        });
      },
      annotations: () => of([]),
      appendRevision: () => of({}),
      addAnnotation: () => of({}),
      providers: () => of({ providers: [] }),
      recordReview: (_line: string, body: { reviewer: string; outcome: string; correctionText?: string | null }) => {
        calls.push(body);
        return of({ reviewId: 'rv-2', outcome: body.outcome });
      },
    };
    TestBed.configureTestingModule({
      imports: [LineEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', 'ln-review');
    fixture.componentRef.setInput('authorId', 'reviewer-b');
    fixture.detectChanges();

    const button = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find(b => (b as HTMLButtonElement).textContent?.trim() === 'Accept') as HTMLButtonElement;
    expect(button).toBeTruthy();
    button.click();

    expect(calls).toEqual([{ reviewer: 'reviewer-b', outcome: 'accept', correctionText: null }]);
    expect(lineLoads).toBeGreaterThan(1); // refresh after recording
  });
});
