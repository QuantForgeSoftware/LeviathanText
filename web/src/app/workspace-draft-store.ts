import type { WorkspaceDraftHandle, WorkspaceDraftLoadResult, WorkspaceDraftSaveResult, WorkspaceDraftScope,
  WorkspaceDraftSnapshot, WorkspaceDraftStorage, WorkspaceJson } from './workspace-access-types';

const PREFIX = 'leviathan:workspace-draft:v1:';
export const WorkspaceDraftLimits = Object.freeze({ recordBytes: 2 * 1024 * 1024, aggregateBytes: 4 * 1024 * 1024,
  recordsPerScope: 512, storageKeys: 2048, jsonDepth: 32, jsonNodes: 50_000 });
const forbidden = /^(?:__proto__|prototype|constructor)$|api.?key|credential|authorization|access.?token|refresh.?token|password|secret|cookie|bearer/i;
const namespace = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/;
interface Envelope { version: 1; recordId: string; parentRecordId: string | null; snapshot: WorkspaceDraftSnapshot; }
interface Entry { key: string; raw: string; value: Envelope; }
class InvalidRecord extends Error {}
function invalid(reason: string): never { throw new InvalidRecord(reason); }
function text(value: unknown, max = 512): value is string {
  return typeof value === 'string' && value.length > 0 && value.length <= max && value === value.trim() && !/[\u0000-\u001f\u007f]/.test(value);
}
function recordId(value: unknown): value is string { return typeof value === 'string' && /^[a-zA-Z0-9_-]{1,128}$/.test(value); }
function object(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null && !Array.isArray(value); }
function keys(value: Record<string, unknown>, expected: readonly string[]): boolean {
  const actual = Object.keys(value).sort(); return actual.length === expected.length && actual.every((key, i) => key === [...expected].sort()[i]);
}
function validScope(value: unknown): value is WorkspaceDraftScope {
  if (!object(value) || !keys(value, ['workspaceNamespace', 'accountId', 'projectId', 'surfaceId', 'lineId', 'layer'])) return false;
  return typeof value['workspaceNamespace'] === 'string' && namespace.test(value['workspaceNamespace'])
    && value['workspaceNamespace'] !== '00000000-0000-0000-0000-000000000000'
    && text(value['accountId'], 256) && text(value['projectId']) && text(value['surfaceId']) && text(value['lineId']) && text(value['layer'], 64);
}
function scopeKey(scope: WorkspaceDraftScope): string {
  return PREFIX + encodeURIComponent(JSON.stringify([scope.workspaceNamespace, scope.accountId, scope.projectId, scope.surfaceId, scope.lineId, scope.layer])) + ':';
}
function sameScope(a: WorkspaceDraftScope, b: WorkspaceDraftScope): boolean { return scopeKey(a) === scopeKey(b); }
/** Copy only validated own data descriptors: accessors/toJSON/prototypes cannot
 * change what is serialized after validation. Opaque component data stays JSON. */
function cloneJson(value: unknown, depth: number, budget: { nodes: number; units: number }, seen: Set<object>): WorkspaceJson {
  if (++budget.nodes > WorkspaceDraftLimits.jsonNodes || depth > WorkspaceDraftLimits.jsonDepth) invalid('json-budget');
  if (value === null || typeof value === 'boolean') return value;
  if (typeof value === 'number') { if (!Number.isFinite(value) || Object.is(value, -0)) invalid('non-json-number'); return value; }
  if (typeof value === 'string') { budget.units += value.length; if (budget.units > WorkspaceDraftLimits.recordBytes) invalid('record-too-large'); return value; }
  if (typeof value !== 'object' || seen.has(value)) invalid('non-json-value');
  const array = Array.isArray(value), prototype = Object.getPrototypeOf(value);
  if (array ? prototype !== Array.prototype : prototype !== Object.prototype && prototype !== null) invalid('non-plain-object');
  if (Object.getOwnPropertySymbols(value).length) invalid('symbol-property');
  seen.add(value);
  try {
    const descriptors = Object.getOwnPropertyDescriptors(value);
    if (array) {
      const length = (value as unknown[]).length;
      if (length > WorkspaceDraftLimits.jsonNodes || Object.keys(descriptors).length !== length + 1) invalid('sparse-or-extra-array-properties');
      const result: WorkspaceJson[] = [];
      for (let i = 0; i < length; i++) {
        const descriptor = descriptors[String(i)];
        if (!descriptor || !('value' in descriptor) || !descriptor.enumerable) invalid('accessor-property');
        result.push(cloneJson(descriptor.value, depth + 1, budget, seen));
      }
      return result;
    }
    const result: Record<string, WorkspaceJson> = {};
    for (const [key, descriptor] of Object.entries(descriptors)) {
      if (forbidden.test(key) || !('value' in descriptor) || !descriptor.enumerable) invalid('unsafe-property');
      budget.units += key.length; if (budget.units > WorkspaceDraftLimits.recordBytes) invalid('record-too-large');
      result[key] = cloneJson(descriptor.value, depth + 1, budget, seen);
    }
    return result;
  } finally { seen.delete(value); }
}
function opaque(value: unknown): boolean {
  return value === null || object(value) && keys(value, ['version', 'kind', 'data']) && value['version'] === 1 && text(value['kind'], 64);
}
function validSnapshot(value: unknown): value is WorkspaceDraftSnapshot {
  if (!object(value) || !keys(value, ['schemaVersion', 'scope', 'base', 'draftText', 'frame', 'undo', 'geometry', 'readingOrder', 'unconfirmedWrites'])
    || value['schemaVersion'] !== 1 || !validScope(value['scope']) || typeof value['draftText'] !== 'string') return false;
  const base = value['base'];
  if (!object(base) || !keys(base, ['revisionId', 'text']) || typeof base['text'] !== 'string'
    || !(base['revisionId'] === null || text(base['revisionId']))) return false;
  const frame = value['frame'];
  if (frame !== null) {
    if (!object(frame) || !keys(frame, ['captureId', 'frameRef', 'checksum', 'width', 'height']) || !text(frame['captureId']) || !text(frame['frameRef'])) return false;
    for (const dimension of [frame['width'], frame['height']]) if (!(dimension === null || typeof dimension === 'number' && Number.isSafeInteger(dimension) && dimension > 0)) return false;
    const checksum = frame['checksum'];
    if (checksum !== null && (!object(checksum) || !keys(checksum, ['algorithm', 'digest']) || !text(checksum['algorithm'], 64) || !text(checksum['digest'], 512))) return false;
  }
  return opaque(value['undo']) && opaque(value['geometry']) && opaque(value['readingOrder'])
    && Array.isArray(value['unconfirmedWrites']) && value['unconfirmedWrites'].length <= 256
    && value['unconfirmedWrites'].every(item => item !== null && opaque(item));
}
function boundedRaw(raw: string): void {
  if (raw.length > WorkspaceDraftLimits.recordBytes || new TextEncoder().encode(raw).length > WorkspaceDraftLimits.recordBytes) invalid('record-too-large');
  let depth = 0, quoted = false, escaped = false;
  for (const char of raw) {
    if (quoted) { if (escaped) escaped = false; else if (char === '\\') escaped = true; else if (char === '"') quoted = false; }
    else if (char === '"') quoted = true;
    else if (char === '{' || char === '[') { if (++depth > WorkspaceDraftLimits.jsonDepth) invalid('json-depth'); }
    else if (char === '}' || char === ']') depth--;
  }
}
function parse(raw: string): Envelope {
  boundedRaw(raw);
  const value = cloneJson(JSON.parse(raw), 0, { nodes: 0, units: 0 }, new Set());
  if (!object(value) || !keys(value, ['version', 'recordId', 'parentRecordId', 'snapshot']) || value['version'] !== 1
    || !recordId(value['recordId']) || !(value['parentRecordId'] === null || recordId(value['parentRecordId']))
    || value['parentRecordId'] === value['recordId'] || !validSnapshot(value['snapshot'])) invalid('record-schema');
  return value as unknown as Envelope;
}
function browserStorage(): WorkspaceDraftStorage | null { try { return globalThis.localStorage ?? null; } catch { return null; } }

/** Append-only atomic recovery journal. No method deletes or overwrites old or
 * ownerless bytes. Branches are ambiguous; compaction requires a later explicit resolution. */
export class WorkspaceDraftStore {
  constructor(private readonly storage: WorkspaceDraftStorage | null = browserStorage(),
    private readonly createRecordId: () => string = () => globalThis.crypto.randomUUID()) {}

  save(input: WorkspaceDraftSnapshot, previous?: WorkspaceDraftHandle): WorkspaceDraftSaveResult {
    if (this.storage === null) return { ok: false, reason: 'storage-unavailable' };
    try {
      const snapshot = cloneJson(input, 1, { nodes: 0, units: 0 }, new Set());
      if (!validSnapshot(snapshot)) return { ok: false, reason: 'snapshot-schema-or-unknown-base' };
      const prefix = scopeKey(snapshot.scope);
      const existing = this.scan(snapshot.scope);
      let parentRecordId: string | null = null;
      if (existing.length > 0) {
        const current = this.load(snapshot.scope);
        if (current.status !== 'found') return { ok: false, reason: 'existing-recovery-not-unique' };
        if (!previous) return { ok: false, reason: 'existing-record-needs-handle' };
        if (current.handle.storageKey !== previous.storageKey || current.handle.recordId !== previous.recordId
          || current.handle.raw !== previous.raw) return { ok: false, reason: 'previous-record-not-current' };
        parentRecordId = previous.recordId;
      } else if (previous) return { ok: false, reason: 'previous-record-changed' };
      if (existing.length >= WorkspaceDraftLimits.recordsPerScope) return { ok: false, reason: 'journal-full' };
      const id = this.createRecordId(); if (!recordId(id)) return { ok: false, reason: 'record-id-unavailable' };
      const key = prefix + id; if (this.storage.getItem(key) !== null) return { ok: false, reason: 'record-id-collision' };
      const value: Envelope = { version: 1, recordId: id, parentRecordId, snapshot };
      const raw = JSON.stringify(value); boundedRaw(raw);
      const total = existing.reduce((sum, entry) => sum + new TextEncoder().encode(entry.raw).length, 0) + new TextEncoder().encode(raw).length;
      if (total > WorkspaceDraftLimits.aggregateBytes) return { ok: false, reason: 'journal-full' };
      this.storage.setItem(key, raw); // One authoritative text/base/state write.
      if (this.storage.getItem(key) !== raw) return { ok: false, reason: 'write-not-verified' };
      const recovered = this.load(snapshot.scope);
      if (recovered.status !== 'found' || recovered.handle.storageKey !== key || recovered.handle.recordId !== id || recovered.handle.raw !== raw)
        return { ok: false, reason: 'recovery-not-unique-current' };
      return { ok: true, handle: { storageKey: key, recordId: id, raw } };
    } catch (error) { return { ok: false, reason: error instanceof InvalidRecord ? error.message : 'storage-or-serialization-failed' }; }
  }
  load(scope: WorkspaceDraftScope): WorkspaceDraftLoadResult {
    if (this.storage === null) return { status: 'unavailable', reason: 'storage-unavailable' };
    try {
      const checked = cloneJson(scope, 0, { nodes: 0, units: 0 }, new Set());
      if (!validScope(checked)) return { status: 'invalid', reason: 'scope-schema' };
      const entries = this.scan(checked); if (entries.length === 0) return { status: 'missing' };
      const records = new Map(entries.map(entry => [entry.value.recordId, entry]));
      const parents = new Set<string>();
      for (const entry of entries) {
        const visited = new Set<string>(); let at: Entry | undefined = entry;
        while (at) {
          if (visited.has(at.value.recordId)) return { status: 'invalid', reason: 'cyclic-lineage' };
          visited.add(at.value.recordId);
          const parent: string | null = at.value.parentRecordId;
          if (parent === null) break;
          parents.add(parent); at = records.get(parent);
          if (!at) return { status: 'invalid', reason: 'incomplete-lineage' };
        }
      }
      const tips = entries.filter(entry => !parents.has(entry.value.recordId));
      if (tips.length !== 1) return { status: 'ambiguous', recordIds: tips.map(entry => entry.value.recordId) };
      const entry = tips[0]; return { status: 'found', snapshot: entry.value.snapshot,
        handle: { storageKey: entry.key, recordId: entry.value.recordId, raw: entry.raw } };
    } catch (error) { return { status: error instanceof InvalidRecord ? 'invalid' : 'unavailable', reason: error instanceof InvalidRecord ? error.message : 'storage-unavailable' }; }
  }
  /** Explicit ownerless inspection only. Never auto-load, bind, remove or rewrite. */
  readLegacyLine(lineId: string): { status: 'missing' | 'unavailable'; reason?: string } | { status: 'ownerless'; raw: Readonly<Record<string, string>> } {
    if (this.storage === null || !text(lineId)) return { status: 'unavailable', reason: 'legacy-unavailable' };
    try {
      const raw: Record<string, string> = {}; let total = 0;
      for (const suffix of ['', ':recovery', ':base']) {
        const key = `leviathan:draft:${lineId}${suffix}`, value = this.storage.getItem(key);
        if (value !== null) { total += new TextEncoder().encode(value).length; if (total > WorkspaceDraftLimits.aggregateBytes) return { status: 'unavailable', reason: 'legacy-too-large' }; raw[key] = value; }
      }
      return Object.keys(raw).length ? { status: 'ownerless', raw } : { status: 'missing' };
    } catch { return { status: 'unavailable', reason: 'legacy-unavailable' }; }
  }
  private scan(scope: WorkspaceDraftScope): Entry[] {
    const storage = this.storage!;
    const count = storage.length;
    if (!Number.isSafeInteger(count) || count < 0 || count > WorkspaceDraftLimits.storageKeys) invalid('storage-key-budget');
    const prefix = scopeKey(scope), entries: Entry[] = [], allKeys: string[] = []; let total = 0;
    for (let i = 0; i < count; i++) {
      const key = storage.key(i); if (key === null) invalid('storage-changed');
      allKeys.push(key);
      if (!key.startsWith(prefix)) continue;
      if (entries.length >= WorkspaceDraftLimits.recordsPerScope) invalid('scope-record-budget');
      const raw = storage.getItem(key); if (raw === null) invalid('storage-changed');
      boundedRaw(raw); total += new TextEncoder().encode(raw).length;
      if (total > WorkspaceDraftLimits.aggregateBytes) invalid('scope-byte-budget');
      const value = parse(raw);
      if (!sameScope(value.snapshot.scope, scope) || key !== prefix + value.recordId) invalid('scope-key-mismatch');
      entries.push({ key, raw, value });
    }
    if (storage.length !== count) invalid('storage-changed');
    for (let i = 0; i < count; i++) if (storage.key(i) !== allKeys[i]) invalid('storage-changed');
    for (const entry of entries) if (storage.getItem(entry.key) !== entry.raw) invalid('storage-changed');
    return entries;
  }
}
