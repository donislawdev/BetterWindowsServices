namespace Bws.Core.Tests.Fakes;

/// <summary>
/// Time that only moves when somebody waits.
///
/// That is the whole trick. A deadline test against the real clock either spends the real
/// seconds - and a test suite nobody waits for is a test suite nobody runs - or cheats by
/// shortening the deadline until it is not the deadline being tested any more. Here a
/// minute passes in no time and passes exactly.
/// </summary>
internal sealed class FakeClock : IClock
{
    public DateTimeOffset Now { get; private set; } = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The ruler, which moves with the waiting and with nothing else.
    ///
    /// <b>Separate from <see cref="Now"/> since 2026-09-03, and the separation is the point</b> -
    /// backlog 299. A test can now push the wall clock somewhere absurd, forwards or backwards,
    /// and this stays where it was, which is exactly what the real pair does when a machine
    /// resumes or somebody corrects the time.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Everything spent waiting, so a test can say how patient the runner was.</summary>
    internal TimeSpan Waited { get; private set; }

    /// <summary>
    /// Somebody set the clock, or the machine resumed and the clock caught up.
    ///
    /// The ruler is deliberately not touched, because the real one is not touched either.
    /// </summary>
    internal void TheWallClockJumps(TimeSpan by) => Now += by;

    public void Wait(TimeSpan duration)
    {
        Now += duration;
        Elapsed += duration;
        Waited += duration;
    }
}
