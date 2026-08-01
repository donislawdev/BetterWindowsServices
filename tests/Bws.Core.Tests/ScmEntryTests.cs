using Bws.Core;

namespace Bws.Core.Tests;

public sealed class ScmEntryTests
{
    /// <summary>
    /// The derived fact the whole product leans on: automatic and not running is a silent
    /// failure. Glossary pitfall P3 exists because status and start type get spoken of as
    /// one thing, and this is where confusing them would cost the most.
    /// </summary>
    [Theory]
    [InlineData(StartType.Automatic, EntryStatus.Stopped, true)]
    [InlineData(StartType.Automatic, EntryStatus.Running, false)]
    [InlineData(StartType.Manual, EntryStatus.Stopped, false)]
    [InlineData(StartType.Disabled, EntryStatus.Stopped, false)]
    [InlineData(StartType.Boot, EntryStatus.Stopped, false)]
    public void An_automatic_entry_that_is_not_running_is_the_signal_we_look_for(
        StartType startType, EntryStatus status, bool expected)
    {
        var entry = Entry(status, Reading<StartType>.Present(startType));

        Assert.Equal(ReadOutcome.Present, entry.RunsAgainstItsStartType.Outcome);
        Assert.Equal(expected, entry.RunsAgainstItsStartType.Value);
    }

    [Fact]
    public void An_unreadable_start_type_never_renders_as_everything_is_fine()
    {
        // The dangerous shortcut would be returning false here. "I could not check" would
        // then be indistinguishable from "checked, nothing wrong", and a machine full of
        // unreadable entries would report a clean bill of health.
        var entry = Entry(EntryStatus.Stopped, Reading<StartType>.Denied("access denied"));

        Assert.Equal(ReadOutcome.Denied, entry.RunsAgainstItsStartType.Outcome);
        Assert.Equal("access denied", entry.RunsAgainstItsStartType.Reason);
    }

    [Fact]
    public void Drivers_are_recognised_as_drivers()
    {
        Assert.True(Entry(EntryStatus.Running, Reading<StartType>.Absent()) with
        {
            EntryType = EntryType.KernelDriver
        } is { IsDriver: true });

        Assert.False(Entry(EntryStatus.Running, Reading<StartType>.Absent()) with
        {
            EntryType = EntryType.SharedProcess
        } is { IsDriver: true });
    }

    private static ScmEntry Entry(EntryStatus status, Reading<StartType> startType) =>
        Entries.Any with
        {
            Status = status,
            ProcessId = Reading<int>.Absent(),
            StartType = startType
        };
}
