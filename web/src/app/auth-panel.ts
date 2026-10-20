/** API key entry for shared deployments (local-only mode when empty). */
import { Component, signal } from '@angular/core';
import { apiKey, hasApiKey, setApiKey } from './auth';

@Component({
  selector: 'app-auth-panel',
  standalone: true,
  template: `
    <section>
      <h2>API key</h2>
      @if (hasKey()) {
        <p class="hint">A key is configured (sent as X-Api-Key; SSE uses a query token).</p>
      } @else {
        <p class="hint">No key configured — local-only server.</p>
      }
      <div class="tools">
        <input [value]="draft()" (input)="onInput($event)" placeholder="paste API key" type="password" />
        <button type="button" (click)="save()">Save</button>
        <button type="button" (click)="clear()">Clear</button>
        <span class="hint">{{ status() }}</span>
      </div>
    </section>
  `,
  styles: [`
    .tools { display: flex; gap: 0.5rem; align-items: center; }
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class AuthPanel {
  protected readonly draft = signal('');
  protected readonly hasKey = signal(hasApiKey());
  protected readonly status = signal('');

  protected onInput(event: Event): void {
    this.draft.set((event.target as HTMLInputElement).value);
  }

  protected save(): void {
    setApiKey(this.draft());
    this.draft.set('');
    this.hasKey.set(hasApiKey());
    this.status.set('saved; reload data to use the new credentials');
  }

  protected clear(): void {
    setApiKey('');
    this.hasKey.set(false);
    this.status.set('cleared');
  }
}
