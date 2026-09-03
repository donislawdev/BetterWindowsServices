namespace Bws.Core;

/// <summary>
/// Time, as far as anything that waits is concerned.
///
/// A seam for the same reason ADR-10 puts one over the manager. Waiting for a service is
/// where the mistakes are - a deadline honoured too early reports a healthy service as
/// stuck, one honoured too late hangs a terminal - and a test that had to spend real
/// seconds to check either would be slow enough that it would not get written.
/// </summary>
public interface IClock
{
    /// <summary>
    /// What time it is, for anything a person will read or compare across runs.
    ///
    /// <b>NOT for measuring how long something took, and that is what the member below is
    /// for.</b> This is the wall clock: it is what a snapshot is stamped with and what a
    /// quarantined file is named after, and both of those have to survive being read next
    /// week. It also moves when somebody sets the clock, when daylight saving arrives, and
    /// when a virtual machine resumes - so a duration worked out from two readings of it can
    /// come out negative.
    /// </summary>
    DateTimeOffset Now { get; }

    /// <summary>
    /// A count that only ever goes forward, for measuring how long something took.
    ///
    /// <b>Added 2026-09-03 for backlog 299.</b> A plan step is watched for up to a minute -
    /// <c>Carrying.Ceiling</c> - and the machines this project tests on are virtual ones,
    /// which is exactly the family where the wall clock jumps on resume. Two readings of
    /// <see cref="Now"/> across such a jump give a deadline that has already passed, a step
    /// reported as timed out that was fine, and a <c>milliseconds</c> figure in the machine
    /// readable output that is NEGATIVE - a number no reader of that field can do anything
    /// sensible with.
    ///
    /// <b>Where it counts from is deliberately not said, because nothing may depend on it.</b>
    /// Only differences between two readings mean anything, which is the whole point: it is
    /// not a time, it is a ruler.
    /// </summary>
    TimeSpan Elapsed { get; }

    void Wait(TimeSpan duration);
}

/// <summary>
/// The real one.
///
/// Blocks the thread it is called on, so whatever runs a plan runs it away from the
/// interface. That is not a limitation of this class - 06-STRUKTURA-I-KONWENCJE already
/// requires background work to stay off the interface thread.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;

    /// <summary>
    /// The performance counter, through the one call that hands it over already scaled.
    ///
    /// <b>Rather than <c>Environment.TickCount64</c>, which is also monotonic and would be
    /// simpler.</b> That one advances in steps of about fifteen milliseconds, and a step of a
    /// plan is reported in milliseconds to whoever reads the machine readable output - so the
    /// cheaper counter would quietly round every short step to the nearest sixteenth of a
    /// second. This one is what <c>Stopwatch</c> itself uses.
    ///
    /// <c>GetElapsedTime(0)</c> reads oddly and is the documented way to turn a raw timestamp
    /// into a <see cref="TimeSpan"/>: it is the time from timestamp zero to now, which is
    /// exactly the ruler this member promises and nothing more.
    /// </summary>
    public TimeSpan Elapsed => System.Diagnostics.Stopwatch.GetElapsedTime(0);

    public void Wait(TimeSpan duration) => Thread.Sleep(duration);
}
