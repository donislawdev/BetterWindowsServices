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
    DateTimeOffset Now { get; }

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

    public void Wait(TimeSpan duration) => Thread.Sleep(duration);
}
