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
        var entry = Entry(EntryStatus.Stopped, Reading<StartType>.Denied(Entries.AccessDenied, "access denied"));

        Assert.Equal(ReadOutcome.Denied, entry.RunsAgainstItsStartType.Outcome);
        Assert.Equal("access denied", entry.RunsAgainstItsStartType.Reason);
    }

    /// <summary>
    /// An entry waiting for a trigger is doing what it was told to do.
    ///
    /// The decision behind this, taken by the owner on 2026-08-01 once the data existed:
    /// four of the ten entries this used to report on a real machine were waiting rather
    /// than broken. A signal wrong four times in ten is one people stop reading.
    /// </summary>
    [Fact]
    public void Something_that_starts_it_by_itself_means_stopped_is_where_it_belongs()
    {
        var waiting = Automatic() with
        {
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
                [new ServiceTrigger(TriggerKind.Custom, TriggerAction.Start)])
        };

        Assert.Equal(ReadOutcome.Present, waiting.RunsAgainstItsStartType.Outcome);
        Assert.False(waiting.RunsAgainstItsStartType.Value);
    }

    [Fact]
    public void A_trigger_that_only_stops_it_does_not_explain_why_it_is_down()
    {
        // The half of the rule that is easy to lose. A trigger is not an excuse in itself -
        // only one that would bring the entry up says anything about why it is not up.
        var stopTrigger = Automatic() with
        {
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
                [new ServiceTrigger(TriggerKind.NetworkEndpoint, TriggerAction.Stop)])
        };

        Assert.True(stopTrigger.RunsAgainstItsStartType.Value);
    }

    [Fact]
    public void Not_having_read_the_triggers_is_answered_with_not_knowing()
    {
        // The price of the decision above, paid openly. The answer now depends on a field
        // that can be unread, and an accusation built on something nobody looked at is
        // exactly what rule 8 forbids.
        var unread = Automatic() with
        {
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.NotRead()
        };

        Assert.Equal(ReadOutcome.NotRead, unread.RunsAgainstItsStartType.Outcome);

        var refused = Automatic() with
        {
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Denied(Entries.AccessDenied, "access denied")
        };

        Assert.Equal(ReadOutcome.Denied, refused.RunsAgainstItsStartType.Outcome);
        Assert.Equal(5, refused.RunsAgainstItsStartType.ErrorCode);
    }

    [Fact]
    public void Not_knowing_about_triggers_spreads_no_further_than_it_has_to()
    {
        // Without this the change above would turn a listing with no triggers read into 810
        // shrugs. Anything that is not both automatic and stopped is answered without the
        // triggers being needed at all.
        var running = Entry(EntryStatus.Running, Reading<StartType>.Present(StartType.Automatic)) with
        {
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.NotRead()
        };

        var manual = Entry(EntryStatus.Stopped, Reading<StartType>.Present(StartType.Manual)) with
        {
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.NotRead()
        };

        Assert.Equal(ReadOutcome.Present, running.RunsAgainstItsStartType.Outcome);
        Assert.False(running.RunsAgainstItsStartType.Value);
        Assert.Equal(ReadOutcome.Present, manual.RunsAgainstItsStartType.Outcome);
        Assert.False(manual.RunsAgainstItsStartType.Value);
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

    /// <summary>Automatic and stopped, which is the only shape the triggers are consulted for.</summary>
    private static ScmEntry Automatic() =>
        Entry(EntryStatus.Stopped, Reading<StartType>.Present(StartType.Automatic));

    /// <summary>
    /// THE OTHER DIRECTION, AND services.msc DOES NOT NAME IT EITHER - backlog 170, decided
    /// 2026-08-18.
    ///
    /// <b>Its own fact rather than a wider RunsAgainstItsStartType, because the remedies are
    /// opposite.</b> A stopped automatic entry gets started or investigated. This one is a decision
    /// about whether to stop it or to re-enable it - and one boolean answering both would stop
    /// saying which of the two somebody has.
    ///
    /// The owner's screenshot from 2026-08-12 carries a real one: two columns side by side saying
    /// opposite things, with nothing anywhere naming the contradiction.
    /// </summary>
    [Theory]
    [InlineData(StartType.Disabled, EntryStatus.Running, true)]
    [InlineData(StartType.Disabled, EntryStatus.Stopped, false)]
    [InlineData(StartType.Automatic, EntryStatus.Running, false)]
    [InlineData(StartType.Manual, EntryStatus.Running, false)]
    [InlineData(StartType.Automatic, EntryStatus.Stopped, false)]
    public void A_disabled_entry_that_is_running_anyway_is_its_own_signal(
        StartType startType, EntryStatus status, bool expected)
    {
        var entry = Entry(status, Reading<StartType>.Present(startType));

        Assert.Equal(ReadOutcome.Present, entry.RunsWhileDisabled.Outcome);
        Assert.Equal(expected, entry.RunsWhileDisabled.Value);
    }

    /// <summary>
    /// AND THE TWO ARE NEVER TRUE TOGETHER, which is what makes one enumeration field able to
    /// carry both without losing the direction - backlog 172.
    ///
    /// One needs an automatic entry that is stopped and the other a disabled entry that is running,
    /// so no entry can wear both marks. Asserted rather than assumed, because the query field
    /// leans on it.
    /// </summary>
    [Theory]
    [InlineData(StartType.Automatic, EntryStatus.Stopped)]
    [InlineData(StartType.Disabled, EntryStatus.Running)]
    [InlineData(StartType.Manual, EntryStatus.Running)]
    public void No_entry_disagrees_with_itself_in_both_directions_at_once(
        StartType startType, EntryStatus status)
    {
        var entry = Entry(status, Reading<StartType>.Present(startType));

        Assert.False(entry.RunsAgainstItsStartType.Value && entry.RunsWhileDisabled.Value);
    }

    /// <summary>
    /// A start type nobody could read leaves this unknown too, for the reason its sibling gives:
    /// "I could not check" must never render as "everything is fine".
    /// </summary>
    [Fact]
    public void An_unreadable_start_type_leaves_the_other_direction_unknown_as_well()
    {
        var refused = Entry(EntryStatus.Running, Reading<StartType>.Denied(5, "access denied"));

        Assert.Equal(ReadOutcome.Denied, refused.RunsWhileDisabled.Outcome);
        Assert.Equal(5, refused.RunsWhileDisabled.ErrorCode);

        var unread = Entry(EntryStatus.Running, Reading<StartType>.NotRead());

        Assert.Equal(ReadOutcome.NotRead, unread.RunsWhileDisabled.Outcome);
    }

    private static ScmEntry Entry(EntryStatus status, Reading<StartType> startType) =>
        Entries.Any with
        {
            Status = status,
            ProcessId = Reading<int>.Absent(),
            StartType = startType
        };
}
