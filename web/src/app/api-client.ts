import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/**
 * Hand-written client for the versioned REST contract (interchange-and-api.md §2).
 * The generated client replaces this during implementation; the shapes here follow the
 * contract JSON conventions (camelCase, string enums).
 */

export interface ProjectSummary {
  id: string;
  name: string;
}

export interface ImportIssue {
  element: string;
  description: string;
  severity: 'Loss' | 'Note';
}

export interface LossEntry {
  category: string;
  count: number;
  description: string;
}

export interface LossReport {
  format: string;
  contractVersion: string;
  entries: LossEntry[];
}

export interface DiplomaticExport {
  text: string;
  lossReport: LossReport;
}

export class ApiClient {
  constructor(private readonly http: HttpClient) {}

  root(): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>('/api/v1/');
  }

  listProjects(): Observable<ProjectSummary[]> {
    return this.http.get<ProjectSummary[]>('/api/v1/projects');
  }

  createProject(name: string): Observable<ProjectSummary> {
    return this.http.post<ProjectSummary>('/api/v1/projects', { name });
  }

  loadProject(id: string): Observable<unknown> {
    return this.http.get<unknown>(`/api/v1/projects/${id}`);
  }

  diplomaticExport(projectId: string, surfaceId: string): Observable<DiplomaticExport> {
    return this.http.get<DiplomaticExport>(
      `/api/v1/projects/${projectId}/surfaces/${surfaceId}/diplomatic`);
  }
}
