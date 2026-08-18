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

        await Copies(model, wanted, "Ctrl+C", async () =>
            // THE PRESS HAS TO REPORT THAT IT DID SOMETHING, and this line is what the mutation
            // registry asked for. Copy returns false when there is nothing to copy, so a version
            // that quietly copied nothing would hand Ctrl+C back to whatever is behind this window
            // - and the clipboard assertion alone could not see it while an earlier test had left
            // the right answer lying there.
            Assert.True(
                await WpfHost.On(() => window.Act(Shortcut.CopyRow)),
                "Ctrl+C reported that it did nothing, so there was nothing to copy."));
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
    public async Task The_context_menu_copies_what_each_item_promises()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Choose(window);
        WpfHost.Settled();

        var items = WpfHost.On(() => window.Entries.ContextMenu!.Items.OfType<MenuItem>().ToList());

        Assert.Equal(4, items.Count);

        var promises = new[]
        {
            // ASKED OF THE SELECTION RATHER THAN OF THE PANEL SINCE 2026-08-18. A copy is about the
            // rows somebody picked, which is a different set from the one entry the panel shows, and
            // the four menu items promise the first of those.
            WpfHost.On(() => Copying.Name(Picked(window))),
            WpfHost.On(() => Copying.DisplayName(Picked(window))),
            WpfHost.On(() => Copying.Description(Picked(window))),
            WpfHost.On(() => Copying.Everything(Picked(window)))
        };

        for (var index = 0; index < items.Count; index++)
        {
            var wanted = promises[index];

            if (string.IsNullOrWhiteSpace(wanted))
            {
                continue;
            }

            var at = index;

            await Copies(model, wanted, $"menu item {at}", () =>
            {
                WpfHost.On(() => items[at].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));

                return Task.CompletedTask;
            });
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
    private static bool Landed(MainViewModel model, string wanted, string how, string saidBefore)
    {
        var onIt = OnTheClipboard();
        var said = WpfHost.On(() => model.Says.Problem);

        if (string.Equals(onIt, wanted, StringComparison.Ordinal))
        {
            return true;
        }

        // AGAINST WHAT IT SAID BEFORE, rather than against empty, and that is not a refinement.
        // A refusal outlives the action that caused it - Says.Problem says so in its own comment -
        // so over the four menu items one genuine refusal would have stood there answering for
        // every item after it. Comparing with the line as it was a moment ago asks whether THIS
        // action said something.
        if (!string.Equals(said, saidBefore, StringComparison.Ordinal))
        {
            return true;
        }

        Assert.False(
            string.Equals(onIt, Sentinel, StringComparison.Ordinal),
            $"{how} put nothing on the clipboard and the window said nothing about it. "
            + $"Wanted <{Shorten(wanted)}>, and the sentinel is still sitting there untouched, "
            + "so this process still owned the clipboard and the copy simply did nothing.");

        // Neither the answer, nor a refusal, nor the sentinel - so something else has written to
        // the clipboard since it was set, and nothing here is evidence about this product.
        Assert.True(
            onIt.Length == 0,
            $"{how} put the WRONG text on the clipboard. Wanted <{Shorten(wanted)}>, "
            + $"clipboard holds <{Shorten(onIt)}>.");

        return false;
    }

    /// <summary>
    /// Asks for the copy, and judges it only while this process still owns the clipboard.
    ///
    /// <b>THE THIRD OUTCOME, AND IT COST A RED RUN ON 2026-08-18 - backlog 197.</b> The assertion
    /// above has two sides on purpose and both are about what the PRODUCT did: the text arrived, or
    /// the window said it could not. It had no third case for the clipboard READ failing - and when
    /// that happens the write had already succeeded, so the window had nothing to complain about
    /// and the test went red for something nobody caused. Measured at one run in seven.
    ///
    /// <b>Retried rather than tolerated, because tolerating it is how a guard stops guarding.</b>
    /// An assertion that passed whenever the clipboard was busy would also pass on a machine where
    /// copying was broken outright. So each attempt either proves something or proves nothing, and
    /// only proving nothing three times running is reported - as the environment, in those words.
    /// </summary>
    private static async Task Copies(MainViewModel model, string wanted, string how, Func<Task> ask)
    {
        for (var attempt = 1; ; attempt++)
        {
            var ours = OursNow();
            var saidBefore = WpfHost.On(() => model.Says.Problem);

            await ask();
            WpfHost.Settled();

            if (ours && Landed(model, wanted, how, saidBefore))
            {
                return;
            }

            // A THIRD VERDICT RATHER THAN A PASS OR A FAILURE, and this project already has the
            // shape: window-journey answers `held` and adverse.ps1 answers `inert`, both meaning
            // THIS RUN MEASURED NOTHING. Passing here would be a guard reporting success for work
            // it never watched; failing would blame the window for another process holding the
            // clipboard. Skipped shows up in the run's own count, so it cannot go quiet.
            //
            // Silence is still a failure and still gets here first - Landed asserts on it before
            // anything reaches this line. What is skipped is only the case where the clipboard was
            // never this process's to read.
            Assert.True(
                attempt < Attempts,
                $"{how}: the clipboard belonged to another process on {Attempts} tries running, so "
                + "nothing here was proved either way. This is the machine, not the window.");

            // WAITING BETWEEN TRIES RATHER THAN HAMMERING, and the first version had no wait at
            // all - which made the three attempts one attempt with extra steps. Whatever holds the
            // clipboard holds it for a moment: Visual Studio and Docker Desktop were both up on the
            // machine where this was measured, and either can own it while a build is running.
            // WPF already retries the WRITE ten times at a hundred milliseconds; nothing retried
            // the READ, and this is that.
            await Task.Delay(Breath).ConfigureAwait(true);
        }
    }

    /// <summary>How many times an inconclusive attempt is worth repeating before it is reported.</summary>
    private const int Attempts = 5;

    /// <summary>How long to let somebody else finish with the clipboard before asking again.</summary>
    private static readonly TimeSpan Breath = TimeSpan.FromMilliseconds(200);

    /// <summary>The clipboard as text, or empty when it will not answer at all.</summary>
    private static string OnTheClipboard() =>
        WpfHost.On(() =>
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return string.Empty;
            }
        });

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

    /// <summary>
    /// Puts the sentinel down and says whether it is really there.
    ///
    /// <b>Reading it back is the whole point, and the version that only wrote it was half of
    /// backlog 197.</b> A swallowed refusal here left the clipboard holding somebody else's text,
    /// and every judgement after it was about that text rather than about this product.
    /// </summary>
    private static bool OursNow() =>
        WpfHost.On(() =>
        {
            try
            {
                // FLUSHED, exactly as the product flushes, and the version that did not was half
                // of why this went red on a busy machine. Writing without a flush leaves this
                // process owning a live OLE object, and the very next thing the product does is
                // flush its own - so every item alternated between two kinds of ownership and the
                // read in between sometimes found neither.
                Clipboard.SetDataObject(Sentinel, copy: true);

                return Clipboard.ContainsText()
                    && string.Equals(Clipboard.GetText(), Sentinel, StringComparison.Ordinal);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // The clipboard belongs to whoever grabbed it last. Saying so is what lets the
                // caller tell "nothing was copied" from "this was never ours to watch".
                return false;
            }
        });

    private static string Shorten(string text) =>
        text.Length <= 60 ? text : text[..60] + "...";

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

        await Copies(model, wanted!, "Ctrl+C over two rows", async () =>
            Assert.True(
                await WpfHost.On(() => window.Act(Shortcut.CopyRow)),
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
