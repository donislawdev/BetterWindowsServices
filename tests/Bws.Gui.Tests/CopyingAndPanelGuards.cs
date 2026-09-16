// Explicit, because UseWPF swaps the implicit using set.
using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The two things a person does with one entry - copy it, and look at it - driven through the
/// window rather than through the view model.
///
/// <b>WRITTEN 2026-08-17 TO OVERTURN A SENTENCE, and the sentence is the interesting part.</b>
/// <c>WindowGuards</c> carried this: "the clipboard call itself needs a window and cannot be
/// tested". Rule 9 of the project notes is about exactly that shape - a recorded impossibility has
/// no guard, later sessions treat it as a fact, and nobody looks again. It says to check whether
/// the limit comes from the PROBLEM or from the MECHANISM somebody chose.
///
/// It was the mechanism. <see cref="WpfHost"/> runs a real Application on a real STA thread and
/// these tests already build real windows on it, so <c>Clipboard</c> works here exactly as it does
/// in the product. Four features the owner asked for on 2026-08-13 - Ctrl+C and three menu items -
/// had no end-to-end cover at all because of one sentence.
///
/// <b>WHY THIS IS NOT A PROBE.</b> tools/gui-probe drives the window through UI Automation, and
/// focus-probe measured years ago that of six ways of posting input to a window without the
/// foreground, one arrives. Keys are not among them, and a probe that stole the foreground would
/// switch on `A10` and freeze the list it was measuring. So the keyboard belongs in process, and
/// saying which half lives where is worth more than either half alone.
///
/// <b>THE CLIPBOARD ASSERTION IS DELIBERATELY TWO-SIDED, and that is not hedging.</b> The
/// clipboard belongs to whichever process grabbed it last, so a copy genuinely fails on a working
/// machine - <c>RefusalTests</c> exists for that and the product reports it rather than pretending.
/// A test demanding success would be red for reasons nobody caused, which is how a suite stops
/// being read. So what is asserted is the product's actual contract: either the right text arrived,
/// or the window SAID it could not do it. Silence is the only outcome that fails.
/// </summary>
public sealed class CopyingAndPanelGuards
{
    [Fact]
    public async Task Control_C_copies_the_whole_entry_or_says_it_could_not()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Choose(window);
        WpfHost.Settled();

        var wanted = WpfHost.On(() => Copying.Everything([.. window.Entries.SelectedItems.OfType<EntryRow>()]));

        Assert.False(string.IsNullOrWhiteSpace(wanted), "There is nothing to copy, so this proves nothing.");

        await TheClipboard.Copies(model, wanted, "Ctrl+C", async () =>
            // THE PRESS HAS TO REPORT THAT IT DID SOMETHING, and this line is what the mutation
            // registry asked for. Copy returns false when there is nothing to copy, so a version
            // that quietly copied nothing would hand Ctrl+C back to whatever is behind this window
            // - and the clipboard assertion alone could not see it while an earlier test had left
            // the right answer lying there.
            Assert.True(
                WpfHost.On(() => window.Act(Shortcut.CopyRow, out _)),
                "Ctrl+C reported that it did nothing, so there was nothing to copy."));
    }

    /// <summary>
    /// The four menu items the owner asked for, each copying what its own label promises.
    ///
    /// <b>Reached by the KEY of their label since 2026-09-15, when the menu became a list.</b>
    /// Until then they were reached by position - the first four - because the headers came from
    /// the language file through DynamicResource and matching on words would stop working the day
    /// a second language file appeared. An item built from an entry carries that entry as its data,
    /// key and all, so each promise below is matched to the item that names it and a reorder can no
    /// longer make a claim about the wrong item silently. The day this changed, "Show details" took
    /// the first place and the old version of this test would have asked the details item to copy a
    /// name.
    /// </summary>
    [Fact]
    public async Task The_context_menu_copies_what_each_item_promises()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Choose(window);
        WpfHost.Settled();

        var promises = new (string Key, string? Wanted)[]
        {
            // ASKED OF THE SELECTION RATHER THAN OF THE PANEL SINCE 2026-08-18. A copy is about the
            // rows somebody picked, which is a different set from the one entry the panel shows, and
            // the four menu items promise the first of those.
            ("gui.menu.copyName", WpfHost.On(() => Copying.Name(Picked(window)))),
            ("gui.menu.copyDisplayName", WpfHost.On(() => Copying.DisplayName(Picked(window)))),
            ("gui.menu.copyDescription", WpfHost.On(() => Copying.Description(Picked(window)))),
            ("gui.menu.copyAll", WpfHost.On(() => Copying.Everything(Picked(window))))
        };

        foreach (var (key, wanted) in promises)
        {
            if (string.IsNullOrWhiteSpace(wanted))
            {
                continue;
            }

            var item = MenuItemFor(window, key);

            await TheClipboard.Copies(model, wanted, key, () =>
            {
                WpfHost.On(() => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));

                return Task.CompletedTask;
            });
        }
    }

    /// <summary>The item of the row menu whose entry carries this label key, and it has to be there.</summary>
    internal static MenuItem MenuItemFor(MainWindow window, string labelKey)
    {
        var item = WpfHost.On(() => window.Entries.ContextMenu!.Items
            .OfType<MenuItem>()
            .FirstOrDefault(one => one.DataContext is RowMenuEntry entry && entry.LabelKey == labelKey));

        Assert.True(item is not null, $"The row menu has no item for {labelKey}, so there is nothing to press.");

        return item!;
    }

    /// <summary>
    /// Enter opens the panel ON THE WINDOW, which is a different claim from the view model moving.
    ///
    /// <c>DetailsViewGuards</c> already asks whether the panel appears when the MODEL is told to
    /// show it. This asks whether the KEY reaches that model at all - and those two fail
    /// separately: a shortcut wired to nothing and a trigger that never fires look identical from
    /// every other test in this suite.
    /// </summary>
    [Fact]
    public async Task Enter_opens_the_panel_on_the_window()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Choose(window);
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));

        Assert.True(
            WpfHost.On(() => window.Act(Shortcut.OpenDetails, out _)),
            "Enter did nothing, so the panel is unreachable from the keyboard.");

        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.DetailsPanel.Visibility));
    }

    /// <summary>
    /// Escape closes the panel BEFORE it touches the query, and the order is the whole claim.
    ///
    /// One key means two things now, and if it did them in the other order somebody pressing Escape
    /// to shut a panel would lose the question they had typed as well. Nothing about the panel or
    /// the query on its own can see that - only the sequence can.
    /// </summary>
    [Fact]
    public async Task Escape_closes_the_panel_before_it_clears_the_query()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.On(() => model.QueryText = "spooler");
        Choose(window);
        WpfHost.On(() => window.Act(Shortcut.OpenDetails, out _));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.DetailsPanel.Visibility));

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));

        Assert.Equal(
            "spooler",
            WpfHost.On(() => model.QueryText));

        // And only then the query, on the second press - which is what makes the first press a
        // panel key rather than a key that does both at once.
        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        WpfHost.Settled();

        Assert.Equal(string.Empty, WpfHost.On(() => model.QueryText));
    }

    /// <summary>
    /// Ctrl+C over TWO chosen rows puts both of them on the clipboard.
    ///
    /// <b>THIS TEST EXISTS BECAUSE THE MUTATION REGISTRY REPORTED THE FIRST ATTEMPT AS MISSED, and the
    /// finding was about the test rather than about the window.</b> The version in SelectionGuards asks
    /// Copying directly, so a window that copied only the FIRST of five rows would satisfy it - the
    /// mutation that does exactly that changed nothing there. Everything about a copy that matters
    /// happens on the path from a key press to the clipboard, and that path only runs through here.
    ///
    /// The long form rather than the names, because it is the one that goes into a ticket and the one
    /// Ctrl+C is bound to.
    /// </summary>
    [Fact]
    public async Task Control_C_over_two_chosen_rows_copies_both_of_them()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        var rows = ChooseTwo(window);

        var wanted = WpfHost.On(() => Copying.Everything(rows));

        Assert.False(string.IsNullOrWhiteSpace(wanted), "There is nothing to copy, so this proves nothing.");

        // The second row really is in what we are about to compare against, so a window that copied
        // only the first cannot pass by accident.
        Assert.Contains("W32Time", wanted!, StringComparison.Ordinal);

        await TheClipboard.Copies(model, wanted!, "Ctrl+C over two rows", async () =>
            Assert.True(
                WpfHost.On(() => window.Act(Shortcut.CopyRow, out _)),
                "Ctrl+C reported that it did nothing, so there was nothing to copy."));
    }

    /// <summary>
    /// Two rows picked, arranged the way the product reads them.
    ///
    /// The grid rather than the model, for the reason written at <see cref="Choose"/>: MainWindow reads
    /// the selection out of the grid at the moment somebody asks, so a test that arranged it anywhere
    /// else would be a test about itself.
    /// </summary>
    private static IReadOnlyList<EntryRow> ChooseTwo(MainWindow window)
    {
        var rows = new[]
        {
            EntryRow.Of(Rows.Entry("Spooler", "Print Spooler")),
            EntryRow.Of(Rows.Entry("W32Time", "Windows Time"))
        };

        WpfHost.On(() =>
        {
            window.Entries.ItemsSource = rows;
            window.Entries.SelectedItem = rows[0];
            window.Entries.SelectedItems.Add(rows[1]);
        });

        WpfHost.Settled();

        return rows;
    }

    /// <summary>
    /// One entry in the grid, selected, which is where the window looks.
    ///
    /// <b>THE GRID RATHER THAN THE MODEL, AND THE FIRST VERSION OF THIS FILE GOT IT WRONG.</b> It
    /// set <c>Chosen.Row</c> straight onto the view model and every copy came back empty with the
    /// window saying nothing - which read exactly like four broken features. It was the test:
    /// <c>MainWindow.Act</c> reads <c>Entries.SelectedItem</c> and tells the model at the moment
    /// somebody asks for something, and its own comment says so. A test that arranges the state a
    /// different way from the product is a test about itself.
    ///
    /// The model is told as well, so the expectations below have something to compare against
    /// before any action runs. That is arrangement, not the claim - every assertion here goes
    /// through the window.
    /// </summary>
    private static EntryRow Choose(MainWindow window)
    {
        var row = EntryRow.Of(Rows.Entry("Spooler", "Print Spooler"));

        WpfHost.On(() =>
        {
            window.Entries.ItemsSource = new[] { row };
            window.Entries.SelectedItem = row;
            ((MainViewModel)window.DataContext).Chosen.Row = row;
        });

        WpfHost.Settled();

        return row;
    }

    /// <summary>
    /// The rows the grid is holding, which is what a copy acts on.
    ///
    /// Read at the moment it is asked for, exactly as the window does it - the selection is never
    /// kept anywhere, because a binding into a list that reconciles itself once a second is another
    /// party in the middle of `A10`.
    /// </summary>
    private static IReadOnlyList<EntryRow> Picked(MainWindow window) =>
        [.. window.Entries.SelectedItems.OfType<EntryRow>()];
}
