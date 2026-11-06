import { WorkspaceDraftStore } from './workspace-draft-store';
import type { WorkspaceDraftScope, WorkspaceDraftSnapshot, WorkspaceDraftStorage } from './workspace-access-types';

const scope: WorkspaceDraftScope = { workspaceNamespace: '11111111-1111-4111-8111-111111111111',
  accountId: 'generated-account', projectId: 'generated-project', surfaceId: 'generated-surface', lineId: 'generated-line', layer: 'L1Diplomatic' };
const prefix = 'leviathan:workspace-draft:v1:' + encodeURIComponent(JSON.stringify([
  scope.workspaceNamespace, scope.accountId, scope.projectId, scope.surfaceId, scope.lineId, scope.layer])) + ':';
function snapshot(id: string): WorkspaceDraftSnapshot {
  return { schemaVersion: 1, scope: { ...scope }, base: { revisionId: 'generated-base', text: 'α' }, draftText: id,
    frame: null, undo: null, geometry: null, readingOrder: null, unconfirmedWrites: [] };
}
class Storage implements WorkspaceDraftStorage {
  readonly values: Map<string, string>; private readonly names: string[];
  constructor(rows: [string, string][]) { this.values = new Map(rows); this.names = [...this.values.keys()]; }
  get length(): number { return this.names.length; }
  key(index: number): string | null { return this.names[index] ?? null; }
  getItem(key: string): string | null { return this.values.get(key) ?? null; }
  setItem(key: string, value: string): void { if (!this.values.has(key)) this.names.push(key); this.values.set(key, value); }
}
type Parent = number | null | 'missing';
function rows(parents: Parent[], reverse = false): [string, string][] {
  const result: [string, string][] = parents.map((parent, index) => [prefix + 'r' + index, JSON.stringify({ version: 1,
    recordId: 'r' + index, parentRecordId: parent === null ? null : parent === 'missing' ? 'unknown' : 'r' + parent,
    snapshot: snapshot('r' + index) })]);
  return reverse ? result.reverse() : result;
}
/** Independent oracle removes childless nodes, instead of walking ancestor chains. */
function classify(parents: Parent[]): { status: string; tips?: string[] } {
  if (!parents.length) return { status: 'missing' };
  if (parents.some((parent, index) => parent === index || parent === 'missing')) return { status: 'invalid' };
  const children = Array<number>(parents.length).fill(0);
  for (const parent of parents) if (typeof parent === 'number') children[parent]++;
  const tips = children.flatMap((count, index) => count === 0 ? [index] : []); const pending = [...tips]; let removed = 0;
  while (pending.length) {
    const index = pending.pop()!; removed++; const parent = parents[index];
    if (typeof parent === 'number' && --children[parent] === 0) pending.push(parent);
  }
  return removed !== parents.length ? { status: 'invalid' } : { status: tips.length === 1 ? 'found' : 'ambiguous', tips: tips.map(index => 'r' + index).sort() };
}

describe('WorkspaceDraftStore complete lineage semantics', () => {
  for (let size = 0; size <= 4; size++) {
    it(`classifies every ${size}-node parent assignment independently in both storage orders`, () => {
      const parents: Parent[] = Array(size), choices: Parent[] = [null, 'missing', ...Array.from({ length: size }, (_, index) => index)]; let checked = 0;
      const enumerate = (at: number) => {
        if (at !== size) { for (const parent of choices) { parents[at] = parent; enumerate(at + 1); } return; }
        const expected = classify(parents);
        for (const reverse of [false, true]) {
          const data = rows(parents, reverse), storage = new Storage(data); const actual = new WorkspaceDraftStore(storage).load(scope);
          const label = JSON.stringify({ parents, reverse });
          if (actual.status !== expected.status) throw new Error(`Lineage classification ${label}: ${actual.status}, expected ${expected.status}`);
          if (actual.status === 'found' && (actual.handle.recordId !== expected.tips![0] || actual.snapshot.draftText !== expected.tips![0])) throw new Error(`Wrong unique tip ${label}`);
          if (actual.status === 'ambiguous' && JSON.stringify([...actual.recordIds].sort()) !== JSON.stringify(expected.tips)) throw new Error(`Wrong branches ${label}`);
          if (JSON.stringify([...storage.values]) !== JSON.stringify(data)) throw new Error(`Read mutated original bytes ${label}`);
          checked++;
        }
      };
      enumerate(0); expect(checked).toBe(2 * choices.length ** size);
    });
  }
  const chain = () => Array.from({ length: 512 }, (_, index): Parent => index === 0 ? null : index - 1);
  for (const reverse of [false, true]) {
    it(`preserves the exact 512-record chain tip and all bytes (${reverse ? 'reverse' : 'forward'} enumeration)`, () => {
      const data = rows(chain(), reverse), storage = new Storage(data), loaded = new WorkspaceDraftStore(storage).load(scope);
      expect(loaded.status).toBe('found');
      if (loaded.status === 'found') { expect(loaded.handle.recordId).toBe('r511'); expect(loaded.snapshot).toEqual(snapshot('r511')); expect(loaded.handle.raw).toBe(storage.getItem(loaded.handle.storageKey)!); }
      expect([...storage.values]).toEqual(data);
    });
  }
  for (const mode of ['cycle', 'missing', 'branch'] as const) {
    it(`refuses or quarantines a full journal with ${mode} without selecting an arbitrary tip`, () => {
      const parents = chain(); if (mode === 'cycle') parents[0] = 511; else if (mode === 'missing') parents[0] = 'missing'; else parents[511] = 0;
      const data = rows(parents), storage = new Storage(data), loaded = new WorkspaceDraftStore(storage).load(scope);
      expect(loaded.status).toBe(mode === 'branch' ? 'ambiguous' : 'invalid');
      if (loaded.status === 'ambiguous') expect([...loaded.recordIds].sort()).toEqual(['r510', 'r511']);
      if (loaded.status === 'invalid') expect(loaded.reason).toBe(mode === 'cycle' ? 'cyclic-lineage' : 'incomplete-lineage');
      expect([...storage.values]).toEqual(data);
    });
  }
  it('isolates returned snapshots and refuses capacity without deleting old records or minting an unused ID', () => {
    const data = rows(chain()), storage = new Storage(data); let allocated = 0;
    const store = new WorkspaceDraftStore(storage, () => { allocated++; return 'new-record'; }); const loaded = store.load(scope);
    expect(loaded.status).toBe('found'); if (loaded.status !== 'found') return;
    (loaded.snapshot.base as { text: string }).text = 'caller mutation';
    const again = store.load(scope); expect(again.status).toBe('found'); if (again.status !== 'found') return;
    expect(again.snapshot).toEqual(snapshot('r511')); expect(store.save(snapshot('next'), again.handle)).toEqual({ ok: false, reason: 'journal-full' });
    expect(allocated).toBe(0); expect([...storage.values]).toEqual(data);
  });
});
