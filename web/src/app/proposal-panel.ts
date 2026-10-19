/**
 * AI assistance panel (todo Phase 2: candidate readings with opt-in acceptance).
 * The client names a revision + scalar range; the server resolves the evidence and
 * never trusts client-supplied source text. Accepting creates an L2 editorial revision;
 * L1 diplomatic text is untouched (interchange-and-api.md §2.3).
 */
import { Component, input, signal } from '@angular/core';
import { ApiClient } from './api-client';

interface ProviderRow {
  configRef: string;
  settings: { modelId: string };
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
              <option [value]="provider.configRef">{{ provider.configRef }} ({{ provider.settings.modelId }})</option>
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
          <button type="button" (click)="accept()" [disabled]="busy()">Accept as editorial (L2)</button>
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

  protected readonly tasks = ['candidateReading', 'wordDivision', 'expansion', 'accentuation', 'restoration'];
  protected readonly providers = signal<ProviderRow[]>([]);
  protected readonly providerRef = signal('');
  protected readonly task = signal('candidateReading');
  protected readonly proposal = signal<Record<string, unknown> | null>(null);
  protected readonly busy = signal(false);
  protected readonly message = signal('');

  private readonly api: ApiClient;

  constructor(api: ApiClient) {
    this.api = api;
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

  protected onProvider(event: Event): void {
    this.providerRef.set((event.target as HTMLSelectElement).value);
  }

  protected onTask(event: Event): void {
    this.task.set((event.target as HTMLSelectElement).value);
  }

  protected propose(): void {
    this.busy.set(true);
    this.message.set('');
    this.api.propose({
      targetRevisionId: this.revisionId(),
      range: { start: this.rangeStart(), end: this.rangeEnd() },
      task: this.task(),
      providerConfigRef: this.providerRef(),
      promptVersion: 'pilot-v1',
    }).subscribe({
      next: (result) => {
        this.busy.set(false);
        const failure = (result as { failure?: { reason?: string } }).failure;
        if (failure) {
          this.message.set(`provider failed: ${failure.reason ?? 'unknown'}`);
          this.proposal.set(null);
          return;
        }
        this.proposal.set(result as Record<string, unknown>);
      },
      error: () => {
        this.busy.set(false);
        this.message.set('proposal request failed (provider error or unknown configuration)');
      },
    });
  }

  protected accept(): void {
    const proposal = this.proposal();
    if (!proposal) return;
    this.busy.set(true);
    this.api.acceptProposal(this.idOf(proposal), proposal, 'workspace-editor').subscribe({
      next: () => {
        this.busy.set(false);
        this.message.set('accepted as L2 editorial revision');
        this.proposal.set(null);
      },
      error: () => {
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

  protected statusOf(proposal: Record<string, unknown>): string {
    return String(proposal['status'] ?? '');
  }
}
