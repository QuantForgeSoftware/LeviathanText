/** Read/create fragment groupings for the current surface (surface membership only). */
import { Component, OnChanges, SimpleChanges, input, signal } from '@angular/core';
import { WorkspaceComponentScope } from './workspace-component-scope';
import { ApiClient, NativeFragmentView } from './api-client';

@Component({
  selector: 'app-fragment-panel',
  standalone: true,
  template: `
    <details data-workspace-protected [hidden]="!workspaceAvailable()" [attr.inert]="!workspaceAvailable() ? '' : null" class="fragments">
      <summary>Fragment grouping</summary>
      <p class="hint">A fragment groups this surface with other surfaces of the same object. Boundaries are explicit; no inference.</p>
      <div class="tools">
        <input [value]="tm()" (input)="onTm($event)" placeholder="TM / inventory ref" aria-label="Fragment external ref" />
        <button type="button" (click)="create()" [disabled]="!workspaceCanEdit()">Create fragment here</button>
        <span class="hint" role="status">{{ status() }}</span>
      </div>
      @if (loaded() && fragments().length === 0) {
        <p class="hint">No fragment grouping includes this surface.</p>
      }
      @if (fragments().length > 0) {
        <ul>
          @for (fragment of fragments(); track fragment.id) {
            <li>
              <code>{{ fragment.id }}</code>
              @for (ref of refs(fragment); track $index) { · {{ ref }} }
              · surfaces {{ fragment.surfaceIds.length }}
            </li>
          }
        </ul>
      }
    </details>
  `,
  styles: [`
    .fragments { border: 1px solid #ccd; border-radius: 6px; padding: 0.5rem 0.75rem; margin: 0.75rem 0; }
    .tools { display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center; margin: 0.5rem 0; }
    .hint { color: #667; font-size: 0.85rem; }
  `],
})
export class FragmentPanel implements OnChanges {
  protected workspaceAvailable(): boolean { return this.scope.active(); }
  protected workspaceAdmin(): boolean { return this.scope.role('admin'); }
  protected workspaceCanEdit(): boolean { return this.scope.mayEdit(); }
  private get api(): ApiClient { const id = this.surfaceId(); return this.scope.request(this.baseApi, undefined, () => this.surfaceId() === id); }

  surfaceId = input.required<string>();

  protected readonly fragments = signal<NativeFragmentView[]>([]);
  protected readonly loaded = signal(false);
  protected readonly status = signal('');
  protected readonly tm = signal('');

  private readonly baseApi: ApiClient;
  private readonly scope: WorkspaceComponentScope;

  constructor(api: ApiClient) {
    this.baseApi = api;
    this.scope = new WorkspaceComponentScope(() => { if (this.ready && this.workspaceAvailable()) this.load(); });
  }

  private ready = false;
  ngOnChanges(_changes: SimpleChanges): void {
    this.ready = true;
    this.fragments.set([]);
    this.loaded.set(false);
    this.status.set('');
    this.load();
  }

  protected onTm(event: Event): void {
    if (!this.workspaceCanEdit()) { (event.target as HTMLInputElement).value = this.tm(); return; }
    this.tm.set((event.target as HTMLInputElement).value);
  }

  protected refs(fragment: NativeFragmentView): string[] {
    return Object.entries(fragment.externalRefs ?? {}).map(([key, value]) => `${key}=${value}`);
  }

  protected create(): void {
    if (!this.workspaceCanEdit()) return;
    const value = this.tm().trim();
    const refs: Record<string, string> = value ? { TM: value } : {};
    this.api.createSurfaceFragment(this.surfaceId(), refs).subscribe({
      next: () => { this.status.set('fragment created'); this.tm.set(''); this.load(); },
      error: () => this.status.set('fragment creation failed'),
    });
  }

  private load(): void {
    if (!this.workspaceAvailable()) return;
    this.api.surfaceFragments(this.surfaceId()).subscribe({
      next: fragments => { this.fragments.set(fragments); this.loaded.set(true); },
      error: () => { this.status.set('fragment list unavailable'); this.loaded.set(true); },
    });
  }
}
