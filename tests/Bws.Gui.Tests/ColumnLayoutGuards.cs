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
        var text = ColumnLayout.Default.Render();

        Assert.Contains("\"schemaVersion\": 1", text, StringComparison.Ordinal);
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

        var read = ColumnLayout.Read(written.Render());

        Assert.Null(read.Unreadable);
        Assert.Equal(written.Columns, read.Layout?.Columns);
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
        var text = ColumnLayout.Default.Render();

        Assert.DoesNotContain("\"width\"", text, StringComparison.Ordinal);
        Assert.All(
            ColumnLayout.Read(text).Layout!.Columns,
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

        var read = ColumnLayout.Read(written.Render()).Layout!;

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
        ]));

        Assert.Equal(["colourOfTheIcon"], plan.Ignored);
        Assert.DoesNotContain(plan.Layout.Columns, column => column.Id == "colourOfTheIcon");
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
        ]));

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
            [new KeptColumn("status", Shown: true, Width: null)]));

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
            [.. Columns.All.Select(column => new KeptColumn(column.Id, Shown: false, Width: null))]));

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
        var read = ColumnLayout.Read(
            """
            { "columns": [ { "id": "serviceName" }, { "id": "description" } ], "schemaVersion": 1 }
            """);

        Assert.True(read.Layout!.Columns[0].Shown);
        Assert.False(read.Layout.Columns[1].Shown);
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
        var read = ColumnLayout.Read(content);

        Assert.Null(read.Layout);
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
        var read = ColumnLayout.Read("""{ "columns": [], "schemaVersion": 1 }""");

        Assert.NotNull(read.Layout);
        Assert.False(read.WorthSaying);

        var plan = ColumnPlan.Of(read.Layout);

        Assert.False(plan.NoneWasShown);
        Assert.Empty(plan.Ignored);
        Assert.Equal(ColumnLayout.Default.Columns, plan.Layout.Columns);
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
        var read = ColumnLayout.Read(
            """
            { "columns": [ { "id": "serviceName", "shown": true } ], "schemaVersion": 4 }
            """);

        Assert.Null(read.Layout);
        Assert.Null(read.Unreadable);
        Assert.Equal(4, read.OtherSchemaVersion);
    }
}
