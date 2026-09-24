using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The details panel as a neighbour of the list - UX-GUI-012 and the two faults found beside it.
///
/// <b>Measured before the change, at the size the window opens at:</b> the panel held a fixed 380
/// points, the list lost a third of its width and cut four of its five columns, and on a maximised
/// window the same panel was a narrow strip beside a thousand pixels of empty list. The owner chose
/// the variant built and photographed as C (2026-09-24): the panel takes a third of the window
/// within two named limits, and a prose column - the description - leaves the list while the panel,
/// which shows it in full, is open.
///
/// <b>The trap that decision walked into is the reason half of this class exists.</b> The saved
/// layout reads which columns are on off the grid, so a column hidden for the panel would have been
/// written to the profile as a column somebody turned off, silently, on every close with the panel
/// open.
/// </summary>
public sealed class PanelBesideTheListGuards
{
    [Fact]
    public void An_open_panel_takes_the_description_off_the_list_and_leaves_it_chosen()
    {
        var bar = new ColumnBar();
        var description = Choice(bar, "description");
        Assert.True(description.IsShown);

        bar.PanelOpen = true;

        Assert.True(description.IsShown, "Opening the panel changed what somebody chose.");
        Assert.False(description.IsOnList, "The description stayed on the list beside a panel that shows it in full.");
        Assert.Equal(Texts.Of("gui.columns.yielded", Texts.Of("gui.column.description")), description.Label);

        bar.PanelOpen = false;

        Assert.True(description.IsOnList);
        Assert.Equal(Texts.Of("gui.column.description"), description.Label);
    }

    /// <summary>A list of nothing is not a list - the panel may not take the only column left.</summary>
    [Fact]
    public void The_panel_never_takes_the_last_column_off_the_list()
    {
        var bar = new ColumnBar();

        foreach (var choice in bar.Choices.Where(choice => choice.Column.Id != "description"))
        {
            choice.IsShown = false;
        }

        bar.PanelOpen = true;

        Assert.True(Choice(bar, "description").IsOnList);
    }

    /// <summary>
    /// With the description away, the one column left on the list is the last one - and the picker
    /// has to know that, or somebody could leave the list empty while the panel is open.
    /// </summary>
    [Fact]
    public void With_the_panel_open_the_one_column_left_on_the_list_may_not_be_hidden()
    {
        var bar = new ColumnBar();

        foreach (var choice in bar.Choices.Where(choice => choice.Column.Id is not ("displayName" or "description")))
        {
            choice.IsShown = false;
        }

        bar.PanelOpen = true;

        Assert.False(Choice(bar, "displayName").MayHide);
        Assert.True(Choice(bar, "description").MayHide);
    }

    [Fact]
    public void A_column_away_for_the_panel_is_kept_as_chosen_in_the_saved_layout()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        WpfHost.Settled();

        WpfHost.On(() => model.Chosen.Row = EntryRow.Of(Rows.Entry("Spooler")));
        WpfHost.On(() => model.Chosen.Show());
        WpfHost.Settled();

        var (visibility, kept) = WpfHost.On(() =>
        (
            window.Entries.Columns.Single(column => column.SortMemberPath == "description").Visibility,
            ListColumns.Harvest(window.Entries).Columns.Single(column => column.Id == "description").Shown));

        Assert.Equal(Visibility.Collapsed, visibility);
        Assert.True(kept, "The saved layout would record the description as turned off by somebody.");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A third of the window, never narrower than the lower limit nor wider than the upper one -
    /// measured on the laid out panel rather than read off the markup, because a width written in
    /// markup and ignored by the layout looks exactly like one that works.
    /// </summary>
    [Theory]
    [InlineData(700)]
    [InlineData(1100)]
    [InlineData(2560)]
    public void The_panel_takes_a_third_of_the_window_within_its_limits(double wide)
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        WpfHost.Settled();

        WpfHost.On(() => model.Chosen.Row = EntryRow.Of(Rows.Entry("Spooler")));
        WpfHost.On(() => model.Chosen.Show());
        WpfHost.Settled();

        var (panel, least, most, whole) = WpfHost.On(() =>
        {
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(wide, 900));
            root.Arrange(new Rect(0, 0, wide, 900));
            root.UpdateLayout();

            return (
                window.DetailsPanel.ActualWidth,
                (double)window.FindResource("WidthDetailsPanelLeast"),
                (double)window.FindResource("WidthDetailsPanelMost"),
                window.Entries.ActualWidth + window.DetailsPanel.ActualWidth);
        });

        var third = Math.Clamp(whole / 3, least, most);

        Assert.InRange(panel, third - 1, third + 1);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The machine overview has the middle of the window, so a panel about one row of a list that
    /// is not on screen goes away - found by reading the code on 2026-09-24: the overview spans
    /// both columns and the panel is declared after it, so it was drawn on top.
    /// </summary>
    [Fact]
    public void The_overview_puts_the_panel_away()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        WpfHost.Settled();

        WpfHost.On(() => model.Chosen.Row = EntryRow.Of(Rows.Entry("Spooler")));
        WpfHost.On(() => model.Chosen.Show());
        WpfHost.On(() => model.ShowingOverview = true);
        WpfHost.Settled();

        Assert.False(model.Chosen.Showing, "The panel stayed open over the machine overview.");
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Enter on another entry after the first one left the listing - found by reading the code on
    /// 2026-09-24: opening never cleared the notice, so the new entry wore the old one's sentence.
    /// </summary>
    [Fact]
    public void Opening_the_panel_on_another_entry_does_not_carry_the_last_ones_notice()
    {
        var gone = EntryRow.Of(Rows.Entry("Spooler"));
        var other = EntryRow.Of(Rows.Entry("Dnscache"));
        var chosen = new Chosen { Row = gone };

        chosen.Show();
        chosen.StillIn([other]);
        Assert.True(chosen.Gone);

        chosen.Row = other;
        chosen.Show();

        Assert.False(chosen.Gone);
        Assert.Equal(string.Empty, chosen.Notice);
    }

    /// <summary>The file and its signature are what an administrator opens the panel for - owner's decision, 2026-09-24.</summary>
    [Fact]
    public void What_it_runs_comes_before_what_kind_of_entry_it_is()
    {
        var headings = Details.Shown(Rows.Entry("Spooler")).Select(section => section.Heading);

        Assert.Equal(
            [Texts.Of(Columns.Basics), Texts.Of(Columns.Binary), Texts.Of(Columns.About), Texts.Of(Columns.Advanced)],
            headings);
    }

    private static ColumnChoice Choice(ColumnBar bar, string id) => bar.Choices.Single(choice => choice.Column.Id == id);
}
