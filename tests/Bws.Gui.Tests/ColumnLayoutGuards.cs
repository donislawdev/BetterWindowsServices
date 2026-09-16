using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What a layout IS, and the shapes a file of one has to survive.
///
/// <b>Nothing here touches a disk, which is what the split off <see cref="PreferencesFileGuards"/>
/// is about.</b> A layout is a document: it is rendered, read back, and reconciled with the columns
/// this build happens to have. Where that document lives, what happens when it cannot be read at
/// all and what the window says about it are a different subject with different failures - and the
/// size ratchet asked the question, on 2026-08-13, at the same seam the product itself has.
///
/// <b>The identifiers in the rendered file are a frozen contract</b> - `docs/02` names the
/// configuration format among the public surfaces. What that means for these tests is that a change
/// which makes them fail is a change to somebody else's file, never a test to update.
/// </summary>
public sealed class ColumnLayoutGuards
{
    /// <summary>
    /// What a rendered layout looks like, checked against the contract rather than against itself.
    ///
    /// <b>The version, the identifiers and the fact that every column is written.</b> A file
    /// holding only the columns somebody turned on would be smaller and would answer the wrong
    /// question the first time a build adds one: a person could not tell a column they had turned
    /// off from a column that did not exist when they saved.
    /// </summary>
    [Fact]
    public void A_rendered_layout_names_its_schema_and_every_column_by_its_glossary_name()
    {
        var text = ColumnLayouts.Default.Render();

        Assert.Contains("\"schemaVersion\": 4", text, StringComparison.Ordinal);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);

        foreach (var column in Columns.All)
        {
            Assert.Contains($"\"id\": \"{column.Id}\"", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A layout survives the trip through text unchanged, widths and all.
    ///
    /// The one property that makes the file worth writing. Everything else in this class is about
    /// files that are wrong.
    /// </summary>
    [Fact]
    public void A_layout_read_back_from_its_own_text_is_the_layout_that_was_written()
    {
        var written = new ColumnLayout(
        [
            new KeptColumn("status", Shown: true, Width: "300"),
            new KeptColumn("serviceName", Shown: false, Width: null),
            new KeptColumn("account", Shown: true, Width: "2.5*")
        ]);

        var read = ColumnLayouts.Read(Only(written).Render());

        Assert.Null(read.Unreadable);
        Assert.Equal(written.Columns, read.Layouts?.Services.Columns);
    }

    /// <summary>
    /// A width is only in the file when somebody moved it, so a theme can still change.
    ///
    /// The other half of this lives where a width means something - see the harvest in
    /// <see cref="KeptColumnGuards"/>. What is held here is that null survives the round trip as
    /// null rather than arriving back as a number nobody chose.
    /// </summary>
    [Fact]
    public void A_column_nobody_dragged_carries_no_width_at_all()
    {
        var text = ColumnLayouts.Default.Render();

        Assert.DoesNotContain("\"width\"", text, StringComparison.Ordinal);
        Assert.All(
            ColumnLayouts.Read(text).Layouts!.Services.Columns,
            column => Assert.Null(column.Width));
    }

    /// <summary>
    /// The array's order is the display order, both ways.
    ///
    /// <b>Written as an order rather than as a number on each entry, and this is the guard for
    /// what that buys.</b> A position field can hold two columns claiming the same place, or a
    /// file that jumps from three to nine - an array cannot be inconsistent about its own order,
    /// so the whole family of faults has nowhere to arrive from.
    /// </summary>
    [Fact]
    public void The_order_the_columns_are_written_in_is_the_order_they_come_back_in()
    {
        var written = new ColumnLayout(
            [.. Columns.All.Reverse().Select(column => new KeptColumn(column.Id, column.ShownAtFirst, null))]);

        var read = ColumnLayouts.Read(Only(written).Render()).Layouts!.Services;

        Assert.Equal(
            written.Columns.Select(column => column.Id),
            read.Columns.Select(column => column.Id));
    }

    /// <summary>
    /// A column this build does not have is left out and named, rather than swallowed.
    ///
    /// <b>Rule 8 in the file layer.</b> This is the shape a layout takes after a column has been
    /// renamed or withdrawn, and it is exactly the case a plan could silently drop - the window
    /// would come up looking almost right, which is the state nobody investigates.
    /// </summary>
    [Fact]
    public void A_column_the_catalogue_does_not_know_is_left_out_and_said_out_loud()
    {
        var plan = ColumnPlan.Of(new ColumnLayout(
        [
            new KeptColumn("serviceName", Shown: true, Width: null),
            new KeptColumn("colourOfTheIcon", Shown: true, Width: null)
        ]), EntryScope.Services);

        Assert.Equal(["colourOfTheIcon"], plan.Ignored);
        Assert.DoesNotContain(plan.Layout.Columns, column => column.Id == "colourOfTheIcon");
    }

    /// <summary>
    /// A layout that names no order is planned with the order this build opens with, and one that
    /// names an order keeps it.
    ///
    /// <b>The shape of every profile written before 2026-09-02, and of the owner's own.</b> A fresh
    /// profile has opened sorted by display name since that day - but a file that already held
    /// columns and no <c>sort</c> line kept planning a null, which the grid reads as "take every
    /// order off", so the list came up in the manager's order: alphabetical by a column that is off
    /// by default, which is no order anybody can see. Found on a screenshot on 2026-09-15. No test
    /// saw it because every test window reads a fixture that names no order either - which is also
    /// why this asks the PLAN rather than a window: the window guards were green over the fault.
    ///
    /// <b>Both halves, because either alone passes for the wrong reason.</b> That a missing order
    /// becomes the default would be satisfied by a plan that ignored the file's order altogether.
    /// That a named order survives would be satisfied by the plan this test replaces.
    /// </summary>
    [Fact]
    public void A_layout_that_names_no_order_is_planned_with_the_order_this_build_opens_with()
    {
        var columns = new[] { new KeptColumn("status", Shown: true, Width: null) };

        var unsaid = ColumnPlan.Of(new ColumnLayout(columns, Sort: null), EntryScope.Services);

        Assert.Equal(ColumnLayout.DefaultFor(EntryScope.Services).Sort, unsaid.Layout.Sort);
        Assert.NotNull(unsaid.Layout.Sort);

        var said = ColumnPlan.Of(
            new ColumnLayout(columns, new KeptSort("status", Descending: true)),
            EntryScope.Services);

        Assert.Equal(new KeptSort("status", Descending: true), said.Layout.Sort);
    }

    /// <summary>
    /// The same column twice is one column, and the second mention is named as ignored.
    ///
    /// A hand edited file, or two files merged by somebody's tooling. Left alone it would put one
    /// column in two places in the display order, which WPF answers by moving whichever it
    /// reaches last - a layout that comes back different from the one on disk, with nothing said.
    /// </summary>
    [Fact]
    public void The_same_column_named_twice_is_taken_once_and_the_repeat_is_said_out_loud()
    {
        var plan = ColumnPlan.Of(new ColumnLayout(
        [
            new KeptColumn("status", Shown: true, Width: "120"),
            new KeptColumn("status", Shown: false, Width: "400")
        ]), EntryScope.Services);

        Assert.Equal(["status"], plan.Ignored);
        Assert.Single(plan.Layout.Columns, column => column.Id == "status");

        // The FIRST one decides, which is the only answer that does not depend on how far down the
        // file the duplicate happens to be.
        var kept = plan.Layout.Columns.First(column => column.Id == "status");

        Assert.True(kept.Shown);
        Assert.Equal("120", kept.Width);
    }

    /// <summary>
    /// A column added since the layout was written arrives with the state it would have had.
    ///
    /// <b>The case that makes an old file safe to keep.</b> Dropping it would be a new column that
    /// silently does not exist for everybody who used the previous version - and turning it on
    /// would rearrange a list somebody had arranged. Its own default is the only answer that is
    /// neither.
    /// </summary>
    [Fact]
    public void A_column_the_file_predates_arrives_at_the_end_with_its_usual_state()
    {
        var plan = ColumnPlan.Of(new ColumnLayout(
            [new KeptColumn("status", Shown: true, Width: null)]), EntryScope.Services);

        Assert.Empty(plan.Ignored);
        Assert.Equal(Columns.All.Count, plan.Layout.Columns.Count);
        Assert.Equal("status", plan.Layout.Columns[0].Id);

        foreach (var column in Columns.All.Where(column => column.Id != "status"))
        {
            var arrived = plan.Layout.Columns.First(kept => kept.Id == column.Id);

            Assert.Equal(column.ShownAtFirst, arrived.Shown);
        }
    }

    /// <summary>
    /// A file that hides every column is refused, because the window it asks for cannot be used.
    ///
    /// Eight hundred rows of nothing above a count line still saying eight hundred - complaint 9
    /// of `docs/11`, reached through a door the picker itself keeps shut. Only a hand edited file
    /// can ask for it, and the honest answer is the usual columns and a sentence.
    /// </summary>
    [Fact]
    public void A_layout_with_every_column_hidden_gets_the_usual_columns_back_and_says_so()
    {
        var plan = ColumnPlan.Of(new ColumnLayout(
            [.. Columns.All.Select(column => new KeptColumn(column.Id, Shown: false, Width: null))]),
            EntryScope.Services);

        Assert.True(plan.NoneWasShown);
        Assert.Equal(
            Columns.All.Count(column => column.ShownAtFirst),
            plan.Layout.Columns.Count(column => column.Shown));
    }

    /// <summary>
    /// An entry holding nothing but an identifier means "this column, as it comes".
    ///
    /// Defaulting to hidden would answer a half written line by taking a column away, which is the
    /// less recoverable of the two guesses: a person who wanted it off can turn it off.
    /// </summary>
    [Fact]
    public void An_entry_that_does_not_say_whether_it_is_shown_gets_the_columns_usual_state()
    {
        var read = ColumnLayouts.Read(
            """
            { "columns": [ { "id": "serviceName" }, { "id": "description" } ], "schemaVersion": 1 }
            """);

        // BOTH EXPECTATIONS FLIPPED ON 2026-09-02 AND THE CLAIM DID NOT: serviceName went off by
        // default and description came on, owner's decision. What this asserts is that an entry
        // saying nothing about "shown" takes the column's own usual state - so the day those two
        // states swapped is the day this test had to swap with them or stop meaning anything.
        Assert.False(read.Layouts!.Services.Columns[0].Shown);
        Assert.True(read.Layouts.Services.Columns[1].Shown);
    }

    /// <summary>
    /// Text that is not a layout is refused with a reason rather than throwing.
    ///
    /// Four shapes, and none of them is exotic for a file kept for months: truncated by a full
    /// disk, edited into something that is not an object, saved without a version, and saved with
    /// no columns at all.
    /// </summary>
    [Theory]
    [InlineData("{ \"columns\": [")]
    [InlineData("[ ]")]
    [InlineData("{ \"columns\": [ ] }")]
    [InlineData("{ \"schemaVersion\": 1 }")]
    public void A_file_that_is_not_a_layout_comes_back_with_a_reason(string content)
    {
        var read = ColumnLayouts.Read(content);

        Assert.Null(read.Layouts);
        Assert.NotNull(read.Unreadable);
        Assert.True(read.WorthSaying);
    }

    /// <summary>
    /// A file that names no columns at all means "nothing was saved", not "hide everything".
    ///
    /// <b>The two are one character apart in the file and opposite on screen.</b> A layout the
    /// window wrote always names all eighteen, so an empty list only arrives by hand or from
    /// something that trimmed the file - and answering it with an empty window would be the
    /// harshest possible reading of somebody's typo.
    /// </summary>
    [Fact]
    public void A_file_naming_no_columns_gets_the_usual_ones_without_a_word_about_it()
    {
        // THE CURRENT VERSION, WHATEVER IT IS TODAY, and the number moves with the schema for a
        // reason: this test is about an empty list of columns and nothing else. It said 1 until
        // 2026-08-19, 2 until the morning of 2026-08-25 and 3 until that afternoon, and each time
        // an older number turned it into a test
        // about carrying a file forward - which is true, useful, and somebody else's subject. A
        // fixture asserting silence has to be silent for the reason it names.
        var read = ColumnLayouts.Read("""{ "columns": [], "schemaVersion": 4 }""");

        Assert.NotNull(read.Layouts);
        Assert.False(read.WorthSaying);

        var plan = ColumnPlan.Of(read.Layouts.Services, EntryScope.Services);

        Assert.False(plan.NoneWasShown);
        Assert.Empty(plan.Ignored);
        Assert.Equal(ColumnLayout.DefaultFor(EntryScope.Services).Columns, plan.Layout.Columns);
    }

    /// <summary>
    /// A schema this build does not read is left alone rather than guessed at or overwritten.
    ///
    /// <b>Told apart from a damaged file on purpose, and the difference is what happens to the
    /// bytes.</b> A damaged file is moved aside so the next write can succeed. A file from another
    /// build is somebody's working layout in a format this one does not know - reading it would
    /// guess at what a field means there, and replacing it would throw away what they get back the
    /// moment they open the other build again.
    /// </summary>
    [Fact]
    public void A_layout_from_another_schema_is_refused_by_number_rather_than_by_damage()
    {
        var read = ColumnLayouts.Read(
            """
            { "columns": [ { "id": "serviceName", "shown": true } ], "schemaVersion": 5 }
            """);

        Assert.Null(read.Layouts);
        Assert.Null(read.Unreadable);
        Assert.Equal(5, read.OtherSchemaVersion);
    }

    /// <summary>
    /// A VERSION 1 FILE IS READ AND CARRIED FORWARD, which is the half of the schema rule that
    /// changed on 2026-08-19 when the file grew a layout per scope.
    ///
    /// <b>Older and newer had meant the same thing, and only one of them ever deserved it.</b> A
    /// file from a build that does not exist yet holds fields this one would guess at. A file from
    /// our own past holds fields all of which are known here - and treating it as foreign would
    /// have made the scope switch a slice that silently forgot the widths of everybody who had ever
    /// dragged a column edge.
    ///
    /// <b>The sections it does not have are that scope's defaults</b>, and the person is told the
    /// file was carried forward, because the next change writes it back in a shape their previous
    /// build cannot read.
    /// </summary>
    [Fact]
    public void A_layout_from_the_previous_schema_is_carried_forward_rather_than_left_behind()
    {
        var read = ColumnLayouts.Read(
            """
            { "columns": [ { "id": "status", "shown": true, "width": "300" } ], "schemaVersion": 1 }
            """);

        Assert.Null(read.Unreadable);
        Assert.Null(read.OtherSchemaVersion);
        Assert.Equal(1, read.CarriedForwardFrom);
        Assert.True(read.WorthSaying);

        // The widths they dragged, exactly where they left them.
        Assert.Equal(
            new KeptColumn("status", Shown: true, Width: "300"),
            read.Layouts!.Services.Columns[0]);

        // And the two scopes the file says nothing about start from their own defaults rather than
        // from empty, which is the same rule a missing column already follows.
        Assert.Equal(ColumnLayout.DefaultFor(EntryScope.Drivers).Columns, read.Layouts.Drivers.Columns);
        Assert.Equal(ColumnLayout.DefaultFor(EntryScope.Everything).Columns, read.Layouts.Everything.Columns);
    }

    /// <summary>
    /// Three layouts go out and the same three come back, each to its own scope.
    ///
    /// <b>Named scopes rather than positions</b>, because a file whose sections could be told apart
    /// only by their order would put somebody's driver columns onto their services list the first
    /// time anything reordered them.
    /// </summary>
    [Fact]
    public void Each_scope_keeps_its_own_layout_through_the_trip_to_text_and_back()
    {
        var written = new ColumnLayouts(
            new ColumnLayout([new KeptColumn("status", Shown: true, Width: "300")]),
            new ColumnLayout([new KeptColumn("serviceName", Shown: true, Width: "120")]),
            new ColumnLayout([new KeptColumn("description", Shown: false, Width: null)]));

        var read = ColumnLayouts.Read(written.Render()).Layouts!;

        Assert.Equal(written.Services.Columns, read.Services.Columns);
        Assert.Equal(written.Drivers.Columns, read.Drivers.Columns);
        Assert.Equal(written.Everything.Columns, read.Everything.Columns);

        // Nothing to say about a file this build wrote itself.
        Assert.Null(ColumnLayouts.Read(written.Render()).CarriedForwardFrom);

        // ASKED BY SCOPE AS WELL AS BY NAME, and this half was missing until a mutation said so.
        // The round trip above compares the three properties directly, so a For() that handed back
        // the wrong one would have gone unnoticed - and For() is the only way the window ever
        // reaches a layout. A scope handed somebody else's columns is a fault that looks exactly
        // like they arranged it that way themselves.
        Assert.Equal(written.Services.Columns, read.For(EntryScope.Services).Columns);
        Assert.Equal(written.Drivers.Columns, read.For(EntryScope.Drivers).Columns);
        Assert.Equal(written.Everything.Columns, read.For(EntryScope.Everything).Columns);

        // And putting one back leaves the other two alone, which is what stops a change made while
        // looking at services from emptying the drivers list.
        var moved = read.With(EntryScope.Drivers, new ColumnLayout([new KeptColumn("status", Shown: true, Width: null)]));

        Assert.Equal(written.Services.Columns, moved.Services.Columns);
        Assert.Equal(written.Everything.Columns, moved.Everything.Columns);
        Assert.Single(moved.Drivers.Columns);
    }

    /// <summary>
    /// THE COLUMNS A DRIVER HAS NOTHING TO PUT IN START OFF, and the numbers are why.
    ///
    /// Measured on this machine on 2026-08-19 through the command line: of 472 drivers, 0 carry a
    /// process identifier and 3 carry an account, against 113 and 312 of 340 services. A single
    /// layout serving both lists spends three columns of the longer one on emptiness.
    ///
    /// <b>Off rather than absent</b>, which those three accounts are the whole reason for - the
    /// argument is at <see cref="Column.OffAtFirstIn"/>. This asserts the default, not the
    /// catalogue: both columns are still there to be turned on.
    /// </summary>
    [Fact]
    public void A_driver_list_starts_without_the_columns_a_driver_cannot_fill()
    {
        var drivers = ColumnLayout.DefaultFor(EntryScope.Drivers);
        var services = ColumnLayout.DefaultFor(EntryScope.Services);

        // ACCOUNT IS THE ONE THAT STILL PROVES OffAtFirstIn DOES ANYTHING, and since 2026-09-02 it is
        // the only one. The process id went off for EVERY scope that day, on the owner's decision to
        // open on the five columns services.msc opens on - so "off here and on there" stopped being
        // true of it, and asserting it would be asserting the wrong half.
        Assert.False(drivers.Columns.Single(column => column.Id == "account").Shown);
        Assert.True(services.Columns.Single(column => column.Id == "account").Shown);

        // The process id keeps its own line, weaker on purpose. Its scope rule is now invisible from
        // outside - it would be off for drivers even without one - and the rule is kept rather than
        // deleted so that turning it back on for services cannot quietly turn it on for drivers too.
        Assert.False(drivers.Columns.Single(column => column.Id == "processId").Shown);

        // Still in the catalogue for both, so somebody can ask for the three drivers that do have
        // an account. A column taken away would answer that question with silence.
        Assert.Equal(services.Columns.Count, drivers.Columns.Count);
    }

    /// <summary>One layout in the shape the file wants, for the tests that are about one.</summary>
    private static ColumnLayouts Only(ColumnLayout layout) =>
        ColumnLayouts.Default with { Services = layout };
}
