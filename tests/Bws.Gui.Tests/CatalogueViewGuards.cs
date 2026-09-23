using System.Windows.Controls;
using Bws.Gui.ViewModels;
using Xunit.Abstractions;

namespace Bws.Gui.Tests;

/// <summary>
/// The views on the catalogue sheet - the third kind of component, since 2026-09-16, and the one
/// that needs a guard most: a view is not a style, so nothing in a dictionary declares it, and a
/// view added to the window and forgotten by the sheet would be forgotten quietly.
///
/// <b>The assembly is the declaration.</b> Every UserControl in Bws.Gui is a view of this window,
/// and the sheet's list of views is compared against the assembly's types in both directions -
/// rule 4, the same shape CatalogueGuards holds for styles, with reflection where the styles have
/// a dictionary walk.
///
/// <b>Two threads, on purpose.</b> The models are prepared off the host - they load the way the
/// product loads and finish wherever they were called - and the views are built on it. That is the
/// seam Catalogue.Views.cs describes, and these tests cross it the way the window does.
/// </summary>
public sealed class CatalogueViewGuards(ITestOutputHelper output)
{
    /// <summary>The window's own views: every UserControl the assembly holds.</summary>
    private static IReadOnlyList<string> DeclaredViews() =>
        typeof(MainWindow).Assembly.GetTypes()
            .Where(type => typeof(UserControl).IsAssignableFrom(type) && !type.IsAbstract)
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static async Task<(Catalogue.Prepared Ready, Catalogue.Group Views)> Built()
    {
        var ready = await Catalogue.PrepareViewsAsync();

        var views = WpfHost.On(() => Catalogue.Views(ready, WpfHost.Resources));

        return (ready, views);
    }

    [Fact]
    public async Task Every_view_the_window_is_built_out_of_is_on_the_sheet_and_nothing_else_is()
    {
        var (ready, views) = await Built();

        using (ready)
        {
            var declared = DeclaredViews();
            var shown = views.Entries.Select(entry => entry.Key).ToArray();

            // The list is its own view on the sheet, built from the window's styles and column
            // builder rather than from a UserControl - MainWindow.xaml holds the real one and
            // stands on the markup ceiling. Named here so that the comparison below is exact.
            var missing = declared.Where(name => !shown.Any(key => key.StartsWith(name, StringComparison.Ordinal))).ToArray();
            var invented = shown
                .Where(key => key != "EntryList" && !declared.Any(name => key.StartsWith(name, StringComparison.Ordinal)))
                .ToArray();

            Assert.True(missing.Length == 0, $"{missing.Length} view(s) of the window are not on the sheet: [{string.Join(", ", missing)}].");
            Assert.True(invented.Length == 0, $"The sheet shows {invented.Length} view(s) the window does not have: [{string.Join(", ", invented)}].");
            Assert.Contains("EntryList", shown);
        }
    }

    [Fact]
    public async Task Every_view_is_drawn_in_at_least_two_states_and_every_drawn_state_puts_a_pixel_on_the_sheet()
    {
        var (ready, views) = await Built();

        using (ready)
        {
            var drawn = new Dictionary<string, int>(StringComparer.Ordinal);
            var blank = new List<string>();

            foreach (var entry in views.Entries)
            {
                var cells = new[] { ("with data", entry.Normal), ("empty", entry.Disabled), ("wrong", entry.Wrong), ("loading", entry.Chosen), ("extreme", entry.Extreme) };

                output.WriteLine(entry.Key + ": " + string.Join(" | ", cells.Select(cell => cell.Item1 + "=" + (cell.Item2.Element is null ? "'" + cell.Item2.Instead + "'" : "drawn"))));

                // Counted per VIEW, not per row: "DetailsView over a driver" is a second row about
                // the same view, and its one state is added to the view's, not held to two alone.
                var view = entry.Key.Split(" over ")[0];

                drawn[view] = drawn.GetValueOrDefault(view) + cells.Count(cell => cell.Item2.Element is not null);

                blank.AddRange(cells.Where(cell => cell.Item2.Element is null && cell.Item2.Instead != "-").Select(cell => entry.Key + " " + cell.Item1 + ": " + cell.Item2.Instead));
            }

            var thin = drawn.Where(pair => pair.Value < 2).Select(pair => pair.Key).ToArray();

            Assert.True(
                blank.Count == 0,
                $"{blank.Count} view state(s) could not be drawn: [{string.Join("; ", blank)}]. A view over a model in a state "
                + "that draws nothing is a view whose visibility the sheet did not arrange - GUI rule 3 asks for every state.");

            Assert.True(thin.Length == 0, $"{thin.Length} view(s) are shown in fewer than two states: [{string.Join(", ", thin)}].");
        }
    }

    /// <summary>
    /// The four states are the model's own, reached the way the product reaches them - so the
    /// model behind each cell says what state it is in, and the cell agrees.
    /// </summary>
    [Fact]
    public async Task The_four_states_of_the_list_are_the_models_own_states()
    {
        var (ready, views) = await Built();

        using (ready)
        {
            var list = views.Entries.Single(entry => entry.Key == "EntryList");

            WpfHost.On(() =>
            {
                Assert.Equal(8, ((DataGrid)list.Normal.Element!).Items.Count);
                Assert.Empty(((DataGrid)list.Disabled.Element!).Items);
                Assert.Empty(((DataGrid)list.Wrong.Element!).Items);
                Assert.Empty(((DataGrid)list.Chosen.Element!).Items);
            });

            // The wrong one has failed, the loading one is still reading, and the two are told apart
            // by the model rather than by the sheet.
            Assert.True(ready.Wrong.Says.ReadingFailed, "The model over the refusing machine does not say its reading failed.");
            Assert.False(ready.Loading.Says.ReadingFailed);
            Assert.Equal(Texts.Of("gui.status.reading"), ready.Loading.Says.Status);
        }
    }

    /// <summary>
    /// The action bar is on the sheet over one entry, over none, and over several - and the third
    /// is the one state in which it is neither all on nor all off.
    ///
    /// <b>Since 2026-09-16, when two of its verbs began to take exactly one entry.</b> GUI rule 4
    /// asks for every state a component has, and the bar over several is a state a person meets on
    /// the first Ctrl+A: four verbs live, the two that end a process off with a reason of their own.
    /// It borrows the "extreme" column because the sheet has no column for it, and this is the guard
    /// that the borrowed cell really shows that shape rather than a second copy of "over one".
    /// </summary>
    [Fact]
    public async Task The_action_bar_is_shown_over_one_entry_over_none_and_over_several()
    {
        var (ready, views) = await Built();

        using (ready)
        {
            var bar = views.Entries.Single(entry => entry.Key == nameof(ActionBar));

            WpfHost.On(() =>
            {
                var one = (ActionBar)bar.Normal.Element!;
                var none = (ActionBar)bar.Disabled.Element!;
                var several = (ActionBar)bar.Extreme.Element!;

                Assert.True(one.Stop.IsEnabled);
                Assert.True(one.ForceStop.IsEnabled);

                Assert.False(none.Stop.IsEnabled);
                Assert.False(none.ForceStop.IsEnabled);

                Assert.True(several.Stop.IsEnabled);
                Assert.False(several.ForceStop.IsEnabled);
                Assert.False(several.ForceRestart.IsEnabled);
                Assert.Equal(Texts.Of("gui.action.force.onlyOne"), several.ForceStop.ToolTip as string);
            });
        }
    }

    /// <summary>
    /// The loading samples hold a read each on a gate, and the gate is opened by disposing what
    /// PrepareViewsAsync handed back - the window does it when it closes. A gate that stayed shut
    /// would be a pool thread per sheet that never came back.
    /// </summary>
    [Fact]
    public async Task Disposing_the_prepared_models_lets_the_stalled_reads_go()
    {
        var ready = await Catalogue.PrepareViewsAsync();

        var loading = ready.Loading;

        Assert.Equal(Texts.Of("gui.status.reading"), loading.Says.Status);

        ready.Dispose();

        // The read returns empty once released, and the model finishes loading - an empty machine.
        // Awaited rather than polled: the task is kept exactly so that somebody can. With a limit,
        // because a gate that stays shut is the fault this guards against, and a guard that
        // waited forever for it would hang the whole run instead of going red.
        await ready.StillReading.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotEqual(Texts.Of("gui.status.reading"), loading.Says.Status);
        Assert.Empty(loading.Rows);
    }

    /// <summary>
    /// Nothing on the sheet ticks. The window's refresh timers live in the window, not in the
    /// model, so a model on the sheet asks its machine once - and this holds that the sheet's
    /// models did not start reading again on their own.
    /// </summary>
    [Fact]
    public async Task No_model_on_the_sheet_reads_its_machine_more_than_once()
    {
        var (ready, _) = await Built();

        using (ready)
        {
            // Frozen answers at once and counts nothing, so this is asked of the one machine that
            // can be asked: the stalled one, whose single read is still waiting on the gate. A
            // second read would be a second thread waiting - counted before the gate opens.
            WpfHost.Until(() => ready.Stalling.Waiting >= 1, "the loading model's read to reach the gate");

            Assert.Equal(1, ready.Stalling.Waiting);
            Assert.Equal(Texts.Of("gui.status.reading"), ready.Loading.Says.Status);
        }
    }
}
