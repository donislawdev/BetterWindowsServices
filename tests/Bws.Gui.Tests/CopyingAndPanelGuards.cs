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

        var wanted = WpfHost.On(() => model.Chosen.Everything);

        Assert.False(string.IsNullOrWhiteSpace(wanted), "There is nothing to copy, so this proves nothing.");

        ClearTheClipboard();

        // THE PRESS HAS TO REPORT THAT IT DID SOMETHING, and this line is what the mutation
        // registry asked for. Copy returns false when there is nothing to copy, so a version that
        // quietly copied nothing would hand Ctrl+C back to whatever is behind this window - and
        // the clipboard assertion alone could not see it while an earlier test had left the right
        // answer lying there.
        Assert.True(
            await WpfHost.On(() => window.Act(Shortcut.CopyRow)),
            "Ctrl+C reported that it did nothing, so there was nothing to copy.");

        WpfHost.Settled();

        Landed(model, wanted, "Ctrl+C");
    }

    /// <summary>
    /// The four menu items the owner asked for, each copying what its own label promises.
    ///
    /// <b>The count is asserted first so that a reorder reddens here rather than silently making
    /// every claim below about a different item.</b> They are reached by position because the
    /// headers come from the language file through DynamicResource - matching on the words would
    /// be a test that stops working the day a second language file appears, which is the trap this
    /// product has been caught by four times in its markup.
    /// </summary>
    [Fact]
    public void The_context_menu_copies_what_each_item_promises()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Choose(window);
        WpfHost.Settled();

        var items = WpfHost.On(() => window.Entries.ContextMenu!.Items.OfType<MenuItem>().ToList());

        Assert.Equal(4, items.Count);

        var promises = new[]
        {
            WpfHost.On(() => model.Chosen.Row!.ServiceName),
            WpfHost.On(() => model.Chosen.Row!.DisplayName),
            WpfHost.On(() => model.Chosen.Description),
            WpfHost.On(() => model.Chosen.Everything)
        };

        for (var index = 0; index < items.Count; index++)
        {
            var wanted = promises[index];

            if (string.IsNullOrWhiteSpace(wanted))
            {
                continue;
            }

            ClearTheClipboard();

            WpfHost.On(() => items[index].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));
            WpfHost.Settled();

            Landed(model, wanted, $"menu item {index}");
        }
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
            await WpfHost.On(() => window.Act(Shortcut.OpenDetails)),
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
        await WpfHost.On(() => window.Act(Shortcut.OpenDetails));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.DetailsPanel.Visibility));

        Assert.True(await WpfHost.On(() => window.Act(Shortcut.Back)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));

        Assert.Equal(
            "spooler",
            WpfHost.On(() => model.QueryText));

        // And only then the query, on the second press - which is what makes the first press a
        // panel key rather than a key that does both at once.
        Assert.True(await WpfHost.On(() => window.Act(Shortcut.Back)));
        WpfHost.Settled();

        Assert.Equal(string.Empty, WpfHost.On(() => model.QueryText));
    }

    /// <summary>
    /// Either the text arrived on the clipboard, or the window said out loud that it could not.
    /// Silence is the failure, and it is the only one - see the two-sided argument at the top.
    /// </summary>
    private static void Landed(MainViewModel model, string wanted, string how)
    {
        var onIt = WpfHost.On(() => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty);
        var complained = WpfHost.On(() => model.Says.Problem);

        Assert.True(
            string.Equals(onIt, wanted, StringComparison.Ordinal)
                || (complained.Length > 0 && !string.Equals(onIt, Sentinel, StringComparison.Ordinal)),
            $"{how} put nothing on the clipboard and the window said nothing about it. "
            + $"Wanted <{Shorten(wanted)}>, clipboard holds <{Shorten(onIt)}>.");
    }

    /// <summary>
    /// Something nothing in this product would ever copy, put on the clipboard BEFORE each action.
    ///
    /// <b>THE FIRST VERSION HAD NO SENTINEL AND THE MUTATION REGISTRY CAUGHT IT, 2026-08-17.</b>
    /// The clipboard is one object shared by every test in the run, so once any test had copied the
    /// right text it stayed there - and a mutation that made the copy do NOTHING went on passing,
    /// because the assertion found the correct answer already sitting in the clipboard from the
    /// test before it. A guard reading state somebody else wrote is a guard about somebody else.
    /// </summary>
    private const string Sentinel = "bws-nothing-copied-yet";

    private static void ClearTheClipboard() =>
        WpfHost.On(() =>
        {
            try
            {
                Clipboard.SetDataObject(Sentinel, copy: false);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // The clipboard belongs to whoever grabbed it last, which is the whole reason the
                // assertion above has two sides. Nothing to do here.
            }
        });

    private static string Shorten(string text) =>
        text.Length <= 60 ? text : text[..60] + "...";

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
}
