using System.Windows.Input;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What a key press means, and what the two that change something actually do.
///
/// <b>The window had one shortcut until 2026-08-05 and it was F5.</b> `docs/11` 9.1 is blunt
/// about why that is a fault rather than a gap: WCAG 2.1.1 asks that everything reachable by
/// mouse be reachable by keyboard, and the tool this one is compared with - <c>services.msc</c> -
/// has never needed a mouse for any of it. Backlog 59.
///
/// These tests exist because the mapping was split away from the handler. A key handler in code
/// behind can only be checked by somebody pressing keys, which is not a guard - so what a press
/// MEANS lives in <see cref="Shortcuts"/> and the doing is one line each.
/// </summary>
public sealed class KeyboardTests
{
    // One fact each rather than a table, because the expected value is an internal type and a
    // public test method cannot take one as a parameter.
    [Fact]
    public void F5_refreshes_the_way_it_does_everywhere_in_windows() =>
        Assert.Equal(Shortcut.Refresh, Shortcuts.For(Key.F5, ModifierKeys.None));

    [Fact]
    public void Control_F_goes_to_the_search_the_way_it_does_everywhere_in_windows() =>
        Assert.Equal(Shortcut.FocusQuery, Shortcuts.For(Key.F, ModifierKeys.Control));

    [Fact]
    public void Escape_backs_out_of_what_was_typed_the_way_it_does_everywhere_in_windows() =>
        Assert.Equal(Shortcut.Back, Shortcuts.For(Key.Escape, ModifierKeys.None));

    /// <summary>
    /// Enter asks for everything about the chosen entry - `docs/11` 9.1, backlog 59.
    ///
    /// <b>What this does NOT say is where the press came from.</b> Enter belongs to the list rather
    /// than to the window, and that half is asked in <see cref="MainWindow"/> because where the
    /// keyboard is happens to be the one piece of state only a window can answer.
    /// </summary>
    [Fact]
    public void Enter_asks_for_the_details_of_the_chosen_entry() =>
        Assert.Equal(Shortcut.OpenDetails, Shortcuts.For(Key.Enter, ModifierKeys.None));

    /// <summary>
    /// A key with a modifier nobody asked for belongs to whatever claims it.
    ///
    /// Not a detail: a program that swallows every combination merely CONTAINING Control breaks
    /// shortcuts it has never heard of, and does it silently, on somebody else's machine.
    /// </summary>
    [Theory]
    [InlineData(Key.F, ModifierKeys.None)]
    [InlineData(Key.F, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.F, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.F5, ModifierKeys.Control)]
    [InlineData(Key.Escape, ModifierKeys.Shift)]
    [InlineData(Key.A, ModifierKeys.None)]
    public void Anything_else_is_left_alone(Key key, ModifierKeys modifiers) =>
        Assert.Equal(Shortcut.None, Shortcuts.For(key, modifiers));

    [Fact]
    public async Task Escape_empties_the_query_and_the_list_comes_back()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("beep")), new SteppedClock());

        await model.LoadAsync();

        // Both kinds are on screen, so what comes back after Escape is the whole of what the scope
        // holds rather than half of it - the window opens on services, and this test is about the
        // question rather than about which list is showing.
        model.Scope = EntryScope.Everything;
        model.QueryText = "name:spooler";

        Assert.Single(model.Rows);
        Assert.True(model.ClearQuery());
        Assert.Equal(string.Empty, model.QueryText);
        Assert.Equal(2, model.Rows.Count);
    }

    /// <summary>
    /// An empty box says so instead of swallowing the press.
    ///
    /// Escape is the one key every window in Windows already has an opinion about, and the
    /// window takes it before anything else gets a chance. Reporting that nothing happened is
    /// what keeps that from becoming a key that stops working.
    /// </summary>
    [Fact]
    public async Task Escape_on_an_empty_query_does_nothing_and_admits_it()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        // THE BOX OPENS EMPTY SINCE 2026-08-19, so this is true from the very first press. It used
        // to open carrying !type:driver, and the first clear had that to do - a step this test had
        // to take before it could ask its own question.
        Assert.False(model.ClearQuery());

        // And again after something really was typed and taken back, which is the state a person
        // arrives in rather than the one the window starts in.
        model.QueryText = "spool";

        Assert.True(model.ClearQuery());
        Assert.False(model.ClearQuery());
    }

    /// <summary>
    /// Clearing the box leaves the window on the list it was on.
    ///
    /// <b>THIS ASSERTS THE REVERSE OF WHAT IT DID UNTIL 2026-08-19, and both are owner's decisions.
    /// It was called Clearing_the_query_brings_the_drivers_switch_back_with_it</b>, and it was
    /// keeping a real promise: while hiding drivers was a member of the query, emptying the query
    /// had to give them back, or the box and the list would have said two different things.
    ///
    /// Scope is a state beside the query now, so Escape empties the QUESTION and nothing else -
    /// which is what a person pressing it means. Throwing away which list they were looking at as
    /// well would be one key press doing two things, and the second one silently.
    /// </summary>
    [Fact]
    public async Task Clearing_the_query_leaves_the_scope_where_it_was()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("beep")), new SteppedClock());

        await model.LoadAsync();

        model.Scope = EntryScope.Drivers;
        model.QueryText = "beep";

        Assert.Single(model.Rows);

        Assert.True(model.ClearQuery());

        Assert.Equal(EntryScope.Drivers, model.Scope);
        Assert.Equal(["beep"], model.Rows.Select(row => row.ServiceName));
    }

    /// <summary>
    /// Escape closes the panel first and empties the query only when there is no panel.
    ///
    /// <b>The order is the decision, and it is the whole reason Escape has a branch at all.</b> One
    /// press doing both at once takes somebody's query away while they were reaching for the panel,
    /// and a query is much the more expensive of the two to type again. Decided in `docs/04` at
    /// Paczka 1 rather than here.
    ///
    /// Driven through the window rather than through the two objects, because the ORDER is the
    /// thing being claimed and it lives in the window - asking the panel and the query separately
    /// would pass on a window that had them the wrong way round.
    /// </summary>
    [Fact]
    public void Escape_closes_the_panel_before_it_touches_the_query()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        // ON THE INTERFACE THREAD, because this model is the window's DataContext - setting a
        // property on it from the test thread raises PropertyChanged into live bindings, which only
        // fails when the machine is busy enough for the two to overlap.
        WpfHost.On(() => model.QueryText = "name:spooler");
        WpfHost.On(() => model.Chosen.Row = EntryRow.Of(Rows.Entry("Spooler")));

        Assert.True(WpfHost.On(() => model.Chosen.Show()));

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.False(model.Chosen.Showing);
        Assert.Equal("name:spooler", model.QueryText);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.Equal(string.Empty, model.QueryText);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Enter with no row chosen is handed back rather than swallowed.
    ///
    /// The window takes Enter in preview, before the list gets a look at it, so a press claimed by
    /// something that opened nothing is a press that silently stops working for whatever needed it
    /// next.
    /// </summary>
    [Fact]
    public void Enter_with_no_row_chosen_is_handed_back()
    {
        var window = WpfHost.Window();

        Assert.False(WpfHost.On(() => window.Act(Shortcut.OpenDetails, out _)));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The one shortcut that waits for the machine claims its key BEFORE it starts waiting -
    /// backlog 302.
    ///
    /// <b>A routed event is over the moment its handler gives control back, and an async method
    /// gives control back at its first await.</b> So <c>e.Handled = await Act(...)</c> wrote the
    /// answer into an argument WPF had already finished reading, and F5 reached the query box as
    /// well as this window. Every other branch got away with the same line, because awaiting a
    /// task that has already finished never yields - which is what made this a fault of shape
    /// rather than of behaviour, and what would have handed it to the next asynchronous branch
    /// anybody wrote.
    ///
    /// <b>WHAT THIS DOES NOT EXECUTE, said rather than left to be assumed: the routed event
    /// itself.</b> Building a real <see cref="KeyEventArgs"/> needs a presentation source, which
    /// needs a window on somebody's screen, and these tests deliberately show none. What is
    /// checked is the one thing the fault was made of - that the answer is in hand before the work
    /// is - and the signature is what now makes the other order impossible to write.
    /// </summary>
    [Fact]
    public async Task Refresh_claims_its_key_before_the_reading_it_starts_has_finished()
    {
        var window = WpfHost.Window();
        var work = Task.CompletedTask;

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Refresh, out work)));

        await work;

        WpfHost.On(window.Close);
    }

    // ---------------------------------------------------------------------------------------
    // The list under the search box - point 9 of `docs/11` 2.14, backlog 15, 2026-09-15. The
    // keys are decided in three places and each is asked here: what a key MEANS (Shortcuts),
    // where it means it (Wanted, with the focus facts handed in), and what it DOES (Act).
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Down_and_Up_mean_the_list_under_the_box() =>
        Assert.Equal(
            [Shortcut.NextSuggestion, Shortcut.PreviousSuggestion],
            [Shortcuts.For(Key.Down, ModifierKeys.None), Shortcuts.For(Key.Up, ModifierKeys.None)]);

    /// <summary>
    /// Where a press means what. The focus facts are arguments, because a window these tests
    /// build is never shown and so never has a keyboard in it - which is exactly why this table
    /// had to be reachable without one.
    /// </summary>
    [Fact]
    public void Down_belongs_to_the_list_under_the_box_from_the_box_and_to_the_grid_from_the_grid()
    {
        var window = WpfHost.Window();

        Assert.Equal(Shortcut.NextSuggestion, Wanted(window, Key.Down, inTheBox: true, inTheGrid: false));
        Assert.Equal(Shortcut.PreviousSuggestion, Wanted(window, Key.Up, inTheBox: true, inTheGrid: false));

        // The grid's own arrows, untouched - and nobody's from anywhere else.
        Assert.Equal(Shortcut.None, Wanted(window, Key.Down, inTheBox: false, inTheGrid: true));
        Assert.Equal(Shortcut.None, Wanted(window, Key.Up, inTheBox: false, inTheGrid: true));
        Assert.Equal(Shortcut.None, Wanted(window, Key.Down, inTheBox: false, inTheGrid: false));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Enter in the box takes the chosen row while the list is open and is nobody's while it is
    /// closed - decision 8 of the design, which is what Enter in the box was before the list
    /// existed. From the grid it is the details, as it has been since backlog 59.
    /// </summary>
    [Fact]
    public void Enter_in_the_box_takes_a_row_only_while_the_list_is_open()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Assert.Equal(Shortcut.None, Wanted(window, Key.Enter, inTheBox: true, inTheGrid: false));

        WpfHost.On(() =>
        {
            model.Suggesting.Keyboard(present: true);
            model.Suggesting.Ask("sta", 3, 0);
        });

        Assert.Equal(Shortcut.TakeSuggestion, Wanted(window, Key.Enter, inTheBox: true, inTheGrid: false));
        Assert.Equal(Shortcut.OpenDetails, Wanted(window, Key.Enter, inTheBox: false, inTheGrid: true));

        // Ctrl+C in the box is the box's own copy, as it was.
        Assert.Equal(Shortcut.None, WpfHost.On(() => window.Wanted(Key.C, ModifierKeys.Control, inTheBox: true, inTheGrid: false)));

        WpfHost.On(window.Close);
    }

    [Fact]
    public void Down_on_a_closed_list_opens_it_and_moves_through_it_once_open()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Typed(window, "sta", 3);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.NextSuggestion, out _)));
        Assert.Equal(["status", "start"], model.Suggesting.Offered.Select(row => row.Word));
        Assert.Equal("status", model.Suggesting.Chosen!.Word);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.NextSuggestion, out _)));
        Assert.Equal("start", model.Suggesting.Chosen!.Word);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.PreviousSuggestion, out _)));
        Assert.Equal("status", model.Suggesting.Chosen!.Word);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Up on a closed list, Enter on a closed list and Down where nothing fits are handed back,
    /// because a press claimed by something that did nothing is a press that stops working for
    /// whatever needed it next - the box, here.
    /// </summary>
    [Fact]
    public void A_press_on_the_list_that_does_nothing_is_handed_back()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Typed(window, "foo:", 4);

        Assert.False(WpfHost.On(() => window.Act(Shortcut.PreviousSuggestion, out _)));
        Assert.False(WpfHost.On(() => window.Act(Shortcut.TakeSuggestion, out _)));
        Assert.False(WpfHost.On(() => window.Act(Shortcut.NextSuggestion, out _)));
        Assert.Equal("foo:", WpfHost.On(() => window.Search.Box.Text));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Decision 13: the row is written THROUGH THE SELECTION, so Ctrl+Z takes it back - and the
    /// caret lands after what was written, where the values of the field are offered at once.
    ///
    /// <b>WHAT THIS DOES NOT ASSERT, measured 2026-09-16: that Undo takes it back.</b> A TextBox in
    /// this host records no undo unit at all - a plain box given "abc", then "def" through its
    /// selection, answers false to Undo() - so the undo half of decision 13 is the live window's
    /// question, in section 6 of the design, and what is held here is the shape that makes it
    /// possible: the range replaced through the selection, the rest of the line untouched, the
    /// selection collapsed after what was written.
    /// </summary>
    [Fact]
    public void Enter_writes_the_chosen_row_through_the_selection_and_puts_the_caret_after_it()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Typed(window, "spool sta", 9);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.NextSuggestion, out _)));
        Assert.True(WpfHost.On(() => window.Act(Shortcut.NextSuggestion, out _)));
        Assert.Equal("start", model.Suggesting.Chosen!.Word);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.TakeSuggestion, out _)));

        Assert.Equal("spool start:", WpfHost.On(() => window.Search.Box.Text));
        Assert.Equal(12, WpfHost.On(() => window.Search.Box.CaretIndex));
        Assert.Equal(0, WpfHost.On(() => window.Search.Box.SelectionLength));

        // Followed at once by what can go after the colon - eight start types and three words.
        Assert.True(model.Suggesting.IsOpen);
        Assert.Equal(11, model.Suggesting.Offered.Count);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Escape closes the list and only the list - the text somebody is still typing stays - and
    /// the next Escape does what Escape did before the list existed.
    /// </summary>
    [Fact]
    public void Escape_closes_the_list_first_and_leaves_the_query_alone()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Typed(window, "sta", 3);

        // THE MODEL HAS TO HOLD THE TEXT BEFORE THE FIRST ESCAPE, or the order cannot be told
        // apart: the binding waits 400 ms, and a chain with the query first would find nothing
        // to clear, fall through to the list, and pass. The mutation registry found that version.
        WpfHost.Until(() => model.QueryText == "sta", "the query text reached the model");

        Assert.True(WpfHost.On(() => window.Act(Shortcut.NextSuggestion, out _)));
        Assert.True(model.Suggesting.IsOpen);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.False(model.Suggesting.IsOpen);
        Assert.Equal("sta", WpfHost.On(() => window.Search.Box.Text));
        Assert.Equal("sta", model.QueryText);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.Equal(string.Empty, model.QueryText);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Ctrl+F is an arrival: an empty box offers the questions that fit the list on screen, a
    /// box with text in it - selected whole, so nothing to complete - offers nothing.
    /// </summary>
    [Fact]
    public void Control_F_on_an_empty_box_offers_the_questions_that_fit_the_list_and_on_a_full_one_nothing()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.FocusQuery, out _)));
        Assert.Equal(QueryExamples.For(model.Scope).Count, model.Suggesting.Offered.Count);

        WpfHost.On(() => model.Suggesting.Close());
        WpfHost.On(() => window.Search.Box.Text = "spool");

        Assert.True(WpfHost.On(() => window.Act(Shortcut.FocusQuery, out _)));
        Assert.False(model.Suggesting.IsOpen);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// On the overview screen the row holding the box is collapsed, and a popup hung off a
    /// collapsed target opens somewhere on the screen with nothing under it - and nothing would
    /// close it but Escape, because the box never had the keyboard to lose. Found by reading the
    /// diff as a stranger (`docs/12` step 7), not by a test.
    /// </summary>
    [Fact]
    public void Control_F_on_the_overview_screen_opens_no_list()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.On(() => model.ShowingOverview = true);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.FocusQuery, out _)));
        Assert.False(model.Suggesting.IsOpen);

        WpfHost.On(() => model.ShowingOverview = false);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.FocusQuery, out _)));
        Assert.True(model.Suggesting.IsOpen);

        WpfHost.On(window.Close);
    }

    private static Shortcut Wanted(MainWindow window, Key key, bool inTheBox, bool inTheGrid) =>
        WpfHost.On(() => window.Wanted(key, ModifierKeys.None, inTheBox, inTheGrid));

    /// <summary>
    /// What the box holds after somebody typed it, with the keyboard in the box.
    ///
    /// <b>SETTLED FIRST, AND THAT LINE COST AN HOUR ON 2026-09-16.</b> The box's Text binding
    /// attaches LAZILY - queued to the data-bind engine at DataBind priority when the window is
    /// built - and an Invoke from the test thread runs at Send, ahead of that queue. So text set
    /// into a freshly built window was overwritten a moment later by the binding attaching and
    /// transferring the model's empty query into the box: the list then held the six examples
    /// rather than the fields, and only sometimes. Found from a stack trace on TextChanged
    /// (BindingExpression.AttachToContext under DataBindEngine.Run), not by reasoning. A shown
    /// window has drained that queue long before anybody types - this host never shows one.
    /// </summary>
    private static void Typed(MainWindow window, string text, int caret)
    {
        WpfHost.Settled();

        WpfHost.On(() =>
        {
            window.Search.Box.Text = text;
            window.Search.Box.CaretIndex = caret;
            ((MainViewModel)window.DataContext).Suggesting.Keyboard(present: true);
        });
    }
}
