import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
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

export interface GeometryPoint {
  x: number;
  y: number;
}

export interface GeometryLine {
  id: string;
  regionId: string;
  polygon: GeometryPoint[];
  baseline: GeometryPoint[];
}

export interface GeometryRegion {
  id: string;
  readingOrderIndex: number | null;
}

export interface RevisionPayload {
  id: string;
  layer: string;
  text: string;
  author: string;
  at: string;
}

export interface JobSummary {
  id: string;
  type: string;
  status: string;
  retriesUsed: number;
  idempotencyKey?: string;
}

export interface AnnotationPayload {
  id: string;
  anchoredRevisionId: string;
}

export interface AnnotationView {
  id: string;
  kind: string;
  anchoredRevisionId: string;
  range: { start: number; end: number };
  gapExtent?: string;
  alternativeText?: string;
  handLabel?: string;
  description?: string;
}

@Injectable({ providedIn: 'root' })
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

  createJob(type: string, ownerId: string, idempotencyKey?: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/v1/jobs', { type, ownerId, idempotencyKey: idempotencyKey ?? null });
  }

  jobEventsUrl(jobId: string): string {
    return `/api/v1/jobs/${jobId}/events`;
  }

  jobs(): Observable<JobSummary[]> {
    return this.http.get<JobSummary[]>('/api/v1/jobs');
  }

  updateLineGeometry(lineId: string, polygon: GeometryPoint[], baseline: GeometryPoint[]): Observable<unknown> {
    return this.http.put(`/api/v1/lines/${lineId}/geometry`, { polygon, baseline });
  }

  updateReadingOrder(surfaceId: string, regionIds: string[]): Observable<unknown> {
    return this.http.put(`/api/v1/surfaces/${surfaceId}/reading-order`, { regionIds });
  }

  updateLineOrder(regionId: string, lineIds: string[]): Observable<unknown> {
    return this.http.put(`/api/v1/regions/${regionId}/line-order`, { lineIds });
  }

  detectLayout(surfaceId: string): Observable<{ lineCount: number; method: string }> {
    return this.http.post<{ lineCount: number; method: string }>(`/api/v1/surfaces/${surfaceId}/detect-layout`, {});
  }

  appendRevision(lineId: string, layer: string, text: string, authorId: string, parentRevisionId?: string): Observable<{ revisionId: string }> {
    return this.http.post<{ revisionId: string }>(`/api/v1/lines/${lineId}/revisions`,
      { layer, text, authorId, parentRevisionId: parentRevisionId ?? null });
  }

  addAnnotation(lineId: string, annotation: unknown): Observable<AnnotationPayload> {
    return this.http.post<AnnotationPayload>(`/api/v1/lines/${lineId}/annotations`, annotation);
  }

  annotations(lineId: string): Observable<AnnotationView[]> {
    return this.http.get<AnnotationView[]>(`/api/v1/lines/${lineId}/annotations`);
  }

  tei(projectId: string, surfaceId: string): Observable<string> {
    return this.http.get(`/api/v1/projects/${projectId}/surfaces/${surfaceId}/tei`, { responseType: 'text' });
  }

  trainingExport(projectId: string): Observable<unknown> {
    return this.http.get<unknown>(`/api/v1/projects/${projectId}/training-export`);
  }

  providers(): Observable<{ providers: { configRef: string; settings: { modelId: string } }[] }> {
    return this.http.get<{ providers: { configRef: string; settings: { modelId: string } }[] }>('/api/v1/providers');
  }

  propose(request: {
    targetRevisionId: string;
    range: { start: number; end: number };
    task: string;
    providerConfigRef: string;
    promptVersion: string;
  }): Observable<unknown> {
    return this.http.post<unknown>('/api/v1/proposals', request);
  }

  acceptProposal(id: string, proposal: unknown, acceptorId: string): Observable<unknown> {
    return this.http.post<unknown>(`/api/v1/proposals/${id}/accept`, { proposal, acceptorId });
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
      lines: {
        id: string;
        regionId: string;
        polygon: GeometryPoint[];
        baseline: GeometryPoint[];
      }[];
      regions: { id: string; readingOrderIndex: number | null }[];
      captures: { id: string; imageLocation?: string }[];
    }[];
  }[];
}
