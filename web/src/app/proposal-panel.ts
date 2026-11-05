/**
 * AI assistance panel (todo Phase 2: candidate readings with opt-in acceptance).
 * The client names a revision + scalar range; the server resolves the evidence and
 * never trusts client-supplied source text. Accepting creates an L2 editorial revision;
 * L1 diplomatic text is untouched (interchange-and-api.md §2.3).
 */
import { Component, input, signal, SimpleChanges } from '@angular/core';
import { ApiClient } from './api-client';

interface ProviderRow {
  configRef: string;
  settings: {
    modelId: string;
    maxSpendUsd?: number | null;
    maxRequests?: number;
    maxOutputTokens?: number;
  };
}

interface ProposalSource {
  lineId: string;
  revisionId: string;
  range: { start: number; end: number };
}

@Component({
  selector: 'app-proposal-panel',
  standalone: true,
  template: `
    <section>
      <h3>AI assistance <small>(opt-in; L1 stays untouched)</small></h3>

      @if (providers().length === 0) {
        <p class="hint">No providers configured on the server (see README: provider configuration).</p>
      } @else {
        <label>Provider
          <select [value]="providerRef()" (change)="onProvider($event)">
            @for (provider of providers(); track provider.configRef) {
              <option [value]="provider.configRef">{{ provider.configRef }} ({{ provider.settings.modelId }}{{ providerBudget(provider) }})</option>
            }
          </select>
        </label>
        <label>Task
          <select [value]="task()" (change)="onTask($event)">
            @for (option of tasks; track option) {
              <option [value]="option">{{ option }}</option>
            }
          </select>
        </label>
        <button type="button" (click)="propose()" [disabled]="busy()">Propose at current revision</button>
      }

      @if (proposal(); as p) {
        <div class="proposal">
          <p><b>Proposed:</b> <span class="greek">{{ textOf(p) }}</span></p>
          <p class="hint">proposal {{ idOf(p) }} · {{ providerOf(p) }} · {{ statusOf(p) }}</p>
          @if (isAdvisory(p)) {
            <p class="hint">Advisory result only; it cannot replace the witness reading.</p>
            <button type="button" (click)="saveAdvisory(p)" [disabled]="busy()">Save as advisory annotation</button>
          } @else {
            <button type="button" (click)="accept()" [disabled]="busy()">Accept as editorial (L2)</button>
          }
        </div>
      }
      @if (message()) { <p class="hint">{{ message() }}</p> }
    </section>
  `,
  styles: [`
    .greek { font-size: 1.2rem; }
    .hint { color: #667; font-size: 0.85rem; }
    .proposal { border: 1px solid #ccd; padding: 0.6rem; margin-top: 0.6rem; }
  `],
})
export class ProposalPanel {
  lineId = input.required<string>();
  revisionId = input.required<string>();
  rangeStart = input.required<number>();
  rangeEnd = input.required<number>();

  protected readonly tasks = ['candidateReading', 'wordDivision', 'expansion', 'accentuation', 'restoration',
    'translation', 'structuredExtraction', 'lexicalAnnotation', 'morphologicalAnnotation'];
  protected readonly providers = signal<ProviderRow[]>([]);
  protected readonly providerRef = signal('');
  protected readonly task = signal('candidateReading');
  protected readonly proposal = signal<Record<string, unknown> | null>(null);
  protected readonly busy = signal(false);
  protected readonly message = signal('');

  private readonly api: ApiClient;
  private source: ProposalSource | null = null;
  private requestVersion = 0;

  constructor(api: ApiClient) {
    this.api = api;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['lineId'] || changes['revisionId']) {
      this.requestVersion++;
      this.clearProposal();
      this.busy.set(false);
      this.message.set('');
    }
  }

  ngOnInit(): void {
    this.api.providers().subscribe({
      next: (response) => {
        this.providers.set(response.providers ?? []);
        if ((response.providers ?? []).length > 0) this.providerRef.set(response.providers[0].configRef);
      },
      error: () => this.providers.set([]),
    });
  }

  protected providerBudget(provider: ProviderRow): string {
    const parts: string[] = [];
    if (provider.settings.maxSpendUsd != null) parts.push(`$${provider.settings.maxSpendUsd}`);
    if ((provider.settings.maxRequests ?? 0) > 0) parts.push(`${provider.settings.maxRequests} req`);
    return parts.length > 0 ? ` · cap ${parts.join('/')}` : '';
  }

  protected onProvider(event: Event): void {
    this.providerRef.set((event.target as HTMLSelectElement).value);
  }

  protected onTask(event: Event): void {
    this.task.set((event.target as HTMLSelectElement).value);
  }

  protected propose(): void {
    const version = ++this.requestVersion;
    const source: ProposalSource = {
      lineId: this.lineId(), revisionId: this.revisionId(),
      range: { start: this.rangeStart(), end: this.rangeEnd() },
    };
    this.clearProposal();
    this.busy.set(true);
    this.message.set('');
    this.api.propose({
      targetRevisionId: source.revisionId,
      range: source.range,
      task: this.task(),
      providerConfigRef: this.providerRef(),
      promptVersion: 'pilot-v1',
    }).subscribe({
      next: (result) => {
        if (!this.isCurrentRequest(version, source)) return;
        this.busy.set(false);
        const failure = (result as { failure?: { reason?: string } }).failure;
        if (failure) {
          this.message.set(`provider failed: ${failure.reason ?? 'unknown'}`);
          this.proposal.set(null);
          return;
        }
        if (!this.matchesSource(result as Record<string, unknown>, source)) {
          this.message.set('proposal source does not match; request a new proposal');
          return;
        }
        this.source = source;
        this.proposal.set(result as Record<string, unknown>);
      },
      error: () => {
        if (!this.isCurrentRequest(version, source)) return;
        this.busy.set(false);
        this.message.set('proposal request failed (provider error or unknown configuration)');
      },
    });
  }

  protected accept(): void {
    const proposal = this.proposal();
    if (!proposal || !this.canSave(proposal)) return;
    const version = this.requestVersion;
    const source = this.source!;
    this.busy.set(true);
    this.api.acceptProposal(this.idOf(proposal), proposal, 'workspace-editor').subscribe({
      next: () => {
        if (!this.isCurrentRequest(version, source)) return;
        this.busy.set(false);
        this.message.set('accepted as L2 editorial revision');
        this.clearProposal();
      },
      error: () => {
        if (!this.isCurrentRequest(version, source)) return;
        this.busy.set(false);
        this.message.set('acceptance failed (stale target or blocked proposal)');
      },
    });
  }

  protected textOf(proposal: Record<string, unknown>): string {
    return String(proposal['proposedText'] ?? '');
  }

  protected idOf(proposal: Record<string, unknown>): string {
    return String(proposal['id'] ?? '');
  }

  protected providerOf(proposal: Record<string, unknown>): string {
    return `${proposal['providerId'] ?? '?'}/${proposal['modelId'] ?? '?'}`;
  }

  protected isAdvisory(proposal: Record<string, unknown>): boolean {
    const request = proposal['request'] as Record<string, unknown> | undefined;
    const task = String(request?.['task'] ?? '');
    return ['Translation', 'translation', 'StructuredExtraction', 'structuredExtraction',
      'LexicalAnnotation', 'lexicalAnnotation', 'MorphologicalAnnotation',
      'morphologicalAnnotation'].includes(task);
  }

  protected saveAdvisory(proposal: Record<string, unknown>): void {
    const kind = this.advisoryKind(proposal);
    if (!kind || !this.canSave(proposal)) return;
    const source = this.source!;
    const version = this.requestVersion;
    this.busy.set(true);
    this.api.addAnnotation(source.lineId, {
      kind,
      anchoredRevisionId: source.revisionId,
      range: { ...source.range },
      alternativeText: this.textOf(proposal),
      description: `${kind} · ${this.providerOf(proposal)}`,
    }).subscribe({
      next: () => {
        if (!this.isCurrentRequest(version, source)) return;
        this.busy.set(false);
        this.clearProposal();
        this.message.set('advisory result saved as annotation');
      },
      error: (error: { status?: number }) => {
        if (!this.isCurrentRequest(version, source)) return;
        this.busy.set(false);
        if (error.status === 409) {
          this.clearProposal();
          this.message.set('source revision changed; request a new proposal');
        } else this.message.set('could not save advisory result');
      },
    });
  }

  private clearProposal(): void {
    this.proposal.set(null);
    this.source = null;
  }

  private isCurrentRequest(version: number, source: ProposalSource): boolean {
    return version === this.requestVersion && source.lineId === this.lineId()
      && source.revisionId === this.revisionId();
  }

  private matchesSource(proposal: Record<string, unknown>, source: ProposalSource): boolean {
    const request = proposal?.['request'] as Record<string, unknown> | undefined;
    const range = request?.['range'] as { start?: number; end?: number } | undefined;
    return request?.['targetRevisionId'] === source.revisionId
      && range?.start === source.range.start && range?.end === source.range.end;
  }

  private canSave(proposal: Record<string, unknown>): boolean {
    if (this.busy()) return false;
    if (proposal === this.proposal() && this.source !== null
      && this.isCurrentRequest(this.requestVersion, this.source)
      && this.matchesSource(proposal, this.source)) return true;
    this.clearProposal();
    this.message.set('source revision changed; request a new proposal');
    return false;
  }

  private advisoryKind(proposal: Record<string, unknown>): string | null {
    const request = proposal['request'] as Record<string, unknown> | undefined;
    const task = String(request?.['task'] ?? '');
    switch (task) {
      case 'Translation': case 'translation': return 'Translation';
      case 'StructuredExtraction': case 'structuredExtraction': return 'StructuredExtraction';
      case 'LexicalAnnotation': case 'lexicalAnnotation': return 'LexicalAnnotation';
      case 'MorphologicalAnnotation': case 'morphologicalAnnotation': return 'MorphologicalAnnotation';
      default: return null;
    }
  }

  protected statusOf(proposal: Record<string, unknown>): string {
    return String(proposal['status'] ?? '');
  }
}
