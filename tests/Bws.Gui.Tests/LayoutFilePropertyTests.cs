using System.IO;
using Bws.Gui.ViewModels;
using CsCheck;

namespace Bws.Gui.Tests;

/// <summary>
/// The window's layout file read back after it was damaged or written by hand - step 3b of the
/// fuzzing project, point C5 (docs/PROJEKT-FUZZING-20261016.md, outside the repository).
///
/// <b>Two promises, one after the other, and the second is the one a person meets.</b>
/// ColumnLayouts.Read promises an answer or a reason and never a throw - the file answers to nobody.
/// ColumnPlan.Of promises that what the grid is then handed is whole: every column this build has,
/// exactly once, at least one of them shown. A reader that kept the first promise and handed on a
/// layout breaking the second would open a window with a column missing or a list of nothing.
///
/// <b>The reading has exactly one of three ends</b> - a layout, a reason it is not one, or a schema
/// this build leaves alone. Opened and moved aside are the file's business (PreferencesFile), not the
/// text's.
/// </summary>
public sealed class LayoutFilePropertyTests
{
    private static readonly EntryScope[] Scopes = [EntryScope.Services, EntryScope.Drivers, EntryScope.Everything];

    [Fact]
    [Trait("search", "weekly")]
    public void A_damaged_layout_file_is_read_or_named_and_what_is_read_is_whole()
    {
        var original = Arranged().Render();

        Damaged.In(original).Searched(damage => Holds(damage.To(original, '}')), new(20_000));
    }

    [Fact]
    [Trait("search", "weekly")]
    public void A_layout_file_written_by_hand_is_read_or_named_and_what_is_read_is_whole()
    {
        LayoutTexts.Shaped.Searched(Holds, new(10_000));
    }

    /// <summary>
    /// A file naming a key twice is not a layout, and is said to be one rather than taking the window
    /// down - backlog 591. Measured 2026-10-17 before these properties were written: JsonNode accepts
    /// the repeat and throws ArgumentException at the first look inside, which the reader did not
    /// catch, so the window would not have opened while the file stayed where it was.
    /// </summary>
    [Theory]
    [InlineData("""{"schemaVersion":5,"columns":[],"columns":[]}""")]
    [InlineData("""{"schemaVersion":5,"schemaVersion":4,"columns":[]}""")]
    [InlineData("""{"schemaVersion":5,"columns":[{"id":"status","id":"account"}]}""")]
    [InlineData("""{"schemaVersion":5,"columns":[],"sort":{"id":"status","id":"account"}}""")]
    public void A_layout_naming_a_key_twice_is_named_rather_than_thrown_over(string text)
    {
        var reading = ColumnLayouts.Read(text);

        Assert.Null(reading.Layouts);
        Assert.NotNull(reading.Unreadable);
    }

    /// <summary>
    /// The same fault as a person meets it: a real window over a layout file naming a key twice. It is
    /// built, and the file is moved aside with its bytes as they were - the door every unreadable layout
    /// goes through. Before 591 the constructor threw, which is what the mutation entry for this test
    /// shows by putting the repeat back.
    ///
    /// <b>The file aside is the one the line under the list names</b>, which is what the changelog
    /// promises a person - review of #76 asked that the file be shown to come from the quarantine, and
    /// the window's own sentence says that without a copy of how the quarantine spells its names.
    /// </summary>
    [Fact]
    public void A_window_whose_layout_file_names_a_key_twice_opens_and_moves_the_file_aside()
    {
        _ = WpfHost.Resources;

        const string written = """{"schemaVersion":5,"columns":[],"columns":[]}""";
        var directory = Path.Combine(Path.GetTempPath(), "bws-keytwice-" + Guid.NewGuid().ToString("N"));
        var kept = Path.Combine(directory, PreferencesFile.Name);

        Directory.CreateDirectory(directory);
        File.WriteAllText(kept, written);

        try
        {
            var (window, model) = WpfHost.On(() =>
            {
                var watched = new MainViewModel();

                return (new MainWindow(new PreferencesFile(directory), watched), watched);
            });

            // Asked before the window closes, because closing writes the layout it opened with.
            var left = Directory.GetFiles(directory);
            var said = WpfHost.On(() => model.Says.Problem);

            WpfHost.On(window.Close);

            Assert.DoesNotContain(kept, left);

            var aside = Assert.Single(left);

            Assert.Contains(aside, said, StringComparison.Ordinal);
            Assert.Equal(written, File.ReadAllText(aside));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The control for the theory above: the same file with each key once is a layout.</summary>
    [Fact]
    public void The_same_layout_naming_each_key_once_is_a_layout()
    {
        var reading = ColumnLayouts.Read("""{"schemaVersion":5,"columns":[{"id":"status"}],"sort":{"id":"status"}}""");

        Assert.Null(reading.Unreadable);
        Assert.Equal("status", reading.Layouts?.Services.Sort?.Id);
    }

    /// <summary>
    /// Whether one text keeps both promises. Throws with what went wrong rather than answering false,
    /// so a failure says WHICH promise and about which list, not only the text that broke it.
    /// </summary>
    private static bool Holds(string text)
    {
        var reading = ColumnLayouts.Read(text);

        var ends = new[] { reading.Layouts is not null, reading.Unreadable is not null, reading.OtherSchemaVersion is not null }
            .Count(end => end);

        if (ends != 1)
        {
            throw new InvalidOperationException($"The reading ended {ends} ways at once rather than one.");
        }

        var broken = reading.Layouts is { } layouts
            ? Scopes.Select(scope => Broken(ColumnPlan.Of(layouts.For(scope), scope), scope)).FirstOrDefault(said => said is not null)
            : null;

        if (broken is not null)
        {
            throw new InvalidOperationException(broken);
        }

        return true;
    }

    /// <summary>What is wrong with the list the grid would be handed, or nothing.</summary>
    private static string? Broken(ColumnPlan plan, EntryScope scope)
    {
        var ids = plan.Layout.Columns.Select(column => column.Id).ToList();

        if (ids.Count != Columns.All.Count || !ids.ToHashSet(StringComparer.Ordinal).SetEquals(Columns.All.Select(column => column.Id)))
        {
            return $"The {scope} list came out as [{string.Join(", ", ids)}] rather than every column once.";
        }

        return plan.Layout.Columns.Any(column => column.Shown) ? null : $"The {scope} list came out with nothing shown.";
    }

    /// <summary>
    /// A layout somebody has used: widths dragged on a third of the columns, shares on another third,
    /// columns turned on, a sort on two lists and both flags set - so that damage can land inside every
    /// kind of value the file holds, not only inside the default one.
    /// </summary>
    private static ColumnLayouts Arranged() => new(
        Used(EntryScope.Services) with { Sort = new KeptSort("status", Descending: true) },
        Used(EntryScope.Drivers),
        Used(EntryScope.Everything) with { Sort = new KeptSort("serviceName", Descending: false) })
    {
        OverviewSeen = true,
        FiltersFolded = true
    };

    private static ColumnLayout Used(EntryScope scope)
    {
        var start = ColumnLayout.DefaultFor(scope);

        return start with
        {
            Columns =
            [
                .. start.Columns.Select((column, at) => column with
                {
                    Shown = column.Shown || at % 2 == 0,
                    Width = (at % 3) switch
                    {
                        0 => "220",
                        1 => "2.5*",
                        _ => null
                    }
                })
            ]
        };
    }
}
