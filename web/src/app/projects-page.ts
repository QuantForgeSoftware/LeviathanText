import { Component, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ApiClient, ProjectPayload } from './api-client';
import { SurfaceView } from './surface-view';

interface ProjectRow {
  id: string;
  name: string;
}

/**
 * Workspace landing page: collection list (todo §4 web workspace). Operates against
 * the versioned REST contract; when the API is offline the page degrades gracefully.
 */
@Component({
  selector: 'app-projects-page',
  standalone: true,
  template: `
    <section>
      <h2>Collections</h2>
      <button type="button" (click)="create()">New collection</button>
      <p>New id: {{ createdId() }}</p>
      <ul>
        @for (project of projects(); track project.id) {
          <li>
            <button type="button" class="link" (click)="select(project.id)">{{ project.name }}</button>
            <code>{{ project.id }}</code>
          </li>
        } @empty {
          <li>No collections yet (API offline or empty).</li>
        }
      </ul>
    </section>

    @if (selected(); as project) {
      @if (payload(selectedId()!); as payload) {
        <section>
          <h2>{{ payload.name }}</h2>
          <ul>
            @for (object of payload.objects; track object.id) {
              @for (surface of object.surfaces; track surface.id) {
                <li>
                  <button type="button" class="link" (click)="openSurface(surface.id)">
                    surface {{ surface.id }} ({{ surface.kind }}, {{ surface.lines.length }} lines)
                  </button>
                </li>
              }
            }
          </ul>
        </section>
      }
    }

    @if (openSurfaceId()) {
      <app-surface-view [projectId]="selectedId()!" [surfaceId]="openSurfaceId()!" />
    }
  `,
  styles: [`
    .link { background: none; border: none; color: #334; cursor: pointer; text-decoration: underline; padding: 0; }
  `],
  imports: [SurfaceView],
})
export class ProjectsPage {
  private readonly api: ApiClient;
  protected readonly projects = signal<ProjectRow[]>([]);
  protected readonly createdId = signal('');
  protected readonly selectedId = signal<string | null>(null);
  protected readonly selected = computed(() =>
    this.projects().find(p => p.id === this.selectedId()) ?? null);
  protected readonly payloads = signal<Record<string, ProjectPayload>>({});
  protected readonly openSurfaceId = signal<string | null>(null);

  constructor(http: HttpClient) {
    this.api = new ApiClient(http);
    this.api.listProjects().subscribe({
      next: (rows) => this.projects.set(rows ?? []),
      error: () => this.projects.set([]),
    });
  }

  protected create(): void {
    this.api.createProject('workspace-collection-' + (this.projects().length + 1))
      .subscribe({
        next: (p) => this.createdId.set(p.id),
        error: () => this.createdId.set('API unreachable'),
      });
  }

  protected select(id: string): void {
    this.selectedId.set(id);
    this.openSurfaceId.set(null);
    if (!this.payloads()[id]) {
      this.api.loadProject(id).subscribe(payload =>
        this.payloads.update(map => ({ ...map, [id]: payload })));
    }
  }

  protected openSurface(surfaceId: string): void {
    this.openSurfaceId.set(surfaceId);
  }

  protected payload(id: string): ProjectPayload | undefined {
    return this.payloads()[id];
  }
}
