import { BoundedSseParser, JobStreamEvent, openHeaderJobStream } from './header-job-stream';

const encoded = (text: string) => new TextEncoder().encode(text);
const streamResponse = (body: ReadableStream<Uint8Array>) => new Response(body, { status: 200, headers: { 'Content-Type': 'text/event-stream; charset=utf-8' } });
const drain = async () => { for (let i = 0; i < 24; i++) await Promise.resolve(); await new Promise<void>(resolve => setTimeout(resolve, 0)); };
class Clock {
  private ordinal = 0;
  private now = 0;
  readonly pending = new Map<number, { callback: () => void; ms: number; due: number }>();
  setTimeout = (callback: () => void, ms: number) => { const id = ++this.ordinal; this.pending.set(id, { callback, ms, due: this.now + ms }); return id; };
  clearTimeout = (handle: unknown) => { this.pending.delete(handle as number); };
  fire(ms: number) {
    const next = [...this.pending].reverse().find(([, timer]) => timer.ms === ms);
    if (!next) throw Error('Expected registered timer ' + ms);
    this.pending.delete(next[0]); this.now = next[1].due; next[1].callback();
  }
  advance(milliseconds: number) {
    const end = this.now + milliseconds;
    while (true) {
      const next = [...this.pending].filter(([, timer]) => timer.due <= end).sort((a, b) => a[1].due - b[1].due)[0];
      if (!next) break;
      this.pending.delete(next[0]); this.now = next[1].due; next[1].callback();
    }
    this.now = end;
  }

}

describe('Independent bounded SSE parser', () => {
  it('preserves UTF-8, CRLF, multiline data and dispatch identity at every single byte split', () => {
    const bytes = encoded('\ufeff:generated\r\nid: 17\r\nevent: job.progress\r\ndata: αβ\r\ndata: second\r\n\r\n');
    for (let split = 0; split <= bytes.length; split++) {
      const events: JobStreamEvent[] = []; const parser = new BoundedSseParser(event => events.push(event));
      parser.feed(bytes.slice(0, split)); parser.feed(bytes.slice(split)); parser.end();
      expect(events).withContext('byte split ' + split).toEqual([{ type: 'job.progress', data: 'αβ\nsecond', id: '17' }]);
    }
  });
  for (const separator of ['\n', '\r\n', '\r']) it('dispatches default message with ' + JSON.stringify(separator), () => {
    const events: JobStreamEvent[] = []; const parser = new BoundedSseParser(event => events.push(event));
    for (const byte of encoded('data:value' + separator + separator)) parser.feed(new Uint8Array([byte]));
    parser.end(); expect(events).toEqual([{ type: 'message', data: 'value', id: '' }]);
  });
  it('ignores comments and unknown fields without inventing events', () => {
    const events: JobStreamEvent[] = []; const parser = new BoundedSseParser(event => events.push(event));
    parser.feed(encoded(':keepalive\nunknown: value\nid: 9\n\n')); parser.end(); expect(events).toEqual([]);
  });
  it('retains a valid id across missing and NUL ids and clears it on an empty dispatched id', () => {
    const events: JobStreamEvent[] = []; const parser = new BoundedSseParser(event => events.push(event));
    parser.feed(encoded('id: 3\ndata:a\n\ndata:b\n\nid: invalid\0id\ndata:c\n\nid:\ndata:d\n\n')); parser.end();
    expect(events.map(event => event.id)).toEqual(['3', '3', '3', '']);
  });
  it('dispatches an explicitly empty data field but discards an incomplete EOF frame', () => {
    const events: JobStreamEvent[] = []; const parser = new BoundedSseParser(event => events.push(event));
    parser.feed(encoded('data:\n\ndata:UNCOMMITTED')); parser.end();
    expect(events).toEqual([{ type: 'message', data: '', id: '' }]);
  });
  it('does not strip a second BOM into a valid field name', () => {
    const events: JobStreamEvent[] = []; const parser = new BoundedSseParser(event => events.push(event));
    parser.feed(encoded('\ufeffdata:first\n\n\ufeffdata:ignored\n\n')); parser.end();
    expect(events.map(event => event.data)).toEqual(['first']);
  });
  it('rejects malformed UTF-8 rather than emitting repaired text', () => {
    const parser = new BoundedSseParser(() => fail('Invalid UTF-8 event dispatched'));
    expect(() => parser.feed(new Uint8Array([0x64, 0x61, 0x74, 0x61, 0x3a, 0xc0, 0xaf]))).toThrow();
  });
  it('rejects incomplete UTF-8 at EOF', () => {
    const parser = new BoundedSseParser(() => fail('Incomplete UTF-8 event dispatched'));
    parser.feed(new Uint8Array([0xe1, 0xbc])); expect(() => parser.end()).toThrow();
  });
  it('rejects oversized line, event and read independently', () => {
    expect(() => new BoundedSseParser(() => {}).feed(encoded('data:' + 'x'.repeat(65537)))).toThrow();
    expect(() => new BoundedSseParser(() => {}).feed(encoded(('data:' + 'x'.repeat(40000) + '\n').repeat(8)))).toThrow();
    expect(() => new BoundedSseParser(() => {}).feed(new Uint8Array(1048577))).toThrow();
  });
  it('validates decimal retries and clamps finite admitted values', () => {
    const retries: number[] = []; const parser = new BoundedSseParser(() => {}, value => retries.push(value));
    parser.feed(encoded('retry: 0\nretry: 500\nretry: 999999\nretry: -1\nretry: 1e3\nretry: 999999999999999999999999\n\n')); parser.end();
    expect(retries).toEqual([250, 500, 30000]);
  });
});

describe('Independent header-authenticated job transport', () => {
  it('pins header credential with no URL/cookie credential and cancels/releases the owned reader', async () => {
    let controller!: ReadableStreamDefaultController<Uint8Array>; let cancelled = 0;
    const body = new ReadableStream<Uint8Array>({ start(value) { controller = value; }, cancel() { cancelled++; } });
    const calls: { input: RequestInfo | URL; init?: RequestInit }[] = [];
    const fetcher: typeof fetch = async (input, init) => { calls.push({ input, init }); return streamResponse(body); };
    const events: JobStreamEvent[] = [];
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-header-secret-a', { current: () => true, onEvent: event => events.push(event), fetch: fetcher });
    try {
      await drain(); expect(calls.length).toBe(1); const headers = new Headers(calls[0].init?.headers);
      expect(headers.get('X-Api-Key')).toBe('generated-header-secret-a'); expect(headers.get('Accept')).toBe('text/event-stream');
      expect(calls[0].init?.credentials).toBe('omit'); expect(String(calls[0].input)).not.toContain('generated-header-secret-a'); expect(String(calls[0].input)).not.toContain('api_key');
      controller.enqueue(encoded('id: 1\nevent: job.progress\ndata:generated\n\n')); await drain(); expect(events.length).toBe(1);
    } finally { stream.close(); await stream.settled; }
    expect(cancelled).toBe(1); expect(body.locked).toBeFalse();
  });
  it('omits an anonymous header and exposes no private fetch-error message in notices', async () => {
    const clock = new Clock(); const notices: unknown[] = []; let headers!: Headers;
    const fetcher: typeof fetch = async (_input, init) => { headers = new Headers(init?.headers); throw Error('generated-private-provider-message'); };
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', '', { current: () => true, onEvent: () => {}, onState: notice => notices.push(notice), fetch: fetcher, clock });
    await drain(); stream.close(); await stream.settled;
    expect(headers.has('X-Api-Key')).toBeFalse(); expect(JSON.stringify(notices)).not.toContain('generated-private-provider-message'); expect(clock.pending.size).toBe(0);
  });
  it('closes and releases its reader if the advisory consumer throws', async () => {
    const clock = new Clock(); let cancelled = 0;
    const response = streamResponse(new ReadableStream({ start(c) { c.enqueue(encoded('data:generated\n\n')); }, cancel() { cancelled++; } }));
    const fetcher = jasmine.createSpy('fetch').and.resolveTo(response);
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => { throw Error('generated-consumer-fault'); }, fetch: fetcher, clock });
    await expectAsync(stream.settled).toBeRejected(); expect(fetcher).toHaveBeenCalledTimes(1); expect(cancelled).toBe(1); expect(response.body!.locked).toBeFalse(); expect(clock.pending.size).toBe(0);
  });
  it('keeps the cleanup deadline alive when close interrupts a transient response body cancellation', async () => {
    const clock = new Clock(); let releaseCancel!: () => void; let cancelling = 0; let settled = false;
    const cancellation = new Promise<void>(resolve => { releaseCancel = resolve; });
    const body = new ReadableStream<Uint8Array>({ cancel() { cancelling++; return cancellation; } });
    const fetcher = jasmine.createSpy('fetch').and.resolveTo(new Response(body, { status: 503 }));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => {}, fetch: fetcher, clock });
    void stream.settled.then(() => { settled = true; }, () => { settled = true; });
    try {
      await drain(); expect(cancelling).toBe(1); stream.close(); clock.advance(1000);
      await drain(); expect(settled).toBeTrue(); expect(body.locked).toBeFalse(); expect(fetcher).toHaveBeenCalledTimes(1);
    } finally { releaseCancel(); stream.close(); await stream.settled; }
  });
  it('settles close while fetch ignores abort and cancels a response arriving after settlement', async () => {
    const clock = new Clock(); let resolveFetch!: (response: Response) => void; let cancelled = 0;
    const fetcher: typeof fetch = () => new Promise(resolve => { resolveFetch = resolve; });
    const events: JobStreamEvent[] = [];
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: event => events.push(event), fetch: fetcher, clock });
    await drain(); stream.close(); await stream.settled;
    const late = new ReadableStream<Uint8Array>({ start(c) { c.enqueue(encoded('data:OLD\n\n')); }, cancel() { cancelled++; } });
    resolveFetch(streamResponse(late)); await drain();
    expect(events).toEqual([]); expect(cancelled).toBe(1); expect(late.locked).toBeFalse(); expect(clock.pending.size).toBe(0);
  });
  it('bounds header admission and permits close during retry without another fetch', async () => {
    const clock = new Clock(); let signal!: AbortSignal;
    const fetcher = jasmine.createSpy('fetch').and.callFake((_input: RequestInfo | URL, init?: RequestInit) => { signal = init!.signal!; return new Promise<Response>(() => {}); });
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => fail('No admitted response'), fetch: fetcher, clock });
    await drain(); clock.fire(45000); await drain(); expect(signal.aborted).toBeTrue();
    stream.close(); await stream.settled; expect(fetcher).toHaveBeenCalledTimes(1); expect(clock.pending.size).toBe(0);
  });
  it('keeps a healthy quiet response open beyond the old body deadline without reconnecting', async () => {
    const clock = new Clock(); let cancelled = 0;
    const body = new ReadableStream<Uint8Array>({ cancel() { cancelled++; } });
    const fetcher = jasmine.createSpy('fetch').and.resolveTo(streamResponse(body));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => fail('Quiet event'), fetch: fetcher, clock });
    try {
      await drain(); clock.advance(46000); await drain();
      expect(stream.readyState).toBe('open'); expect(fetcher).toHaveBeenCalledTimes(1); expect(cancelled).toBe(0); expect(body.locked).toBeTrue();
    } finally { stream.close(); await stream.settled; }
  });
  it('still closes a quiet admitted body when the captured lifetime becomes stale', async () => {
    const clock = new Clock(); let current = true; let cancelled = 0;
    const body = new ReadableStream<Uint8Array>({ cancel() { cancelled++; } });
    const fetcher = jasmine.createSpy('fetch').and.resolveTo(streamResponse(body));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => current, onEvent: () => fail('Quiet event'), fetch: fetcher, clock });
    await drain(); clock.advance(46000); await drain(); expect(stream.readyState).toBe('open');
    current = false; clock.advance(1000); await stream.settled;
    expect(cancelled).toBe(1); expect(body.locked).toBeFalse(); expect(fetcher).toHaveBeenCalledTimes(1); expect(clock.pending.size).toBe(0);
  });
  for (const status of [401, 403]) it('reports known authorization denial on headers before waiting for a slow body ' + status, async () => {
    const clock = new Clock(); let releaseCancel!: () => void; let cancelled = 0;
    const cancellation = new Promise<void>(resolve => { releaseCancel = resolve; });
    const body = new ReadableStream<Uint8Array>({ cancel() { cancelled++; return cancellation; } });
    const notices: { state: string; reason?: string }[] = [];
    const fetcher = jasmine.createSpy('fetch').and.resolveTo(new Response(body, { status }));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => fail('Denied event'), onState: notice => notices.push(notice), fetch: fetcher, clock });
    try {
      await drain(); expect(cancelled).toBe(1);
      expect(notices.some(notice => notice.state === 'closed' && notice.reason === 'authorization')).toBeTrue();
    } finally { releaseCancel(); stream.close(); await stream.settled; }
  });
  it('never reports a late old-owner denial as current-owner authorization failure', async () => {
    const clock = new Clock(); let resolveFetch!: (response: Response) => void; let owner = 'A'; let invalidatedOwner: string | null = null;
    const notices: { reason?: string }[] = [];
    const fetcher: typeof fetch = () => new Promise(resolve => { resolveFetch = resolve; });
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => owner === 'A', onEvent: () => fail('Old owner event'), onState: notice => { notices.push(notice); if (notice.reason === 'authorization') invalidatedOwner = owner; }, fetch: fetcher, clock });
    await drain(); owner = 'B'; resolveFetch(new Response(null, { status: 403 })); await stream.settled;
    expect(invalidatedOwner).toBeNull(); expect(notices.some(notice => notice.reason === 'authorization')).toBeFalse(); expect(owner).toBe('B');
  });
  for (const callback of ['event', 'state'] as const) for (const value of [null, undefined])
    it('rejects the exact ' + String(value) + ' thrown by the ' + callback + ' consumer without retrying', async () => {
      const clock = new Clock(); let cancelled = 0; let settled = false; let rejected = false; let received: unknown = 'not-settled';
      const body = new ReadableStream<Uint8Array>({ start(c) { c.enqueue(encoded('data:generated\n\n')); }, cancel() { cancelled++; } });
      const fetcher = jasmine.createSpy('fetch').and.resolveTo(streamResponse(body));
      const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true,
        onEvent: () => { if (callback === 'event') throw value; }, onState: notice => { if (callback === 'state' && notice.state === 'open') throw value; }, fetch: fetcher, clock });
      void stream.settled.then(() => { settled = true; }, error => { settled = true; rejected = true; received = error; });
      try {
        await drain(); expect(settled).toBeTrue(); expect(rejected).toBeTrue(); expect(received).toBe(value);
        expect(fetcher).toHaveBeenCalledTimes(1); expect(body.locked).toBeFalse(); expect(cancelled).toBe(1); expect(clock.pending.size).toBe(0);
      } finally { stream.close(); await stream.settled.catch(() => {}); }
    });
  it('preserves the first consumer failure when the closed-state observer also throws', async () => {
    const clock = new Clock(); const first = 'generated-FIRST-consumer-value', second = 'generated-SECOND-close-value'; let received: unknown;
    const body = new ReadableStream<Uint8Array>({ start(c) { c.enqueue(encoded('data:generated\n\n')); } });
    const fetcher = jasmine.createSpy('fetch').and.resolveTo(streamResponse(body));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => { throw first; }, onState: notice => { if (notice.state === 'closed') throw second; }, fetch: fetcher, clock });
    try { await stream.settled; fail('Consumer failure unexpectedly resolved'); } catch (error) { received = error; }
    expect(received).toBe(first); expect(body.locked).toBeFalse(); expect(fetcher).toHaveBeenCalledTimes(1); expect(clock.pending.size).toBe(0);
  });
  it('cancels an unread denied response body even if its authorization observer throws', async () => {
    const clock = new Clock(); let cancelled = 0; const first = 'generated-denial-observer-fault'; let received: unknown;
    const body = new ReadableStream<Uint8Array>({ cancel() { cancelled++; } });
    const fetcher = jasmine.createSpy('fetch').and.resolveTo(new Response(body, { status: 403 }));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => fail('Denied event'), onState: notice => { if (notice.reason === 'authorization') throw first; }, fetch: fetcher, clock });
    try { await stream.settled; fail('Observer failure resolved'); } catch (error) { received = error; }
    try { expect(received).toBe(first); expect(cancelled).toBe(1); expect(body.locked).toBeFalse(); expect(clock.pending.size).toBe(0); }
    finally { await body.cancel(); }
  });
  it('never sends credentials to another origin or a credential-bearing URL', async () => {
    const fetcher = jasmine.createSpy('fetch').and.rejectWith(Error('Must not fetch'));
    for (const url of ['https://different.invalid/events', '/api/v1/jobs/job-x/events?api_key=generated-url-secret']) {
      let stream: ReturnType<typeof openHeaderJobStream> | undefined;
      try { stream = openHeaderJobStream(url, 'generated-a', { current: () => true, onEvent: () => fail('Unexpected event'), fetch: fetcher }); await stream.settled; }
      catch { /* Synchronous validation refusal is also safe. */ }
      finally { stream?.close(); }
    }
    expect(fetcher).not.toHaveBeenCalled();
  });
  it('suppresses a delayed response after captured lifetime expires and releases its body', async () => {
    let resolve!: (response: Response) => void; let current = true; let cancelled = 0;
    const body = new ReadableStream<Uint8Array>({ start(c) { c.enqueue(encoded('data:OLD\n\n')); }, cancel() { cancelled++; } });
    const fetcher: typeof fetch = () => new Promise(done => { resolve = done; });
    const events: JobStreamEvent[] = [];
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => current, onEvent: event => events.push(event), fetch: fetcher });
    await drain(); current = false; resolve(streamResponse(body)); await stream.settled;
    expect(events).toEqual([]); expect(cancelled).toBe(1); expect(body.locked).toBeFalse();
  });
  for (const status of [204, 401, 403, 404, 429]) it('does not reconnect after HTTP ' + status, async () => {
    const clock = new Clock(); const fetcher = jasmine.createSpy('fetch').and.resolveTo(new Response(null, { status }));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => fail('Denied event'), fetch: fetcher, clock });
    await stream.settled; expect(fetcher).toHaveBeenCalledTimes(1); expect(clock.pending.size).toBe(0);
  });
  it('refuses a connection whose accumulated bounded comment reads exceed the fixed total', async () => {
    const clock = new Clock(); const chunk = encoded((':x\n').repeat(10000)); let cancelled = 0;
    const body = new ReadableStream<Uint8Array>({ pull(c) { c.enqueue(chunk); }, cancel() { cancelled++; } });
    const fetcher = jasmine.createSpy('fetch').and.resolveTo(streamResponse(body));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => fail('Comments dispatched'), fetch: fetcher, clock });
    await stream.settled; expect(fetcher).toHaveBeenCalledTimes(1); expect(cancelled).toBe(1); expect(body.locked).toBeFalse(); expect(clock.pending.size).toBe(0);
  });
  it('does not reconnect after non-SSE content or an overflow', async () => {
    for (const response of [new Response('not events', { headers: { 'Content-Type': 'application/json' } }), streamResponse(new ReadableStream({ start(c) { c.enqueue(encoded('data:' + 'x'.repeat(65537))); } }))]) {
      const clock = new Clock(); const fetcher = jasmine.createSpy('fetch').and.resolveTo(response);
      const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => fail('Invalid event'), fetch: fetcher, clock });
      await stream.settled; expect(fetcher).toHaveBeenCalledTimes(1); expect(clock.pending.size).toBe(0); expect(response.body?.locked ?? false).toBeFalse();
    }
  });
  it('reconnects with only the last dispatched valid cursor and closes cancellably', async () => {
    const clock = new Clock(); const calls: RequestInit[] = []; let secondBody!: ReadableStream<Uint8Array>;
    const fetcher: typeof fetch = async (_input, init) => {
      calls.push(init ?? {});
      if (calls.length === 1) return streamResponse(new ReadableStream({ start(c) { c.enqueue(encoded('id: 7\ndata:dispatched\n\nid: 999\ndata:incomplete')); c.close(); } }));
      secondBody = new ReadableStream(); return streamResponse(secondBody);
    };
    const events: JobStreamEvent[] = []; const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: event => events.push(event), fetch: fetcher, clock });
    try {
      await drain(); expect(events.map(event => event.id)).toEqual(['7']); clock.fire(1000); await drain();
      expect(calls.length).toBe(2); expect(new Headers(calls[1].headers).get('Last-Event-ID')).toBe('7'); expect(new Headers(calls[1].headers).get('X-Api-Key')).toBe('generated-a');
    } finally { stream.close(); await stream.settled; }
    expect(clock.pending.size).toBe(0); expect(secondBody.locked).toBeFalse();
  });
  it('honors clamped server retry and closes its wait without later fetches', async () => {
    const clock = new Clock(); const fetcher = jasmine.createSpy('fetch').and.callFake(async () => streamResponse(new ReadableStream({ start(c) { c.enqueue(encoded('retry: 1\n\n')); c.close(); } })));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => {}, fetch: fetcher, clock });
    await drain(); clock.fire(250); await drain(); expect(fetcher).toHaveBeenCalledTimes(2);
    stream.close(); await stream.settled; expect(clock.pending.size).toBe(0); expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it('makes only three finite reconnects after transient failures', async () => {
    const clock = new Clock(); const fetcher = jasmine.createSpy('fetch').and.callFake(async () => new Response(null, { status: 503 }));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => {}, fetch: fetcher, clock });
    await drain(); clock.fire(1000); await drain(); clock.fire(2000); await drain(); clock.fire(4000);
    await stream.settled; expect(fetcher).toHaveBeenCalledTimes(4); expect(clock.pending.size).toBe(0);
  });
  it('never opens a fetch for an already inactive lifetime', async () => {
    const fetcher = jasmine.createSpy('fetch');
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => false, onEvent: () => fail('Inactive event'), fetch: fetcher });
    await stream.settled; expect(fetcher).not.toHaveBeenCalled();
  });
  for (const event of ['job.completed', 'job.failed', 'job.cancelled', 'job.resync-required']) it('does not reconnect after terminal/capacity ' + event, async () => {
    const clock = new Clock(); const data = event === 'job.resync-required' ? '{"reason":"subscription_capacity"}' : '{}';
    const fetcher = jasmine.createSpy('fetch').and.resolveTo(streamResponse(new ReadableStream({ start(c) { c.enqueue(encoded('event: ' + event + '\ndata: ' + data + '\n\n')); } })));
    const stream = openHeaderJobStream('/api/v1/jobs/job-generated/events', 'generated-a', { current: () => true, onEvent: () => {}, fetch: fetcher, clock });
    await stream.settled; expect(fetcher).toHaveBeenCalledTimes(1); expect(clock.pending.size).toBe(0);
  });
});
