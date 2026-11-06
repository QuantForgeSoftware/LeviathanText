import { provideWorkspaceTestAccess, readOwnedDraft } from './workspace-testing.fixture.spec';
import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiClient } from './api-client';
import { SurfaceView } from './surface-view';

describe('SurfaceView native AI save refresh', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function setup() {
    const acceptance = new Subject<unknown>();
    const advisory = new Subject<unknown>();
    const revisionSave = new Subject<{ revisionId: string }>();
    let editorial = 'αδ';
    let annotations: { id: string; kind: string; anchoredRevisionId: string; range: { start: number; end: number }; alternativeText?: string }[] = [];
    const payload = (line: string) => {
      const text = line === 'ln-first' ? 'αβ' : 'ζη';
      return { line, diplomatic: text, history: [{ id: 'rev-' + line, text, author: 'fixture', at: '' }] };
    };
    const api = {
      createSession: () => of({ id: 'session-fixture', nodeId: 'node-fixture' }),
      touchPresence: () => of({ editor: 'fixture', editors: [] }),
      leavePresence: () => of({}), revokeSession: () => of({}), sessions: () => of([]), surfaceFragments: () => of([]),
      diplomaticExport: () => of({ text: 'αβ\nζη', lossReport: { format: 'text', contractVersion: 'fixture', entries: [] } }),
      reviewQueue: () => of([]), models: () => of([]), profileReport: () => of({ profile: 'fixture', version: '1', surfaces: [] }),
      loadProjectFresh: () => of({ id: 'col-fixture', name: 'fixture', objects: [{ id: 'obj-fixture', surfaces: [{
        id: 'surf-fixture', kind: 'page', captures: [], regions: [{ id: 'reg-fixture' }],
        lines: ['ln-first', 'ln-second'].map(id => ({ id, regionId: 'reg-fixture' })),
      }] }] }),
      line: jasmine.createSpy('line').and.callFake((line: string) => of(payload(line))),
      annotations: jasmine.createSpy('annotations').and.callFake(() => of(annotations)),
      lineLayerHistory: jasmine.createSpy('lineLayerHistory').and.callFake((line: string) => of([
        { id: 'l2-' + line, layer: 'L2Editorial', text: line === 'ln-first' ? editorial : 'ζηθ', author: 'fixture', at: '' },
      ])),
      providers: () => of({ providers: [{ configRef: 'generated', settings: { modelId: 'fixture' } }] }),
      propose: jasmine.createSpy('propose').and.callFake((request: unknown) => of({
        id: 'prop-fixture', request, proposedText: 'αΩ', providerId: 'generated', modelId: 'fixture', status: 'proposed',
      })),
      acceptProposal: jasmine.createSpy('acceptProposal').and.returnValue(acceptance),
      addAnnotation: jasmine.createSpy('addAnnotation').and.returnValue(advisory),
      appendRevision: jasmine.createSpy('appendRevision').and.returnValue(revisionSave),
    };
    TestBed.configureTestingModule({ imports: [SurfaceView], providers: [provideWorkspaceTestAccess({ projectId: 'col-fixture' }), { provide: ApiClient, useValue: api }] });
    const fixture = TestBed.createComponent(SurfaceView);
    fixture.componentRef.setInput('projectId', 'col-fixture'); fixture.componentRef.setInput('surfaceId', 'surf-fixture');
    fixture.componentRef.setInput('focusLineId', 'ln-first'); fixture.detectChanges();
    const button = (scope: string, label: string) => Array.from(fixture.nativeElement.querySelectorAll(`${scope} button`) as NodeListOf<HTMLButtonElement>)
      .find(item => item.textContent?.includes(label))!;
    const click = (scope: string, label: string) => { button(scope, label).click(); fixture.detectChanges(); };
    const propose = (task?: string) => {
      if (task) {
        const select = Array.from(fixture.nativeElement.querySelectorAll('app-proposal-panel select') as NodeListOf<HTMLSelectElement>)
          .find(element => element.querySelector('option[value="translation"]'))!;
        select.value = task; select.dispatchEvent(new Event('change')); fixture.detectChanges();
      }
      click('app-proposal-panel', 'Propose');
    };
    const edit = (text: string) => {
      const input = fixture.nativeElement.querySelector('app-line-editor input.greek') as HTMLInputElement;
      input.value = text; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    };
    const editorText = () => (fixture.nativeElement.querySelector('app-line-editor input.greek') as HTMLInputElement).value;
    const paneText = () => (fixture.nativeElement.querySelectorAll('app-dual-pane .pane')[1] as HTMLElement).querySelector('.greek')?.textContent;
    return { fixture, api, acceptance, advisory, revisionSave, propose, edit, editorText, paneText, button, click,
      setEditorial: (text: string) => { editorial = text; }, setAnnotations: (value: typeof annotations) => { annotations = value; } };
  }

  it('shows newly accepted editorial text on the current line without navigation or clearing its L1 draft', () => {
    const s = setup();
    expect(s.paneText()).toBe('αδ');
    s.propose(); s.click('app-proposal-panel', 'Accept'); s.edit('αβε');
    s.setEditorial('αΩ'); s.acceptance.next({ revisionId: 'rev-new-l2' }); s.fixture.detectChanges();
    expect(s.paneText()).toBe('αΩ');
    expect(s.editorText()).toBe('αβε');
    expect(readOwnedDraft('ln-first', { projectId: 'col-fixture', surfaceId: 'surf-fixture' })?.draftText).toBe('αβε');
    expect(localStorage.getItem('leviathan:draft:ln-first')).toBeNull();
    expect(s.button('app-line-editor', 'Save revision').disabled).toBeFalse();
  });

  it('refreshes saved advisory annotations while retaining an unsaved diplomatic draft', () => {
    const s = setup();
    s.propose('translation'); s.click('app-proposal-panel', 'Save as advisory'); s.edit('αβε');
    const reads = s.api.lineLayerHistory.calls.count();
    s.setAnnotations([{ id: 'ann-translation', kind: 'Translation', anchoredRevisionId: 'rev-ln-first',
      range: { start: 0, end: 2 }, alternativeText: 'generated translation' }]);
    s.advisory.next({ id: 'ann-translation' }); s.fixture.detectChanges();
    expect(s.api.lineLayerHistory.calls.count()).toBe(reads + 1);
    expect(s.fixture.nativeElement.querySelector('app-line-editor').textContent).toContain('generated translation');
    expect(s.editorText()).toBe('αβε');
    expect(s.paneText()).toBe('αδ');
  });

  it('blocks an interleaved L1 write until the editorial acceptance is acknowledged', () => {
    const s = setup();
    s.propose(); s.click('app-proposal-panel', 'Accept'); s.edit('αβε');
    s.click('app-line-editor', 'Save revision');
    expect(s.api.appendRevision).not.toHaveBeenCalled();
    const reads = s.api.line.calls.count();
    s.setEditorial('αΩ'); s.acceptance.next({}); s.fixture.detectChanges();
    expect(s.api.line.calls.count()).toBe(reads + 2); // Both editor and read-only pane can refresh after the serialized acceptance.
    expect(s.paneText()).toBe('αΩ');
    expect(s.editorText()).toBe('αβε');
    expect(readOwnedDraft('ln-first', { projectId: 'col-fixture', surfaceId: 'surf-fixture' })?.draftText).toBe('αβε');
    expect(localStorage.getItem('leviathan:draft:ln-first')).toBeNull();
    expect(s.button('app-line-editor', 'Save revision').disabled).toBeFalse();
    s.click('app-line-editor', 'Save revision');
    expect(s.api.appendRevision).toHaveBeenCalledTimes(1);
    expect(s.button('app-line-editor', 'Save revision').disabled).toBeTrue();
  });

  for (const advisory of [false, true]) {
    it(`does not notify readers after a failed ${advisory ? 'advisory' : 'editorial'} write`, () => {
      const s = setup();
      s.propose(advisory ? 'translation' : undefined);
      s.click('app-proposal-panel', advisory ? 'Save as advisory' : 'Accept');
      const reads = s.api.lineLayerHistory.calls.count();
      s.setEditorial('αΩ');
      (advisory ? s.advisory : s.acceptance).error({ status: 422 }); s.fixture.detectChanges();
      expect(s.api.lineLayerHistory.calls.count()).toBe(reads);
      expect(s.paneText()).toBe('αδ');
      expect(s.fixture.nativeElement.querySelector('app-proposal-panel').textContent)
        .toContain(advisory ? 'could not save advisory result' : 'acceptance failed');
    });
  }

  for (const returnToFirst of [false, true]) {
    it(`retains the pending acceptance on attempted navigation${returnToFirst ? ' away and back' : ''}`, () => {
      const s = setup();
      s.propose(); s.click('app-proposal-panel', 'Accept');
      s.fixture.componentRef.setInput('focusLineId', 'ln-second'); s.fixture.detectChanges();
      if (returnToFirst) { s.fixture.componentRef.setInput('focusLineId', 'ln-first'); s.fixture.detectChanges(); }
      expect(s.paneText()).toBe('αδ');
      expect(s.editorText()).toBe('αβ');
      expect(s.button('app-proposal-panel', 'Accept').disabled).toBeTrue();
      const reads = s.api.lineLayerHistory.calls.count();
      s.setEditorial('αΩ'); s.acceptance.next({}); s.fixture.detectChanges();
      expect(s.api.line.calls.allArgs().every(args => args[0] === 'ln-first')).toBeTrue();
      expect(s.api.lineLayerHistory.calls.count()).toBe(reads + 1);
      expect(s.paneText()).toBe('αΩ');
      expect(s.editorText()).toBe('αβ');
    });
  }

  it('allows a fresh focus navigation when the current line has no pending work', () => {
    const s = setup();
    s.fixture.componentRef.setInput('focusLineId', 'ln-second'); s.fixture.detectChanges();
    expect(s.editorText()).toBe('ζη');
    expect(s.paneText()).toBe('ζηθ');
    expect(s.api.line.calls.allArgs().some(args => args[0] === 'ln-second')).toBeTrue();
  });

  it('invalidates pending success notifications when the view is destroyed', () => {
    const s = setup();
    s.propose(); s.click('app-proposal-panel', 'Accept');
    const reads = s.api.line.calls.count();
    s.fixture.destroy(); s.acceptance.next({});
    expect(s.api.line.calls.count()).toBe(reads);
  });
});
