import { provideWorkspaceTestAccess } from './workspace-testing.fixture.spec';
import { authInterceptor } from './auth';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClient } from './api-client';
import { ProposalPanel } from './proposal-panel';

describe('Native AI browser contract', () => {
  it('renders a proposal object and accepts through the native single-proposal route', () => {
    TestBed.configureTestingModule({ imports: [ProposalPanel], providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideWorkspaceTestAccess({ projectId: 'col-native', credential: 'generated-native-test' })] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(ProposalPanel);
    fixture.componentRef.setInput('lineId', 'ln-native'); fixture.componentRef.setInput('revisionId', 'rev-native');
    fixture.componentRef.setInput('rangeStart', 1); fixture.componentRef.setInput('rangeEnd', 3); fixture.detectChanges();
    http.expectOne('/api/v1/providers').flush({ providers: [{ configRef: 'generated', settings: { modelId: 'generated-model' } }] });
    fixture.detectChanges();
    const button = (label: string) => Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(item => item.textContent?.includes(label))!;
    button('Propose').click();
    const request = http.expectOne('/api/v1/proposals');
    expect(request.request.headers.get('X-Api-Key')).toBe('generated-native-test');
    expect(request.request.urlWithParams).not.toContain('api_key');
    expect(request.request.body.targetRevisionId).toBe('rev-native');
    expect(request.request.body.range).toEqual({ start: 1, end: 3 });
    const proposal = {
      id: 'prop-native', request: { ...request.request.body, sourceText: 'βγ' }, proposedText: 'Ω',
      providerId: 'generated', modelId: 'generated-model', promptVersion: 'pilot-v1',
      confidence: { value: 0.5, scale: 'rawModelProbability' }, status: 'proposed', createdAt: '2026-11-05T00:00:00Z',
    };
    request.flush(proposal); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ω');
    button('Accept').click();
    const acceptance = http.expectOne('/api/v1/proposals/prop-native/accept');
    expect(acceptance.request.body.proposal).toEqual(proposal);
    acceptance.flush({ proposal: { ...proposal, status: 'accepted' }, revisionId: 'rev-l2', receipt: { proposalId: 'prop-native' }, alreadyAccepted: false });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('accepted as L2 editorial revision');
    http.verify();
  });

  it('passes optional explicit project disambiguation without changing the proposal graph', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController); const api = TestBed.inject(ApiClient);
    const request = { targetRevisionId: 'rev-shared', range: { start: 0, end: 1 }, task: 'candidateReading', providerConfigRef: 'generated', promptVersion: 'v1' };
    api.propose(request, 'col-selected').subscribe();
    const proposed = http.expectOne('/api/v1/proposals?projectId=col-selected');
    expect(proposed.request.body).toEqual(request); proposed.flush({ id: 'prop-issued' });
    api.acceptProposal('prop-issued', { id: 'prop-issued' }, 'local-editor', 'col-selected').subscribe();
    const accepted = http.expectOne('/api/v1/proposals/prop-issued/accept');
    expect(accepted.request.body).toEqual({ proposal: { id: 'prop-issued' }, acceptorId: 'local-editor', projectId: 'col-selected' });
    accepted.flush({}); http.verify();
  });
});
