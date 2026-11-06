/**
 * Remote model registry panel (todo Kraken parity: registry UI). Catalog fetch and pulls are
 * server-side, admin-only and subject to the configured host allow-list/HTTPS policy; the
 * browser never receives registry credentials or arbitrary filesystem paths.
 */
import { Component, signal } from '@angular/core';
import { WorkspaceComponentScope } from './workspace-component-scope';
import { ApiClient, RegistryEntry } from './api-client';

@Component({
  selector: 'app-model-registry-panel',
  standalone: true,
  template: `
    <details data-workspace-protected data-workspace-role="admin" [hidden]="!workspaceAvailable() || !workspaceAdmin()" [attr.inert]="!workspaceAvailable() || !workspaceAdmin() ? '' : null" class="registry">
      <summary>Model registry</summary>
      <p class="hint">
        The server must allow the registry host in
        <code>LeviathanText:Models:RegistryAllowHosts</code>. Pulls write to the server model
        directory after a checksum-verified download.
      </p>
      <div class="tools">
        <input [value]="indexUrl()" (input)="onUrl($event)" placeholder="https://registry.example/index.json" aria-label="Registry index URL" />
        <button type="button" (click)="load()">Load catalog</button>
        <span class="hint" role="status">{{ status() }}</span>
      </div>
      @if (entries().length > 0) {
        <ul>
          @for (entry of entries(); track entry.id) {
            <li>
              <strong>{{ entry.name }}</strong> <code>{{ entry.id }}</code> · {{ entry.task }}
              · {{ entry.sha256 ? 'checksummed' : 'NO CHECKSUM' }}
              <button type="button" (click)="pull(entry)" [disabled]="!entry.sha256">Pull</button>
            </li>
          }
        </ul>
      }
    </details>
  `,
})
export class ModelRegistryPanel {
  protected workspaceAvailable(): boolean { return this.scope.active(); }
  protected workspaceAdmin(): boolean { return this.scope.role('admin'); }
  protected workspaceCanEdit(): boolean { return this.scope.mayEdit(); }
  private get api(): ApiClient { return this.scope.request(this.baseApi); }

  private readonly baseApi: ApiClient;
  private readonly scope: WorkspaceComponentScope;
  protected readonly indexUrl = signal('');
  protected readonly entries = signal<RegistryEntry[]>([]);
  protected readonly status = signal('');

  constructor(api: ApiClient) { this.baseApi = api; this.scope = new WorkspaceComponentScope(); }

  protected onUrl(event: Event): void {
    if (!this.workspaceAdmin()) { (event.target as HTMLInputElement).value = this.indexUrl(); return; }
    this.indexUrl.set((event.target as HTMLInputElement).value);
  }

  protected load(): void {
    if (!this.workspaceAdmin()) return;
    const url = this.indexUrl().trim();
    if (!url) return;
    this.api.registry(url).subscribe({
      next: entries => {
        this.entries.set(entries);
        this.status.set(`${entries.length} model(s)`);
      },
      error: () => {
        this.entries.set([]);
        this.status.set('Registry refused or unreachable (check the server allow-list).');
      },
    });
  }

  protected pull(entry: RegistryEntry): void {
    if (!this.workspaceAdmin()) return;
    this.status.set(`pulling ${entry.id}...`);
    this.api.pullRegistry(this.indexUrl().trim(), entry.id).subscribe({
      next: result => this.status.set(`pulled ${result.id} (${result.sha256.slice(0, 12)}...)`),
      error: () => this.status.set('Pull failed (trust policy, checksum or target directory).'),
    });
  }
}
