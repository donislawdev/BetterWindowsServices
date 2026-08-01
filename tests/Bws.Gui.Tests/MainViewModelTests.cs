using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the window will show, checked without opening one.
///
/// The list is the whole of this slice, and the interesting decisions in it are not about
/// markup: which entries appear, and what a cell says when the value behind it is one of the
/// four read states. Both live in the view model, which is why it exists as a layer at all.
///
/// A screenshot is still the other half of the check, and it is a person's job. This half is
/// the one that can fail on its own at three in the morning.
/// </summary>
public sealed class MainViewModelTests
{
    [Fact]
    public async Task Every_entry_the_manager_hands_over_becomes_a_row()
    {
        // Nothing filtered, nothing collapsed, nothing dropped. Filtering arrives in its own
        // slice, and a list that quietly showed fewer entries than the machine has would be
        // the silence rule 8 forbids, in the one place a person would believe it.
        var entries = new[] { Entry("Spooler"), Entry("BFE"), Entry("Winmgmt") };

        var model = new MainViewModel(() => entries);
        var rows = await model.LoadAsync();

        Assert.Equal(3, rows.Count);
        Assert.Equal(3, model.Entries.Count);
        Assert.Equal(["Spooler", "BFE", "Winmgmt"], model.Entries.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task The_line_under_the_list_says_how_many_there_are()
    {
        var model = new MainViewModel(() => [Entry("Spooler")]);

        await model.LoadAsync();

        Assert.Contains("1", model.Status, StringComparison.Ordinal);
        Assert.False(model.Incomplete);
    }

    [Fact]
    public async Task A_manager_that_will_not_open_is_reported_instead_of_taking_the_window_down()
    {
        // The window has to survive it and say what happened. A dialog nobody can act on, or
        // a crash, would both be worse than a sentence under an empty list.
        var model = new MainViewModel(() => throw new InvalidOperationException("no manager here"));

        var rows = await model.LoadAsync();

        Assert.Empty(rows);
        Assert.True(model.Incomplete);
        Assert.Contains("no manager here", model.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_four_read_states_do_not_look_alike_in_a_cell()
    {
        // The whole reason this project has four states, arriving at the last place it could
        // be undone. A value nobody could read must not render like a value nobody has - one
        // of those says the machine is set up that way, the other says we do not know.
        var model = new MainViewModel(() =>
        [
            Entry("Present") with { Account = Reading<string>.Present("LocalSystem") },
            Entry("Absent") with { Account = Reading<string>.Absent() },
            Entry("Denied") with { Account = Reading<string>.Denied(5, "Access is denied.") },
            Entry("NotRead") with { Account = Reading<string>.NotRead() }
        ]);

        await model.LoadAsync();

        var cells = model.Entries.ToDictionary(row => row.ServiceName, row => row.Account, StringComparer.Ordinal);

        Assert.Equal("LocalSystem", cells["Present"]);
        Assert.Equal(string.Empty, cells["Absent"]);

        // The two that are not values say so in words, and they do not say the same words.
        Assert.NotEqual(string.Empty, cells["Denied"]);
        Assert.NotEqual(string.Empty, cells["NotRead"]);
        Assert.NotEqual(cells["Denied"], cells["NotRead"]);
    }

    [Fact]
    public void Not_one_string_a_person_reads_is_written_into_the_code()
    {
        // Rule 13, checked rather than trusted. Every one of these comes from the language
        // file, so a key that is missing shows up as the key itself - which is ugly on screen
        // and impossible to miss, unlike a sentence quietly hard-coded in English.
        foreach (var key in new[]
                 {
                     "gui.window.title", "gui.column.name", "gui.column.displayName",
                     "gui.column.status", "gui.column.startType", "gui.column.account",
                     "gui.column.processId", "gui.status.reading", "gui.status.read",
                     "gui.status.failed", "gui.cell.unknown", "gui.cell.noAccess"
                 })
        {
            Assert.NotEqual(key, Bws.Gui.Texts.Of(key));
        }
    }

    private static ScmEntry Entry(string name) => new()
    {
        ServiceName = name,
        DisplayName = name + " display name",
        EntryType = EntryType.OwnProcess,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1234),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Absent(),
        Account = Reading<string>.Present("LocalSystem"),
        DependsOn = Reading<IReadOnlyList<string>>.Absent(),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
        BinaryPath = Reading<string>.Absent(),
        BinaryFile = Reading<string>.Absent(),
        BinaryOnDisk = Reading<bool>.Absent(),
        Signature = Reading<BinarySignature>.NotRead(),
        FileVersion = Reading<string>.NotRead(),
        BinaryHash = Reading<string>.NotRead(),
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
        SidType = Reading<ServiceSidType>.Absent(),
        SecurityDescriptor = Reading<string>.Absent(),
        ErrorControl = Reading<ErrorControl>.Absent(),
        LoadOrderGroup = Reading<string>.Absent(),
        Memory = Reading<ProcessMemory>.NotRead()
    };
}
