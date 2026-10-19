import { Component, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ApiClient } from './api-client';

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
          <li><code>{{ project.id }}</code> {{ project.name }}</li>
        } @empty {
          <li>No collections yet (API offline or empty).</li>
        }
      </ul>
    </section>
  `,
})
export class ProjectsPage {
  private readonly api: ApiClient;
  protected readonly projects = signal<ProjectRow[]>([]);
  protected readonly createdId = signal('');

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
}

interface ProjectRow {
  id: string;
  name: string;
}
