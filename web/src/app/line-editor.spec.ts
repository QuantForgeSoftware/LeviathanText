import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
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

    expect(calls).toEqual([{ layer: 'L2Editorial', text: 'aXd', parent: undefined }]);
  });
});

describe('LineEditor (orientation)', () => {
  it('saves direction/script as metadata without appending a text revision', () => {
    const calls: { direction: string | null; script: string | null }[] = [];
    let revisions = 0;
    const stub = {
      line: () => of({
        line: 'ln-dir', diplomatic: 'αβγ', direction: null, script: null,
        history: [{ id: 'rev-1', author: 'x', text: 'αβγ' }],
      }),
      annotations: () => of([]),
      appendRevision: () => { revisions++; return of({}); },
      addAnnotation: () => of({}),
      providers: () => of({ providers: [] }),
      setLineOrientation: (_line: string, direction: string | null, script: string | null) => {
        calls.push({ direction, script });
        return of({ lineId: 'ln-dir', direction, script });
      },
    };
    TestBed.configureTestingModule({
      imports: [LineEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', 'ln-dir');
    fixture.componentRef.setInput('authorId', 'tester');
    fixture.detectChanges();

    const directionSelect = Array.from(fixture.nativeElement.querySelectorAll('select'))
      .find((s: unknown) => (s as HTMLSelectElement).querySelector('option[value="rtl"]')) as HTMLSelectElement;
    directionSelect.value = 'rtl';
    directionSelect.dispatchEvent(new Event('change'));
    const scriptInput = Array.from(fixture.nativeElement.querySelectorAll('input'))
      .find((i: unknown) => (i as HTMLInputElement).placeholder === 'Grek') as HTMLInputElement;
    scriptInput.value = 'Grek';
    scriptInput.dispatchEvent(new Event('input'));

    const apply = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.includes('Apply orientation')) as HTMLButtonElement;
    apply.click();
    fixture.detectChanges();

    expect(calls).toEqual([{ direction: 'rtl', script: 'Grek' }]);
    expect(revisions).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('orientation saved');
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
          { id: 'rev-l0', layer: 'l0RawRecognition', text: 'model read', author: 'model:tiny', at: '' },
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
    const calls: { reviewer: string; outcome: string; correctionText?: string | null; revisionId?: string | null }[] = [];
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
      recordReview: (_line: string, body: { reviewer: string; outcome: string; correctionText?: string | null; revisionId?: string | null }) => {
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

    expect(calls).toEqual([{ reviewer: 'reviewer-b', outcome: 'accept', correctionText: null, revisionId: 'rev-1' }]);
    expect(lineLoads).toBeGreaterThan(1); // refresh after recording
  });
});

describe('LineEditor (annotation retraction)', () => {
  it('retracts an annotation and reloads the active list', () => {
    const calls: string[] = [];
    let annotationLoads = 0;
    const stub = {
      line: () => of({
        line: 'ln-retract', diplomatic: 'αβγ',
        history: [{ id: 'rev-1', layer: 'L1Diplomatic', text: 'αβγ', author: 'human', at: '' }],
        revisions: [{ id: 'rev-1', layer: 'L1Diplomatic', text: 'αβγ', author: 'human', at: '' }],
      }),
      annotations: () => {
        annotationLoads++;
        return annotationLoads === 1
          ? of([{ id: 'ann-1', kind: 'Unclear', anchoredRevisionId: 'rev-1', range: { start: 1, end: 2 } }])
          : of([]);
      },
      retractAnnotation: (id: string) => {
        calls.push(id);
        return of({ retractionId: 'ar-1', annotationId: id });
      },
      appendRevision: () => of({}),
      addAnnotation: () => of({}),
      providers: () => of({ providers: [] }),
    };
    TestBed.configureTestingModule({
      imports: [LineEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', 'ln-retract');
    fixture.componentRef.setInput('authorId', 'reviewer-a');
    fixture.detectChanges();

    const button = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find(b => (b as HTMLButtonElement).textContent?.trim() === 'Retract') as HTMLButtonElement;
    expect(button).toBeTruthy();
    button.click();
    fixture.detectChanges();

    expect(calls).toEqual(['ann-1']);
    expect(annotationLoads).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('No annotations on this line');
  });
});

describe('LineEditor (abbreviation expansion)', () => {
  it('creates an L2 revision and a linked Abbreviation annotation without changing L1', () => {
    let appended = '';
    let annotation: Record<string, unknown> | null = null;
    const stub = {
      line: () => of({
        line: 'ln-abbr', diplomatic: 'κϲ',
        history: [{ id: 'rev-l1', layer: 'L1Diplomatic', text: 'κϲ', author: 'human', at: '' }],
      }),
      annotations: () => of([]),
      appendRevision: (_line: string, layer: string, text: string) => {
        appended = `${layer}:${text}`;
        return of({ revisionId: 'rev-l2' });
      },
      addAnnotation: (_line: string, body: unknown) => {
        annotation = body as Record<string, unknown>;
        return of({ annotationId: 'ann-abbr' });
      },
      providers: () => of({ providers: [] }),
    };
    TestBed.configureTestingModule({
      imports: [LineEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', 'ln-abbr');
    fixture.componentRef.setInput('authorId', 'tester');
    fixture.detectChanges();

    const kind = Array.from(fixture.nativeElement.querySelectorAll('select'))
      .find((s: unknown) => (s as HTMLSelectElement).querySelector('option[value="Abbreviation"]')) as HTMLSelectElement;
    kind.value = 'Abbreviation';
    kind.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const numbers = fixture.nativeElement.querySelectorAll('input[type="number"]') as NodeListOf<HTMLInputElement>;
    numbers[0].value = '0';
    numbers[0].dispatchEvent(new Event('input'));
    numbers[1].value = '2';
    numbers[1].dispatchEvent(new Event('input'));
    const expansion = fixture.nativeElement.querySelector('input[placeholder="expanded text"]') as HTMLInputElement;
    expansion.value = 'κυρίου';
    expansion.dispatchEvent(new Event('input'));
    const add = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.trim() === 'Add') as HTMLButtonElement;
    add.click();
    fixture.detectChanges();

    expect(appended).toBe('L2Editorial:κυρίου');
    expect(annotation).not.toBeNull();
    expect(annotation!['kind']).toBe('Abbreviation');
    expect(annotation!['expansionText']).toBe('κυρίου');
    expect(annotation!['expansionRevisionId']).toBe('rev-l2');
    expect(annotation!['range']).toEqual({ start: 0, end: 2 });
  });
});

describe('LineEditor (correction sequence)', () => {
  it('orders explicit sequence entries and exposes replay controls', () => {
    const stub = {
      line: () => of({
        line: 'ln-seq', diplomatic: 'αβ',
        history: [{ id: 'rev-l1', layer: 'L1Diplomatic', text: 'αβ', author: 'human', at: '' }],
      }),
      annotations: () => of([
        { id: 'ann-2', kind: 'CorrectionSequence', anchoredRevisionId: 'rev-l1', range: { start: 1, end: 2 }, sequenceId: 'main', sequenceIndex: 1, orderBasis: 'Explicit', handLabel: 'second' },
        { id: 'ann-1', kind: 'CorrectionSequence', anchoredRevisionId: 'rev-l1', range: { start: 0, end: 1 }, sequenceId: 'main', sequenceIndex: 0, orderBasis: 'Explicit', handLabel: 'first' },
      ]),
      appendRevision: () => of({ revisionId: 'rev-l2' }),
      addAnnotation: () => of({}),
      providers: () => of({ providers: [] }),
    };
    TestBed.configureTestingModule({
      imports: [LineEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', 'ln-seq');
    fixture.componentRef.setInput('authorId', 'tester');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Correction sequence (2)');
    const sequenceButtons = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .map((b: unknown) => (b as HTMLButtonElement).textContent?.trim() ?? '')
      .filter(label => label.startsWith('main /'));
    expect(sequenceButtons).toEqual(['main / 0', 'main / 1']);
    const next = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.trim() === 'Next') as HTMLButtonElement;
    next.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Selected intervention spans scalars 1–2');
  });
});

describe('LineEditor (concurrent merge)', () => {
  function setup(append: (merge?: boolean) => unknown) {
    const calls: { parent?: string; merge?: boolean; text: string }[] = [];
    const stub = {
      line: () => of({
        line: 'ln-merge', diplomatic: 'αβγ',
        history: [{ id: 'rev-1', layer: 'L1Diplomatic', text: 'αβγ', author: 'x', at: '' }],
      }),
      annotations: () => of([]),
      appendRevision: (_line: string, _layer: string, text: string, _author: string, parent?: string, merge?: boolean) => {
        calls.push({ text, parent, merge });
        return append(merge);
      },
      addAnnotation: () => of({}),
      providers: () => of({ providers: [] }),
    };
    TestBed.configureTestingModule({
      imports: [LineEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', 'ln-merge');
    fixture.componentRef.setInput('authorId', 'tester');
    fixture.detectChanges();
    return { fixture, calls };
  }

  it('asks the server for a three-way merge when saving a stale parent', () => {
    const { fixture, calls } = setup(() => of({ revisionId: 'rev-2', merged: true }));
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'Αβγ';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const save = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.includes('Save revision')) as HTMLButtonElement;
    save.click();

    expect(calls).toEqual([{ text: 'Αβγ', parent: 'rev-1', merge: true }]);
  });

  it('surfaces an overlapping-edit conflict without discarding the draft', () => {
    const { fixture } = setup(() => throwError(() => ({ status: 409 })));
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'Αβγ';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const save = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((b: unknown) => (b as HTMLButtonElement).textContent?.includes('Save revision')) as HTMLButtonElement;
    save.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('overlap a newer revision');
    expect((fixture.nativeElement.querySelector('input') as HTMLInputElement).value).toBe('Αβγ');
  });
});

describe('LineEditor annotation coordinates', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function setup() {
    const appendRevision = jasmine.createSpy('appendRevision').and.returnValue(of({ revisionId: 'rev-new' }));
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [{ provide: ApiClient, useValue: {
      line: () => of({ line: 'ln-anchors', diplomatic: 'αβ',
        // The real line API's L1 history deliberately has no layer property.
        history: [{ id: 'rev-current', text: 'αβ', author: 'human', at: '' }],
        revisions: [{ id: 'rev-current', layer: 'L1Diplomatic', text: 'αβ', author: 'human', at: '' },
          { id: 'rev-editorial', layer: 'L2Editorial', text: 'αγβ', author: 'human', at: '' }],
      }),
      annotations: () => of([
        { id: 'ann-current', kind: 'unclear', anchoredRevisionId: 'rev-current', range: { start: 0, end: 1 } },
        { id: 'ann-old', kind: 'erased', anchoredRevisionId: 'rev-old', range: { start: 1, end: 2 } },
        { id: 'ann-l2', kind: 'suppliedEditorial', anchoredRevisionId: 'rev-editorial', range: { start: 1, end: 2 } },
        { id: 'ann-alt-old', kind: 'alternativeReading', anchoredRevisionId: 'rev-old', range: { start: 0, end: 1 }, alternativeText: 'δ' },
        { id: 'ann-alt-l2', kind: 'alternativeReading', anchoredRevisionId: 'rev-editorial', range: { start: 0, end: 1 }, alternativeText: 'ε' },
      ]), providers: () => of({ providers: [] }), appendRevision,
    } }] });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', 'ln-anchors');
    fixture.componentRef.setInput('authorId', 'reviewer');
    fixture.detectChanges();
    return { fixture, appendRevision };
  }

  it('marks only annotations anchored to the displayed saved L1, not L2 or old revisions', () => {
    const { fixture } = setup();
    expect(fixture.nativeElement.querySelector('.marked').textContent).toBe('αβ');
    expect(fixture.nativeElement.querySelector('.marked .mark-unclear')?.textContent).toBe('α');
    expect(fixture.nativeElement.querySelector('.marked .mark-supplied')).toBeNull();
    expect(fixture.nativeElement.querySelector('.marked .mark-erased')).toBeNull();
    expect(fixture.nativeElement.querySelector('.marked .mark-alternative')).toBeNull();
  });

  it('offers no L2 acceptance action for stale or L2-anchored alternatives', () => {
    const { fixture, appendRevision } = setup();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>);
    expect(buttons.filter(button => button.textContent?.includes('Accept as L2')).length).toBe(0);
    expect(appendRevision).not.toHaveBeenCalled();
  });

  it('suppresses saved offsets on an unsaved draft, restoring marks when its original text returns', () => {
    const { fixture } = setup();
    const editor = fixture.nativeElement.querySelector('input.greek') as HTMLInputElement;
    editor.value = 'γαβ';
    editor.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.marked [class^="mark-"]')).toBeNull();
    editor.value = 'αβ';
    editor.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.marked .mark-unclear')?.textContent).toBe('α');
  });
});

describe('LineEditor line navigation', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  const payload = (line: string, text = line === 'ln-first' ? 'αβ' : 'γδ') => ({
    line, diplomatic: text, direction: line === 'ln-first' ? 'rtl' : 'ltr', script: line,
    history: [{ id: `${line}-revision`, text, author: 'human', at: '' }],
  });

  function setup(overrides: Record<string, unknown> = {}) {
    const api = {
      line: jasmine.createSpy('line').and.callFake((line: string) => of(payload(line))),
      annotations: jasmine.createSpy('annotations').and.returnValue(of([])),
      providers: () => of({ providers: [] }),
      appendRevision: jasmine.createSpy('appendRevision').and.returnValue(of({ revisionId: 'rev-new' })),
      addAnnotation: jasmine.createSpy('addAnnotation').and.returnValue(of({ annotationId: 'ann-new' })),
      setLineOrientation: jasmine.createSpy('setLineOrientation'),
      ...overrides,
    };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [{ provide: ApiClient, useValue: api }] });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', 'ln-first');
    fixture.componentRef.setInput('authorId', 'reviewer');
    fixture.detectChanges();
    const edit = (text: string) => {
      const input = fixture.nativeElement.querySelector('input.greek') as HTMLInputElement;
      input.value = text;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    const click = (label: string) => {
      const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
        .find(candidate => candidate.textContent?.trim() === label)!;
      button.click();
      fixture.detectChanges();
    };
    const switchTo = (line: string) => {
      fixture.componentRef.setInput('lineId', line);
      fixture.detectChanges();
    };
    return { api, fixture, edit, click, switchTo };
  }

  it('loads the new line and its own draft, resetting undo and annotation form state', () => {
    localStorage.setItem('leviathan:draft:ln-second', 'second draft');
    const { api, fixture, edit, switchTo } = setup();
    edit('first draft');
    const kind = fixture.nativeElement.querySelector('.annotation-form select') as HTMLSelectElement;
    kind.value = 'Abbreviation'; kind.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    switchTo('ln-second');

    expect(api.line).toHaveBeenCalledWith('ln-second');
    expect(fixture.nativeElement.querySelector('h3').textContent).toContain('ln-second');
    expect(fixture.nativeElement.querySelector('input.greek').value).toBe('second draft');
    expect(fixture.nativeElement.querySelector('.orientation select').value).toBe('ltr');
    expect(fixture.nativeElement.querySelector('.orientation input').value).toBe('ln-second');
    expect(fixture.nativeElement.querySelector('.annotation-form select').value).toBe('Unclear');
    const undo = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.trim() === 'Undo')!;
    expect(undo.disabled).toBeTrue();
    expect(localStorage.getItem('leviathan:draft:ln-first')).toBe('first draft');
  });

  it('ignores a late initial line response and error after selecting another line', () => {
    const first = new Subject<ReturnType<typeof payload>>();
    const { fixture, switchTo } = setup({ line: (line: string) => line === 'ln-first' ? first : of(payload(line)) });
    switchTo('ln-second');
    first.next(payload('ln-first'));
    first.error({ status: 500 });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input.greek').value).toBe('γδ');
    expect(fixture.nativeElement.querySelector('h3').textContent).toContain('ln-second');
  });

  it('ignores the original request when navigating away and back to the same line', () => {
    const original = new Subject<ReturnType<typeof payload>>();
    let firstReads = 0;
    const { fixture, switchTo } = setup({ line: (line: string) => {
      if (line === 'ln-first' && firstReads++ === 0) return original;
      return of(payload(line, line === 'ln-first' ? 'new first reading' : 'γδ'));
    } });
    switchTo('ln-second'); switchTo('ln-first');
    original.next(payload('ln-first', 'old first reading'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input.greek').value).toBe('new first reading');
  });

  it('ignores late annotation responses from the previous line', () => {
    const first = new Subject<unknown[]>();
    const { fixture, switchTo } = setup({ annotations: (line: string) => line === 'ln-first' ? first : of([]) });
    switchTo('ln-second');
    first.next([{ id: 'ann-old', kind: 'Unclear', anchoredRevisionId: 'ln-first-revision',
      range: { start: 0, end: 1 }, description: 'previous-line annotation' }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('previous-line annotation');
    expect(fixture.nativeElement.querySelector('input.greek').value).toBe('γδ');
  });

  for (const outcome of ['success', 'conflict']) {
    it(`keeps the new line's draft when the previous save returns ${outcome}`, () => {
      const saved = new Subject<{ revisionId: string }>();
      const { api, fixture, edit, click, switchTo } = setup({ appendRevision: () => saved });
      edit('first draft'); click('Save revision');
      switchTo('ln-second'); edit('second unsaved draft');
      const readsBeforeResponse = api.line.calls.count();
      if (outcome === 'success') saved.next({ revisionId: 'saved-first' });
      else saved.error({ status: 409 });
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('input.greek').value).toBe('second unsaved draft');
      expect(localStorage.getItem('leviathan:draft:ln-second')).toBe('second unsaved draft');
      expect(fixture.nativeElement.querySelector('.conflict')).toBeNull();
      expect(api.line.calls.count()).toBe(readsBeforeResponse);
    });
  }

  it('ignores an old orientation save result after navigation', () => {
    const response = new Subject<{ direction: string; script: string }>();
    const { fixture, click, switchTo } = setup({ setLineOrientation: () => response });
    click('Apply orientation'); switchTo('ln-second');
    response.next({ direction: 'rtl', script: 'old-script' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.orientation select').value).toBe('ltr');
    expect(fixture.nativeElement.querySelector('.orientation input').value).toBe('ln-second');
    expect(fixture.nativeElement.textContent).not.toContain('orientation saved');
  });

  it('does not create an abbreviation annotation on the new line after an old L2 save', () => {
    const response = new Subject<{ revisionId: string }>();
    const { api, fixture, click, switchTo } = setup({ appendRevision: () => response });
    const kind = fixture.nativeElement.querySelector('.annotation-form select') as HTMLSelectElement;
    kind.value = 'Abbreviation'; kind.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const end = fixture.nativeElement.querySelectorAll('.annotation-form input[type="number"]')[1] as HTMLInputElement;
    end.value = '1'; end.dispatchEvent(new Event('input'));
    const expansion = fixture.nativeElement.querySelector('input[placeholder="expanded text"]') as HTMLInputElement;
    expansion.value = 'expanded'; expansion.dispatchEvent(new Event('input'));
    fixture.detectChanges(); click('Add'); switchTo('ln-second');
    response.next({ revisionId: 'old-line-l2' });
    fixture.detectChanges();
    expect(api.addAnnotation).not.toHaveBeenCalled();
  });

  it('does not start follow-up requests after destruction', () => {
    const initial = new Subject<ReturnType<typeof payload>>();
    const { api, fixture } = setup({ line: () => initial });
    fixture.destroy(); initial.next(payload('ln-first'));
    expect(api.annotations).not.toHaveBeenCalled();
  });
});

describe('LineEditor editorial parent semantics', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  for (const existingL2 of [false, true]) {
    it(`uses ${existingL2 ? 'the L2 head' : 'no parent for the first L2'} when accepting an L1 alternative`, () => {
      const expectedParent = existingL2 ? 'rev-l2' : undefined;
      const line = jasmine.createSpy('line').and.returnValue(of({
        line: 'ln-parent', diplomatic: 'αβ', history: [{ id: 'rev-l1', text: 'αβ', author: 'human', at: '' }],
        revisions: [{ id: 'rev-l1', text: 'αβ', layer: 'L1Diplomatic', author: 'human', at: '' },
          ...(existingL2 ? [{ id: 'rev-l2', text: 'αδ', layer: 'l2Editorial', author: 'human', at: '' }] : [])],
      }));
      // Mirrors AppendLineRevision's layer-specific parent check: an L1 parent is rejected.
      const appendRevision = jasmine.createSpy('appendRevision').and.callFake(
        (_line: string, _layer: string, _text: string, _author: string, parent?: string) =>
          parent === expectedParent ? of({ revisionId: 'rev-new-l2' }) : throwError(() => ({ status: 409 })));
      TestBed.configureTestingModule({ imports: [LineEditor], providers: [{ provide: ApiClient, useValue: {
        line, appendRevision, providers: () => of({ providers: [] }),
        annotations: () => of([{ id: 'ann-alt', kind: 'AlternativeReading', anchoredRevisionId: 'rev-l1',
          range: { start: 1, end: 2 }, alternativeText: 'γ' }]),
      } }] });
      const fixture = TestBed.createComponent(LineEditor);
      fixture.componentRef.setInput('lineId', 'ln-parent');
      fixture.componentRef.setInput('authorId', 'human');
      fixture.detectChanges();
      const accept = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
        .find(button => button.textContent?.includes('Accept as L2'))!;
      accept.click(); fixture.detectChanges();
      expect(appendRevision).toHaveBeenCalledOnceWith('ln-parent', 'L2Editorial', 'αγ', 'human', expectedParent);
      expect(line).toHaveBeenCalledTimes(2);
    });
  }
});
