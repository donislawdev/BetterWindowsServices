using System.IO;
using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// An administrator window under User Account Control creates, writes and moves nothing in the
/// profile - security report S-7, owner's decision of 2026-10-06.
///
/// <b>Why nothing at all.</b> The folder it would write into is named by the account's own HKCU User
/// Shell Folders, which that account holds with full control, and the path follows the environment
/// the elevated process inherits from it (both measured that day, docs/PROJEKT-PACZKA-SD-20261006.md).
/// The half of the account WITHOUT elevation chooses the place, so a check of the path would not have
/// closed it.
///
/// <b>Each claim has its control beside it</b> - the same file handed the other answer does write, so
/// a build that never wrote anything would not pass here.
/// </summary>
public sealed class ProfileLeftAloneGuards : IDisposable
{
    private readonly List<string> _made = [];

    [Fact]
    public void An_administrator_window_creates_no_folder_and_writes_nothing()
    {
        var directory = Unmade();
        var file = new PreferencesFile(directory, leavesTheProfileAlone: true);

        Assert.Equal(Texts.Of("gui.layout.notKeptAsAdministrator"), file.Write(ColumnLayouts.Default));

        var kept = new KeptColumns(file);
        kept.TheFiltersWere(folded: true, new Says());
        kept.TheOverviewWasSeen(new Says());

        Assert.False(Directory.Exists(directory));

        // The control: the same place, an ordinary window, and the folder and the file appear.
        Assert.Null(new PreferencesFile(directory, leavesTheProfileAlone: false).Write(ColumnLayouts.Default));
        Assert.True(File.Exists(Path.Combine(directory, PreferencesFile.Name)));
    }

    [Fact]
    public void An_administrator_window_leaves_a_damaged_layout_where_it_is_and_says_so()
    {
        var file = Made(leavesTheProfileAlone: true);

        File.WriteAllText(file.Where, "not a layout");

        var reading = file.Read();
        var kept = new KeptColumns(file);

        Assert.Equal("not a layout", File.ReadAllText(file.Where));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(file.Where)!));
        Assert.Null(reading.MovedAside);

        // Its own sentence - not the one about a move that failed, because no move was tried.
        Assert.Equal(Texts.Of("gui.layout.unreadableLeftAsAdministrator", reading.Unreadable!), kept.Trouble([]));

        // The control: an ordinary window moves the same file aside.
        Assert.NotNull(MovedBy(leavesTheProfileAlone: false));
    }

    [Fact]
    public void An_administrator_window_leaves_a_file_too_big_to_be_a_layout_where_it_is()
    {
        var file = Made(leavesTheProfileAlone: true);

        using (var stream = File.Create(file.Where))
        {
            stream.SetLength((1024 * 1024) + 1);
        }

        _ = new KeptColumns(file);

        Assert.True(File.Exists(file.Where));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(file.Where)!));
    }

    [Fact]
    public void An_administrator_window_still_reads_the_layout_its_person_kept()
    {
        var file = Made(leavesTheProfileAlone: true);

        File.WriteAllText(file.Where, (ColumnLayouts.Default with { FiltersFolded = true }).Render());

        Assert.True(new KeptColumns(file).FiltersFolded);
    }

    [Fact]
    public void The_window_asks_the_session_whether_to_leave_the_profile_alone()
    {
        Assert.Equal(Session.IsElevatedWithTwin(), new PreferencesFile().LeavesTheProfileAlone);
    }

    /// <summary>Where a damaged file went in a window of the given kind, or null when it stayed.</summary>
    private string? MovedBy(bool leavesTheProfileAlone)
    {
        var file = Made(leavesTheProfileAlone);

        File.WriteAllText(file.Where, "not a layout");

        return file.Read().MovedAside;
    }

    private string Unmade()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "bws-profile-left-alone-tests",
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));

        _made.Add(directory);

        return directory;
    }

    private PreferencesFile Made(bool leavesTheProfileAlone)
    {
        var directory = Unmade();

        Directory.CreateDirectory(directory);

        return new PreferencesFile(directory, leavesTheProfileAlone);
    }

    public void Dispose()
    {
        foreach (var directory in _made.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
