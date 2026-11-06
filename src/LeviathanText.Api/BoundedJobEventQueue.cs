using System.Threading.Channels;
using System.Diagnostics.CodeAnalysis;

namespace LeviathanText.Api;

/// <summary>One SSE consumer's bounded references/characters and one coalesced wake signal.</summary>
internal sealed class BoundedJobEventQueue(int maxEvents = 256, int maxCharacters = 65536) : IDisposable
{
    private readonly int _maxEvents = maxEvents > 0 ? maxEvents : throw new ArgumentOutOfRangeException(nameof(maxEvents));
    private readonly int _maxCharacters = maxCharacters > 0 ? maxCharacters : throw new ArgumentOutOfRangeException(nameof(maxCharacters));
    private readonly object _gate = new();
    private readonly Queue<JobEventBus.JobEvent> _events = [];
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true, SingleWriter = false,
    });
    private long _characters, _gapThrough;
    private bool _disposed;

    public void Push(JobEventBus.JobEvent evt)
    {
        lock (_gate)
        {
            if (_disposed) return;
            var characters = (long)evt.Kind.Length + evt.Payload.Length;
            if (_gapThrough != 0 || _events.Count >= _maxEvents || _characters + characters > _maxCharacters)
                _gapThrough = Math.Max(_gapThrough, evt.Sequence);
            else { _events.Enqueue(evt); _characters += characters; }
        }
        _wake.Writer.TryWrite(true);
    }

    public long GapThrough { get { lock (_gate) return _gapThrough; } }
    public (int Events, long Characters) Retained { get { lock (_gate) return (_events.Count, _characters); } }

    public void DiscardThrough(long sequence)
    {
        lock (_gate)
        {
            while (_events.TryPeek(out var evt) && evt.Sequence <= sequence)
            {
                _events.Dequeue(); _characters -= (long)evt.Kind.Length + evt.Payload.Length;
            }
            if (_gapThrough <= sequence) _gapThrough = 0;
        }
    }

    public bool TryRead([NotNullWhen(true)] out JobEventBus.JobEvent? evt)
    {
        lock (_gate)
        {
            if (!_events.TryDequeue(out evt)) return false;
            _characters -= (long)evt.Kind.Length + evt.Payload.Length;
            return true;
        }
    }

    public void AcknowledgeWake() { while (_wake.Reader.TryRead(out _)) { } }
    public Task<bool> WaitAsync(CancellationToken cancellation)
    {
        lock (_gate)
        {
            if (_disposed) return Task.FromResult(false);
            if (_events.Count != 0 || _gapThrough != 0) return Task.FromResult(true);
        }
        return _wake.Reader.WaitToReadAsync(cancellation).AsTask();
    }

    public void Dispose()
    {
        lock (_gate) { _disposed = true; _events.Clear(); _characters = 0; _gapThrough = 0; }
        _wake.Writer.TryComplete();
    }
}
