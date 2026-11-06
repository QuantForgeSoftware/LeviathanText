import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { capturedApiKeyContext, eventsUrlWithKey } from './auth';
import { cachedRequest, clearCache } from './offline-cache';
import { Observable, EMPTY, catchError, defer, filter, throwError } from 'rxjs';
import type { PrincipalProfile } from './principal-profile';

/**
 * Hand-written client for the versioned REST contract (interchange-and-api.md §2).
 * The generated client replaces this during implementation; the shapes here follow the
 * contract JSON conventions (camelCase, string enums).
 */

export interface CaptureDisplayDescriptor {
  schemaVersion: 1;
  projectId: string;
  surfaceId: string;
  captureId: string;
  sourceFrameRef: string;
  recordedSourceChecksum: { algorithm: string; digest: string };
  recordedChecksumStatus: 'verified' | 'unavailable';
  observedSourceChecksum: { algorithm: 'sha256'; digest: string };
  sourceWidth: number;
  sourceHeight: number;
  displayWidth: number;
  displayHeight: number;
  scaleX: number;
  scaleY: number;
  orientation: 'raw';
  mediaType: 'image/png';
  derivativeId: string;
  displayChecksum: { algorithm: 'sha256'; digest: string };
}

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
  /** The line endpoint returns L1 history without a layer property. */
  history: Omit<RevisionPayload, 'layer'>[];
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
  frameRef?: string | null;
  regionId: string | null;
  polygon: GeometryPoint[];
  baseline: GeometryPoint[];
}

export interface GeometryRegion {
  id: string;
  frameRef?: string | null;
  readingOrderIndex: number | null;
}

export interface ConfidenceValue {
  value: number;
  scale: string;
}

export interface ProjectInviteView {
  id: string;
  projectId: string;
  role: string;
  createdBy?: string | null;
  createdAt: string;
  expiresAt: string;
  usedAt?: string | null;
  usedBy?: string | null;
}

export interface NativeFragmentView {
  id: string;
  externalRefs: Record<string, string>;
  metadata: Record<string, string>;
  surfaceIds: string[];
}

export interface ProjectIntegrityIssue {
  code: string;
  scope: string;
  message: string;
}

export interface ProjectOverlap {
  kind: string;
  value: string;
  entities: string;
  note: string;
}

export interface RegistryEntry {
  id: string;
  name: string;
  task: string;
  downloadUrl: string;
  sha256?: string | null;
  packageFormatVersion: number;
}

export interface SessionView {
  id: string;
  userId: string;
  userName: string;
  nodeId: string;
  surfaceId?: string | null;
  createdAt: string;
  lastSeenAt: string;
  expiresAt: string;
  revokedAt?: string | null;
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
  private workspaceCurrent: (() => boolean) | undefined;
  constructor(private readonly http: HttpClient) {}

  /** Per-intent transport. It never grants access or persists credentials.
   * Call capture() again for every action; identity/project probes use the root client. */
  forWorkspaceRequest(intent: { credential: string; current(): boolean }): ApiClient {
    const upstream = this.http;
    const scoped = new ApiClient(new HttpClient({ handle: request => defer(() => {
      if (!intent.current()) return EMPTY;
      const context = new HttpContext();
      for (const token of request.context.keys()) context.set(token, request.context.get(token));
      const captured = capturedApiKeyContext(intent.credential, () => intent.current());
      for (const token of captured.keys()) context.set(token, captured.get(token));
      const headers = request.method === 'GET'
        ? request.headers.set('Cache-Control', 'no-cache, no-store').set('Pragma', 'no-cache') : request.headers;
      return upstream.request(request.clone({ context, headers })).pipe(
        filter(() => intent.current()),
        catchError(error => {
          if (!intent.current()) return EMPTY;
          if (error?.status === 401 || error?.status === 403 || error?.status === 404) {
            clearCache(); // Fence pending cache writes and synchronously suspend the workspace.
            return EMPTY;
          }
          return throwError(() => error);
        }),
      );
    }) }));
    scoped.workspaceCurrent = () => intent.current();
    return scoped;
  }

  root(): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>('/api/v1/');
  }

  listProjectsFresh(): Observable<ProjectSummary[]> {
    return this.http.get<ProjectSummary[]>('/api/v1/projects', {
      headers: { 'Cache-Control': 'no-cache, no-store', Pragma: 'no-cache' },
    });
  }

  listProjects(): Observable<ProjectSummary[]> {
    return cachedRequest('projects', credential => this.http.get<ProjectSummary[]>('/api/v1/projects',
      { context: capturedApiKeyContext(credential) }), this.workspaceCurrent);
  }

  createProject(name: string, credential?: string): Observable<ProjectSummary> {
    return this.http.post<ProjectSummary>('/api/v1/projects', { name },
      credential === undefined ? {} : { context: capturedApiKeyContext(credential) });
  }

  loadProject(id: string): Observable<ProjectPayload> {
    return cachedRequest(`project:${id}`, credential => this.http.get<ProjectPayload>(`/api/v1/projects/${id}`,
      { context: capturedApiKeyContext(credential) }), this.workspaceCurrent);
  }

  loadProjectFresh(id: string, credential?: string): Observable<ProjectPayload> {
    return this.http.get<ProjectPayload>(`/api/v1/projects/${encodeURIComponent(id)}`, {
      headers: { 'Cache-Control': 'no-cache, no-store', Pragma: 'no-cache' },
      ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }),
    });
  }

  captureDisplay(projectId: string, surfaceId: string, captureId: string): Observable<CaptureDisplayDescriptor> {
    return this.http.get<CaptureDisplayDescriptor>(this.captureDisplayPath(projectId, surfaceId, captureId));
  }

  captureDisplayImage(projectId: string, surfaceId: string, captureId: string, derivativeId: string): Observable<Blob> {
    return this.http.get(`${this.captureDisplayPath(projectId, surfaceId, captureId)}/image`, {
      responseType: 'blob', headers: { 'If-Match': `"${derivativeId}"` },
    });
  }

  private captureDisplayPath(projectId: string, surfaceId: string, captureId: string): string {
    return `/api/v1/projects/${encodeURIComponent(projectId)}/surfaces/${encodeURIComponent(surfaceId)}/captures/${encodeURIComponent(captureId)}/display`;
  }

  diplomaticExport(projectId: string, surfaceId: string): Observable<DiplomaticExport> {
    return this.http.get<DiplomaticExport>(
      `/api/v1/projects/${projectId}/surfaces/${surfaceId}/diplomatic`);
  }

  lineFresh(lineId: string): Observable<LinePayload> {
    return this.http.get<LinePayload>(`/api/v1/lines/${encodeURIComponent(lineId)}`, {
      headers: { 'Cache-Control': 'no-cache, no-store', Pragma: 'no-cache' },
    });
  }

  line(lineId: string): Observable<LinePayload> {
    return cachedRequest(`line:${lineId}`, credential => this.http.get<LinePayload>(`/api/v1/lines/${lineId}`,
      { context: capturedApiKeyContext(credential) }), this.workspaceCurrent);
  }

  lineCrop(lineId: string): Observable<Blob> {
    return this.http.get(`/api/v1/lines/${encodeURIComponent(lineId)}/crop`, { responseType: 'blob' });
  }

  lineCropUrl(lineId: string): string {
    return eventsUrlWithKey(`/api/v1/lines/${lineId}/crop`);
  }

  lineLayerHistory(lineId: string, layer: string): Observable<RevisionPayload[]> {
    return this.http.get<RevisionPayload[]>(`/api/v1/lines/${lineId}/revisions?layer=${layer}`);
  }

  createJob(type: string, ownerId: string, idempotencyKey?: string,
    credential?: string, current?: () => boolean): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/v1/jobs', { type, ownerId, idempotencyKey: idempotencyKey ?? null },
      { context: credential === undefined ? undefined : capturedApiKeyContext(credential, current) });
  }

  jobEventsUrl(jobId: string): string {
    return `/api/v1/jobs/${encodeURIComponent(jobId)}/events`;
  }

  jobs(credential?: string, current?: () => boolean): Observable<JobSummary[]> {
    return this.http.get<JobSummary[]>('/api/v1/jobs',
      { context: credential === undefined ? undefined : capturedApiKeyContext(credential, current) });
  }

  jobSnapshot(jobId: string, credential?: string, current?: () => boolean): Observable<JobSummary> {
    return this.http.get<JobSummary>(`/api/v1/jobs/${encodeURIComponent(jobId)}`,
      { context: credential === undefined ? undefined : capturedApiKeyContext(credential, current) });
  }

  updateLineGeometry(lineId: string, polygon: GeometryPoint[], baseline: GeometryPoint[],
    expectedGeometry?: Pick<GeometryLine, 'polygon' | 'baseline'>): Observable<unknown> {
    return this.http.put(`/api/v1/lines/${lineId}/geometry`,
      expectedGeometry ? { polygon, baseline, expectedGeometry } : { polygon, baseline });
  }

  setLineOrientation(lineId: string, direction: string | null, script: string | null): Observable<OrientationResponse> {
    return this.http.put<OrientationResponse>(`/api/v1/lines/${lineId}/orientation`, { direction, script });
  }

  updateReadingOrder(surfaceId: string, regionIds: string[], expectedRegionIds?: string[]): Observable<unknown> {
    return this.http.put(`/api/v1/surfaces/${surfaceId}/reading-order`,
      expectedRegionIds === undefined ? { regionIds } : { regionIds, expectedRegionIds });
  }

  importLineText(projectId: string, csvText: string, layer: 'L1Diplomatic' | 'L2Editorial', credential?: string): Observable<{
    applied: number; skipped: number; rows: { lineId: string; applied: boolean; reason?: string }[];
  }> {
    return this.http.post<{
      applied: number; skipped: number; rows: { lineId: string; applied: boolean; reason?: string }[];
    }>(`/api/v1/projects/${projectId}/import-line-text`, { csvText, layer },
      credential === undefined ? {} : { context: capturedApiKeyContext(credential) });
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

  updateLineOrder(regionId: string, lineIds: string[], expectedLineIds?: string[]): Observable<unknown> {
    return this.http.put(`/api/v1/regions/${regionId}/line-order`,
      expectedLineIds === undefined ? { lineIds } : { lineIds, expectedLineIds });
  }

  addLine(surfaceId: string, polygon: GeometryPoint[], baseline: GeometryPoint[], regionId?: string): Observable<{ lineId: string }> {
    return this.http.post<{ lineId: string }>(`/api/v1/surfaces/${surfaceId}/lines`, regionId ? { polygon, baseline, regionId } : { polygon, baseline });
  }

  addCaptureLine(display: CaptureDisplayDescriptor, polygon: GeometryPoint[], baseline: GeometryPoint[],
    expectedLineIds: string[], expectedRegionIds: string[], regionId?: string): Observable<{ lineId: string }> {
    const route = this.captureDisplayPath(display.projectId, display.surfaceId, display.captureId).replace(/\/display$/, '/lines');
    return this.http.post<{ lineId: string }>(route, { polygon, baseline, expectedLineIds, expectedRegionIds, regionId },
      { headers: { 'If-Match': `"${display.derivativeId}"` } });
  }

  updateCaptureLineGeometry(display: CaptureDisplayDescriptor, lineId: string, polygon: GeometryPoint[], baseline: GeometryPoint[],
    expectedGeometry: { polygon: GeometryPoint[]; baseline: GeometryPoint[] }): Observable<unknown> {
    const route = this.captureDisplayPath(display.projectId, display.surfaceId, display.captureId)
      .replace(/\/display$/, `/lines/${encodeURIComponent(lineId)}/geometry`);
    return this.http.put(route, { polygon, baseline, expectedGeometry },
      { headers: { 'If-Match': `"${display.derivativeId}"` } });
  }

  updateCaptureReadingOrder(display: CaptureDisplayDescriptor, regionIds: string[], expectedSurfaceRegionIds: string[]): Observable<unknown> {
    const route = this.captureDisplayPath(display.projectId, display.surfaceId, display.captureId).replace(/\/display$/, '/reading-order');
    return this.http.put(route, { regionIds, expectedSurfaceRegionIds }, { headers: { 'If-Match': `"${display.derivativeId}"` } });
  }

  updateCaptureLineOrder(display: CaptureDisplayDescriptor, regionId: string, lineIds: string[], expectedRegionLineIds: string[]): Observable<unknown> {
    const route = this.captureDisplayPath(display.projectId, display.surfaceId, display.captureId)
      .replace(/\/display$/, `/regions/${encodeURIComponent(regionId)}/line-order`);
    return this.http.put(route, { lineIds, expectedRegionLineIds }, { headers: { 'If-Match': `"${display.derivativeId}"` } });
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

  appendRevision(lineId: string, layer: string, text: string, authorId: string,
    parentRevisionId?: string, merge = false): Observable<{ revisionId: string; merged?: boolean }> {
    return this.http.post<{ revisionId: string; merged?: boolean }>(`/api/v1/lines/${lineId}/revisions`,
      { layer, text, authorId, parentRevisionId: parentRevisionId ?? null, merge });
  }

  addAnnotation(lineId: string, annotation: unknown): Observable<{ annotationId: string; anchoredRevisionId: string }> {
    return this.http.post<{ annotationId: string; anchoredRevisionId: string }>(`/api/v1/lines/${lineId}/annotations`, annotation);
  }

  recordReview(lineId: string, body: {
    reviewer: string;
    outcome: 'accept' | 'correct' | 'dispute';
    note?: string | null;
    correctionText?: string | null;
    revisionId?: string | null;
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

  tei(projectId: string, surfaceId: string, credential?: string): Observable<string> {
    return this.http.get(`/api/v1/projects/${projectId}/surfaces/${surfaceId}/tei`,
      { responseType: 'text', ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }) });
  }

  runs(kind?: string, limit = 100): Observable<RunRecord[]> {
    const params: Record<string, string> = { limit: String(limit) };
    if (kind) params['kind'] = kind;
    return this.http.get<RunRecord[]>('/api/v1/runs', { params });
  }

  run(id: string): Observable<RunRecord> {
    return this.http.get<RunRecord>(`/api/v1/runs/${id}`);
  }

  me(credential?: string): Observable<PrincipalProfile> {
    // Identity reauthorization is always a new network read, never cachedRequest.
    return this.http.get<PrincipalProfile>('/api/v1/me', { headers: {
      'Cache-Control': 'no-cache, no-store', 'Pragma': 'no-cache',
    }, ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }) });
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

  touchPresence(surfaceId: string, editorId: string, sessionId?: string): Observable<{ editors: string[]; editor?: string }> {
    return this.http.post<{ editors: string[]; editor?: string }>(`/api/v1/surfaces/${surfaceId}/presence`,
      { editorId, sessionId: sessionId ?? null });
  }

  createSession(surfaceId: string, ttlMinutes = 60): Observable<{ id: string; nodeId: string; expiresAt: string }> {
    return this.http.post<{ id: string; nodeId: string; expiresAt: string }>('/api/v1/sessions',
      { surfaceId, ttlMinutes });
  }

  revokeSession(sessionId: string): Observable<{ revoked: boolean }> {
    return this.http.delete<{ revoked: boolean }>(`/api/v1/sessions/${sessionId}`);
  }

  sessions(surfaceId?: string): Observable<SessionView[]> {
    const params = surfaceId ? { surfaceId } : undefined;
    return this.http.get<SessionView[]>('/api/v1/sessions', { params });
  }

  registry(indexUrl: string): Observable<RegistryEntry[]> {
    return this.http.post<RegistryEntry[]>('/api/v1/models/registry', { indexUrl });
  }

  pullRegistry(indexUrl: string, modelId: string): Observable<{ id: string; directory: string; sha256: string }> {
    return this.http.post<{ id: string; directory: string; sha256: string }>(
      '/api/v1/models/registry/pull', { indexUrl, modelId });
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

  invites(projectId: string): Observable<ProjectInviteView[]> {
    return this.http.get<ProjectInviteView[]>(`/api/v1/projects/${projectId}/invites`);
  }

  revokeInvite(projectId: string, inviteId: string): Observable<{ revoked: boolean }> {
    return this.http.delete<{ revoked: boolean }>(
      `/api/v1/projects/${projectId}/invites/${inviteId}`);
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

  reviewSummary(projectId: string, credential?: string): Observable<{
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
    }>(`/api/v1/projects/${projectId}/review-summary`,
      credential === undefined ? {} : { context: capturedApiKeyContext(credential) });
  }

  adjudicationCsv(projectId: string, credential?: string): Observable<string> {
    return this.http.get(`/api/v1/projects/${projectId}/adjudication.csv`,
      { responseType: 'text', ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }) });
  }

  projectBundle(projectId: string, credential?: string): Observable<Blob> {
    return this.http.get(`/api/v1/projects/${projectId}/bundle`,
      { responseType: 'blob', ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }) });
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

  stats(projectId: string, credential?: string): Observable<{
    objects: number; surfaces: number; captures: number; regions: number; lines: number;
    transcribedLines: number; linesWithL0: number; l0Revisions: number; l2Revisions: number;
    annotations: number; flaggedLines: number;
  }> {
    return this.http.get<{
      objects: number; surfaces: number; captures: number; regions: number; lines: number;
      transcribedLines: number; linesWithL0: number; l0Revisions: number; l2Revisions: number;
      annotations: number; flaggedLines: number;
    }>(`/api/v1/projects/${projectId}/stats`,
      credential === undefined ? {} : { context: capturedApiKeyContext(credential) });
  }

  search(projectId: string, query: string, mode: 'literal' | 'base', credential?: string): Observable<{
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
    }>(`/api/v1/projects/${projectId}/search`, { params: { q: query, mode }, ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }) });
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

  surfaceFragments(surfaceId: string): Observable<NativeFragmentView[]> {
    return this.http.get<NativeFragmentView[]>(`/api/v1/surfaces/${surfaceId}/fragments`);
  }

  createSurfaceFragment(surfaceId: string, externalRefs: Record<string, string>): Observable<{
    id: string; surfaceIds: string[];
  }> {
    return this.http.post<{ id: string; surfaceIds: string[] }>(
      `/api/v1/surfaces/${surfaceId}/fragments`, { externalRefs });
  }

  wordBoundaryCsv(projectId: string, credential?: string): Observable<string> {
    return this.http.get(`/api/v1/projects/${projectId}/word-boundaries`,
      { responseType: 'text', ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }) });
  }

  projectIntegrity(projectId: string): Observable<ProjectIntegrityIssue[]> {
    return this.http.get<ProjectIntegrityIssue[]>(`/api/v1/projects/${projectId}/integrity`);
  }

  projectOverlap(projectId: string): Observable<ProjectOverlap[]> {
    return this.http.get<ProjectOverlap[]>(`/api/v1/projects/${projectId}/overlap`);
  }

  integrityCsv(projectId: string): Observable<string> {
    return this.http.get(`/api/v1/projects/${projectId}/integrity?format=csv`,
      { responseType: 'text' });
  }

  overlapCsv(projectId: string): Observable<string> {
    return this.http.get(`/api/v1/projects/${projectId}/overlap`, { responseType: 'text' });
  }

  projectSearchablePdf(projectId: string, credential?: string): Observable<Blob> {
    return this.http.get(`/api/v1/projects/${projectId}/searchable-pdf`,
      { responseType: 'blob', ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }) });
  }

  projectEditionHtml(projectId: string, credential?: string): Observable<string> {
    return this.http.get(`/api/v1/projects/${projectId}/edition.html`,
      { responseType: 'text', ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }) });
  }

  editionHtml(projectId: string, surfaceId: string, credential?: string): Observable<string> {
    return this.http.get(
      `/api/v1/projects/${projectId}/surfaces/${surfaceId}/edition.html`,
      { responseType: 'text', ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }) });
  }

  searchablePdf(projectId: string, surfaceId: string, credential?: string): Observable<Blob> {
    return this.http.get(`/api/v1/projects/${projectId}/surfaces/${surfaceId}/searchable-pdf`,
      { responseType: 'blob', ...(credential === undefined ? {} : { context: capturedApiKeyContext(credential) }) });
  }

  trainingExport(projectId: string, credential?: string): Observable<unknown> {
    return this.http.get<unknown>(`/api/v1/projects/${projectId}/training-export`,
      credential === undefined ? {} : { context: capturedApiKeyContext(credential) });
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
  }, projectId?: string): Observable<unknown> {
    const url = '/api/v1/proposals' + (projectId ? '?projectId=' + encodeURIComponent(projectId) : '');
    return this.http.post<unknown>(url, request);
  }

  acceptProposal(id: string, proposal: unknown, acceptorId: string, projectId?: string): Observable<unknown> {
    return this.http.post<unknown>(`/api/v1/proposals/${id}/accept`,
      projectId === undefined ? { proposal, acceptorId } : { proposal, acceptorId, projectId });
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
        regionId: string | null;
        frameRef?: string | null;
        polygon: GeometryPoint[];
        baseline: GeometryPoint[];
        direction?: string | null;
        script?: string | null;
      }[];
      regions: {
        id: string;
        frameRef?: string | null;
        readingOrderIndex: number | null;
        direction?: string | null;
        script?: string | null;
      }[];
      captures: { id: string; imageLocation?: string }[];
    }[];
  }[];
}
