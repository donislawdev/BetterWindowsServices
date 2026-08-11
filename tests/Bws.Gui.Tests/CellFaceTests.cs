using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What a row says about an entry, and in what shape.
///
/// Asked through <see cref="EntryRow"/> rather than through the mapping behind it, because the
/// row is what the window binds to - a test against the private half could stay green while the
/// row stopped passing anything on.
///
/// <b>None of this needs a window and none of it can see one.</b> Whether the colour a shape
/// stands for reaches a pixel is a different question with a different instrument -
/// tools/gui-probe/check.ps1 - and this project has been caught believing a property read four
/// times. What is held here is the half that a screenshot cannot check: that the codes are
/// distinct, that they are not the words, and that four read states still say four things.
/// </summary>
public sealed class CellFaceTests
{
    [Fact]
    public void Doing_something_and_not_doing_it_take_different_shapes()
    {
        Assert.Equal(CellShapes.Running, Row(Entry() with { Status = EntryStatus.Running }).StatusShape);
        Assert.Equal(CellShapes.Stopped, Row(Entry() with { Status = EntryStatus.Stopped }).StatusShape);
        Assert.Equal(CellShapes.Paused, Row(Entry() with { Status = EntryStatus.Paused }).StatusShape);

        foreach (var moving in new[]
                 {
                     EntryStatus.StartPending,
                     EntryStatus.StopPending,
                     EntryStatus.ContinuePending,
                     EntryStatus.PausePending
                 })
        {
            Assert.Equal(CellShapes.Transit, Row(Entry() with { Status = moving }).StatusShape);
        }
    }

    /// <summary>
    /// Every state the manager has says something, and none of them says it by being blank.
    /// Rule 8: an empty cell means "there is none", which is the one thing none of these are.
    /// </summary>
    [Fact]
    public void No_status_the_manager_can_report_comes_out_empty()
    {
        foreach (var status in Enum.GetValues<EntryStatus>())
        {
            var row = Row(Entry() with { Status = status });

            Assert.False(string.IsNullOrWhiteSpace(row["status"]), $"{status} has no word");
            Assert.False(string.IsNullOrWhiteSpace(row.StatusShape), $"{status} has no shape");
        }
    }

    /// <summary>
    /// The shape is a code and the word is a translation, and the theme matches on the first.
    ///
    /// <b>This is the whole reason they are two properties.</b> A DataTrigger comparing against
    /// "Running" works today and stops working the day a second language file arrives - and it
    /// stops silently, because a trigger that matches nothing simply leaves the default in
    /// place. The row would lose its colour and nothing anywhere would say why.
    /// </summary>
    [Fact]
    public void The_shape_is_never_the_word()
    {
        foreach (var status in Enum.GetValues<EntryStatus>())
        {
            var row = Row(Entry() with { Status = status });

            Assert.NotEqual(row["status"], row.StatusShape);
        }
    }

    [Fact]
    public void The_start_type_carries_what_changes_its_meaning()
    {
        var row = Row(Entry() with
        {
            StartType = Reading<StartType>.Present(StartType.Automatic),
            DelayedAuto = Reading<bool>.Present(true),
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
                [new ServiceTrigger(TriggerKind.Unknown, TriggerAction.Start)]),
            BinaryOnDisk = Reading<bool>.Present(false)
        });

        Assert.Contains("delayed", row["startType"], StringComparison.Ordinal);
        Assert.Contains("trigger", row["startType"], StringComparison.Ordinal);
        Assert.Contains("missing", row["startType"], StringComparison.Ordinal);
    }

    /// <summary>
    /// Four of the ten entries that read as "automatic and not running" on a real machine were
    /// waiting to be asked for rather than broken. Without this the window sends somebody to
    /// investigate four healthy services first.
    /// </summary>
    [Fact]
    public void Waiting_for_a_trigger_is_said_in_words_and_not_marked_as_a_fault()
    {
        var row = Row(Entry() with
        {
            Status = EntryStatus.Stopped,
            StartType = Reading<StartType>.Present(StartType.Automatic),
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
                [new ServiceTrigger(TriggerKind.Unknown, TriggerAction.Start)])
        });

        Assert.Contains("trigger", row["startType"], StringComparison.Ordinal);
        Assert.Equal(CellShapes.Ordinary, row.StartShape);
    }

    /// <summary>
    /// A trigger that STOPS an entry says nothing about whether it will come up, so it must not
    /// be reported where a reader is deciding whether a stopped service is broken.
    /// </summary>
    [Fact]
    public void A_trigger_that_only_stops_the_entry_is_not_reported_as_one_that_starts_it()
    {
        var row = Row(Entry() with
        {
            StartType = Reading<StartType>.Present(StartType.Automatic),
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
                [new ServiceTrigger(TriggerKind.Unknown, TriggerAction.Stop)])
        });

        Assert.DoesNotContain("trigger", row["startType"], StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_file_outranks_being_switched_off()
    {
        var row = Row(Entry() with
        {
            StartType = Reading<StartType>.Present(StartType.Disabled),
            BinaryOnDisk = Reading<bool>.Present(false)
        });

        Assert.Equal(CellShapes.Missing, row.StartShape);
    }

    [Fact]
    public void Being_switched_off_is_a_fact_rather_than_a_shade_of_one()
    {
        Assert.Equal(
            CellShapes.Disabled,
            Row(Entry() with { StartType = Reading<StartType>.Present(StartType.Disabled) }).StartShape);

        Assert.Equal(
            CellShapes.Ordinary,
            Row(Entry() with { StartType = Reading<StartType>.Present(StartType.Manual) }).StartShape);
    }

    /// <summary>
    /// The delay flag only means something where Windows acts on it. "Manual (delayed)" would be
    /// a sentence about a setting with no effect, which was true of eight entries on the machine
    /// where this was measured.
    /// </summary>
    [Fact]
    public void A_delay_flag_is_only_said_where_it_does_anything()
    {
        var row = Row(Entry() with
        {
            StartType = Reading<StartType>.Present(StartType.Manual),
            DelayedAuto = Reading<bool>.Present(true)
        });

        Assert.DoesNotContain("delayed", row["startType"], StringComparison.Ordinal);
    }

    /// <summary>
    /// A refusal is not an absence and neither is a reading nobody took. Both used to arrive as
    /// the manager's own enum name and now go through the same three-way split the other cells
    /// have had since S6a.
    /// </summary>
    [Fact]
    public void A_refused_start_type_says_so_rather_than_naming_a_type()
    {
        var denied = Row(Entry() with { StartType = Reading<StartType>.Denied(5, "Access is denied.") });
        var absent = Row(Entry() with { StartType = Reading<StartType>.Absent() });

        Assert.Equal(CellShapes.Unknown, denied.StartShape);
        Assert.NotEqual(string.Empty, denied["startType"]);
        Assert.Equal(string.Empty, absent["startType"]);
    }

    /// <summary>
    /// The reading nobody has taken yet, which is the fourth state and the one that had no test.
    ///
    /// <b>NotRead is not Denied and it is not Absent</b>, and the whole of `ADR-13` depends on the
    /// difference: expensive fields arrive in a second pass, so between the two passes every one
    /// of them is legitimately unread. A cell that showed those as "no access" would accuse the
    /// machine of refusing something nobody had asked for yet.
    ///
    /// Found uncovered on 2026-08-11 while repairing backlog 161 - the branch existed, was
    /// correct, and nothing had ever executed it.
    /// </summary>
    [Fact]
    public void A_start_type_nobody_has_read_yet_is_not_a_refusal_and_not_an_absence()
    {
        var unread = Row(Entry() with { StartType = Reading<StartType>.NotRead() });
        var denied = Row(Entry() with { StartType = Reading<StartType>.Denied(5, "Access is denied.") });
        var absent = Row(Entry() with { StartType = Reading<StartType>.Absent() });

        Assert.Equal(CellShapes.Unknown, unread.StartShape);
        Assert.NotEqual(string.Empty, unread["startType"]);

        // The three say three things. Without this the test above passes on a cell that answers
        // "unknown" to every question it was not given a value for, which is the collapse this
        // project spends most of its rules preventing.
        Assert.NotEqual(denied["startType"], unread["startType"]);
        Assert.NotEqual(absent["startType"], unread["startType"]);
    }

    /// <summary>
    /// A start type the manager gave us and we have no name for.
    ///
    /// <c>StartType.Unknown</c> is the zero of that enum, so it is what a value outside the five
    /// Windows documents becomes. Naming it "unknown" rather than printing the number is the same
    /// rule as everywhere else here, and it is worth a test because the alternative - a cell
    /// showing <c>0</c> - looks like data rather than like a gap.
    /// </summary>
    [Fact]
    public void A_start_type_outside_the_five_is_named_rather_than_numbered()
    {
        var strange = Row(Entry() with { StartType = Reading<StartType>.Present(StartType.Unknown) });

        Assert.NotEqual(string.Empty, strange["startType"]);
        Assert.DoesNotContain("0", strange["startType"], StringComparison.Ordinal);

        // It is present, so it is not the shape for something nobody could read - the entry has
        // an answer, we just have no word of our own for it.
        Assert.Equal(CellShapes.Ordinary, strange.StartShape);
    }

    /// <summary>
    /// Automatic, where the machine refused to say whether the delay applies.
    ///
    /// <b>The branch below "delayed" and it is the one that was never run.</b> Windows acts on
    /// the delay flag only for automatic entries, so the qualifier asks about it only there - and
    /// when the flag itself was refused, the cell has to say the delay is unknown rather than
    /// silently reading as a plain automatic entry, which is rule 8 at the width of one word.
    /// </summary>
    [Fact]
    public void An_automatic_entry_whose_delay_flag_was_refused_says_the_delay_is_unknown()
    {
        var refused = Row(Entry() with
        {
            StartType = Reading<StartType>.Present(StartType.Automatic),
            DelayedAuto = Reading<bool>.Denied(5, "Access is denied.")
        });

        var plain = Row(Entry() with
        {
            StartType = Reading<StartType>.Present(StartType.Automatic),
            DelayedAuto = Reading<bool>.Present(false)
        });

        Assert.NotEqual(plain["startType"], refused["startType"]);
        Assert.Contains(plain["startType"], refused["startType"], StringComparison.Ordinal);
    }

    private static EntryRow Row(ScmEntry entry) => EntryRow.Of(entry);

    // Rows.cs, shared with MainViewModelTests. It arrived here as a second copy of the same
    // twenty lines and the size ratchet is what noticed.
    private static ScmEntry Entry() => Rows.Entry("Spooler", "Print Spooler");
}
