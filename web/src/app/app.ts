import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ProjectsPage } from './projects-page';
import { RenderingCheck } from './rendering-check';
import { JobsPanel } from './jobs-panel';
import { AuthPanel } from './auth-panel';
import { OfflineBanner } from './offline-banner';
import { RunsPanel } from './runs-panel';
import { UsersPanel } from './users-panel';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ProjectsPage, RenderingCheck, JobsPanel, AuthPanel, OfflineBanner, RunsPanel, UsersPanel],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly title = signal('LeviathanText');
}
