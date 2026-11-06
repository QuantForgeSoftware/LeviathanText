using LeviathanText.Contracts;

namespace LeviathanText.Worker;

public sealed record LeaseAttemptIdentity(EntityId JobId, EntityId WorkerId, string ClaimId);
public enum LeaseRenewalResult { Renewed, Contended, OwnershipLost }

/// <summary>Owned by exactly one renewal task; must not share the handler connection.</summary>
public interface ILeaseRenewalSession : IDisposable
{
    LeaseRenewalResult Renew(LeaseAttemptIdentity attempt, DateTimeOffset now, TimeSpan leaseDuration);
}
public interface ILeaseRenewalTicks
{
    DateTimeOffset UtcNow { get; }
    Task Delay(TimeSpan interval, CancellationToken cancellation);
}
public sealed class SystemLeaseRenewalTicks : ILeaseRenewalTicks
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public Task Delay(TimeSpan interval, CancellationToken cancellation) => Task.Delay(interval, cancellation);
}
public sealed record LeaseRenewalDiagnostic(LeaseAttemptIdentity Attempt, string Kind, string? ExceptionType = null);
public sealed record LeaseRenewalSummary(int Renewed, int Contended, bool OwnershipLost, string? FaultType, string? ObserverFaultType);
public sealed record LeaseRenewalOptions
{
    public required Func<ILeaseRenewalSession> OpenSession { get; init; }
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(10);
    public ILeaseRenewalTicks Ticks { get; init; } = new SystemLeaseRenewalTicks();
    public Action<LeaseRenewalDiagnostic>? Observe { get; init; }
    internal void Validate(TimeSpan leaseDuration)
    {
        if (OpenSession is null || Ticks is null || Interval <= TimeSpan.Zero || Interval >= leaseDuration)
            throw new ArgumentException("Renewal requires a session factory, clock, and positive interval shorter than the lease.");
    }
}

/// <summary>Single attempt: stop and join before final publication, then release the owned session.</summary>
internal sealed class AttemptLeaseRenewal : IDisposable
{
    private readonly CancellationTokenSource stopping = new();
    private readonly Task<LeaseRenewalSummary> completion;
    private bool joined;
    private string? stopFault;
    public AttemptLeaseRenewal(LeaseAttemptIdentity attempt, TimeSpan duration, LeaseRenewalOptions options)
    {
        completion = Task.Run(async () =>
        {
            int renewed = 0, contended = 0; bool lost = false;
            string? fault = null, observerFault = null;
            void Observe(string kind, string? exception = null)
            {
                try { options.Observe?.Invoke(new(attempt, kind, exception)); }
                catch (Exception ex) { observerFault ??= ex.GetType().Name; }
            }
            try
            {
                stopping.Token.ThrowIfCancellationRequested();
                using var session = options.OpenSession();
                while (true)
                {
                    await options.Ticks.Delay(options.Interval, stopping.Token).ConfigureAwait(false);
                    stopping.Token.ThrowIfCancellationRequested();
                    var result = session.Renew(attempt, options.Ticks.UtcNow, duration);
                    switch (result)
                    {
                        case LeaseRenewalResult.Renewed: renewed++; Observe("renewed"); break;
                        case LeaseRenewalResult.Contended: contended++; Observe("contended"); break;
                        case LeaseRenewalResult.OwnershipLost: lost = true; Observe("ownership-lost"); break;
                        default: throw new InvalidOperationException("Unknown renewal outcome.");
                    }
                    if (lost) break;
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            catch (Exception ex) { fault = ex.GetType().Name; Observe("fault", fault); }
            return new LeaseRenewalSummary(renewed, contended, lost, fault, observerFault);
        });
    }
    public LeaseRenewalSummary StopAndJoin()
    {
        if (!joined)
        {
            joined = true;
            try { stopping.Cancel(); }
            catch (Exception ex) { stopFault = ex.GetType().Name; }
        }
        var summary = completion.GetAwaiter().GetResult();
        return stopFault is null ? summary : summary with { FaultType = summary.FaultType ?? stopFault };
    }
    public void Dispose() { try { StopAndJoin(); } finally { stopping.Dispose(); } }
}
