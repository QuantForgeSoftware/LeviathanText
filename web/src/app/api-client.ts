import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { eventsUrlWithKey } from './auth';
import { cachedRequest } from './offline-cache';
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

export interface ReviewPayload {
  id: string;
  reviewer: string;
  outcome: string;
  at: string;
  note?: string;
  revisionId: string;
  correctedRevisionId?: string;
}

export interface LinePayload {
  line: string;
  diplomatic: string | null;
  direction?: string | null;
  script?: string | null;
  history: RevisionPayload[];
  revisions?: RevisionPayload[];
  reviews?: ReviewPayload[];
}

export interface AuthAuditEntry {
  id: number;
  at: string;
  userId?: string;
  userName?: string;
  role?: string;
  action: string;
  method: string;
  path: string;
  status?: number;
}

export interface UserAccount {
  id: string;
  name: string;
  role: string;
  createdAt: string;
  disabled?: boolean;
}

export interface OrientationResponse {
  lineId?: string;
  regionId?: string;
  direction?: string | null;
  script?: string | null;
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

export interface ConfidenceValue {
  value: number;
  scale: string;
}

export interface RevisionPayload {
  id: string;
  layer: string;
  text: string;
  author: string;
  at: string;
  confidence?: ConfidenceValue | 'none';
  graphemeConfidences?: { start: number; end: number; confidence: ConfidenceValue | 'none' }[];
}

export interface RunRecord {
  id: string;
  kind: string;
  name: string;
  createdAt: string;
  createdBy?: string;
  datasetDigest?: string;
  config: Record<string, string>;
  metrics: Record<string, number>;
  history: number[];
  artifacts: string[];
  notes?: string;
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
  expansionText?: string;
  expansionRevisionId?: string;
  expansionRange?: { start: number; end: number };
  sequenceId?: string;
  sequenceIndex?: number;
  orderBasis?: string;
}

@Injectable({ providedIn: 'root' })
export class ApiClient {
  constructor(private readonly http: HttpClient) {}

  root(): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>('/api/v1/');
  }

  listProjects(): Observable<ProjectSummary[]> {
    return cachedRequest('projects', this.http.get<ProjectSummary[]>('/api/v1/projects'));
  }

  createProject(name: string): Observable<ProjectSummary> {
    return this.http.post<ProjectSummary>('/api/v1/projects', { name });
  }

  loadProject(id: string): Observable<ProjectPayload> {
    return cachedRequest(`project:${id}`, this.http.get<ProjectPayload>(`/api/v1/projects/${id}`));
  }

  diplomaticExport(projectId: string, surfaceId: string): Observable<DiplomaticExport> {
    return this.http.get<DiplomaticExport>(
      `/api/v1/projects/${projectId}/surfaces/${surfaceId}/diplomatic`);
  }

  line(lineId: string): Observable<LinePayload> {
    return cachedRequest(`line:${lineId}`, this.http.get<LinePayload>(`/api/v1/lines/${lineId}`));
  }

  lineCropUrl(lineId: string): string {
    return eventsUrlWithKey(`/api/v1/lines/${lineId}/crop`);
  }

  lineLayerHistory(lineId: string, layer: string): Observable<RevisionPayload[]> {
    return this.http.get<RevisionPayload[]>(`/api/v1/lines/${lineId}/revisions?layer=${layer}`);
  }

  createJob(type: string, ownerId: string, idempotencyKey?: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/v1/jobs', { type, ownerId, idempotencyKey: idempotencyKey ?? null });
  }

  jobEventsUrl(jobId: string): string {
    return eventsUrlWithKey(`/api/v1/jobs/${jobId}/events`);
  }

  jobs(): Observable<JobSummary[]> {
    return this.http.get<JobSummary[]>('/api/v1/jobs');
  }

  updateLineGeometry(lineId: string, polygon: GeometryPoint[], baseline: GeometryPoint[]): Observable<unknown> {
    return this.http.put(`/api/v1/lines/${lineId}/geometry`, { polygon, baseline });
  }

  setLineOrientation(lineId: string, direction: string | null, script: string | null): Observable<OrientationResponse> {
    return this.http.put<OrientationResponse>(`/api/v1/lines/${lineId}/orientation`, { direction, script });
  }

  updateReadingOrder(surfaceId: string, regionIds: string[]): Observable<unknown> {
    return this.http.put(`/api/v1/surfaces/${surfaceId}/reading-order`, { regionIds });
  }

  importLineText(projectId: string, csvText: string, layer: 'L1Diplomatic' | 'L2Editorial'): Observable<{
    applied: number; skipped: number; rows: { lineId: string; applied: boolean; reason?: string }[];
  }> {
    return this.http.post<{
      applied: number; skipped: number; rows: { lineId: string; applied: boolean; reason?: string }[];
    }>(`/api/v1/projects/${projectId}/import-line-text`, { csvText, layer });
  }

  unplacedLines(surfaceId: string): Observable<{ lineId: string; order: number; referenceText: string }[]> {
    return this.http.get<{ lineId: string; order: number; referenceText: string }[]>(
      `/api/v1/surfaces/${surfaceId}/unplaced-lines`);
  }

  geometryHistory(surfaceId: string): Observable<{
    id: string; kind: string; at: string; author: string; lineId?: string; regionId?: string;
  }[]> {
    return this.http.get<{
      id: string; kind: string; at: string; author: string; lineId?: string; regionId?: string;
    }[]>(`/api/v1/surfaces/${surfaceId}/geometry-history`);
  }

  updateLineOrder(regionId: string, lineIds: string[]): Observable<unknown> {
    return this.http.put(`/api/v1/regions/${regionId}/line-order`, { lineIds });
  }

  addLine(surfaceId: string, polygon: GeometryPoint[], baseline: GeometryPoint[]): Observable<{ lineId: string }> {
    return this.http.post<{ lineId: string }>(`/api/v1/surfaces/${surfaceId}/lines`, { polygon, baseline });
  }

  deleteLine(lineId: string): Observable<unknown> {
    return this.http.delete(`/api/v1/lines/${lineId}`);
  }

  splitLine(lineId: string, y: number): Observable<{ topLineId: string; bottomLineId: string }> {
    return this.http.post<{ topLineId: string; bottomLineId: string }>(`/api/v1/lines/${lineId}/split`, { y });
  }

  mergeLines(lineId: string, otherLineId: string): Observable<{ lineId: string }> {
    return this.http.post<{ lineId: string }>(`/api/v1/lines/${lineId}/merge`, { otherLineId });
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

  recordReview(lineId: string, body: {
    reviewer: string;
    outcome: 'accept' | 'correct' | 'dispute';
    note?: string | null;
    correctionText?: string | null;
  }): Observable<{ reviewId: string; outcome: string; correctedRevisionId?: string }> {
    return this.http.post<{ reviewId: string; outcome: string; correctedRevisionId?: string }>(
      `/api/v1/lines/${lineId}/review`, body);
  }

  retractAnnotation(annotationId: string, reviewer: string, reason?: string | null): Observable<{ retractionId: string; annotationId: string }> {
    return this.http.post<{ retractionId: string; annotationId: string }>(
      `/api/v1/annotations/${annotationId}/retract`, { reviewer, reason: reason ?? null });
  }

  annotations(lineId: string): Observable<AnnotationView[]> {
    return this.http.get<AnnotationView[]>(`/api/v1/lines/${lineId}/annotations`);
  }

  tei(projectId: string, surfaceId: string): Observable<string> {
    return this.http.get(`/api/v1/projects/${projectId}/surfaces/${surfaceId}/tei`, { responseType: 'text' });
  }

  runs(kind?: string, limit = 100): Observable<RunRecord[]> {
    const params: Record<string, string> = { limit: String(limit) };
    if (kind) params['kind'] = kind;
    return this.http.get<RunRecord[]>('/api/v1/runs', { params });
  }

  run(id: string): Observable<RunRecord> {
    return this.http.get<RunRecord>(`/api/v1/runs/${id}`);
  }

  me(): Observable<{ authenticated: boolean; name: string; role: string }> {
    return this.http.get<{ authenticated: boolean; name: string; role: string }>('/api/v1/me');
  }

  users(): Observable<UserAccount[]> {
    return this.http.get<UserAccount[]>('/api/v1/users');
  }

  createUser(name: string, role: string): Observable<{ account: UserAccount; apiKey: string }> {
    return this.http.post<{ account: UserAccount; apiKey: string }>('/api/v1/users', { name, role });
  }

  rotateUser(userId: string): Observable<{ apiKey: string }> {
    return this.http.post<{ apiKey: string }>(`/api/v1/users/${userId}/rotate`, {});
  }

  disableUser(userId: string): Observable<void> {
    return this.http.post<void>(`/api/v1/users/${userId}/disable`, {});
  }

  touchPresence(surfaceId: string, editorId: string): Observable<{ editors: string[] }> {
    return this.http.post<{ editors: string[] }>(`/api/v1/surfaces/${surfaceId}/presence`, { editorId });
  }

  presence(surfaceId: string): Observable<{ editors: string[] }> {
    return this.http.get<{ editors: string[] }>(`/api/v1/surfaces/${surfaceId}/presence`);
  }

  leavePresence(surfaceId: string, editorId: string): Observable<{ editors: string[] }> {
    return this.http.delete<{ editors: string[] }>(`/api/v1/surfaces/${surfaceId}/presence/${encodeURIComponent(editorId)}`);
  }

  audit(limit = 50): Observable<AuthAuditEntry[]> {
    return this.http.get<AuthAuditEntry[]>('/api/v1/audit', { params: { limit: String(limit) } });
  }

  projectMembers(projectId: string): Observable<string[]> {
    return this.http.get<string[]>(`/api/v1/projects/${projectId}/members`);
  }

  createInvite(projectId: string, role: string, expiresInHours = 24): Observable<{
    inviteId: string; token: string; role: string; expiresAt: string;
  }> {
    return this.http.post<{ inviteId: string; token: string; role: string; expiresAt: string }>(
      `/api/v1/projects/${projectId}/invites`, { role, expiresInHours });
  }

  acceptInvite(token: string, name: string): Observable<{
    account: UserAccount; apiKey: string; projectId: string; role: string;
  }> {
    return this.http.post<{ account: UserAccount; apiKey: string; projectId: string; role: string }>(
      '/api/v1/invites/accept', { token, name });
  }

  addProjectMember(projectId: string, userId: string): Observable<string[]> {
    return this.http.post<string[]>(`/api/v1/projects/${projectId}/members`, { userId });
  }

  removeProjectMember(projectId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`/api/v1/projects/${projectId}/members/${userId}`);
  }

  reviewSummary(projectId: string): Observable<{
    transcribedLines: number;
    reviewedLines: number;
    doubleReviewedLines: number;
    disputedLines: number;
    acceptedLines?: number;
    correctedLines?: number;
    needsAdjudication: number;
    lines: {
      lineId: string; surfaceId: string; text: string; decisions: number;
      reviewers: string[]; outcomes: string[]; latestOutcome: string | null;
      doubleReviewed: boolean; needsAdjudication: boolean;
    }[];
  }> {
    return this.http.get<{
      transcribedLines: number;
      reviewedLines: number;
      doubleReviewedLines: number;
      disputedLines: number;
      needsAdjudication: number;
      lines: {
        lineId: string; surfaceId: string; text: string; decisions: number;
        reviewers: string[]; outcomes: string[]; latestOutcome: string | null;
        doubleReviewed: boolean; needsAdjudication: boolean;
      }[];
    }>(`/api/v1/projects/${projectId}/review-summary`);
  }

  adjudicationUrl(projectId: string): string {
    return `/api/v1/projects/${projectId}/adjudication.csv`;
  }

  reviewQueue(projectId: string, surfaceId: string): Observable<{
    lineId: string; revisionId: string; meanConfidence: number | null; emptyOutput: boolean; text: string;
  }[]> {
    return this.http.get<{
      lineId: string; revisionId: string; meanConfidence: number | null; emptyOutput: boolean; text: string;
    }[]>(`/api/v1/projects/${projectId}/review-queue`, { params: { surfaceId } });
  }

  stats(projectId: string): Observable<{
    objects: number; surfaces: number; captures: number; regions: number; lines: number;
    transcribedLines: number; linesWithL0: number; l0Revisions: number; l2Revisions: number;
    annotations: number; flaggedLines: number;
  }> {
    return this.http.get<{
      objects: number; surfaces: number; captures: number; regions: number; lines: number;
      transcribedLines: number; linesWithL0: number; l0Revisions: number; l2Revisions: number;
      annotations: number; flaggedLines: number;
    }>(`/api/v1/projects/${projectId}/stats`);
  }

  search(projectId: string, query: string, mode: 'literal' | 'base'): Observable<{
    query: string;
    mode: string;
    totalHits: number;
    truncated: boolean;
    hits: { surfaceId: string; lineId: string; revisionId: string; layer: string; snippet: string; match: { start: number; end: number } }[];
  }> {
    return this.http.get<{
      query: string;
      mode: string;
      totalHits: number;
      truncated: boolean;
      hits: { surfaceId: string; lineId: string; revisionId: string; layer: string; snippet: string; match: { start: number; end: number } }[];
    }>(`/api/v1/projects/${projectId}/search`, { params: { q: query, mode } });
  }

  profileReport(projectId: string): Observable<{
    profile: string;
    version: string;
    surfaces: { surfaceId: string; lines: number; flagged: number; issues: { lineId: string; scalarOffset: number; category: string; scalar: string }[] }[];
  }> {
    return this.http.get<{
      profile: string;
      version: string;
      surfaces: { surfaceId: string; lines: number; flagged: number; issues: { lineId: string; scalarOffset: number; category: string; scalar: string }[] }[];
    }>(`/api/v1/projects/${projectId}/profile-report`);
  }

  trainingExport(projectId: string): Observable<unknown> {
    return this.http.get<unknown>(`/api/v1/projects/${projectId}/training-export`);
  }

  models(): Observable<{ name: string; modifiedAt: string; hasManifest: boolean }[]> {
    return this.http.get<{ name: string; modifiedAt: string; hasManifest: boolean }[]>('/api/v1/models');
  }

  recognizeSurface(surfaceId: string, model: string): Observable<{
    surfaceId: string;
    model: string;
    lines: number;
    skipped: number;
    reviewQueue: { lineId: string; meanConfidence: number | null; emptyOutput: boolean }[];
  }> {
    return this.http.post<{
      surfaceId: string;
      model: string;
      lines: number;
      skipped: number;
      reviewQueue: { lineId: string; meanConfidence: number | null; emptyOutput: boolean }[];
    }>(`/api/v1/surfaces/${surfaceId}/recognize`, { model });
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
        direction?: string | null;
        script?: string | null;
      }[];
      regions: {
        id: string;
        readingOrderIndex: number | null;
        direction?: string | null;
        script?: string | null;
      }[];
      captures: { id: string; imageLocation?: string }[];
    }[];
  }[];
}
