import { WorkspaceDraftStore, WorkspaceDraftLimits } from './workspace-draft-store';
import type { WorkspaceDraftHandle, WorkspaceDraftScope, WorkspaceDraftSnapshot, WorkspaceDraftStorage } from './workspace-access-types';

class MemoryStorage implements WorkspaceDraftStorage {
  readonly values = new Map<string, string>();
  get length(): number { return this.values.size; }
  key(index: number): string | null { return [...this.values.keys()][index] ?? null; }
  getItem(key: string): string | null { return this.values.get(key) ?? null; }
  setItem(key: string, raw: string): void { this.values.set(key, raw); }
}
const SCOPE: WorkspaceDraftScope = { workspaceNamespace: '11111111-1111-4111-8111-111111111111', accountId: 'account-a', projectId: 'col-a', surfaceId: 'surf-a', lineId: 'ln-a', layer: 'L1Diplomatic' };
function snapshot(scope = SCOPE): WorkspaceDraftSnapshot {
  return { schemaVersion: 1, scope: { ...scope }, base: { revisionId: 'rev-base', text: 'generated αβ' }, draftText: 'generated αγβ',
    frame: { captureId: 'cap-a', frameRef: 'cap-a', checksum: { algorithm: 'sha256', digest: 'a'.repeat(64) }, width: 100, height: 80 },
    undo: { version: 1, kind: 'line-editor.undo', data: { entries: ['generated αβ', 'generated α'] } },
    geometry: { version: 1, kind: 'geometry-editor', data: { draft: [{ x: 1, y: 2 }], expected: [{ x: 0, y: 2 }], provisional: true } },
    readingOrder: { version: 1, kind: 'reading-order', data: { draft: ['ln-b', 'ln-a'], expected: ['ln-a', 'ln-b'] } },
    unconfirmedWrites: [{ version: 1, kind: 'line-save', data: { requestId: 'generated-write', submittedText: 'generated αγβ', baseRevisionId: 'rev-base' } }],
  };
}

describe('WorkspaceDraftStore', () => {
  let storage: MemoryStorage, store: WorkspaceDraftStore, sequence: number;
  beforeEach(() => { storage = new MemoryStorage(); sequence = 0; store = new WorkspaceDraftStore(storage, () => `record-${++sequence}`); });
  function save(value = snapshot(), previous?: WorkspaceDraftHandle): WorkspaceDraftHandle {
    const result = store.save(value, previous); if (!result.ok) throw Error(result.reason); return result.handle;
  }
  it('atomically round-trips true base, text, undo, frame, geometry, order and unconfirmed writes', () => {
    const input = snapshot(); const write = spyOn(storage, 'setItem').and.callThrough(); const handle = save(input);
    expect(write).toHaveBeenCalledTimes(1); const loaded = store.load(SCOPE); expect(loaded.status).toBe('found');
    if (loaded.status === 'found') { expect(loaded.snapshot).toEqual(input); expect(loaded.handle).toEqual(handle); }
    expect(handle.raw).not.toContain('generated-key');
  });
  it('preserves earlier immutable bytes while loading the sole linear journal tip', () => {
    const first = save(); const next = { ...snapshot(), draftText: 'generated next' }; const second = save(next, first);
    expect(storage.getItem(first.storageKey)).toBe(first.raw); expect(storage.getItem(second.storageKey)).toBe(second.raw);
    const loaded = store.load(SCOPE); expect(loaded.status).toBe('found'); if (loaded.status === 'found') expect(loaded.snapshot.draftText).toBe('generated next');
  });
  it('fails closed on concurrent branches instead of choosing a timestamp/latest record', () => {
    const first = save(); const left = save({ ...snapshot(), draftText: 'left' }, first);
    const branch = JSON.parse(left.raw); branch.recordId = 'injected-right-branch'; branch.snapshot.draftText = 'right';
    storage.setItem(left.storageKey.slice(0, -left.recordId.length) + branch.recordId, JSON.stringify(branch));
    const loaded = store.load(SCOPE); expect(loaded.status).toBe('ambiguous'); if (loaded.status === 'ambiguous') expect([...loaded.recordIds].sort()).toEqual([left.recordId, branch.recordId].sort());
    expect(storage.getItem(first.storageKey)).toBe(first.raw);
  });
  it('treats separate root records for one exact scope as ambiguous', () => {
    const first = save(); const other = JSON.parse(first.raw); other.recordId = 'injected-other-root';
    storage.setItem(first.storageKey.slice(0, -first.recordId.length) + other.recordId, JSON.stringify(other));
    expect(store.load(SCOPE).status).toBe('ambiguous');
  });
  for (const field of ['workspaceNamespace', 'accountId', 'projectId', 'surfaceId', 'lineId', 'layer'] as const)
    it(`never falls back across ${field}`, () => {
      save(); const other = { ...SCOPE, [field]: field === 'workspaceNamespace' ? '22222222-2222-4222-8222-222222222222' : 'different-generated-value' };
      expect(store.load(other).status).toBe('missing');
    });
  it('rejects previous handles belonging to a different scope/owner', () => {
    const old = save(); const result = store.save(snapshot({ ...SCOPE, accountId: 'account-b' }), old);
    expect(result).toEqual({ ok: false, reason: 'previous-record-changed' }); expect(storage.getItem(old.storageKey)).toBe(old.raw);
  });
  it('does not bind changed old bytes to a newer draft', () => {
    const old = save(); storage.values.set(old.storageKey, old.raw.replace('generated αγβ', 'externally changed'));
    const result = store.save({ ...snapshot(), draftText: 'new' }, old); expect(result.ok).toBeFalse(); expect(storage.length).toBe(1);
  });
  it('returns failure on quota exceptions without touching the prior or mounted copy', () => {
    const old = save(); const mounted = snapshot(); spyOn(storage, 'setItem').and.throwError('generated quota');
    expect(store.save(mounted, old).ok).toBeFalse(); expect(storage.getItem(old.storageKey)).toBe(old.raw); expect(mounted.draftText).toBe('generated αγβ');
  });
  it('never reports success when readback does not verify the atomic record', () => {
    const old = save(); const real = storage.getItem.bind(storage);
    spyOn(storage, 'getItem').and.callFake(key => key === old.storageKey ? real(key) : storage.values.has(key) ? 'generated wrong readback' : null);
    const result = store.save({ ...snapshot(), draftText: 'new' }, old); expect(result).toEqual({ ok: false, reason: 'write-not-verified' }); expect(real(old.storageKey)).toBe(old.raw);
  });
  it('does not lose the old copy if storage commits a complete new record then throws', () => {
    const old = save(); spyOn(storage, 'setItem').and.callFake((key, raw) => { storage.values.set(key, raw); throw Error('generated after-write fault'); });
    expect(store.save({ ...snapshot(), draftText: 'new' }, old).ok).toBeFalse(); expect(storage.getItem(old.storageKey)).toBe(old.raw);
  });
  it('leaves all ownerless legacy raw/base/recovery bytes untouched and never loads them as owned', () => {
    const original = { 'leviathan:draft:ln-a': 'generated legacy draft', 'leviathan:draft:ln-a:base': 'generated stale base', 'leviathan:draft:ln-a:recovery': '{not JSON and not assigned an owner}' };
    for (const [key, raw] of Object.entries(original)) storage.setItem(key, raw);
    expect(store.load(SCOPE).status).toBe('missing'); const inspected = store.readLegacyLine('ln-a'); expect(inspected).toEqual({ status: 'ownerless', raw: original });
    save(); for (const [key, raw] of Object.entries(original)) expect(storage.getItem(key)).toBe(raw);
  });
  for (const field of ['base', 'scope'] as const) it(`rejects missing ${field} rather than inventing a recovery ancestor/owner`, () => {
    const input = { ...snapshot(), [field]: undefined } as unknown as WorkspaceDraftSnapshot; expect(store.save(input).ok).toBeFalse(); expect(storage.length).toBe(0);
  });
  it('accepts a genuinely known empty base, keeping it separate from an unknown base', () => {
    const input = { ...snapshot(), base: { revisionId: null, text: '' } }; save(input); const loaded = store.load(SCOPE);
    expect(loaded.status).toBe('found'); if (loaded.status === 'found') expect(loaded.snapshot.base).toEqual({ revisionId: null, text: '' });
  });
  for (const owner of ['', '00000000-0000-0000-0000-000000000000', 'NOT-A-NAMESPACE']) it(`rejects missing/invalid namespace ${owner}`, () => {
    expect(store.save(snapshot({ ...SCOPE, workspaceNamespace: owner })).ok).toBeFalse(); expect(storage.length).toBe(0);
  });
  it('rejects functions, arbitrary prototypes, cycles and nonfinite opaque values', () => {
    const cycle: Record<string, unknown> = {}; cycle['self'] = cycle;
    for (const data of [() => 1, new Date(), cycle, { x: Infinity }, { x: undefined }]) {
      const input = { ...snapshot(), geometry: { version: 1, kind: 'geometry', data } } as unknown as WorkspaceDraftSnapshot;
      expect(store.save(input).ok).toBeFalse();
    }
    expect(storage.length).toBe(0);
  });
  it('rejects accessor/toJSON and credential fields without invoking them', () => {
    let invoked = 0; const getter = Object.defineProperty({}, 'x', { enumerable: true, get: () => { invoked++; return 1; } });
    for (const data of [getter, { toJSON: () => { invoked++; return {}; } }, { credential: 'generated secret' }, { apiKey: 'generated secret' }, JSON.parse('{"__proto__":{"bad":true}}')]) {
      expect(store.save({ ...snapshot(), undo: { version: 1, kind: 'undo', data } } as unknown as WorkspaceDraftSnapshot).ok).toBeFalse();
    }
    expect(invoked).toBe(0); expect(storage.length).toBe(0);
  });
  it('refuses malformed/extra stored fields instead of silently ignoring them', () => {
    const old = save(); const entry = JSON.parse(old.raw); entry.credential = 'generated secret'; storage.values.set(old.storageKey, JSON.stringify(entry));
    expect(store.load(SCOPE).status).toBe('invalid'); expect(storage.getItem(old.storageKey)).toContain('generated secret');
  });
  it('rejects cycles and missing parent records without deleting any entry', () => {
    const a = save(), b = save(snapshot(), a); const first = JSON.parse(a.raw); first.parentRecordId = b.recordId; storage.values.set(a.storageKey, JSON.stringify(first));
    expect(store.load(SCOPE).status).toBe('invalid'); storage.values.delete(a.storageKey); expect(store.load(SCOPE).status).toBe('invalid'); expect(storage.getItem(b.storageKey)).toBe(b.raw);
  });
  it('validates the exact scope inside the record against the namespaced key', () => {
    const old = save(); const value = JSON.parse(old.raw); value.snapshot.scope.accountId = 'account-b'; storage.values.set(old.storageKey, JSON.stringify(value));
    expect(store.load(SCOPE).status).toBe('invalid');
  });
  it('bounds ALL untrusted storage-key scanning before reading matching records', () => {
    const huge: WorkspaceDraftStorage = { length: WorkspaceDraftLimits.storageKeys + 1, key: jasmine.createSpy('key'), getItem: jasmine.createSpy('get'), setItem: jasmine.createSpy('set') };
    expect(new WorkspaceDraftStore(huge).load(SCOPE).status).toBe('invalid'); expect(huge.key).not.toHaveBeenCalled(); expect(huge.getItem).not.toHaveBeenCalled();
  });
  it('fails safely on too-large/deep records before parsing/recovery', () => {
    const old = save(); storage.values.set(old.storageKey, '['.repeat(1000) + '0' + ']'.repeat(1000)); expect(store.load(SCOPE).status).toBe('invalid');
    storage.values.set(old.storageKey, ' '.repeat(WorkspaceDraftLimits.recordBytes + 1)); expect(store.load(SCOPE).status).toBe('invalid');
  });
  it('returns unavailable when browser storage fails, preserving the caller snapshot', () => {
    const input = snapshot(); expect(new WorkspaceDraftStore(null).save(input).ok).toBeFalse(); expect(new WorkspaceDraftStore(null).load(SCOPE).status).toBe('unavailable'); expect(input.base.text).toBe('generated αβ');
  });
  it('rejects record-id collisions instead of overwriting immutable evidence', () => {
    const fixed = new WorkspaceDraftStore(storage, () => 'fixed-generated-id'); const first = fixed.save(snapshot()); expect(first.ok).toBeTrue(); const raw = [...storage.values.values()][0];
    expect(fixed.save({ ...snapshot(), draftText: 'next' }, first.ok ? first.handle : undefined).ok).toBeFalse(); expect([...storage.values.values()][0]).toBe(raw);
  });
  it('reports bounded journal exhaustion explicitly without deleting old data', () => {
    const old = save(); const large = { ...snapshot(), draftText: 'x'.repeat(WorkspaceDraftLimits.recordBytes) };
    expect(store.save(large, old).ok).toBeFalse(); expect(storage.getItem(old.storageKey)).toBe(old.raw);
  });
  it('refuses a stale parent handle after a newer child is already the unique tip', () => {
    const first = save(); const second = save({ ...snapshot(), draftText: 'newer child' }, first);
    expect(store.save({ ...snapshot(), draftText: 'stale caller' }, first).ok).toBeFalse();
    expect(storage.length).toBe(2); expect(storage.getItem(first.storageKey)).toBe(first.raw); expect(storage.getItem(second.storageKey)).toBe(second.raw);
  });
  it('requires the existing unique handle rather than claiming success for a second root', () => {
    const first = save(); expect(store.save({ ...snapshot(), draftText: 'unrelated root' }).ok).toBeFalse();
    expect(storage.length).toBe(1); expect(storage.getItem(first.storageKey)).toBe(first.raw);
  });
  it('reports failure if a concurrent branch appears between append and recovery verification', () => {
    const first = save(); const prefix = first.storageKey.slice(0, -first.recordId.length);
    spyOn(storage, 'setItem').and.callFake((key, raw) => {
      storage.values.set(key, raw); const branch = JSON.parse(raw); branch.recordId = 'concurrent-generated-branch';
      branch.snapshot.draftText = 'concurrent work'; storage.values.set(prefix + branch.recordId, JSON.stringify(branch));
    });
    expect(store.save({ ...snapshot(), draftText: 'local work' }, first).ok).toBeFalse();
    expect(store.load(SCOPE).status).toBe('ambiguous'); expect(storage.getItem(first.storageKey)).toBe(first.raw); expect(storage.length).toBe(3);
  });

  it('refuses the next save at the explicit512-record bound without compacting old bytes', () => {
    const first = save(); const prefix = first.storageKey.slice(0, -first.recordId.length); let parent = first.recordId;
    for (let i = 2; i <= WorkspaceDraftLimits.recordsPerScope; i++) {
      const value = JSON.parse(first.raw); value.recordId = `injected-${i}`; value.parentRecordId = parent;
      storage.setItem(prefix + value.recordId, JSON.stringify(value)); parent = value.recordId;
    }
    const current = store.load(SCOPE); expect(current.status).toBe('found');
    if (current.status === 'found') expect(store.save(snapshot(), current.handle)).toEqual({ ok: false, reason: 'journal-full' });
    expect(storage.length).toBe(WorkspaceDraftLimits.recordsPerScope); expect(storage.getItem(first.storageKey)).toBe(first.raw);
  });
  it('bounds total scoped bytes even when every individual record is below the per-record cap', () => {
    const first = save(); const prefix = first.storageKey.slice(0, -first.recordId.length);
    for (let i = 0; i < 11; i++) { const value = JSON.parse(first.raw); value.recordId = `large-${i}`; value.snapshot.draftText = 'x'.repeat(400_000); storage.setItem(prefix + value.recordId, JSON.stringify(value)); }
    expect(store.load(SCOPE)).toEqual({ status: 'invalid', reason: 'scope-byte-budget' }); expect(storage.getItem(first.storageKey)).toBe(first.raw); expect(storage.length).toBe(12);
  });
  it('detects a source record changing while a bounded lookup is in progress', () => {
    const first = save(); let reads = 0; spyOn(storage, 'getItem').and.callFake(key => key === first.storageKey && ++reads > 1 ? first.raw + ' ' : storage.values.get(key) ?? null);
    expect(store.load(SCOPE)).toEqual({ status: 'invalid', reason: 'storage-changed' }); expect(storage.values.get(first.storageKey)).toBe(first.raw);
  });
  it('does not choose a valid-looking duplicate-key lookup as a unique recovery', () => {
    const first = save(); const duplicate: WorkspaceDraftStorage = { length: 2, key: () => first.storageKey, getItem: () => first.raw, setItem: () => {} };
    expect(new WorkspaceDraftStore(duplicate).load(SCOPE).status).toBe('ambiguous');
  });

});
