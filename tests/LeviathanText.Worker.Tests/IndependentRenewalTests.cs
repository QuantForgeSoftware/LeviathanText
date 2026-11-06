using System.Threading.Channels;
using LeviathanText.Contracts;
using LeviathanText.Worker;
using Xunit;
namespace LeviathanText.Worker.Tests;

public sealed class IndependentRenewalTests
{
    static readonly DateTimeOffset Start = new(2026,11,6,0,0,0,TimeSpan.Zero);
    static TaskCompletionSource Ready()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    static async Task Await(Task task)=>await task.WaitAsync(TimeSpan.FromSeconds(5));
    static JobRecord Job()=>new(){Id=new(EntityKind.Job,Guid.NewGuid().ToString("N")),Type="generated",Inputs=[],Owner=new(){Id="generated-independent",IsModel=false},Status=JobStatus.Pending};
    static LeaseRenewalOptions Options(Func<ILeaseRenewalSession> factory,Clock clock,Action<LeaseRenewalDiagnostic>? observe=null)=>new(){OpenSession=factory,Ticks=clock,Interval=TimeSpan.FromSeconds(1),Observe=observe};
    [Fact]
    public async Task Automatic_loop_renews_captured_claim_on_a_different_thread_and_disposes_before_finalization()
    {
        var store=new Store();var job=Job();store.Save(job);using var handler=new Held();var clock=new Clock();var renewed=Ready();var session=new Session(store);int opened=0;
        var host=new WorkerHost(store,[handler],TimeSpan.FromSeconds(3),leaseRenewal:Options(()=>{Interlocked.Increment(ref opened);session.OpenThread=Environment.CurrentManagedThreadId;return session;},clock,d=>{if(d.Kind=="renewed")renewed.TrySetResult();}));
        store.BeforeFinalization=()=>Assert.Equal(1,session.Disposed);
        var running=Task.Run(()=>host.RunNext(now:Start));
        try{await Await(handler.Entered.Task);var initial=store.Find(job.Id)!;await clock.Step(Start.AddSeconds(2));await Await(renewed.Task);
            var current=store.Find(job.Id)!;Assert.Equal(initial.Lease!.ClaimId,current.Lease!.ClaimId);Assert.Equal(Start.AddSeconds(5),current.Lease.ExpiresAt);
            Assert.NotEqual(handler.Thread,session.OpenThread);Assert.Equal(new LeaseAttemptIdentity(job.Id,host.WorkerId,initial.Lease.ClaimId!),session.LastAttempt);
        }finally{handler.Release();await Await(running);}
        Assert.Equal(1,opened);Assert.Equal(1,session.Disposed);Assert.Equal(1,host.LastRenewal!.Renewed);Assert.Equal(JobStatus.Succeeded,store.Find(job.Id)!.Status);
        var calls=session.Calls;await Task.Yield();Assert.Equal(calls,session.Calls);
    }
    [Fact]
    public async Task Join_waits_for_an_inflight_renewal_before_any_final_CAS()
    {
        var store=new Store();var job=Job();store.Save(job);using var handler=new Held();var clock=new Clock();using var blocked=new ManualResetEventSlim();var entered=Ready();var session=new Session(store){BeforeRenew=()=>{entered.TrySetResult();Assert.True(blocked.Wait(TimeSpan.FromSeconds(5)));}};
        var host=new WorkerHost(store,[handler],TimeSpan.FromSeconds(3),leaseRenewal:Options(()=>session,clock));var running=Task.Run(()=>host.RunNext(now:Start));
        try{await Await(handler.Entered.Task);await clock.Step(Start.AddSeconds(2));await Await(entered.Task);handler.Release();await Await(handler.Returning.Task);
            Assert.NotSame(running,await Task.WhenAny(running,Task.Delay(40)));Assert.Equal(JobStatus.Running,store.Find(job.Id)!.Status);Assert.Equal(0,session.Disposed);
        }finally{blocked.Set();handler.Release();await Await(running);}
        Assert.Equal(1,session.Disposed);Assert.Equal(JobStatus.Succeeded,store.Find(job.Id)!.Status);
    }
    [Fact]
    public async Task External_cancel_is_preserved_and_renewal_session_is_joined()
    {
        var store=new Store();var job=Job();store.Save(job);using var handler=new Held();var clock=new Clock();var lost=Ready();var session=new Session(store);
        var host=new WorkerHost(store,[handler],TimeSpan.FromSeconds(3),leaseRenewal:Options(()=>session,clock,d=>{if(d.Kind=="ownership-lost")lost.TrySetResult();}));var running=Task.Run(()=>host.RunNext(now:Start));
        try{await Await(handler.Entered.Task);store.Update(job.Id,j=>j with{Status=JobStatus.Cancelled,Lease=null});await clock.Step(Start.AddSeconds(2));await Await(lost.Task);}
        finally{handler.Release();await Await(running);}
        Assert.Equal(JobStatus.Cancelled,(await running)!.Status);Assert.Equal(JobStatus.Cancelled,store.Find(job.Id)!.Status);Assert.True(host.LastRenewal!.OwnershipLost);Assert.Equal(1,session.Disposed);
    }
    [Fact]
    public async Task Replaced_claim_is_not_renewed_or_finalized_by_the_old_handler()
    {
        var store=new Store();var job=Job();store.Save(job);using var handler=new Held();var clock=new Clock();var lost=Ready();var session=new Session(store);
        var host=new WorkerHost(store,[handler],TimeSpan.FromSeconds(3),leaseRenewal:Options(()=>session,clock,d=>{if(d.Kind=="ownership-lost")lost.TrySetResult();}));var running=Task.Run(()=>host.RunNext(now:Start));JobRecord? replacement=null;
        try{await Await(handler.Entered.Task);replacement=store.Update(job.Id,j=>j with{Lease=j.Lease! with{ClaimId="generated-replacement",Worker=new(EntityKind.Job,"foreign-worker"),ExpiresAt=Start.AddMinutes(1)}});await clock.Step(Start.AddSeconds(2));await Await(lost.Task);}
        finally{handler.Release();await Await(running);}
        Assert.Null(await running);Assert.Equal(replacement,store.Find(job.Id));Assert.True(host.LastRenewal!.OwnershipLost);Assert.Equal(1,host.CompletedAttempts);Assert.Equal(1,session.Disposed);
    }
    [Fact]
    public async Task Contention_is_observed_and_next_tick_can_renew_without_changing_claim()
    {
        var store=new Store();var job=Job();store.Save(job);using var handler=new Held();var clock=new Clock();var contended=Ready();var renewed=Ready();var session=new Session(store){ContendFirst=true};
        var host=new WorkerHost(store,[handler],TimeSpan.FromSeconds(3),leaseRenewal:Options(()=>session,clock,d=>{if(d.Kind=="contended")contended.TrySetResult();if(d.Kind=="renewed")renewed.TrySetResult();}));var running=Task.Run(()=>host.RunNext(now:Start));
        try{await Await(handler.Entered.Task);await clock.Step(Start.AddSeconds(1));await Await(contended.Task);await clock.Step(Start.AddSeconds(2));await Await(renewed.Task);}
        finally{handler.Release();await Await(running);}
        Assert.Equal(1,host.LastRenewal!.Contended);Assert.Equal(1,host.LastRenewal.Renewed);Assert.Equal(1,session.Disposed);
    }
    [Fact]
    public async Task Observer_exception_is_bounded_and_does_not_stop_renewal_or_cleanup()
    {
        var store=new Store();var job=Job();store.Save(job);using var handler=new Held();var clock=new Clock();var observed=Ready();var session=new Session(store);
        var host=new WorkerHost(store,[handler],TimeSpan.FromSeconds(3),leaseRenewal:Options(()=>session,clock,_=>{observed.TrySetResult();throw new InvalidOperationException("dummy-private-message-must-not-be-recorded");}));var running=Task.Run(()=>host.RunNext(now:Start));
        try{await Await(handler.Entered.Task);await clock.Step(Start.AddSeconds(2));await Await(observed.Task);}
        finally{handler.Release();await Await(running);}
        Assert.Equal(nameof(InvalidOperationException),host.LastRenewal!.ObserverFaultType);Assert.Null(host.LastRenewal.FaultType);Assert.Equal(1,session.Disposed);
    }
    [Theory][InlineData("renew")][InlineData("dispose")][InlineData("factory")][InlineData("tick")]
    public async Task Every_fault_path_joins_and_disposes_any_successfully_created_session(string fault)
    {
        var store=new Store();var job=Job();store.Save(job);using var handler=new Held();var clock=new Clock{ThrowDelay=fault=="tick"};var reported=Ready();var session=new Session(store){ThrowRenew=fault=="renew",ThrowDispose=fault=="dispose"};
        var host=new WorkerHost(store,[handler],TimeSpan.FromSeconds(3),leaseRenewal:Options(()=>fault=="factory"?throw new InvalidOperationException("dummy-private-factory-message"):session,clock,d=>{if(d.Kind=="fault")reported.TrySetResult();}));var running=Task.Run(()=>host.RunNext(now:Start));
        try{await Await(handler.Entered.Task);if(fault=="renew")await clock.Step(Start.AddSeconds(2));if(fault!="dispose")await Await(reported.Task);else await Await(clock.FirstDelay.Task);}
        finally{handler.Release();await Await(running);}
        Assert.Equal(nameof(InvalidOperationException),host.LastRenewal!.FaultType);Assert.Equal(fault=="factory"?0:1,session.Disposed);Assert.Equal(JobStatus.Succeeded,store.Find(job.Id)!.Status);
    }
    [Fact]
    public async Task Caller_cancellation_stops_waiting_ticks_and_returns_pending_after_join()
    {
        var store=new Store();var job=Job();store.Save(job);using var handler=new Held();var clock=new Clock();var session=new Session(store);using var cancellation=new CancellationTokenSource();
        var host=new WorkerHost(store,[handler],TimeSpan.FromSeconds(3),leaseRenewal:Options(()=>session,clock));var running=Task.Run(()=>host.RunNext(cancellation.Token,Start));
        await Await(handler.Entered.Task);await Await(clock.FirstDelay.Task);cancellation.Cancel();await Await(running);
        Assert.Equal(JobStatus.Pending,store.Find(job.Id)!.Status);Assert.Null(store.Find(job.Id)!.Lease);Assert.Equal(1,session.Disposed);Assert.Equal(0,session.Calls);
    }
    [Fact]
    public void Precancelled_dispatch_never_opens_session_or_runs_handler()
    {
        var store=new Store();store.Save(Job());var opened=0;using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        var host=new WorkerHost(store,[],TimeSpan.FromSeconds(3),leaseRenewal:Options(()=>{opened++;return new Session(store);},new Clock()));
        Assert.Throws<OperationCanceledException>(()=>host.RunNext(cancellation.Token,Start));Assert.Equal(0,opened);Assert.Null(host.LastRenewal);Assert.Equal(JobStatus.Pending,store.PendingJobs()[0].Status);
    }
    [Theory][InlineData(null)][InlineData("")][InlineData("generated-forged")]
    public void Missing_or_forged_nonce_cannot_renew(string? claim)
    {
        var store=new Store();var job=Job();store.Save(job);var host=new WorkerHost(store,[],TimeSpan.FromSeconds(3));host.TryClaim(job,Start);var before=store.Find(job.Id);
        Assert.Null(host.HeartbeatAttempt(job.Id,claim!,Start.AddSeconds(2)));Assert.Equal(before,store.Find(job.Id));Assert.Null(host.Heartbeat(job.Id,Start.AddSeconds(2)));
    }
    [Fact]
    public void Captured_nonce_renews_but_stale_same_worker_nonce_never_does_and_time_never_moves_backwards()
    {
        var store=new Store();var job=Job();store.Save(job);var host=new WorkerHost(store,[],TimeSpan.FromSeconds(3));var first=host.TryClaim(job,Start)!;var second=host.TryClaim(job,Start.AddSeconds(4))!;var before=store.Find(job.Id);
        Assert.Null(host.HeartbeatAttempt(job.Id,first.Lease!.ClaimId!,Start.AddSeconds(5)));Assert.Equal(before,store.Find(job.Id));
        var renewed=host.HeartbeatAttempt(job.Id,second.Lease!.ClaimId!,Start.AddSeconds(6))!;Assert.Equal(Start.AddSeconds(9),renewed.Lease!.ExpiresAt);
        var backwards=host.HeartbeatAttempt(job.Id,second.Lease.ClaimId!,Start.AddSeconds(5))!;Assert.Equal(renewed.Lease,backwards.Lease);
    }
    [Fact]
    public async Task Handler_exception_still_joins_renewal_before_failed_finalization()
    {
        var store = new Store(); var job = Job(); store.Save(job);
        using var handler = new Held { ThrowOnRelease = true };
        var clock = new Clock(); var session = new Session(store); var renewed = Ready();
        var host = new WorkerHost(store, [handler], TimeSpan.FromSeconds(3),
            leaseRenewal: Options(() => session, clock, d => { if (d.Kind == "renewed") renewed.TrySetResult(); })) { MaxRetries = 0 };
        store.BeforeFinalization = () => Assert.Equal(1, session.Disposed);
        var running = Task.Run(() => host.RunNext(now: Start));
        try
        {
            await Await(handler.Entered.Task); await clock.Step(Start.AddSeconds(2)); await Await(renewed.Task);
        }
        finally { handler.Release(); await Await(running); }
        var final = store.Find(job.Id)!;
        Assert.Equal(JobStatus.Failed, final.Status); Assert.Equal(1, final.RetriesUsed);
        Assert.Null(final.Lease); Assert.Equal(1, host.CompletedAttempts); Assert.Equal(1, session.Disposed);
        Assert.Equal(nameof(InvalidOperationException), final.Failure!.Details!["exception"]);
        Assert.NotNull(host.LastRenewal); Assert.Null(host.LastRenewal.FaultType); Assert.Equal(1, host.LastRenewal.Renewed);
    }
    sealed class Clock:ILeaseRenewalTicks
    {
        readonly Channel<TaskCompletionSource> waits=Channel.CreateUnbounded<TaskCompletionSource>();readonly object gate=new();DateTimeOffset now=Start;
        public readonly TaskCompletionSource FirstDelay=Ready();public bool ThrowDelay;
        public DateTimeOffset UtcNow{get{lock(gate)return now;}}
        public Task Delay(TimeSpan interval,CancellationToken cancellation){if(ThrowDelay)throw new InvalidOperationException("dummy-private-tick-message");var step=Ready();waits.Writer.TryWrite(step);FirstDelay.TrySetResult();return step.Task.WaitAsync(cancellation);}
        public async Task Step(DateTimeOffset value){var next=await waits.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));lock(gate)now=value;next.TrySetResult();}
    }
    sealed class Held:IDisposable,IJobHandler
    {
        readonly ManualResetEventSlim release=new();public readonly TaskCompletionSource Entered=Ready(),Returning=Ready();public int Thread;public bool ThrowOnRelease;public string Type=>"generated";
        public JobRecord Execute(JobRecord job,CancellationToken cancellation){Thread=Environment.CurrentManagedThreadId;Entered.TrySetResult();try{if(!release.Wait(TimeSpan.FromSeconds(10),cancellation))throw new TimeoutException("Owned handler guard");if(ThrowOnRelease)throw new InvalidOperationException("Generated handler failure");return job;}finally{Returning.TrySetResult();}}
        public void Release()=>release.Set();public void Dispose(){release.Set();release.Dispose();}
    }
    sealed class Session(Store store):ILeaseRenewalSession
    {
        public int Disposed,Calls,OpenThread;public bool ContendFirst,ThrowRenew,ThrowDispose;public Action? BeforeRenew;public LeaseAttemptIdentity? LastAttempt;
        public LeaseRenewalResult Renew(LeaseAttemptIdentity attempt,DateTimeOffset now,TimeSpan duration){Interlocked.Increment(ref Calls);LastAttempt=attempt;BeforeRenew?.Invoke();if(ThrowRenew)throw new InvalidOperationException("dummy-private-renew-message");if(ContendFirst&&Calls==1)return LeaseRenewalResult.Contended;
            return store.Update(attempt.JobId,j=>j.Status==JobStatus.Running&&j.Lease is{}l&&l.Worker==attempt.WorkerId&&l.ClaimId==attempt.ClaimId?j with{Lease=l with{LastHeartbeat=now,ExpiresAt=now+duration}}:null)is null?LeaseRenewalResult.OwnershipLost:LeaseRenewalResult.Renewed;}
        public void Dispose(){Interlocked.Increment(ref Disposed);if(ThrowDispose)throw new InvalidOperationException("dummy-private-dispose-message");}
    }
    sealed class Store:IJobStore
    {
        readonly object gate=new();readonly Dictionary<EntityId,JobRecord> jobs=[];public Action? BeforeFinalization;
        public void Save(JobRecord job){lock(gate)jobs[job.Id]=job;}public JobRecord? Find(EntityId id){lock(gate)return jobs.GetValueOrDefault(id);}public IReadOnlyList<JobRecord> PendingJobs(){lock(gate)return jobs.Values.Where(j=>j.Status==JobStatus.Pending).ToArray();}
        public JobRecord? Update(EntityId id,Func<JobRecord,JobRecord?> transition){lock(gate){if(!jobs.TryGetValue(id,out var current)||transition(current)is not{} next)return null;if(current.Status==JobStatus.Running&&next.Status!=JobStatus.Running)BeforeFinalization?.Invoke();jobs[id]=next;return next;}}
    }
}
