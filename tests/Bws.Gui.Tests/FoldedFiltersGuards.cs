using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The filter chips open the way this profile last left them - UX-GUI-007, schema 5 of the one file
/// this program keeps, owner's decision 2026-09-24. Until that day a fold was forgotten at every
/// start, and the chips took nearly half the height of the window above the list.
///
/// <b>Read back through a second reader</b>, for the reason the overview's own guard gives: a value
/// held in memory proves nothing about a file.
/// </summary>
public sealed class FoldedFiltersGuards
{
    /// <summary>Folding is written down at the press, and so is opening again.</summary>
    [Fact]
    public void Folding_the_filters_is_written_down_and_read_back()
    {
        var file = WpfHost.Nowhere();

        Assert.False(new KeptColumns(file).FiltersFolded);

        new KeptColumns(file).TheFiltersWere(folded: true, new Says());
        Assert.True(new KeptColumns(file).FiltersFolded);

        new KeptColumns(file).TheFiltersWere(folded: false, new Says());
        Assert.False(new KeptColumns(file).FiltersFolded);
    }

    /// <summary>
    /// Two facts in one file, and writing either keeps the other - the overview put away stays put
    /// away when the chips are folded, and the other way round.
    /// </summary>
    [Fact]
    public void Folding_the_filters_and_putting_the_overview_away_keep_each_other()
    {
        var file = WpfHost.Nowhere(seenTheOverview: false);

        new KeptColumns(file).TheFiltersWere(folded: true, new Says());
        new KeptColumns(file).TheOverviewWasSeen(new Says());

        var read = new KeptColumns(file);

        Assert.True(read.FiltersFolded);
        Assert.True(read.OverviewSeen);
    }

    /// <summary>
    /// A window opens with the chips the way the file says, and a press on the toggle is written -
    /// asked of the real toggle, so a wiring that never reached it goes red here.
    /// </summary>
    [Fact]
    public void A_window_opens_folded_when_it_was_left_folded_and_writes_the_next_press()
    {
        var file = WpfHost.Nowhere();
        new KeptColumns(file).TheFiltersWere(folded: true, new Says());

        _ = WpfHost.Resources;
        var window = WpfHost.On(() => new MainWindow(file));

        Assert.False(WpfHost.On(() => window.Filters.Switch.IsChecked));

        WpfHost.On(() => window.Filters.Switch.IsChecked = true);

        Assert.False(new KeptColumns(file).FiltersFolded);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A first run - no file at all - opens with the chips showing. Complaint 7 of `docs/11` was
    /// closed by making them visible, and remembering a fold must not reopen it.
    /// </summary>
    [Fact]
    public void A_first_run_opens_with_the_filters_showing()
    {
        var window = WpfHost.Window();

        Assert.True(WpfHost.On(() => window.Filters.Switch.IsChecked));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The key is written only when the chips are folded, and in the ordinal place the file keeps
    /// its keys in - after everythingSort, before overviewSeen - so a file diffs against yesterday's
    /// copy on nothing.
    /// </summary>
    [Fact]
    public void The_fold_is_written_only_when_folded_and_in_its_place()
    {
        Assert.DoesNotContain("filtersFolded", ColumnLayouts.Default.Render(), StringComparison.Ordinal);

        var both = (ColumnLayouts.Default with { FiltersFolded = true, OverviewSeen = true }).Render();

        Assert.Contains("\"filtersFolded\": true", both, StringComparison.Ordinal);
        Assert.True(
            both.IndexOf("filtersFolded", StringComparison.Ordinal) < both.IndexOf("overviewSeen", StringComparison.Ordinal),
            "filtersFolded should come before overviewSeen in the file");
    }
}
