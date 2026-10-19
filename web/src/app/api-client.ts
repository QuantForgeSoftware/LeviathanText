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

export interface SurfaceSummary {
  id: string;
  kind: string;
  lineCount: number;
}

export interface LinePayload {
  line: string;
  diplomatic: string | null;
  history: RevisionPayload[];
}

export interface RevisionPayload {
  id: string;
  layer: string;
  text: string;
  author: string;
  at: string;
}

export interface AnnotationPayload {
  id: string;
  anchoredRevisionId: string;
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

  loadProject(id: string): Observable<ProjectPayload> {
    return this.http.get<ProjectPayload>(`/api/v1/projects/${id}`);
  }

  diplomaticExport(projectId: string, surfaceId: string): Observable<DiplomaticExport> {
    return this.http.get<DiplomaticExport>(
      `/api/v1/projects/${projectId}/surfaces/${surfaceId}/diplomatic`);
  }

  line(lineId: string): Observable<LinePayload> {
    return this.http.get<LinePayload>(`/api/v1/lines/${lineId}`);
  }

  lineLayerHistory(lineId: string, layer: string): Observable<RevisionPayload[]> {
    return this.http.get<RevisionPayload[]>(`/api/v1/lines/${lineId}/revisions?layer=${layer}`);
  }

  appendRevision(lineId: string, layer: string, text: string, authorId: string, parentRevisionId?: string): Observable<{ revisionId: string }> {
    return this.http.post<{ revisionId: string }>(`/api/v1/lines/${lineId}/revisions`,
      { layer, text, authorId, parentRevisionId: parentRevisionId ?? null });
  }

  addAnnotation(lineId: string, annotation: unknown): Observable<AnnotationPayload> {
    return this.http.post<AnnotationPayload>(`/api/v1/lines/${lineId}/annotations`, annotation);
  }
}

/** Minimal projection of the native project payload needed by the workspace. */
export interface ProjectPayload {
  id: string;
  name: string;
  objects: {
    id: string;
    name?: string;
    surfaces: {
      id: string;
      kind: string;
      lines: { id: string }[];
      captures: { imageLocation?: string }[];
    }[];
  }[];
}
