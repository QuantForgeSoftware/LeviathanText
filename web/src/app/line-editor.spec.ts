import { provideWorkspaceTestAccess, TEST_WORKSPACE_SURFACE_ID, seedOwnedDraft, readOwnedDraft } from './workspace-testing.fixture.spec';
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
      providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
    fixture.componentRef.setInput('lineId', lineId);
    fixture.componentRef.setInput('authorId', 'tester');
    return fixture;
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('restores an unsaved draft that differs from the server text', () => {
    seedOwnedDraft(lineId, 'draft text', 'server text');
    const fixture = setup('server text');
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('draft text');
    expect(fixture.nativeElement.textContent).toContain('Unsaved draft restored');
  });

  it('ignores a stale draft identical to the server text', () => {
    seedOwnedDraft(lineId, 'same text', 'same text');
    const fixture = setup('same text');
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('same text');
    expect(fixture.nativeElement.textContent).not.toContain('Unsaved draft restored');
  });

  it('preserves an ownerless atomic legacy envelope without reading it as the current owner draft', () => {
    const key = `leviathan:draft:${lineId}:recovery`;
    const raw = JSON.stringify({ version: 1, text: 'ownerless private draft', base: 'old private ancestor' });
    localStorage.setItem(key, raw);
    const fixture = setup('current server text'); fixture.detectChanges();
    expect((fixture.nativeElement.querySelector('input') as HTMLInputElement).value).toBe('current server text');
    expect(fixture.nativeElement.textContent).not.toContain('ownerless private draft');
    expect(localStorage.getItem(key)).toBe(raw);
    expect(readOwnedDraft(lineId)?.draftText).toBe('current server text');
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
      line: () => of({ line: 'ln-alt', diplomatic: 'abd', revisions: [], history: [{ id: 'rev-1', author: 'x', text: 'abd' }] }),
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
      providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
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
      providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
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
      providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
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
      providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
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
      providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
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
        line: 'ln-abbr', diplomatic: 'κϲ', revisions: [],
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
      providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
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
      providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
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
      providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
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

    expect(fixture.nativeElement.textContent).toContain('needs comparison with the latest saved reading');
    expect((fixture.nativeElement.querySelector('input') as HTMLInputElement).value).toBe('Αβγ');
  });
});

describe('LineEditor annotation coordinates', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function setup() {
    const appendRevision = jasmine.createSpy('appendRevision').and.returnValue(of({ revisionId: 'rev-new' }));
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: {
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
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
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
    line, diplomatic: text, revisions: [], direction: line === 'ln-first' ? 'rtl' : 'ltr', script: line,
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
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: api }] });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
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
    seedOwnedDraft('ln-second', 'second draft', 'γδ', { baseRevisionId: 'ln-second-revision' });
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
    expect(readOwnedDraft('ln-first')?.draftText).toBe('first draft');
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
      expect(readOwnedDraft('ln-second')?.draftText).toBe('second unsaved draft');
      expect(fixture.nativeElement.querySelector('.conflict')).toBeNull();
      expect(api.line.calls.count()).toBe(readsBeforeResponse);
    });
  }

  it('retains the original orientation workflow and quarantines forced navigation before an old result', () => {
    const response = new Subject<{ direction: string; script: string }>();
    const { api, fixture, click, switchTo } = setup({ setLineOrientation: () => response });
    click('Apply orientation'); switchTo('ln-second');
    response.next({ direction: 'rtl', script: 'old-script' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.orientation select').value).toBe('rtl');
    expect(fixture.nativeElement.querySelector('.orientation input').value).toBe('ln-first');
    expect((fixture.componentInstance as any).payload().line).toBe('ln-first');
    expect((fixture.componentInstance as any).hasNavigationWork()).toBeTrue();
    expect(api.line).toHaveBeenCalledTimes(1);
    expect(readOwnedDraft('ln-first')?.unconfirmedWrites.length).toBeGreaterThan(0);
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
          ...(existingL2 ? [{ id: 'rev-l2', text: 'αβ', layer: 'l2Editorial', author: 'human', at: '' }] : [])],
      }));
      // Mirrors AppendLineRevision's layer-specific parent check: an L1 parent is rejected.
      const appendRevision = jasmine.createSpy('appendRevision').and.callFake(
        (_line: string, _layer: string, _text: string, _author: string, parent?: string) =>
          parent === expectedParent ? of({ revisionId: 'rev-new-l2' }) : throwError(() => ({ status: 409 })));
      TestBed.configureTestingModule({ imports: [LineEditor], providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: {
        line, appendRevision, providers: () => of({ providers: [] }),
        annotations: () => of([{ id: 'ann-alt', kind: 'AlternativeReading', anchoredRevisionId: 'rev-l1',
          range: { start: 1, end: 2 }, alternativeText: 'γ' }]),
      } }] });
      const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
      fixture.componentRef.setInput('lineId', 'ln-parent');
      fixture.componentRef.setInput('authorId', 'human');
      fixture.detectChanges();
      const accept = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
        .find(button => button.textContent?.includes('Accept as L2'))!;
      accept.click(); fixture.detectChanges();
      expect(appendRevision).toHaveBeenCalledOnceWith('ln-parent', 'L2Editorial', 'αγ', 'human', expectedParent, !existingL2);
      expect(line).toHaveBeenCalledTimes(2);
    });
  }
});

describe('LineEditor same-line pending writes', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function setup(initial = 'αβ') {
    const payload = (text: string, revision = 'rev-base') => ({
      line: 'ln-pending', diplomatic: text,
      history: [{ id: revision, text, author: 'human', at: '' }],
    });
    const saved = new Subject<{ revisionId: string }>();
    const refreshed = new Subject<ReturnType<typeof payload>>();
    const reviewed = new Subject<{}>();
    let reads = 0;
    const api = {
      line: jasmine.createSpy('line').and.callFake(() => reads++ === 0 ? of(payload(initial)) : refreshed),
      annotations: () => of([]), providers: () => of({ providers: [] }),
      appendRevision: jasmine.createSpy('appendRevision').and.returnValue(saved),
      addAnnotation: jasmine.createSpy('addAnnotation').and.returnValue(of({ annotationId: 'ann-new' })),
      recordReview: jasmine.createSpy('recordReview').and.returnValue(reviewed),
    };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: api }] });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
    fixture.componentRef.setInput('lineId', 'ln-pending');
    fixture.componentRef.setInput('authorId', 'human');
    fixture.detectChanges();
    const edit = (text: string) => {
      const input = fixture.nativeElement.querySelector('input.greek') as HTMLInputElement;
      input.value = text; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    };
    const button = (label: string) => Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(candidate => candidate.textContent?.trim() === label)!;
    const click = (label: string) => { button(label).click(); fixture.detectChanges(); };
    const text = () => (fixture.nativeElement.querySelector('input.greek') as HTMLInputElement).value;
    return { api, fixture, edit, click, button, text, saved, refreshed, reviewed, payload };
  }

  it('retains text typed after submission through acknowledgement and reconciliation', () => {
    const s = setup(); s.edit('αγ'); s.click('Save revision'); s.edit('αγδ');
    s.saved.next({ revisionId: 'rev-saved' });
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('αγδ');
    s.refreshed.next(s.payload('αγ', 'rev-saved')); s.fixture.detectChanges();
    expect(s.text()).toBe('αγδ');
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('αγδ');
    expect(s.fixture.nativeElement.textContent).toContain('unsaved');
  });

  it('retains edits made while the reconciliation read is pending', () => {
    const s = setup(); s.edit('αγ'); s.click('Save revision');
    s.saved.next({ revisionId: 'rev-saved' }); s.edit('αγδ');
    s.refreshed.next(s.payload('αγ', 'rev-saved')); s.fixture.detectChanges();
    expect(s.text()).toBe('αγδ');
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('αγδ');
  });

  it('acknowledges a merged saved value when no later edits exist', () => {
    const s = setup(); s.edit('αγ'); s.click('Save revision');
    s.saved.next({ revisionId: 'rev-saved' });
    s.refreshed.next(s.payload('δαγ', 'rev-saved')); s.fixture.detectChanges();
    expect(s.text()).toBe('δαγ');
    const resolved = readOwnedDraft('ln-pending')!;
    expect(resolved.draftText).toBe(resolved.base.text);
    expect(resolved.unconfirmedWrites).toEqual([]);
  });

  it('prevents overlapping saves until the new parent has been reconciled', () => {
    const s = setup(); s.edit('αγ'); s.click('Save revision'); s.edit('αγδ'); s.click('Save revision');
    expect(s.api.appendRevision).toHaveBeenCalledTimes(1);
    s.saved.next({ revisionId: 'rev-saved' }); s.fixture.detectChanges();
    expect(s.button('Save revision').disabled).toBeTrue();
    s.refreshed.next(s.payload('αγ', 'rev-saved')); s.fixture.detectChanges();
    expect(s.button('Save revision').disabled).toBeFalse();
  });

  it('does not let a review refresh overwrite a later unsaved draft', () => {
    const s = setup(); s.click('Accept'); s.edit('αγ'); s.reviewed.next({});
    s.refreshed.next(s.payload('αβ')); s.fixture.detectChanges();
    expect(s.text()).toBe('αγ');
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('αγ');
  });

  it('refuses acceptance and source-offset annotations while a draft is displayed', () => {
    const s = setup(); s.edit('αγ'); s.click('Accept'); s.click('Mark unclear (at caret)');
    expect(s.api.recordReview).not.toHaveBeenCalled();
    expect(s.api.addAnnotation).not.toHaveBeenCalled();
  });

  it('retains later edits after a correction creates a new revision', () => {
    const s = setup(); s.edit('αγ'); s.click('Correct with current text'); s.edit('αγδ');
    s.reviewed.next({}); s.refreshed.next(s.payload('αγ', 'rev-corrected')); s.fixture.detectChanges();
    expect(s.text()).toBe('αγδ');
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('αγδ');
    expect(s.button('Save revision').disabled).toBeFalse();
  });

  it('keeps drafts and permits retry after an explicit validation refusal', () => {
    const s = setup(); s.edit('αγ'); s.click('Save revision'); s.edit('αγδ');
    s.saved.error({ status: 422 }); s.fixture.detectChanges();
    expect(s.text()).toBe('αγδ');
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('αγδ');
    expect(s.button('Save revision').disabled).toBeFalse();
  });

  it('keeps the submitted draft if a successful save cannot be refreshed', () => {
    const s = setup(); s.edit('αγ'); s.click('Save revision');
    s.saved.next({ revisionId: 'rev-saved' }); s.refreshed.error({ status: 500 }); s.fixture.detectChanges();
    expect(s.text()).toBe('αγ');
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('αγ');
    expect(s.button('Save revision').disabled).toBeTrue();
    expect(s.button('Keep my draft as replacement').disabled).toBeTrue();
  });

  it('persists undo so crash recovery restores the visible text', () => {
    const s = setup(); s.edit('αγ'); s.edit('αγδ'); s.click('Undo');
    expect(s.text()).toBe('αγ');
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('αγ');
  });

  it('rebases typing after a merged acknowledgement before allowing a new save', () => {
    const s = setup(); s.edit('αγ'); s.click('Save revision'); s.edit('αγε');
    s.saved.next({ revisionId: 'rev-merged' });
    s.refreshed.next(s.payload('δαγ', 'rev-merged')); s.fixture.detectChanges();
    expect(s.text()).toBe('δαγε');
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('δαγε');
    expect(readOwnedDraft('ln-pending')?.base.text).toBe('δαγ');
    expect(s.button('Undo').disabled).toBeTrue();
    s.click('Save revision');
    expect(s.api.appendRevision.calls.mostRecent().args).toEqual(['ln-pending', 'L1Diplomatic', 'δαγε', 'human', 'rev-merged', true]);
  });

  it('resets source offsets after reconciliation changes the displayed text', () => {
    const s = setup();
    const offsets = s.fixture.nativeElement.querySelectorAll('.annotation-form input[type="number"]') as NodeListOf<HTMLInputElement>;
    offsets[0].value = '1'; offsets[0].dispatchEvent(new Event('input'));
    offsets[1].value = '2'; offsets[1].dispatchEvent(new Event('input'));
    s.edit('αγ'); s.click('Save revision');
    s.saved.next({ revisionId: 'rev-merged' });
    s.refreshed.next(s.payload('δαγ', 'rev-merged')); s.fixture.detectChanges();
    expect(s.text()).toBe('δαγ');
    expect(offsets[0].value).toBe('0'); expect(offsets[1].value).toBe('0');
    expect(s.fixture.nativeElement.textContent).toContain('position 0');
  });

  it('discards annotation form offsets selected before editing once a new text revision is saved', () => {
    const s = setup();
    const offsets = s.fixture.nativeElement.querySelectorAll('.annotation-form input[type="number"]') as NodeListOf<HTMLInputElement>;
    offsets[0].value = '1'; offsets[0].dispatchEvent(new Event('input'));
    offsets[1].value = '2'; offsets[1].dispatchEvent(new Event('input'));
    s.edit('γδε'); s.click('Save revision');
    s.saved.next({ revisionId: 'rev-saved' });
    s.refreshed.next(s.payload('γδε', 'rev-saved')); s.fixture.detectChanges();
    expect(offsets[0].value).toBe('0'); expect(offsets[1].value).toBe('0');
  });

  for (const readStartedBeforeSave of [false, true]) {
    it(`does not let an earlier review unlock a pending write (read already started: ${readStartedBeforeSave})`, () => {
      const s = setup(); s.click('Accept');
      if (!readStartedBeforeSave) {
        s.edit('αγ'); s.click('Save revision'); s.edit('αγε');
        expect(s.api.appendRevision).not.toHaveBeenCalled();
        expect(s.button('Save revision').disabled).toBeTrue();
        s.reviewed.next({}); s.refreshed.next(s.payload('αβ')); s.fixture.detectChanges();
        expect(s.text()).toBe('αγε'); expect(s.button('Save revision').disabled).toBeFalse();
        s.click('Save revision'); s.edit('αγεδ');
        expect(s.api.appendRevision).toHaveBeenCalledTimes(1);
        s.saved.next({ revisionId: 'rev-saved' });
        s.refreshed.next(s.payload('αγε', 'rev-saved')); s.fixture.detectChanges();
        expect(s.text()).toBe('αγεδ'); expect(s.button('Save revision').disabled).toBeFalse();
        return;
      }
      if (readStartedBeforeSave) s.reviewed.next({});
      s.edit('αγ'); s.click('Save revision'); s.edit('αγε');
      if (!readStartedBeforeSave) s.reviewed.next({});
      s.refreshed.next(s.payload('αβ')); s.fixture.detectChanges();
      expect(s.button('Save revision').disabled).toBeTrue();
      s.click('Save revision');
      expect(s.api.appendRevision).toHaveBeenCalledTimes(1);
      s.saved.next({ revisionId: 'rev-saved' });
      s.refreshed.next(s.payload('αγ', 'rev-saved')); s.fixture.detectChanges();
      expect(s.text()).toBe('αγε');
      expect(s.button('Save revision').disabled).toBeFalse();
    });
  }

  it('keeps overlapping post-submit edits blocked across reload until explicitly compared', () => {
    const s = setup(); s.edit('αγ'); s.click('Save revision'); s.edit('αδε');
    s.saved.next({ revisionId: 'rev-merged' });
    s.refreshed.next(s.payload('αζ', 'rev-merged')); s.fixture.detectChanges();
    expect(s.text()).toBe('αδε');
    expect(s.button('Save revision').disabled).toBeTrue();
    expect(s.button('Mark unclear (at caret)').disabled).toBeTrue();
    expect(s.fixture.nativeElement.textContent).toContain('Latest saved reading: αζ');
    s.click('Reload latest'); s.refreshed.next(s.payload('αζ', 'rev-merged')); s.fixture.detectChanges();
    expect(s.text()).toBe('αδε');
    expect(s.button('Save revision').disabled).toBeTrue();
    s.click('Keep my draft as replacement'); s.click('Save revision');
    expect(s.api.appendRevision.calls.mostRecent().args).toEqual(['ln-pending', 'L1Diplomatic', 'αδε', 'human', 'rev-merged', true]);
  });

  it('rebases a persisted draft onto a newer saved reading after restart', () => {
    seedOwnedDraft('ln-pending', 'αγ', 'αβ', { baseRevisionId: 'rev-base' });
    const s = setup('δαβ');
    expect(s.text()).toBe('δαγ');
    s.click('Save revision');
    expect(s.api.appendRevision.calls.mostRecent().args[2]).toBe('δαγ');
  });

  it('retains an overlapping recovered draft without silently naming the new parent', () => {
    seedOwnedDraft('ln-pending', 'αγ', 'αβ', { baseRevisionId: 'rev-base' });
    const s = setup('αδ');
    expect(s.text()).toBe('αγ');
    expect(s.button('Save revision').disabled).toBeTrue();
    s.click('Save revision');
    expect(s.api.appendRevision).not.toHaveBeenCalled();
    s.click('Use latest saved text');
    expect(s.text()).toBe('αδ');
    const resolved = readOwnedDraft('ln-pending')!;
    expect(resolved.draftText).toBe(resolved.base.text);
    expect(resolved.unconfirmedWrites).toEqual([]);
  });

  it('quarantines ownerless recovery without an ancestor instead of assigning the current owner', () => {
    const key = 'leviathan:draft:ln-pending';
    localStorage.setItem(key, 'αγ');
    const s = setup('δαβ');
    expect(s.text()).toBe('δαβ');
    expect(s.button('Save revision').disabled).toBeTrue();
    expect(localStorage.getItem(key)).toBe('αγ');
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('δαβ');
    expect(s.button('Reload latest')).toBeUndefined();
    expect(s.button('Keep my draft as replacement')).toBeUndefined();
    s.click('Save revision'); // Disabled clean-source action cannot submit ownerless text.
    expect(s.text()).toBe('δαβ');
    expect(localStorage.getItem(key)).toBe('αγ');
    expect(s.api.appendRevision).not.toHaveBeenCalled();
  });

  it('rebases an unsaved draft during metadata refresh rather than overwriting remote changes later', () => {
    const s = setup(); s.click('Accept'); s.edit('αγ'); s.reviewed.next({});
    s.refreshed.next(s.payload('δαβ', 'rev-other')); s.fixture.detectChanges();
    expect(s.text()).toBe('δαγ');
    s.click('Save revision');
    expect(s.api.appendRevision.calls.mostRecent().args).toEqual(['ln-pending', 'L1Diplomatic', 'δαγ', 'human', 'rev-other', true]);
  });

  it('requires a fresh comparison after a correction-review conflict', () => {
    const s = setup(); s.edit('αγ'); s.click('Correct with current text');
    s.reviewed.error({ status: 409 }); s.fixture.detectChanges();
    expect(s.text()).toBe('αγ');
    expect(s.button('Save revision').disabled).toBeTrue();
    expect(s.button('Keep my draft as replacement').disabled).toBeTrue();
    s.click('Reload latest'); s.refreshed.next(s.payload('αδ', 'rev-other')); s.fixture.detectChanges();
    expect(s.button('Save revision').disabled).toBeTrue();
    expect(s.button('Keep my draft as replacement').disabled).toBeFalse();
  });

  it('preserves separate ownerless text and ancestor keys without adopting either', () => {
    localStorage.setItem('leviathan:draft:ln-pending', 'αγ');
    localStorage.setItem('leviathan:draft:ln-pending:base', 'αβ');
    const s = setup('δαβ');
    expect(s.text()).toBe('δαβ');
    expect(s.button('Save revision').disabled).toBeTrue();
    expect(localStorage.getItem('leviathan:draft:ln-pending')).toBe('αγ');
    expect(localStorage.getItem('leviathan:draft:ln-pending:base')).toBe('αβ');
    expect(readOwnedDraft('ln-pending')?.base.text).toBe('δαβ');
    expect(s.api.appendRevision).not.toHaveBeenCalled();
  });

  it('keeps the mounted newest draft and blocks navigation when atomic owned persistence fails', () => {
    const s = setup(); const blocked: boolean[] = [];
    s.fixture.componentInstance.navigationBlocked.subscribe(value => blocked.push(value));
    s.edit('αγ');
    const before = readOwnedDraft('ln-pending')!;
    expect(before.draftText).toBe('αγ'); expect(before.base.text).toBe('αβ');
    expect(before.base.revisionId).toBe('rev-base');
    const originalSet = Storage.prototype.setItem;
    const writes = spyOn(Storage.prototype, 'setItem').and.callFake(function(this: Storage, key: string, value: string) {
      if (key.startsWith('leviathan:workspace-draft:v1:')) throw new DOMException('quota exceeded', 'QuotaExceededError');
      originalSet.call(this, key, value);
    });
    s.edit('αγε');
    expect(s.text()).toBe('αγε');
    expect(readOwnedDraft('ln-pending')).toEqual(before);
    expect(blocked.at(-1)).toBeTrue();
    expect(s.fixture.nativeElement.textContent).toContain('mounted draft is retained');
    expect(localStorage.getItem('leviathan:draft:ln-pending')).toBeNull();
    expect(localStorage.getItem('leviathan:draft:ln-pending:recovery')).toBeNull();
    writes.and.callThrough();
    // Retrying a real edit persists the complete text/base together; no fallback
    // legacy write or destructive navigation is used to manufacture recovery.
    s.edit('αγεζ');
    const retry = readOwnedDraft('ln-pending')!;
    expect(retry.draftText).toBe('αγεζ'); expect(retry.base).toEqual(before.base);
  });

  it('retains every previous owned journal byte if its atomic append fails', () => {
    const s = setup(); s.edit('αγ');
    const before = readOwnedDraft('ln-pending');
    const records = () => Array.from({ length: localStorage.length }, (_, index) => localStorage.key(index)!)
      .filter(key => key.startsWith('leviathan:workspace-draft:v1:')).sort()
      .map(key => [key, localStorage.getItem(key)]);
    const bytesBefore = records();
    const originalSet = Storage.prototype.setItem;
    const writes = spyOn(Storage.prototype, 'setItem').and.callFake(function(this: Storage, key: string, value: string) {
      if (key.startsWith('leviathan:workspace-draft:v1:')) throw new DOMException('quota exceeded', 'QuotaExceededError');
      originalSet.call(this, key, value);
    });
    s.edit('αγε');
    expect(s.text()).toBe('αγε');
    expect(records()).toEqual(bytesBefore);
    expect(readOwnedDraft('ln-pending')).toEqual(before);
    expect(readOwnedDraft('ln-pending')?.draftText).toBe('αγ');
    writes.and.callThrough();
  });

  it('keeps a failed initial read unloaded until retry succeeds', () => {
    const api = {
      line: jasmine.createSpy('line').and.returnValue(throwError(() => ({ status: 500 }))),
      appendRevision: jasmine.createSpy('appendRevision'), addAnnotation: jasmine.createSpy('addAnnotation'),
      annotations: () => of([]), providers: () => of({ providers: [] }),
    };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [provideWorkspaceTestAccess(), { provide: ApiClient, useValue: api }] });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
    fixture.componentRef.setInput('lineId', 'ln-failed'); fixture.componentRef.setInput('authorId', 'human'); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input.greek')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Could not load this line');
    expect(api.appendRevision).not.toHaveBeenCalled(); expect(api.addAnnotation).not.toHaveBeenCalled();
    api.line.and.returnValue(of({ line: 'ln-failed', diplomatic: 'αβ', history: [{ id: 'rev-loaded', text: 'αβ', author: 'human', at: '' }] }));
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input.greek').value).toBe('αβ');
    expect(fixture.nativeElement.textContent).not.toContain('Could not load this line');
  });
});
