using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The window itself, built rather than reasoned about.
///
/// <b>This is the guard the two worst faults in `docs/10` went past.</b> Merging WPF UI's
/// dictionaries beside a <c>ThemeMode</c> ends the process at startup, and removing the
/// <c>ThemeMode</c> without giving the window a background leaves the list invisible while the
/// count line still reports every entry. Both were found by a person running the program. Both
/// are markup resolving - or failing to - at the moment a window is constructed, which is what
/// the first test here does.
///
/// It cannot say what the window LOOKS like. RowStateGuards renders the list for that, and
/// <c>tools/gui-probe/check.ps1</c> is still the only thing that sees a real screen.
/// </summary>
public sealed class WindowGuards
{
    /// <summary>
    /// The markup resolves and the window is built.
    ///
    /// Every <c>StaticResource</c> in <c>MainWindow.xaml</c> is resolved while the file is read,
    /// so a name that stopped existing - a style renamed here, a key dropped by an update to the
    /// control library - throws right here rather than on somebody's machine.
    /// </summary>
    [Fact]
    public void The_window_is_built_and_every_name_its_markup_uses_resolves()
    {
        var window = WpfHost.Window();

        Assert.NotNull(window);
    }

    /// <summary>
    /// Ctrl+F reports that it did something, so the press is not passed on to a control that
    /// would type an f into the query box.
    /// </summary>
    [Fact]
    public async Task Control_F_is_taken_by_the_window()
    {
        var window = WpfHost.Window();

        Assert.True(await WpfHost.On(() => window.Act(Shortcut.FocusQuery)));
    }

    /// <summary>
    /// AND THE ONE THAT MATTERS MORE: Escape on an empty box is handed back.
    ///
    /// The window takes these keys in preview, before anything else in it gets a look, so a
    /// shortcut that claims a press it did nothing with is a key that silently stops working for
    /// whatever needed it next. Escape is the one every dialog in Windows already has plans for.
    /// </summary>
    [Fact]
    public async Task Escape_with_nothing_to_clear_is_handed_back()
    {
        var window = WpfHost.Window();

        Assert.False(await WpfHost.On(() => window.Act(Shortcut.ClearQuery)));
    }

    [Fact]
    public async Task A_key_the_window_has_no_use_for_is_passed_on()
    {
        var window = WpfHost.Window();

        Assert.False(await WpfHost.On(() => window.Act(Shortcut.None)));
    }

    /// <summary>
    /// What a copy would put on the clipboard is decided where it can be checked.
    ///
    /// The clipboard call itself needs a window and cannot be tested - which is the reason the
    /// decision was moved out of it rather than left beside it.
    /// </summary>
    [Fact]
    public async Task With_no_row_chosen_there_is_nothing_to_copy()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        Assert.Null(model.SelectedServiceName);
        Assert.Null(model.SelectedDisplayName);
    }

    /// <summary>
    /// The jump a letter makes, asked of the window rather than of the rule behind it.
    ///
    /// <b><see cref="MainWindow.JumpTo"/> was made internal so that it could be checked, and then
    /// was not</b> - found on 2026-08-11 while repairing backlog 161, with the whole method
    /// uncovered. <c>TypeToFindTests</c> holds the rule about WHICH row a letter picks;
    /// what is here is the half only a window has: the grid's own order, the selection actually
    /// moving, and the answer that decides whether the key press is passed on.
    ///
    /// <b>The rows are handed to the grid rather than loaded through the model, on purpose.</b>
    /// The window builds its own view model and that model reads this machine's service control
    /// manager - a unit test that did so would be measuring the machine. Setting ItemsSource is
    /// also the honest shape for the claim: backlog 150 established that the jump has to follow
    /// what is ON SCREEN rather than the model's order, because a sorted column makes those two
    /// different sequences.
    /// </summary>
    [Fact]
    public void A_letter_moves_the_selection_to_the_next_row_that_starts_with_it()
    {
        var window = WpfHost.Window();

        var moved = WpfHost.On(() =>
        {
            window.Entries.ItemsSource = new[]
            {
                EntryRow.Of(Rows.Entry("Appinfo")),
                EntryRow.Of(Rows.Entry("Spooler")),
                EntryRow.Of(Rows.Entry("Winmgmt"))
            };

            return window.JumpTo('s');
        });

        Assert.True(moved);

        var landed = WpfHost.On(() => (window.Entries.SelectedItem as EntryRow)?.ServiceName);

        Assert.Equal("Spooler", landed);
    }

    /// <summary>
    /// The two ways a jump does nothing, and both have to say so.
    ///
    /// <b>A press marked handled by something that did nothing is a press that silently stops
    /// working for whatever needed it next</b> - which is the sentence the method's own comment
    /// gives as the reason it returns a bool at all. So the claim is the answer, not the
    /// selection: a letter nothing starts with, and no letter at all.
    /// </summary>
    [Fact]
    public void A_letter_nothing_starts_with_is_handed_back()
    {
        var window = WpfHost.Window();

        var answers = WpfHost.On(() =>
        {
            window.Entries.ItemsSource = new[] { EntryRow.Of(Rows.Entry("Spooler")) };

            return (Nothing: window.JumpTo('q'), NoLetter: window.JumpTo(null));
        });

        Assert.False(answers.Nothing);
        Assert.False(answers.NoLetter);
    }

    /// <summary>
    /// F5 reads everything again, and reports that it did - so the key is not passed on to the
    /// grid, which has its own plans for it.
    ///
    /// This is the one shortcut whose work is a real reading, so it is the one that costs a
    /// moment. It reads THIS machine, deliberately: the window builds its own view model and the
    /// point of the test is that the window's own path works, not that a fake can be refreshed.
    /// </summary>
    [Fact]
    public async Task F5_reads_everything_again_and_says_it_did()
    {
        var window = WpfHost.Window();

        Assert.True(await WpfHost.On(() => window.Act(Shortcut.Refresh)));
    }

    /// <summary>
    /// The Columns button carries the eighteen columns, and points its menu at itself.
    ///
    /// <b>THE FIRST VERSION OF THIS ASSERTED THAT THE MENU WAS OPEN, AND IT WAS FLAKY - which is
    /// worse than not having it.</b> It passed on its own and failed inside a full check.ps1 run,
    /// with the button reporting that it had opened a menu whose IsOpen read false. The window
    /// these tests build is never SHOWN, and whether WPF will really open a popup over a window
    /// that was never shown is a question about WPF rather than about this product.
    ///
    /// So what is claimed here is the part that is ours: the button has a menu, the menu is
    /// pointed at the button, and it carries the eighteen choices - <b>handed over rather than
    /// inherited</b>, because a menu hangs off a Popup, which is not in the visual tree, so what
    /// it would inherit is a question with an answer nobody should have to know.
    ///
    /// <b>That the menu actually opens is guarded elsewhere and by a person</b>, which is stated
    /// rather than left as a gap: <c>tools/window-journey</c> invokes this button on a real window
    /// and then finds and ticks the items, which cannot work unless it opened - and it was watched
    /// opening on the real window on 2026-08-11.
    /// </summary>
    /// <summary>
    /// The examples button carries the six questions and points at itself - `P5`, 2026-08-12.
    ///
    /// The same claim as the columns button below, for the same reason: a button whose menu carries
    /// nothing looks exactly like a feature that is not there, and the syntax now lives behind this
    /// one rather than in a placeholder that vanished at the first keystroke.
    ///
    /// <b>That clicking an item writes the query is guarded elsewhere</b> - the handler is on the
    /// menu rather than on each item, and it was driven end to end on the real window: six items
    /// offered, the click wrote <c>start:disabled status:running</c> and the count line answered
    /// 5 of 809.
    /// </summary>
    [Fact]
    public void The_examples_button_carries_the_questions_to_start_from()
    {
        var window = WpfHost.Window();

        var opened = WpfHost.On(() => window.OpenExamples());
        var menu = WpfHost.On(() => window.ExamplesButton.ContextMenu);

        Assert.True(opened, "The button has no menu to open, so the query language has no way in.");
        Assert.Equal(6, WpfHost.On(() => menu!.Items.Count));
        Assert.Same(WpfHost.On(() => (object)window.ExamplesButton), WpfHost.On(() => menu!.PlacementTarget));

        WpfHost.On(() => menu!.IsOpen = false);
    }

    [Fact]
    public void The_columns_button_carries_the_eighteen_columns()
    {
        var window = WpfHost.Window();

        var opened = WpfHost.On(() => window.OpenColumns());
        var menu = WpfHost.On(() => window.ColumnsButton.ContextMenu);

        Assert.True(opened, "The button has no menu to open, so there is no way in to the columns.");

        // TWENTY TWO SINCE 2026-08-12: eighteen columns and four headings, which are items in
        // the same flat list rather than groups around it. It was twenty one earlier the same day,
        // and the description made it twenty two - backlog 171. Grouping a menu with GroupStyle
        // takes its contents out of the automation tree entirely - measured on the real window,
        // where an open picker offered twelve togglable elements, all of them filter chips, and
        // none of the columns. The count is asserted whole rather than filtered so that a heading
        // quietly becoming tickable, or a column quietly becoming a heading, still moves it.
        Assert.Equal(22, WpfHost.On(() => menu!.Items.Count));
        Assert.Same(WpfHost.On(() => (object)window.ColumnsButton), WpfHost.On(() => menu!.PlacementTarget));

        // Closed again, because this host is shared and a menu left open sits over whatever the
        // next test builds - the same reason the probe that drives the real window shuts it.
        WpfHost.On(() => menu!.IsOpen = false);
    }

    /// <summary>
    /// A column heading gives way with an ellipsis, exactly as every cell has since 2026-08-05.
    ///
    /// <b>Complaint 6 of the eleven, in the one place the fix for it never reached.</b> Cells got
    /// trimming and a tooltip when the widths were measured; headings did not, because all six of
    /// them were short and there was nothing to see. S6d2 brought headings like "Against its start
    /// type" and "Required privileges" over columns that shrink to a 90 unit floor, and the real
    /// window was photographed reading "Required priv" - cut in the middle of a word, which
    /// `docs/11` 3.5 says reads as a rendering fault rather than as "this does not fit".
    ///
    /// <b>It asks the STYLE rather than a pixel, and that limit is stated rather than implied.</b>
    /// Whether the ellipsis appears is a question for a window at a real width, which is
    /// <c>tools/gui-probe/columns-shot.ps1</c> and a person's eye. What can be held here is that
    /// the setter exists and reaches the text - a heading is a ContentControl and TextTrimming
    /// belongs to a TextBlock, so this only works through a template and it is easy to write a
    /// setter that resolves, applies and does nothing.
    /// </summary>
    [Fact]
    public void A_column_heading_gives_way_with_an_ellipsis_like_every_cell_does()
    {
        var heading = WpfHost.On(() => (Style)WpfHost.Resources["ColumnHeading"]);

        var template = WpfHost.On(() =>
            heading.Setters.OfType<Setter>()
                .SingleOrDefault(setter => setter.Property == ContentControl.ContentTemplateProperty)?
                .Value as DataTemplate);

        Assert.True(
            template is not null,
            "The column heading has no ContentTemplate, so nothing carries its text and a heading "
            + "too long for its column is cut mid-character. There is no setter on the header "
            + "itself that reaches the text.");

        var text = WpfHost.On(() => template!.LoadContent() as TextBlock);

        Assert.True(
            text is not null,
            "The heading's template no longer builds a TextBlock, so whatever it does build decides "
            + "the trimming and this guard can say nothing about it.");

        Assert.Equal(TextTrimming.CharacterEllipsis, WpfHost.On(() => text!.TextTrimming));
    }

    /// <summary>
    /// The list scrolls sideways, which is the owner's decision of 2026-08-12 reversing his own of
    /// 2026-08-05.
    ///
    /// <b>A guard over one attribute, and what makes it worth having is what the attribute costs
    /// when it goes back.</b> Photographed with all seventeen columns on before this changed:
    /// nothing ran past the right edge, and seven columns collapsed to twenty pixels each - Status,
    /// Start, PID, Entry type, Against its start type, Error control and Service SID type, every
    /// heading a single full stop over a column of sliced dots. Seven columns saying nothing while
    /// the window claims to show them is rule 8 of the project notes, and Disabled is one word away.
    ///
    /// Auto rather than Visible, because a scrollbar under a list that fits is a control that never
    /// does anything, and the six columns the window opens with do fit.
    /// </summary>
    [Fact]
    public void The_list_scrolls_sideways_rather_than_crushing_its_columns()
    {
        var window = WpfHost.Window();

        Assert.Equal(
            ScrollBarVisibility.Auto,
            WpfHost.On(() => window.Entries.HorizontalScrollBarVisibility));
    }

    /// <summary>
    /// No column can be squeezed below the width it was sized for.
    ///
    /// <b>This is the line that actually made seventeen columns readable, and turning the scrollbar
    /// on did not.</b> Measured on the real window with all seventeen on and sideways scrolling
    /// already enabled: seven columns still sat at twenty pixels each. A width is a request - when
    /// the columns want more room than there is, DataGrid takes it back from whatever it can, down
    /// to its own MinColumnWidth of twenty, and a scrollbar does not stop it. A floor is what says
    /// the number is not negotiable. Measured again with the floor in place: Status 150, Start 215,
    /// PID 72, Entry type 150, Against its start type 175, Error control 120, Service SID type 140,
    /// the starred ones at their own floor of 90, and 1922 pixels of row behind a 1039 viewport.
    ///
    /// <b>What it cannot say is what the pixels do</b>, and that limit is worth stating precisely
    /// because a harness got this wrong: a grid laid out in these tests reported the fixed columns
    /// holding their widths while the real window had them at twenty, and its own scroll extent
    /// contradicted its own column widths. So a width claim belongs to
    /// <c>tools/gui-probe/columns.ps1</c> and <c>columns-shot.ps1</c> on a real window. What is held
    /// here is the instruction the window is given, which is the half that can be checked anywhere.
    /// </summary>
    [Fact]
    public void No_column_can_be_squeezed_below_the_width_it_was_sized_for()
    {
        _ = WpfHost.Resources;

        var bar = new ColumnBar();

        var grid = WpfHost.On(() =>
        {
            var built = new DataGrid();

            ListColumns.Fill(built, bar);

            return built;
        });

        var floor = WpfHost.On(() => (double)WpfHost.Resources["ColumnFloor"]);

        var squeezable = WpfHost.On(() => grid.Columns
            .Where(column => column.MinWidth < (column.Width.IsStar ? floor : column.Width.Value))
            .Select(column => $"{column.Header} may shrink to {column.MinWidth} from {column.Width}")
            .ToList());

        Assert.True(
            squeezable.Count == 0,
            "A column with no floor under it is one DataGrid may take down to twenty pixels when the "
            + "row runs out of room - which is a heading reading as a single full stop over a column "
            + "of sliced dots, with nothing on screen saying so:"
            + Environment.NewLine + string.Join(Environment.NewLine, squeezable));
    }

    /// <summary>
    /// The frozen column is the leftmost one ON SCREEN, through both things that can move it.
    ///
    /// <b>This is the guard for a fault that a literal number in the markup has and this window does
    /// not.</b> WPF freezes the first N of the DISPLAY order, and a collapsed column keeps its
    /// place in it - so with the count written as one, turning the name column off leaves the freeze
    /// on a column nobody can see, and the row's identity scrolls away exactly as if nothing had
    /// been frozen at all. Measured on a built window on 2026-08-12 before the count was worked
    /// out: name collapsed and still frozen, "Display name" leftmost on screen and not frozen.
    ///
    /// <b>Driven through the picker rather than by setting Visibility on the column</b>, because the
    /// wiring is the thing under test. Reaching past <see cref="ColumnBar"/> into the grid would set
    /// the state without ever calling what keeps the two in step, and the guard would fail against a
    /// window that works.
    ///
    /// <b>What this cannot say:</b> whether a person DRAGGING a heading can reach the first place at
    /// all while a column is frozen. That is WPF's drag handler, it needs a real mouse, and clicking
    /// by coordinate is closed on this machine - <c>tools/gui-probe/interact.ps1</c> refuses because
    /// Windows will not hand the foreground to a background session. What is held here is the
    /// programmatic move, which is the half that is ours.
    /// </summary>
    [Fact]
    public void The_frozen_column_follows_whichever_one_is_leftmost_on_screen()
    {
        // FORCED HERE RATHER THAN LEFT TO A NEIGHBOUR, and this test needed the reminder: the
        // columns read their widths and cell styles out of the theme with FindResource, which throws
        // on a grid whose application has no dictionaries yet. Run inside the class it passed,
        // because another test had already merged them - which is the arrangement WpfHost's own
        // comment records as having been green for the wrong reason once before.
        _ = WpfHost.Resources;

        var bar = new ColumnBar();

        var grid = WpfHost.On(() =>
        {
            var built = new DataGrid();

            ListColumns.Fill(built, bar);

            return built;
        });

        Assert.Equal(1, WpfHost.On(() => grid.FrozenColumnCount));

        // The name column off, which is the state the literal one gets wrong. Its own place in the
        // display order stays where it was, so the count has to grow to reach past it.
        WpfHost.On(() => bar.Choices[0].IsShown = false);

        Assert.True(
            WpfHost.On(() => grid.Columns[1].IsFrozen),
            "With the name column turned off, the leftmost column on screen is not frozen - so "
            + "scrolling right leaves nothing saying which service a row belongs to.");

        // NOW A REORDER WHILE THAT COLUMN IS STILL OFF, and the two halves have to be combined like
        // this or the second one proves nothing. Measured by the mutation runner: with every column
        // on, reading the COLLECTION's order instead of the grid's display order gives the same
        // answer, because moving a column to the front pushes the name column to second place in
        // both. It is a hidden column sitting before the moved one that tells them apart - so this
        // moves PID to the front with the name column collapsed, where the collection order says two
        // columns are frozen and the display order says one.
        WpfHost.On(() => grid.Columns[5].DisplayIndex = 0);

        Assert.True(
            WpfHost.On(() => grid.Columns[5].IsFrozen),
            "A column moved to the front is not the frozen one, so the freeze is still on whatever "
            + "used to be there.");

        Assert.Equal(1, WpfHost.On(() => grid.FrozenColumnCount));

        // Put back, so what follows is about turning the column on rather than about the move.
        WpfHost.On(() => grid.Columns[5].DisplayIndex = 5);

        // And the name column back on, because a freeze that only ever grew would leave two columns
        // frozen out of a window that asked for one.
        WpfHost.On(() => bar.Choices[0].IsShown = true);

        Assert.Equal(1, WpfHost.On(() => grid.FrozenColumnCount));
    }

    [Fact]
    public async Task The_chosen_row_is_what_a_copy_would_take()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler", "Print Spooler")), new SteppedClock());

        await model.LoadAsync();

        model.Selected = model.Rows[0];

        Assert.Equal("Spooler", model.SelectedServiceName);
        Assert.Equal("Print Spooler", model.SelectedDisplayName);
    }
}
