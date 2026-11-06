import { signal, reflectComponentType } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiClient, LinePayload } from './api-client';
import { LineEditor } from './line-editor';
import { ProposalPanel } from './proposal-panel';
import { WorkspaceDraftStore } from './workspace-draft-store';
import { WORKSPACE_ACCESS, WorkspaceAccess, WorkspaceAccessState, WorkspaceOwner, WorkspaceRequestIntent } from './workspace-access-types';

class ControlledAccess implements WorkspaceAccess {
  private readonly value = signal<WorkspaceAccessState>({ status: 'active', generation: 0, reason: 'generated fresh confirmation',
    principal: { mode: 'account', accountId: 'usr-generated-a', workspaceNamespace: '9da6ed1b-167c-4ecf-982b-39e45dd0629d', role: 'Annotator' } });
  private readonly listeners = new Set<(state: WorkspaceAccessState) => void>();
  private scopeDrift = false;
  driftWithoutNotification() { this.scopeDrift = true; }
  state() { return this.value(); }
  isActive() {
    if (this.scopeDrift) { this.scopeDrift = false; this.transition('suspended'); }
    return this.state().status === 'active';
  }
  owner(): WorkspaceOwner | null { return this.isActive() ? { accountId: 'usr-generated-a', workspaceNamespace: '9da6ed1b-167c-4ecf-982b-39e45dd0629d' } : null; }
  projectId() { return this.isActive() ? 'generated-project' : null; }
  capture(): WorkspaceRequestIntent | null {
    if (!this.isActive()) return null;
    const generation = this.state().generation;
    return { credential: 'generated-key', projectId: 'generated-project', generation,
      current: () => this.isActive() && generation === this.state().generation };
  }
  subscribe(listener: (state: WorkspaceAccessState) => void) { this.listeners.add(listener); listener(this.state()); return () => this.listeners.delete(listener); }
  setRole(role: string) {
    this.value.update(previous => ({ ...previous, generation: previous.generation + 1,
      principal: previous.principal ? { ...previous.principal, role } : null }));
    for (const listener of this.listeners) listener(this.state());
  }
  retry() { this.transition('active'); }
  transition(status: WorkspaceAccessState['status']) {
    this.value.update(previous => ({ ...previous, status, generation: previous.generation + 1 }));
    for (const listener of this.listeners) listener(this.state());
  }
}
const saved = (text = 'BASE'): LinePayload => ({ line: 'generated-line', diplomatic: text, history: [{ id: 'generated-r1', text, author: 'generated', at: '2026-11-06T00:00:00Z' }] });

function setupLine(withAccess = true, initialText = 'BASE') {
  const access = new ControlledAccess(); const append = new Subject<{ revisionId: string }>(); const fresh = new Subject<LinePayload>();
  const api: Record<string, any> = {
    line: jasmine.createSpy('line').and.returnValue(of(saved(initialText))), lineFresh: jasmine.createSpy('lineFresh').and.callFake(() => {
      const initial = !(api['lineFresh'] as jasmine.Spy).calls.count() || (api['lineFresh'] as jasmine.Spy).calls.count() === 1;
      return initial ? of(saved(initialText)) : fresh;
    }),
    annotations: jasmine.createSpy('annotations').and.returnValue(of([])), providers: () => of({ providers: [] }),
    appendRevision: jasmine.createSpy('appendRevision').and.returnValue(append), setLineOrientation: jasmine.createSpy('orientation').and.returnValue(of({})),
    recordReview: jasmine.createSpy('recordReview').and.returnValue(of({})),
    addAnnotation: jasmine.createSpy('addAnnotation').and.returnValue(of({})), lineCropUrl: () => '/generated-crop',
  };
  api['forWorkspaceRequest'] = jasmine.createSpy('forWorkspaceRequest').and.returnValue(api);
  TestBed.configureTestingModule({ imports: [LineEditor], providers: [{ provide: ApiClient, useValue: api },
    ...(withAccess ? [{ provide: WORKSPACE_ACCESS, useValue: access }] : [])] });
  const fixture = TestBed.createComponent(LineEditor);
  fixture.componentRef.setInput('lineId', 'generated-line');
  if (reflectComponentType(LineEditor)?.inputs.some(input => input.propName === 'surfaceId'))
    fixture.componentRef.setInput('surfaceId', 'generated-surface');
  fixture.componentRef.setInput('authorId', 'generated-author'); fixture.detectChanges();
  const component = fixture.componentInstance as any;
  function edit(text: string) { component.onEdit({ target: { value: text, selectionStart: text.length } }); fixture.detectChanges(); }
  return { fixture, component, api, access, append, fresh, edit };
}

describe('mounted editor workspace boundary', () => {
  beforeEach(() => localStorage.clear()); afterEach(() => localStorage.clear());
  it('keeps ordinary authorized loading, editing, undo and saving functional', () => {
    const { component, api, edit } = setupLine(); expect(component.text()).toBe('BASE');
    edit('AUTHORIZED EDIT'); component.undo(); expect(component.text()).toBe('BASE');
    edit('AUTHORIZED SAVE'); component.save();
    expect(api['appendRevision']).toHaveBeenCalledOnceWith('generated-line', 'L1Diplomatic', 'AUTHORIZED SAVE',
      'generated-author', 'generated-r1', true);
  });
  it('keeps a current ordinary conflict visible without losing the local draft', () => {
    const { component, append, edit } = setupLine(); edit('AUTHORIZED EDIT'); component.save();
    append.error({ status: 409 });
    expect(component.text()).toBe('AUTHORIZED EDIT'); expect(component.savingRevision()).toBeFalse();
    expect(component.conflict()).toBeTrue(); expect(component.draftBaseText).toBe('BASE');
  });
  it('keeps a freshly read-only principal from dispatching writes or changing its retained draft', () => {
    const { component, api, access, edit } = setupLine(); edit('RETAINED WRITER DRAFT'); access.setRole('Reader');
    component.save(); component.applyOrientation(); component.markLost();
    component.onEdit({ target: { value: 'WRONG', selectionStart: 0 } });
    expect(component.text()).toBe('RETAINED WRITER DRAFT');
    expect(api['appendRevision']).not.toHaveBeenCalled(); expect(api['setLineOrientation']).not.toHaveBeenCalled();
    expect(api['addAnnotation']).not.toHaveBeenCalled();
  });
  it('does not submit reviewer-only decisions from an Annotator principal', () => {
    const { component, api } = setupLine(); component.recordReview('accept');
    expect(api['recordReview']).not.toHaveBeenCalled();
  });
  it('fails closed when the workspace provider is missing', () => {
    const { api, fixture } = setupLine(false);
    expect(api['line']).not.toHaveBeenCalled(); expect(api['lineFresh']).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('input')).toBeNull();
  });
  it('conceals protected presentation while keeping the exact mounted dirty input and undo state', () => {
    const { fixture, component, access, edit } = setupLine(); edit('DIRTY SENTINEL');
    const input = fixture.nativeElement.querySelector('input');
    access.transition('suspended'); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input')).toBe(input);
    expect(input.closest('[hidden]')).not.toBeNull();
    expect(component.text()).toBe('DIRTY SENTINEL'); expect(component.undoStack().at(-1).text).toBe('BASE');
  });
  it('blocks direct actions, keyboard dispatch and text mutation synchronously while suspended', () => {
    const { component, api, access, edit } = setupLine(); edit('DIRTY SENTINEL'); access.transition('suspended');
    component.undo(); component.onEdit({ target: { value: 'WRONG', selectionStart: 0 } }); component.save();
    component.applyOrientation(); component.markLost(); component.onKeydown({ ctrlKey: true, key: 's', preventDefault() {} });
    expect(component.text()).toBe('DIRTY SENTINEL'); expect(api['appendRevision']).not.toHaveBeenCalled();
    expect(api['setLineOrientation']).not.toHaveBeenCalled(); expect(api['addAnnotation']).not.toHaveBeenCalled();
  });
  it('checks access synchronously before local edits even when storage drift has not sent a signal or event', () => {
    const { component, fixture, access, edit } = setupLine(); edit('DIRTY SENTINEL');
    fixture.detectChanges(); access.driftWithoutNotification();
    component.onEdit({ target: { value: 'WRONG OWNER EDIT', selectionStart: 0 } });
    expect(component.text()).toBe('DIRTY SENTINEL'); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input').closest('[hidden]')).not.toBeNull();
  });
  it('restores the mounted input value after rejecting a queued inactive input event', () => {
    const { fixture, access, fresh, edit } = setupLine(); edit('DIRTY SENTINEL');
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    access.transition('suspended'); input.value = 'REJECTED QUEUED VALUE'; input.dispatchEvent(new Event('input'));
    access.transition('active'); fresh.next(saved()); fixture.detectChanges();
    expect(input.value).toBe('DIRTY SENTINEL');
  });
  it('uses a fresh line read on confirmed same-owner resumption and preserves the true base and undo', () => {
    const { component, api, access, fresh, edit } = setupLine(); edit('DIRTY SENTINEL');
    const previousReads = api['lineFresh'].calls.count();
    access.transition('suspended'); access.transition('active');
    expect(api['lineFresh']).toHaveBeenCalledTimes(previousReads + 1); fresh.next(saved());
    expect(component.text()).toBe('DIRTY SENTINEL'); expect(component.draftBaseText).toBe('BASE');
    component.undo(); expect(component.text()).toBe('BASE');
  });
  it('does not adopt a changed saved parent as the base of an unresolved dirty edit', () => {
    const { component, access, fresh, edit } = setupLine(); edit('LOCAL');
    access.transition('suspended'); access.transition('active'); fresh.next(saved('REMOTE'));
    expect(component.text()).toBe('LOCAL'); expect(component.draftBaseText).toBe('BASE'); expect(component.conflict()).toBeTrue();
  });
  it('does not clear a newer draft or issue a refresh from a late old-scope write acknowledgement', () => {
    const { component, api, access, append, edit } = setupLine(); edit('SUBMITTED'); component.save(); edit('NEWER');
    access.transition('suspended'); append.next({ revisionId: 'late-success' });
    expect(component.text()).toBe('NEWER'); expect(component.draftBaseText).toBe('BASE');
    expect(api['line'].calls.count() + api['lineFresh'].calls.count()).toBe(1); expect(component.unconfirmedWrite()).toBeTrue();
  });
  it('does not clear an outstanding write from a late old-scope conflict response', () => {
    const { component, access, append, edit } = setupLine(); edit('SUBMITTED'); component.save();
    access.transition('suspended'); append.error({ status: 409 });
    expect(component.savingRevision()).toBeTrue(); expect(component.text()).toBe('SUBMITTED');
    expect(component.draftBaseText).toBe('BASE');
  });
  it('retains unknown write uncertainty after a fresh read instead of declaring cancellation a rollback', () => {
    const { component, api, access, fresh, edit } = setupLine(); edit('SUBMITTED'); component.save();
    access.transition('suspended'); access.transition('active'); fresh.next(saved()); component.save();
    expect(component.unconfirmedWrite()).toBeTrue(); expect(component.savingRevision()).toBeTrue();
    expect(api['appendRevision']).toHaveBeenCalledTimes(1); expect(component.hasNavigationWork()).toBeTrue();
    expect(component.text()).toBe('SUBMITTED'); expect(component.draftBaseText).toBe('BASE');
  });
  it('does not show an old draft when a different principal is quarantined', () => {
    const { component, fixture, access, edit } = setupLine(); edit('OWNER A ONLY');
    access.transition('quarantined'); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input').closest('[hidden]')).not.toBeNull();
    component.save(); expect(component.text()).toBe('OWNER A ONLY');
  });
  it('never imports, overwrites or deletes ownerless legacy draft bytes', () => {
    localStorage.setItem('leviathan:draft:generated-line', 'OTHER OWNER');
    localStorage.setItem('leviathan:draft:generated-line:recovery', 'EXACT OLD BYTES');
    const { component, fixture, edit } = setupLine(); expect(component.text()).toBe('BASE'); edit('NEW OWNER WORK'); fixture.destroy();
    expect(localStorage.getItem('leviathan:draft:generated-line')).toBe('OTHER OWNER');
    expect(localStorage.getItem('leviathan:draft:generated-line:recovery')).toBe('EXACT OLD BYTES');
  });
  it('stores exact owner/project/surface/line/layer, base and undo in the owned recovery store', () => {
    const { component, access, edit } = setupLine(); edit('NEW OWNER WORK');
    const result = new WorkspaceDraftStore().load({ ...access.owner()!, projectId: 'generated-project', surfaceId: 'generated-surface', lineId: 'generated-line', layer: 'L1Diplomatic' });
    expect(result.status).toBe('found');
    if (result.status !== 'found') return;
    expect(result.snapshot.draftText).toBe('NEW OWNER WORK'); expect(result.snapshot.base.text).toBe('BASE');
    expect(JSON.stringify(result.snapshot.undo?.data)).toBe(JSON.stringify([{ text: 'BASE', revisionId: 'generated-r1' }]));
    expect(component.hasNavigationWork()).toBeTrue();
  });
  it('retains the recorded true base on recovery of an unconfirmed write even when current text equals the submission', () => {
    const scope = { workspaceNamespace: '9da6ed1b-167c-4ecf-982b-39e45dd0629d', accountId: 'usr-generated-a',
      projectId: 'generated-project', surfaceId: 'generated-surface', lineId: 'generated-line', layer: 'L1Diplomatic' };
    const result = new WorkspaceDraftStore().save({ schemaVersion: 1, scope, base: { revisionId: 'original-parent', text: 'BASE' },
      draftText: 'SUBMITTED', frame: null, undo: null, geometry: null, readingOrder: null,
      unconfirmedWrites: [{ version: 1, kind: 'line-editor-write', data: { submitted: 'SUBMITTED', acknowledged: false } }] });
    expect(result.ok).toBeTrue(); const { component, api } = setupLine(true, 'SUBMITTED');
    expect(component.text()).toBe('SUBMITTED'); expect(component.draftBaseText).toBe('BASE');
    expect(component.draftBaseRevisionId).toBe('original-parent'); expect(component.savingRevision()).toBeTrue();
    component.save(); expect(api['appendRevision']).not.toHaveBeenCalled();
  });
  it('keeps the mounted copy and reports recovery failure when persistence cannot write', () => {
    const { component, edit } = setupLine(); spyOn(Storage.prototype, 'setItem').and.throwError('generated quota failure');
    edit('ONLY MOUNTED COPY'); expect(component.text()).toBe('ONLY MOUNTED COPY');
    expect(component.draftPersistenceStatus()).toContain('failed'); expect(component.hasNavigationWork()).toBeTrue();
  });
});

describe('proposal workspace boundary', () => {
  function pendingProposal() {
    const access = new ControlledAccess(), response = new Subject<any>(), accepted = new Subject<any>();
    const api: Record<string, any> = { providers: () => of({ providers: [{ configRef: 'generated', settings: { modelId: 'generated' } }] }),
      propose: jasmine.createSpy('propose').and.returnValue(response),
      acceptProposal: jasmine.createSpy('acceptProposal').and.returnValue(accepted) };
    api['forWorkspaceRequest'] = () => api;
    TestBed.configureTestingModule({ imports: [ProposalPanel], providers: [{ provide: ApiClient, useValue: api }, { provide: WORKSPACE_ACCESS, useValue: access }] });
    const fixture = TestBed.createComponent(ProposalPanel);
    for (const [name, value] of Object.entries({ lineId: 'generated-line', revisionId: 'generated-r1', rangeStart: 0, rangeEnd: 1 })) fixture.componentRef.setInput(name, value);
    fixture.detectChanges(); const component = fixture.componentInstance as any;
    return { access, response, accepted, api, fixture, component };
  }
  it('lets the same confirmed owner request a new proposal after its old nonmutation request was canceled', () => {
    const s = pendingProposal(); s.component.propose();
    s.access.transition('suspended'); s.access.transition('active'); s.component.propose();
    expect(s.api['propose']).toHaveBeenCalledTimes(2);
  });
  it('does not dispatch a proposal after the confirmed principal becomes a Reader', () => {
    const s = pendingProposal(); s.access.setRole('Reader'); s.component.propose();
    expect(s.api['propose']).not.toHaveBeenCalled();
  });
  it('keeps an unconfirmed acceptance blocked after an active network failure', () => {
    const s = pendingProposal(); s.component.propose();
    s.response.next({ id: 'generated-p', proposedText: 'GENERATED', request: { targetRevisionId: 'generated-r1', range: { start: 0, end: 1 } } });
    s.component.accept(); s.accepted.error({ status: 0 }); s.component.accept();
    expect(s.api['acceptProposal']).toHaveBeenCalledTimes(1);
    expect(s.component.busy()).toBeTrue();
  });

  it('fences a late proposal success and prevents acceptance under a suspended owner', () => {
    const access = new ControlledAccess(); const response = new Subject<any>();
    const api: Record<string, any> = { providers: () => of({ providers: [{ configRef: 'generated', settings: { modelId: 'generated' } }] }),
      propose: jasmine.createSpy('propose').and.returnValue(response), acceptProposal: jasmine.createSpy('acceptProposal').and.returnValue(of({})) };
    api['forWorkspaceRequest'] = () => api;
    TestBed.configureTestingModule({ imports: [ProposalPanel], providers: [{ provide: ApiClient, useValue: api }, { provide: WORKSPACE_ACCESS, useValue: access }] });
    const fixture = TestBed.createComponent(ProposalPanel);
    for (const [name, value] of Object.entries({ lineId: 'generated-line', revisionId: 'generated-r1', rangeStart: 0, rangeEnd: 1 })) fixture.componentRef.setInput(name, value);
    fixture.detectChanges(); const component = fixture.componentInstance as any; component.propose();
    access.transition('suspended'); response.next({ id: 'generated-p', proposedText: 'PRIVATE', request: { targetRevisionId: 'generated-r1', range: { start: 0, end: 1 } } });
    component.accept(); expect(component.proposal()).toBeNull(); expect(api['acceptProposal']).not.toHaveBeenCalled();
  });
});
