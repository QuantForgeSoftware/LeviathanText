import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { of, Subject } from 'rxjs';
import { ApiClient } from './api-client';
import { LineEditor } from './line-editor';
import { ProposalPanel } from './proposal-panel';
import { WorkspaceAccess, WorkspaceAccessState } from './workspace-access-types';
import { provideWorkspaceTestAccess, TEST_WORKSPACE_SURFACE_ID, TEST_WORKSPACE_PROJECT_ID, TEST_WORKSPACE_OWNER, readOwnedDraft } from './workspace-testing.fixture.spec';

class ManualTestAccess implements WorkspaceAccess {
  private value: WorkspaceAccessState = { status: 'active', generation: 0, reason: 'generated',
    principal: { mode: 'account', ...TEST_WORKSPACE_OWNER, role: 'Reviewer' } };
  private listeners = new Set<(state: WorkspaceAccessState) => void>();
  state() { return this.value; }
  isActive() { return this.value.status === 'active'; }
  owner() { return this.isActive() ? { ...TEST_WORKSPACE_OWNER } : null; }
  projectId() { return this.isActive() ? TEST_WORKSPACE_PROJECT_ID : null; }
  capture() {
    if (!this.isActive()) return null;
    const generation = this.value.generation;
    return { credential: 'generated-manual-credential', generation, projectId: TEST_WORKSPACE_PROJECT_ID,
      current: () => this.isActive() && this.value.generation === generation };
  }
  subscribe(listener: (state: WorkspaceAccessState) => void) { this.listeners.add(listener); listener(this.value); return () => { this.listeners.delete(listener); }; }
  retry() { this.setActive(true); }
  setActive(active: boolean) {
    this.value = { ...this.value, status: active ? 'active' : 'suspended', generation: this.value.generation + 1 };
    for (const listener of this.listeners) listener(this.value);
  }
}

/** Actual handlers and component composition; generated recording Subjects only. */
describe('Manual editor mutation outcomes and composed write admission', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function setup(editorialText?: string, includeEditorialHistory = true) {
    const access = new ManualTestAccess();
    const annotation = new Subject<any>(), revision = new Subject<any>(), acceptance = new Subject<any>();
    const payload = { line: 'ln-manual-outcome', diplomatic: 'ab',
      history: [{ id: 'rev-manual-base', text: 'ab', layer: 'L1Diplomatic', author: 'generated', at: '' }],
      revisions: includeEditorialHistory ? [{ id: 'rev-manual-base', text: 'ab', layer: 'L1Diplomatic', author: 'generated', at: '' },
        ...(editorialText === undefined ? [] : [{ id: 'rev-current-editorial', text: editorialText, layer: 'L2Editorial', author: 'generated', at: '' }])] : undefined };
    const api = {
      line: jasmine.createSpy('line').and.returnValue(of(payload)),
      annotations: () => of([]), providers: () => of({ providers: [{ configRef: 'generated', settings: { modelId: 'double' } }] }),
      addAnnotation: jasmine.createSpy('addAnnotation').and.returnValue(annotation),
      appendRevision: jasmine.createSpy('appendRevision').and.returnValue(revision),
      retractAnnotation: jasmine.createSpy('retractAnnotation').and.returnValue(of({})),
      setLineOrientation: jasmine.createSpy('orientation').and.returnValue(of({})),
      recordReview: jasmine.createSpy('review').and.returnValue(of({})),
      acceptProposal: jasmine.createSpy('acceptProposal').and.returnValue(acceptance),
      propose: () => of({ id: 'proposal-generated', request: { targetRevisionId: 'rev-manual-base', range: { start: 0, end: 2 }, task: 'candidateReading' },
        proposedText: 'AB', status: 'proposed', providerId: 'double', modelId: 'double' }),
    };
    TestBed.configureTestingModule({ imports: [LineEditor], providers: [provideWorkspaceTestAccess({ access }), { provide: ApiClient, useValue: api }] });
    const create = () => {
      const fixture = TestBed.createComponent(LineEditor);
      fixture.componentRef.setInput('surfaceId', TEST_WORKSPACE_SURFACE_ID);
      fixture.componentRef.setInput('lineId', 'ln-manual-outcome'); fixture.componentRef.setInput('authorId', 'generated'); fixture.detectChanges();
      const editor = fixture.componentInstance as any;
      editor.formStart.set(0); editor.formEnd.set(1); editor.formDescription.set('frozen note');
      const edit = () => { editor.onEdit({ target: { value: 'new ab', selectionStart: 6 } }); fixture.detectChanges(); };
      return { fixture, editor, edit, proposal: fixture.debugElement.query(By.directive(ProposalPanel)).componentInstance as any };
    };
    return { ...create(), create, api, annotation, revision, acceptance, access, payload };
  }

  for (const status of [0, 500]) {
    it(`retains an unknown annotation after status${status} and refuses repeated dispatch`, () => {
      const s = setup(); s.editor.addAnnotation(); s.annotation.error({ status });
      s.editor.addAnnotation();
      expect(s.api.addAnnotation).toHaveBeenCalledTimes(1);
      expect(s.editor.hasNavigationWork()).toBeTrue();
      expect(s.editor.formDescription()).toBe('frozen note');
    });
  }
  it('refuses orientation and retraction while an L1 write is unresolved', () => {
    const s = setup(); s.edit(); s.editor.save();
    s.editor.applyOrientation(); s.editor.retractAnnotation({ id: 'ann-existing' });
    expect(s.api.setLineOrientation).not.toHaveBeenCalled(); expect(s.api.retractAnnotation).not.toHaveBeenCalled();
  });
  it('does not submit L1 while a manual annotation is in flight', () => {
    const s = setup(); s.editor.addAnnotation(); s.edit(); s.editor.save();
    expect(s.api.appendRevision).not.toHaveBeenCalled();
    expect(s.editor.text()).toBe('new ab');
  });
  it('keeps a known L2 partial commit locked after link422 without creating another revision', () => {
    const s = setup(); s.editor.formKind.set('Abbreviation'); s.editor.formExpansion.set('EXP');
    s.editor.addAnnotation(); s.revision.next({ revisionId: 'rev-known-partial' });
    expect(s.api.addAnnotation.calls.mostRecent().args[1].expansionRevisionId).toBe('rev-known-partial');
    s.annotation.error({ status: 422 }); s.editor.addAnnotation();
    expect(s.api.appendRevision).toHaveBeenCalledTimes(1);
    expect(s.editor.hasNavigationWork()).toBeTrue(); expect(s.editor.formExpansion()).toBe('EXP');
  });
  it('blocks L1 dispatch while child proposal acceptance is unresolved', () => {
    const s = setup(); s.proposal.propose(); s.fixture.detectChanges(); s.proposal.accept();
    expect(s.api.acceptProposal).toHaveBeenCalledTimes(1);
    s.editor.onEdit({ target: { value: 'new ab', selectionStart: 6 } }); s.editor.save(); expect(s.api.appendRevision).not.toHaveBeenCalled();
  });
  it('blocks proposal acceptance while a parent L1 write is unresolved', () => {
    const s = setup(); s.proposal.propose(); s.fixture.detectChanges(); s.edit(); s.editor.save();
    s.proposal.accept(); expect(s.api.acceptProposal).not.toHaveBeenCalled();
  });
  it('retains the original mounted payload when inputs change during a manual request', () => {
    const s = setup(); s.editor.addAnnotation();
    s.fixture.componentRef.setInput('lineId', 'ln-different'); s.fixture.detectChanges();
    expect(s.editor.hasNavigationWork()).toBeTrue(); expect(s.editor.payload()?.line).toBe('ln-manual-outcome');
    expect(s.api.line).toHaveBeenCalledTimes(1);
  });
  it('keeps a recovered unknown workflow blocked instead of repeating the annotation', () => {
    const s = setup(); s.editor.addAnnotation(); s.annotation.error({ status: 500 }); s.fixture.destroy();
    const again = s.create(); again.editor.addAnnotation();
    expect(s.api.addAnnotation).toHaveBeenCalledTimes(1); expect(again.editor.hasNavigationWork()).toBeTrue();
    expect(readOwnedDraft('ln-manual-outcome')?.unconfirmedWrites.length).toBeGreaterThan(0);
    const reads = s.api.line.calls.count();
    again.fixture.componentRef.setInput('lineId', 'ln-different'); again.fixture.detectChanges();
    expect(again.editor.hasNavigationWork()).toBeTrue(); expect(again.editor.payload()?.line).toBe('ln-manual-outcome');
    expect(s.api.line.calls.count()).toBe(reads);
  });
  it('releases an ordinary acknowledged single annotation without changing L1 text', () => {
    const s = setup(); s.editor.addAnnotation(); s.annotation.next({ annotationId: 'ann-known' });
    expect(s.editor.hasNavigationWork()).toBeFalse(); expect(s.editor.text()).toBe('ab');
    expect(s.editor.formDescription()).toBe(''); expect(s.api.addAnnotation).toHaveBeenCalledTimes(1);
  });
  it('persists the exact partial workflow and known L2 ID without treating L1 as L2 ancestry', () => {
    const s = setup(); s.editor.formKind.set('Abbreviation'); s.editor.formExpansion.set('EXP');
    s.editor.addAnnotation();
    const beforeAck = readOwnedDraft('ln-manual-outcome')!.unconfirmedWrites[0].data as any;
    expect(beforeAck.followup.expansionRevisionId).toBeNull(); expect(beforeAck.followup.expansionText).toBe('EXP');
    expect(beforeAck.followup.description).toBe('frozen note');
    s.revision.next({ revisionId: 'rev-known-partial' });
    s.editor.formExpansion.set('NEWER'); s.annotation.error({ status: 422 });
    const snapshot = readOwnedDraft('ln-manual-outcome')!;
    expect(snapshot.base).toEqual({ text: 'ab', revisionId: 'rev-manual-base' });
    const packet = snapshot.unconfirmedWrites.find(record => record.kind === 'line-editor-manual-workflow')!.data as any;
    expect(packet.targetLayer).toBe('L2Editorial'); expect(packet.phase).toBe('partial');
    expect(packet.knownRevisionId).toBe('rev-known-partial');
    expect(packet.linkAttempted).toBeTrue(); expect(packet.linkAcknowledged).toBeFalse(); expect(packet.editorialBase).toEqual({ state: 'known-empty' });
    expect(packet.request).toEqual({ text: 'EXPb', authorId: 'generated', parentRevisionId: null, merge: true });
    expect(packet.followup.expansionText).toBe('EXP'); expect(s.editor.formExpansion()).toBe('NEWER');
  });
  it('persists an interrupted packet only under its original scope and never serializes credentials', () => {
    const s = setup(); s.editor.addAnnotation(); s.access.setActive(false);
    s.fixture.componentRef.setInput('lineId', 'ln-different'); s.fixture.detectChanges();
    s.annotation.next({ annotationId: 'ann-late' }); s.fixture.destroy();
    const original = readOwnedDraft('ln-manual-outcome')!;
    expect(original.unconfirmedWrites.length).toBe(1); expect(original.scope.lineId).toBe('ln-manual-outcome');
    expect(readOwnedDraft('ln-different')).toBeNull();
    const raw = JSON.stringify(original); expect(raw).not.toContain('generated-manual-credential');
    expect((original.unconfirmedWrites[0].data as any).request.description).toBe('frozen note');
  });
  it('retains a mounted unknown operation and warns when persistence fails instead of claiming recovery', () => {
    const s = setup(); const previous = readOwnedDraft('ln-manual-outcome')!;
    spyOn(Storage.prototype, 'setItem').and.throwError('generated storage failure');
    s.editor.addAnnotation(); s.annotation.error({ status: 500 }); s.editor.addAnnotation();
    expect(s.api.addAnnotation).toHaveBeenCalledTimes(1); expect(s.editor.hasNavigationWork()).toBeTrue();
    expect(s.editor.draftPersistenceStatus()).toContain('retained only in this editor');
    expect(readOwnedDraft('ln-manual-outcome')).toEqual(previous);
  });
  it('releases a documented single-leg422 refusal while keeping its form', () => {
    const s = setup(); s.editor.addAnnotation(); s.annotation.error({ status: 422 });
    expect(s.editor.hasNavigationWork()).toBeFalse(); expect(s.editor.formDescription()).toBe('frozen note');
    expect(readOwnedDraft('ln-manual-outcome')?.unconfirmedWrites).toEqual([]);
  });
  it('keeps an acknowledged editorial result locked until its fresh layer state is observed', () => {
    const s = setup(); s.editor.formKind.set('Abbreviation'); s.editor.formExpansion.set('EXP'); s.editor.addAnnotation();
    s.revision.next({ revisionId: 'rev-acknowledged-l2' }); s.annotation.next({ annotationId: 'ann-linked' });
    expect(s.editor.hasNavigationWork()).toBeTrue();
    expect(s.api.appendRevision).toHaveBeenCalledTimes(1);
    s.api.line.and.returnValue(of({ ...s.payload, revisions: [...(s.payload.revisions ?? []),
      { id: 'rev-acknowledged-l2', text: 'EXPb', layer: 'L2Editorial', author: 'generated', at: '' }] }));
    s.access.setActive(false); s.access.setActive(true);
    expect(s.editor.hasNavigationWork()).toBeFalse(); expect(s.editor.text()).toBe('ab');
    expect(s.api.appendRevision).toHaveBeenCalledTimes(1); expect(s.api.addAnnotation).toHaveBeenCalledTimes(1);
  });

  it('never lets a duplicate older acknowledgement clear a newer operation form or lock', () => {
    const s = setup(); s.editor.addAnnotation(); s.annotation.next({ annotationId: 'ann-first' });
    const second = new Subject<any>(); s.api.addAnnotation.and.returnValue(second);
    s.editor.formDescription.set('second frozen note'); s.editor.addAnnotation();
    s.editor.formDescription.set('newer than second');
    s.annotation.next({ annotationId: 'ann-duplicate-old' });
    expect(s.editor.formDescription()).toBe('newer than second'); expect(s.editor.hasNavigationWork()).toBeTrue();
    const packet = readOwnedDraft('ln-manual-outcome')!.unconfirmedWrites[0].data as any;
    expect(packet.request.description).toBe('second frozen note'); expect(s.api.addAnnotation).toHaveBeenCalledTimes(2);
  });
  it('retains both acknowledged IDs after an editorial metadata refresh fails without replay', () => {
    const s = setup(); const fresh = new Subject<any>();
    s.editor.formKind.set('Abbreviation'); s.editor.formExpansion.set('EXP'); s.editor.addAnnotation();
    s.revision.next({ revisionId: 'rev-acknowledged-l2' }); s.api.line.and.returnValue(fresh);
    s.annotation.next({ annotationId: 'ann-acknowledged-link' }); fresh.error({ status: 500 });
    s.editor.addAnnotation(); s.editor.reloadAfterConflict();
    expect(s.editor.hasNavigationWork()).toBeTrue(); expect(s.api.appendRevision).toHaveBeenCalledTimes(1);
    expect(s.api.addAnnotation).toHaveBeenCalledTimes(1); expect(s.api.line).toHaveBeenCalledTimes(2);
    const packet = readOwnedDraft('ln-manual-outcome')!.unconfirmedWrites[0].data as any;
    expect(packet.phase).toBe('acknowledged'); expect(packet.knownRevisionId).toBe('rev-acknowledged-l2');
    expect(packet.knownResultId).toBe('ann-acknowledged-link');
    expect(packet.linkAttempted).toBeTrue(); expect(packet.linkAcknowledged).toBeTrue();
  });
  it('refuses an editorial refresh for a different line even when a matching ID is present', () => {
    const s = setup(); s.editor.formKind.set('Abbreviation'); s.editor.formExpansion.set('EXP'); s.editor.addAnnotation();
    s.revision.next({ revisionId: 'rev-acknowledged-l2' });
    s.api.line.and.returnValue(of({ ...s.payload, line: 'ln-wrong', revisions: [{ id: 'rev-acknowledged-l2', text: 'WRONG', layer: 'L2Editorial' }] }));
    s.annotation.next({ annotationId: 'ann-acknowledged-link' });
    expect(s.editor.hasNavigationWork()).toBeTrue(); expect(s.editor.payload().line).toBe('ln-manual-outcome');
    expect(s.editor.text()).toBe('ab');
  });

  for (const operation of ['alternative', 'expansion']) {
    it(`preserves unrelated current L2 text when a manual ${operation} would replace it`, () => {
      const s = setup('aXb');
      if (operation === 'alternative') s.editor.acceptAlternative({ id: 'ann-existing', kind: 'AlternativeReading',
        anchoredRevisionId: 'rev-manual-base', range: { start: 0, end: 1 }, alternativeText: 'Y' });
      else { s.editor.formKind.set('Abbreviation'); s.editor.formExpansion.set('Y'); s.editor.addAnnotation(); }
      expect(s.api.appendRevision).withContext('shortcut must not drop X; actual generated requests: ' + JSON.stringify(s.api.appendRevision.calls.allArgs())).not.toHaveBeenCalled();
      expect(s.editor.manualNotice()).toContain('aXb'); expect(s.editor.text()).toBe('ab');
    });
  }

  for (const operation of ['alternative', 'expansion']) {
    it(`permits a manual ${operation} when known current L2 equals L1`, () => {
      const s = setup('ab');
      if (operation === 'alternative') s.editor.acceptAlternative({ id: 'ann-existing', kind: 'AlternativeReading',
        anchoredRevisionId: 'rev-manual-base', range: { start: 0, end: 1 }, alternativeText: 'Y' });
      else { s.editor.formKind.set('Abbreviation'); s.editor.formExpansion.set('Y'); s.editor.addAnnotation(); }
      expect(s.api.appendRevision).toHaveBeenCalledTimes(1); expect(s.api.appendRevision.calls.mostRecent().args[2]).toBe('Yb');
      expect(s.api.appendRevision.calls.mostRecent().args[4]).toBe('rev-current-editorial');
      expect(s.api.appendRevision.calls.mostRecent().args[5]).toBeFalse();
    });
    it(`refuses a manual ${operation} with unknown editorial history until a fresh read proves it`, () => {
      const s = setup(undefined, false);
      if (operation === 'alternative') s.editor.acceptAlternative({ id: 'ann-existing', kind: 'AlternativeReading',
        anchoredRevisionId: 'rev-manual-base', range: { start: 0, end: 1 }, alternativeText: 'Y' });
      else { s.editor.formKind.set('Abbreviation'); s.editor.formExpansion.set('Y'); s.editor.addAnnotation(); }
      expect(s.api.appendRevision).not.toHaveBeenCalled(); expect(s.editor.manualNotice()).toContain('unavailable');
      s.api.line.and.returnValue(of({ ...s.payload, revisions: [] })); s.editor.reloadEditorialHistory();
      expect(s.api.appendRevision).not.toHaveBeenCalled(); expect(s.editor.editorialHistoryUnavailable()).toBeFalse();
    });
  }
  it('distinguishes known L2 before link attempt from an attempted unknown link across a boundary', () => {
    const s = setup(); const originalSet = Storage.prototype.setItem; let fenced = false;
    spyOn(Storage.prototype, 'setItem').and.callFake(function(this: Storage, key, value) {
      originalSet.call(this, key, value);
      if (!fenced && value.includes('"knownRevisionId":"rev-before-link"') && value.includes('"linkAttempted":false')) {
        fenced = true; s.access.setActive(false);
      }
    });
    s.editor.formKind.set('Abbreviation'); s.editor.formExpansion.set('EXP'); s.editor.addAnnotation();
    s.revision.next({ revisionId: 'rev-before-link' });
    expect(fenced).toBeTrue(); expect(s.api.addAnnotation).not.toHaveBeenCalled();
    const packet = readOwnedDraft('ln-manual-outcome')!.unconfirmedWrites[0].data as any;
    expect(packet.knownRevisionId).toBe('rev-before-link'); expect(packet.linkAttempted).toBeFalse();
    expect(packet.linkAcknowledged).toBeFalse(); expect(s.editor.hasNavigationWork()).toBeTrue();
  });

  for (const operation of ['alternative', 'expansion']) {
    it(`uses an atomic expected-empty precondition for the first manual ${operation}`, () => {
      const s = setup();
      if (operation === 'alternative') s.editor.acceptAlternative({ id: 'ann-existing', kind: 'AlternativeReading',
        anchoredRevisionId: 'rev-manual-base', range: { start: 0, end: 1 }, alternativeText: 'Y' });
      else { s.editor.formKind.set('Abbreviation'); s.editor.formExpansion.set('Y'); s.editor.addAnnotation(); }
      expect(s.api.appendRevision).toHaveBeenCalledOnceWith('ln-manual-outcome', 'L2Editorial', 'Yb', 'generated', undefined, true);
      const packet = readOwnedDraft('ln-manual-outcome')!.unconfirmedWrites[0].data as any;
      expect(packet.request.parentRevisionId).toBeNull(); expect(packet.request.merge).toBeTrue();
      s.revision.error({ status: 409 });
      expect(s.api.addAnnotation).not.toHaveBeenCalled(); expect(s.editor.hasNavigationWork()).toBeFalse();
      expect(s.editor.manualNotice()).toContain('refused');
    });
  }

  for (const newer of [false, true]) {
    it(`orientation acknowledgement ${newer ? 'preserves newer local fields' : 'normalizes unchanged submitted fields'}`, () => {
      const s = setup(); const reply = new Subject<any>(); s.api.setLineOrientation.and.returnValue(reply);
      s.editor.direction.set('rtl'); s.editor.script.set('  Grek  '); s.editor.applyOrientation();
      expect(s.api.setLineOrientation).toHaveBeenCalledOnceWith('ln-manual-outcome', 'rtl', 'Grek');
      if (newer) {
        s.editor.onDirection({ target: { value: 'ltr' } }); s.editor.onScript({ target: { value: 'Latn' } });
      }
      reply.next({ direction: 'rtl', script: 'Grek' });
      expect(s.editor.direction()).toBe(newer ? 'ltr' : 'rtl'); expect(s.editor.script()).toBe(newer ? 'Latn' : 'Grek');
      expect(s.editor.hasNavigationWork()).toBeFalse(); expect(s.editor.text()).toBe('ab');
      expect(s.api.setLineOrientation).toHaveBeenCalledTimes(1);
    });
  }

});
