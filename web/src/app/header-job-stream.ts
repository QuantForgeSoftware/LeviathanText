/** Header-authenticated, bounded job SSE. This deliberately has finite retry/read
 * policies; it is not a drop-in implementation of native EventSource. */
export interface JobStreamEvent { type: string; data: string; id: string; }
export type JobStreamState = 'connecting' | 'open' | 'retrying' | 'closed';
export type JobStreamReason = 'manual' | 'terminal' | 'capacity' | 'authorization' | 'not-found'
  | 'protocol' | 'limit' | 'retry-exhausted' | 'stale';
export interface JobStreamNotice { state: JobStreamState; reason?: JobStreamReason; status?: number; attempt: number; }
export interface JobStreamClock { setTimeout(callback: () => void, milliseconds: number): unknown; clearTimeout(handle: unknown): void; }
export interface HeaderJobStreamOptions {
  current(): boolean;
  onEvent(event: JobStreamEvent): void;
  onState?(notice: JobStreamNotice): void;
  fetch?: typeof globalThis.fetch;
  clock?: JobStreamClock;
  /** Test-origin seam; fetch mode still enforces the real browser's same-origin policy. */
  origin?: string;
}
export interface HeaderJobStream {
  close(): void;
  readonly readyState: JobStreamState;
  readonly lastEventId: string;
  /** Resolves only after read-loop cleanup; consumer callback failures reject. */
  readonly settled: Promise<void>;
}
export const JobStreamLimits = Object.freeze({ chunkBytes: 1024 * 1024, lineUnits: 64 * 1024,
  eventUnits: 256 * 1024, connectionBytes: 32 * 1024 * 1024, reconnects: 3,
  retryMinimumMs: 250, retryMaximumMs: 30_000, headersTimeoutMs: 45_000, cleanupMs: 1000, scopePollMs: 1000 });
export class JobStreamProtocolError extends Error { constructor() { super('Invalid job stream encoding or framing.'); } }
export class JobStreamLimitError extends Error { constructor() { super('Job stream exceeded a safe parsing limit.'); } }

/** UTF-8/CRLF/LF/CR incremental framing. Incomplete EOF is discarded. Invalid UTF-8
 * is refused instead of replacement-decoded. ID-only blocks retain parser state,
 * but the job transport advances its cursor only on a dispatched event. */
export class BoundedSseParser {
  private readonly decoder = new TextDecoder('utf-8', { fatal: true });
  private line = '';
  private cr = false;
  private data: string[] = [];
  private units = 0;
  private type = '';
  private id: string;
  private ended = false;
  constructor(private readonly onEvent: (event: JobStreamEvent) => void,
    private readonly onRetry?: (milliseconds: number) => void, initialId = '') { this.id = initialId; }
  feed(bytes: Uint8Array): void {
    if (this.ended) throw new Error('Job stream parser is closed.');
    if (bytes.byteLength > JobStreamLimits.chunkBytes) throw new JobStreamLimitError();
    let text: string;
    try { text = this.decoder.decode(bytes, { stream: true }); } catch { throw new JobStreamProtocolError(); }
    this.text(text);
  }
  end(): void {
    if (this.ended) return;
    this.ended = true;
    try { let tail: string; try { tail = this.decoder.decode(); } catch { throw new JobStreamProtocolError(); } this.text(tail); }
    finally { this.line = ''; this.data = []; this.units = 0; this.type = ''; }
  }
  private text(text: string): void {
    for (const char of text) {
      if (this.cr) { this.cr = false; if (char === '\n') continue; }
      if (char === '\r' || char === '\n') {
        const line = this.line; this.line = ''; this.cr = char === '\r'; this.process(line);
      } else {
        this.line += char;
        if (this.line.length > JobStreamLimits.lineUnits) throw new JobStreamLimitError();
      }
    }
  }
  private process(line: string): void {
    if (line === '') {
      const event = this.data.length ? { type: this.type || 'message', data: this.data.join('\n'), id: this.id } : null;
      this.data = []; this.units = 0; this.type = '';
      if (event) this.onEvent(event);
      return;
    }
    if (line.startsWith(':')) return;
    const colon = line.indexOf(':');
    const field = colon < 0 ? line : line.slice(0, colon);
    let value = colon < 0 ? '' : line.slice(colon + 1);
    if (value.startsWith(' ')) value = value.slice(1);
    if (field === 'data') {
      this.units += value.length + 1;
      if (this.units > JobStreamLimits.eventUnits) throw new JobStreamLimitError();
      this.data.push(value);
    } else if (field === 'event') this.type = value;
    else if (field === 'id') { if (!value.includes('\0')) this.id = value; }
    else if (field === 'retry' && /^[0-9]+$/.test(value)) {
      const milliseconds = Number(value);
      if (Number.isSafeInteger(milliseconds)) this.onRetry?.(Math.min(JobStreamLimits.retryMaximumMs,
        Math.max(JobStreamLimits.retryMinimumMs, milliseconds)));
    }
    if (this.units + this.type.length + this.id.length > JobStreamLimits.eventUnits) throw new JobStreamLimitError();
  }
}
const nativeClock: JobStreamClock = {
  setTimeout: (callback, ms) => globalThis.setTimeout(callback, ms),
  clearTimeout: handle => globalThis.clearTimeout(handle as ReturnType<typeof setTimeout>),
};
function validCursor(id: string): boolean {
  return /^[0-9]{1,128}$/.test(id) && BigInt(id) <= 9223372036854775807n;
}
class ReadTimeout extends Error {}
class StreamAborted extends Error {}
function withAbort<T>(promise: Promise<T>, signal: AbortSignal): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const cancel = () => { signal.removeEventListener('abort', cancel); reject(new StreamAborted()); };
    if (signal.aborted) { cancel(); return; }
    signal.addEventListener('abort', cancel, { once: true });
    promise.then(value => { signal.removeEventListener('abort', cancel); resolve(value); },
      error => { signal.removeEventListener('abort', cancel); reject(error); });
  });
}

export function openHeaderJobStream(url: string, credential: string, options: HeaderJobStreamOptions): HeaderJobStream {
  const origin = options.origin ?? globalThis.location?.origin;
  if (!origin) throw new Error('Job stream requires a browser origin.');
  const target = new URL(url, origin);
  if (target.origin !== origin || target.username || target.password || target.search || target.hash
      || !/^\/api\/v1\/jobs\/[^/]+\/events$/.test(target.pathname))
    throw new Error('Job stream requires a same-origin job route without query credentials.');
  const clock = options.clock ?? nativeClock, fetcher = options.fetch ?? globalThis.fetch.bind(globalThis);
  let state: JobStreamState = 'connecting', cursor = '', stopped = false, attempt = 0, serverRetry: number | null = null;
  let controller: AbortController | null = null, reader: ReadableStreamDefaultReader<Uint8Array> | null = null;
  let retryTimer: unknown, pollTimer: unknown, wakeRetry: ((value: boolean) => void) | null = null;
  let callbackError: unknown;
  let callbackFailed = false;
  const rememberCallbackError = (error: unknown) => {
    if (!callbackFailed) { callbackFailed = true; callbackError = error; }
  };
  const timers = new Set<unknown>();
  const cleanupTimers = new Set<unknown>();
  const timer = (callback: () => void, ms: number) => {
    const handle = clock.setTimeout(() => {
      timers.delete(handle);
      try { callback(); } catch (error) {
        rememberCallbackError(error);
        try { stop('protocol'); } catch { /* settled retains the original consumer failure */ }
      }
    }, ms); timers.add(handle); return handle;
  };
  const clear = (handle: unknown) => { if (timers.delete(handle)) clock.clearTimeout(handle); };
  const notice = (next: JobStreamState, reason?: JobStreamReason, status?: number) => {
    state = next;
    try { options.onState?.({ state, reason, status, attempt }); }
    catch (error) { rememberCallbackError(error); throw error; }
  };
  const stop = (reason: JobStreamReason, status?: number) => {
    if (stopped) return;
    stopped = true; controller?.abort();
    for (const handle of [...timers]) clear(handle);
    wakeRetry?.(false); wakeRetry = null;
    if (reader) void reader.cancel().catch(() => {});
    notice('closed', reason, status);
  };
  const current = () => {
    if (stopped) return false;
    try { if (options.current() === true) return true; }
    catch (error) { rememberCallbackError(error); throw error; }
    stop('stale'); return false;
  };
  const boundedCleanup = async (action: Promise<unknown>) => {
    let handle: unknown;
    try { await Promise.race([action.catch(() => {}), new Promise<void>(resolve => {
      handle = clock.setTimeout(() => { cleanupTimers.delete(handle); resolve(); }, JobStreamLimits.cleanupMs);
      cleanupTimers.add(handle);
    })]); }
    finally { if (cleanupTimers.delete(handle)) clock.clearTimeout(handle); }
  };
  const release = async () => {
    const active = reader; reader = null;
    if (!active) return;
    try { await boundedCleanup(active.cancel()); }
    finally { active.releaseLock(); }
  };
  const poll = () => { if (current()) pollTimer = timer(poll, JobStreamLimits.scopePollMs); };
  const delay = (ms: number) => new Promise<boolean>(resolve => {
    wakeRetry = resolve; retryTimer = timer(() => { wakeRetry = null; resolve(true); }, ms);
  });
  const run = async () => {
    try {
      if (!current()) return;
      pollTimer = timer(poll, JobStreamLimits.scopePollMs);
      while (current()) {
        let transient = false;
        let unreadBody: ReadableStream<Uint8Array> | null = null;
        controller = new AbortController();
        try {
          notice(attempt ? 'retrying' : 'connecting');
          if (!current()) break;
          const headers = new Headers({ Accept: 'text/event-stream', 'Cache-Control': 'no-cache' });
          if (credential) headers.set('X-Api-Key', credential);
          if (cursor) headers.set('Last-Event-ID', cursor);
          const request = controller;
          let headerTimer: unknown;
          let response: Response;
          try {
            const pending = fetcher(target.href, { method: 'GET', headers, signal: request.signal,
              credentials: 'omit', mode: 'same-origin', redirect: 'error', cache: 'no-store' });
            // An injected/nonconforming fetch may settle after cancellation. Its body
            // is still ours to cancel, even after the read loop has already settled.
            void pending.then(late => { if (stopped || request.signal.aborted) void late.body?.cancel().catch(() => {}); }, () => {});
            response = await Promise.race([withAbort(pending, request.signal), new Promise<never>((_resolve, reject) => {
              headerTimer = timer(() => { request.abort(); reject(new ReadTimeout()); }, JobStreamLimits.headersTimeoutMs);
            })]);
          } finally { clear(headerTimer); }
          unreadBody = response.body; // Own it before ANY consumer/current/status callback can throw.
          if (!current()) break;
          if (response.status !== 200) {
            // A CURRENT authority refusal fences consumers immediately on headers.
            // Cancelling an untrusted body must never delay known revocation.
            if (response.status === 401 || response.status === 403) stop('authorization', response.status);
            else if (response.status === 404) stop('not-found', response.status);
            else if (response.status === 429) stop('capacity', response.status);
            else if (response.status >= 500 && response.status <= 599) transient = true;
            else stop('protocol', response.status);
          } else if (response.redirected || response.headers.get('Content-Type')?.split(';', 1)[0].trim().toLowerCase() !== 'text/event-stream' || !response.body) {
            stop('protocol');
          } else {
            reader = response.body.getReader(); unreadBody = null; notice('open');
            let bytes = 0;
            const parser = new BoundedSseParser(event => {
              if (!current()) return;
              if (event.id === '' || validCursor(event.id)) cursor = event.id;
              try { options.onEvent(event); } catch (error) { rememberCallbackError(error); throw error; }
              if (!current()) return;
              if (['job.completed', 'job.failed', 'job.cancelled'].includes(event.type)) stop('terminal');
              else if (event.type === 'job.resync-required' && event.data.length <= 1024) {
                try { if (JSON.parse(event.data).reason === 'subscription_capacity') stop('capacity'); } catch { /* advisory malformed JSON is still delivered */ }
              }
            }, ms => { serverRetry = ms; }, cursor);
            while (current()) {
              const chunk = await withAbort(reader.read(), controller.signal);
              if (!current()) break;
              if (chunk.done) { parser.end(); transient = true; break; }
              bytes += chunk.value.byteLength;
              if (bytes > JobStreamLimits.connectionBytes) throw new JobStreamLimitError();
              parser.feed(chunk.value);
            }
          }
        } catch (error) {
          if (callbackFailed) throw error;
          if (!stopped) {
            if (error instanceof JobStreamLimitError) stop('limit');
            else if (error instanceof JobStreamProtocolError) stop('protocol');
            else transient = true;
          }
        } finally {
          try { await release(); }
          finally {
            try { if (unreadBody) await boundedCleanup(unreadBody.cancel()); }
            finally { controller?.abort(); controller = null; }
          }
        }
        if (!current() || !transient) break;
        if (attempt >= JobStreamLimits.reconnects) { stop('retry-exhausted'); break; }
        const milliseconds = serverRetry ?? Math.min(30_000, 1000 * 2 ** attempt);
        attempt++; notice('retrying');
        if (!await delay(milliseconds)) break;
      }
    } finally {
      let cleanupFailed = false, cleanupError: unknown;
      try { await release(); } catch (error) { cleanupFailed = true; cleanupError = error; }
      try {
        controller?.abort(); for (const handle of [...timers]) clear(handle);
        for (const handle of cleanupTimers) clock.clearTimeout(handle); cleanupTimers.clear();
        if (!stopped) { try { stop('protocol'); } catch (error) { rememberCallbackError(error); } }
      } catch (error) { if (!cleanupFailed) { cleanupFailed = true; cleanupError = error; } }
      if (callbackFailed) throw callbackError;
      if (cleanupFailed) throw cleanupError;
    }

  };
  let resolve!: () => void, reject!: (error: unknown) => void;
  const settled = new Promise<void>((yes, no) => { resolve = yes; reject = no; });
  queueMicrotask(() => { void run().then(resolve, reject); });
  return { close: () => { try { stop('manual'); } catch (error) { rememberCallbackError(error); } }, get readyState() { return state; }, get lastEventId() { return cursor; }, settled };
}
