using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The plan panel as a thing on a window. Packet 2 of `S7`, the window's dry run.
///
/// <b>Everything the panel SAYS is decided in <see cref="Planned"/> and everything it WOULD DO is
/// decided in the core, so what is left for this file is the one question neither can ask: whether
/// any of it reaches a window.</b> This project has been caught four times by markup that binds
/// correctly and paints something else - an empty list from internal view models, headings outside
/// the visual tree, a dead row background, a text box whose style replaced the library's.
///
/// <b>The model is loaded BEFORE it is handed to the window</b>, so no reading happens while bindings
/// are live and nothing here touches the interface from the test thread.
/// </summary>
public sealed class PlanViewGuards
{
    [Fact]
    public async Task The_panel_is_off_the_window_until_somebody_asks_and_off_again_after()
    {
        var window = await Ready();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.PlanPanel.Visibility));

        Assert.True(WpfHost.On(() => window.PlanPanel.Dismiss()));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// What the panel says is about the rows somebody picked.
    ///
    /// <b>Read off the window rather than off the model, which is the whole point of this file.</b>
    /// The heading and the steps are separate bindings, and either can be dead on its own.
    /// </summary>
    [Fact]
    public async Task What_it_shows_is_the_plan_for_the_picked_rows()
    {
        var window = await Ready();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var heading = WpfHost.On(() => window.PlanPanel.Heading.Text);
        var steps = WpfHost.On(() => window.PlanPanel.StepLines);
        var notice = WpfHost.On(() => window.PlanPanel.Notice.Text);

        Assert.NotEqual(string.Empty, heading);

        // Both picked entries have a step, and the one nobody picked does not.
        Assert.Contains(steps, line => line.Contains("Spooler", StringComparison.Ordinal));
        Assert.Contains(steps, line => line.Contains("W32Time", StringComparison.Ordinal));
        Assert.DoesNotContain(steps, line => line.Contains("Dnscache", StringComparison.Ordinal));

        // AND THAT NOTHING HAS HAPPENED, which is the most important line in the panel: everything
        // above it is written in the conditional and a list of steps still reads as a report.
        Assert.NotEqual(string.Empty, notice);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The title calls the entry what a person calls it, and the manager's own name stands under it.
    ///
    /// <b>Both, never one.</b> A title reading "What stopping pla would do" names something nobody
    /// recognises - that entry is called Performance Logs and Alerts on screen and in
    /// <c>services.msc</c>. A title carrying only the display name would be worse: display names
    /// are translated, so it would name nothing anybody could type into a command, which is `ADR-14`
    /// in the one place where the next thing a person does is change a machine.
    ///
    /// <b>The second line GOES rather than empties when there is nothing to say</b>, which is the
    /// half a binding gets right by accident and a visibility gets wrong - backlog 203.
    /// </summary>
    [Fact]
    public async Task The_title_names_the_entry_the_way_a_person_does_with_the_manager_name_under_it()
    {
        var window = await Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.On(() =>
        {
            window.Entries.UnselectAll();
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Spooler");
        });

        WpfHost.Settled();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Contains(
            "Print Spooler",
            WpfHost.On(() => window.PlanPanel.Heading.Text),
            StringComparison.Ordinal);

        Assert.Equal("Spooler", WpfHost.On(() => window.PlanPanel.Subtitle.Text));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.PlanPanel.Subtitle.Visibility));

        // MORE THAN ONE ROW HAS NO SINGLE ENTRY TO NAME, so the line under the title goes with it.
        WpfHost.On(() =>
            window.Entries.SelectedItems.Add(model.Rows.First(row => row.ServiceName == "W32Time")));

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Subtitle.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The commands are the ones the core renders, rather than a second spelling of them.
    ///
    /// <b>`E5`, and the reason this is asserted through the window:</b> a command that looks right and
    /// is not one fails when somebody pastes it, and nothing on screen would say so. What makes these
    /// real is that the core writes them and a guard in the command line's own tests holds that the
    /// command line accepts them.
    /// </summary>
    [Fact]
    public async Task The_commands_on_screen_are_the_ones_the_core_renders()
    {
        var window = await Ready();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Restart)));
        WpfHost.Settled();

        var shown = WpfHost.On(() => window.PlanPanel.CommandLines);

        Assert.Contains("bws restart Spooler", shown);
        Assert.Contains("bws restart W32Time", shown);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// THE TWO PANELS SHARE A COLUMN, so opening one puts the other away.
    ///
    /// Arranged by the window rather than by either panel - neither has any business knowing the other
    /// exists. Without it both would be visible in one cell and the narrower one would be drawn over
    /// the other, which looks like a rendering fault rather than like two open panels.
    /// </summary>
    [Fact]
    public async Task Opening_one_panel_puts_the_other_away()
    {
        var window = await Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.On(() => model.Chosen.Row = model.Rows[0]);
        Assert.True(WpfHost.On(() => model.Chosen.Show()));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.DetailsPanel.Visibility));

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.PlanPanel.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));

        // And back the other way, which is the half that is easy to leave out.
        WpfHost.On(() => model.Chosen.Row = model.Rows[0]);
        Assert.True(WpfHost.On(() => window.Act(Shortcut.OpenDetails, out _)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.DetailsPanel.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Escape backs out of the plan before it touches anything else.
    ///
    /// <b>The order is the decision rather than the implementation</b>, exactly as it was when the
    /// details panel joined the same queue: one press doing two things at once takes somebody's query
    /// away while they were reaching for a panel.
    /// </summary>
    [Fact]
    public async Task Escape_closes_the_plan_before_it_clears_the_query()
    {
        var window = await Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.On(() => model.QueryText = "name:Spooler");
        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));

        // The query survived that press, which is the half worth asserting.
        Assert.Equal("name:Spooler", WpfHost.On(() => model.QueryText));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Nothing picked opens nothing, and says it opened nothing.
    ///
    /// A panel that came up empty would look exactly like a broken feature, and a menu item that
    /// silently did nothing would look like one too - so the answer is handed back.
    /// </summary>
    [Fact]
    public async Task With_nothing_picked_there_is_no_plan_to_show()
    {
        var window = await Ready();

        WpfHost.On(() => window.Entries.UnselectAll());
        WpfHost.Settled();

        Assert.False(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// AN ENTRY THE PLAN REFUSES IS ON SCREEN, and the panel still opens for the rest.
    ///
    /// Owner's decision, 2026-08-18: a refusal belongs to its own entry and the rest of the selection
    /// carries on. The price is that the preview carries two lists, and this is the guard that the
    /// second one reaches a window - without it, a selection of two with one refused would look
    /// exactly like a selection of one.
    /// </summary>
    [Fact]
    public async Task An_entry_with_no_plan_is_named_on_screen()
    {
        var window = await Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        // THE SCOPE IS OPENED FIRST, AND THAT IS THE PRODUCT RATHER THAN THE TEST. The window opens
        // on services, so a driver is not among the rows until somebody asks for one. The first
        // version of this test reached for one anyway and threw, which is the double doing its job:
        // the row genuinely was not there.
        //
        // IT USED TO BE THE QUERY THAT HID IT and it is the scope switch since 2026-08-19 - the
        // clearing that stood here does nothing to a driver any more. This is also still the real
        // road to this state: somebody moves to the list that has drivers in it and then rubber
        // bands a range that happens to include one.
        WpfHost.On(() => model.Scope = ViewModels.EntryScope.Everything);
        WpfHost.Settled();

        WpfHost.On(() =>
        {
            window.Entries.UnselectAll();
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "amdkmdag");
        });

        WpfHost.Settled();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var problems = WpfHost.On(() => window.PlanPanel.ProblemLines);

        Assert.Contains(problems, line => line.Contains("amdkmdag", StringComparison.Ordinal));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// It takes no focus of its own, but it does take clicks - unlike the empty state.
    ///
    /// A UserControl is a ContentControl and arrives Focusable and a tab stop, `docs/10` trap 8. Hit
    /// testing has to stay on here, because the panel holds the button that closes it.
    /// </summary>
    [Fact]
    public void It_takes_no_focus_of_its_own_and_still_takes_clicks()
    {
        var window = WpfHost.Window();

        Assert.False(WpfHost.On(() => window.PlanPanel.Focusable));
        Assert.False(WpfHost.On(() => window.PlanPanel.IsTabStop));
        Assert.True(WpfHost.On(() => window.PlanPanel.IsHitTestVisible));

        WpfHost.On(window.Close);
    }

    // -- fixtures --------------------------------------------------------------------------

    /// <summary>
    /// A window looking at a small machine, with two entries picked and a third left alone.
    ///
    /// <b>The reading happens before the model reaches the window</b>, so nothing is read while
    /// bindings are live - the same order <see cref="SelectionGuards"/> uses and for the same reason.
    /// </summary>
    /// <summary>
    /// Every word this panel puts on screen can actually be read on the surface it is drawn on.
    ///
    /// <b>WRITTEN 2026-09-02 BECAUSE THE PANEL SHIPPED WITH BLACK TEXT ON GREY AND EVERY GUARD IN
    /// THIS PROJECT WAS SATISFIED.</b> Two of its data templates named a face and a size and no
    /// Foreground, so the framework's default arrived instead - which is black. Against the panel's
    /// own #343434 that is a ratio of 1.69 where WCAG asks 4.5, and it covered the warnings, the
    /// refusals, the failures and the command somebody is meant to copy.
    ///
    /// <b>WHY NOTHING CAUGHT IT, WHICH IS THE PART WORTH KEEPING.</b> AppearanceGuards refuses a
    /// theme TextBlock STYLE that names no colour - and a DataTemplate is not a style. ContrastGuards
    /// measures every declared brush against the WINDOW - and a panel is lighter than the window, so
    /// those numbers are optimistic here and say nothing at all about a colour nobody declared. Both
    /// guards were right about what they check and neither could see this.
    ///
    /// <b>SO THIS ASKS THE BUILT PANEL RATHER THAN THE MARKUP.</b> It walks what is actually on
    /// screen and reads the Foreground each TextBlock ENDED UP with, whether that came from a style,
    /// a setter, a trigger or an inherited value - which is the only form of the question that
    /// cannot be answered correctly and still be wrong.
    /// </summary>
    [Fact]
    public async Task Every_word_on_the_panel_can_be_read_on_the_surface_it_is_drawn_on()
    {
        var window = await Ready();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var (thin, read) = WpfHost.On(() =>
        {
            // ARRANGED, OR THIS GUARD CHECKS NOTHING - and the first version of it did not, which
            // was caught by taking the colour back out and watching it stay green. An ItemsControl
            // builds no containers until a layout pass runs, so every sentence and every command
            // this test exists for simply was not in the tree yet.
            window.Width = 1100;
            window.Height = 700;
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;
            window.Show();
            window.UpdateLayout();

            var surface = ((SolidColorBrush)WpfHost.Resources["SurfacePanel"]).Color;
            var found = new List<TextBlock>();
            Collect(window.PlanPanel, found);

            var words = found.Where(text => !string.IsNullOrWhiteSpace(text.Text)).ToList();

            return (words
                .Select(text => (text.Text, Ratio(Ink(text), surface)))
                .Where(pair => pair.Item2 < 4.5)
                .Select(pair => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{pair.Item2:F2}  {pair.Text}"))
                .ToList(), words.Count);
        });

        // A GUARD SATISFIED BY ABSENCE IS SATISFIED FOR AS LONG AS NOBODY BUILDS ANYTHING, and this
        // project has that lesson written in three other files. The panel shows a title, a name, a
        // state, a heading and a step at the very least.
        Assert.True(read >= 5, $"Only {read} lines were found on the panel, so nothing was measured.");

        Assert.True(
            thin.Count == 0,
            "These lines are drawn on the plan panel at less than the 4.5 WCAG 2.2 SC 1.4.3 asks of "
            + "text. A TextBlock that names no Foreground gets the framework's default, which is "
            + "black, and black on this surface measures 1.69:"
            + Environment.NewLine + string.Join(Environment.NewLine, thin));

        WpfHost.On(window.Close);
    }

    /// <summary>The colour a TextBlock ended up with, however it got there.</summary>
    private static Color Ink(TextBlock text) =>
        text.Foreground is SolidColorBrush brush ? brush.Color : Colors.Black;

    /// <summary>Every TextBlock under something, including the ones a template built.</summary>
    private static void Collect(DependencyObject from, List<TextBlock> into)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++)
        {
            var child = VisualTreeHelper.GetChild(from, i);

            if (child is TextBlock text)
            {
                into.Add(text);
            }

            Collect(child, into);
        }
    }

    /// <summary>
    /// "Copy all" stands over the commands when there are several, carries every one of them one
    /// per line, and is not there over a single command - the owner's request of 2026-09-16.
    ///
    /// <b>The Tag rather than the clipboard</b>, because the press travels the same road as the
    /// button beside each line - CopyCommandRequested reads whichever Tag was pressed - and that
    /// road already has a guard. What is new is what the Tag holds and when the button is there.
    /// </summary>
    [Fact]
    public async Task Copy_all_stands_over_several_commands_with_all_of_them_on_it_and_not_over_one()
    {
        var window = await Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var (shown, carried, lines) = WpfHost.On(() => (
            window.PlanPanel.CopyAllButton.Visibility,
            window.PlanPanel.CopyAllButton.Tag as string,
            window.PlanPanel.CommandLines));

        Assert.True(lines.Count > 1, "the fixture picks two rows, so the plan should print two commands");
        Assert.Equal(Visibility.Visible, shown);
        Assert.Equal(string.Join(Environment.NewLine, lines), carried);

        // ONE ROW, ONE COMMAND, NO BUTTON - beside a single "Copy" a "Copy all" is two buttons for
        // one thing.
        WpfHost.On(() =>
        {
            window.Entries.SelectedItems.Clear();
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Spooler");
        });

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Single(WpfHost.On(() => window.PlanPanel.CommandLines));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.CopyAllButton.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>WCAG 2.2 relative luminance, the same arithmetic ContrastGuards uses.</summary>
    private static double Ratio(Color ink, Color surface)
    {
        var one = Luminance(ink);
        var other = Luminance(surface);

        return (Math.Max(one, other) + 0.05) / (Math.Min(one, other) + 0.05);
    }

    private static double Luminance(Color colour) =>
        (0.2126 * Channel(colour.R)) + (0.7152 * Channel(colour.G)) + (0.0722 * Channel(colour.B));

    private static double Channel(byte value)
    {
        var part = value / 255.0;

        return part <= 0.04045 ? part / 12.92 : Math.Pow((part + 0.055) / 1.055, 2.4);
    }

    private static async Task<MainWindow> Ready()
    {
        var machine = new LiveMachine(
            Rows.Entry("Spooler", "Print Spooler"),
            Rows.Entry("W32Time", "Windows Time"),
            Rows.Entry("Dnscache", "DNS Client"),
            Rows.Driver("amdkmdag"));

        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        // HANDED TO THE WINDOW RATHER THAN ASSIGNED AFTERWARDS. Replacing DataContext leaves the
        // window's own handlers talking to the model it built for itself, so a plan would be shown on
        // one object while the panel watched another - which is how the first version of this file
        // failed, silently and in five places at once.
        var window = WpfHost.Window(model);

        WpfHost.On(() =>
        {
            window.Entries.ItemsSource = model.Rows;
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Spooler");
            window.Entries.SelectedItems.Add(model.Rows.First(row => row.ServiceName == "W32Time"));
        });

        WpfHost.Settled();

        return window;
    }
}
