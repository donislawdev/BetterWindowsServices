using Bws.Core;

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

    /// <summary>Everything spent waiting, so a test can say how patient the runner was.</summary>
    internal TimeSpan Waited { get; private set; }

    public void Wait(TimeSpan duration)
    {
        Now += duration;
        Waited += duration;
    }
}
