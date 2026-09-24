// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the list looks like as a file - the promise being "what the list shows", not "what the
/// window read".
///
/// <b>Split in two on purpose, because the fault has two halves.</b> What a CSV SAYS is a pure
/// question and is asked of <see cref="Exporting"/> without a window. WHICH rows and WHICH columns
/// is a question only a grid answers, and both have an answer that is easy to get almost right -
/// the model's order rather than the sorted one, the catalogue's order rather than the dragged one.
///
/// <b>What nothing here presses is the button itself.</b> It opens a save dialog, which is modal and
/// belongs to whoever is at the machine - the same reason nothing presses the button that restarts
/// this program as administrator. What that handler does either side of the dialog is two lines.
/// </summary>
public sealed class ExportingGuards
{
    /// <summary>
    /// The headings are the ones on screen and the cells are what those columns say.
    ///
    /// <b>The headings are words rather than identifiers, which is the opposite of what the layout
    /// file writes</b> - and the difference is the audience. A layout is read by this program on
    /// another machine, where a translated word names no column. This is read by a person, so it
    /// says what they saw, and somebody who needs stable field names has the command line.
    /// </summary>
    [Fact]
    public void The_file_says_what_the_headings_say_and_what_the_cells_under_them_say()
    {
        var rows = new[] { EntryRow.Of(Rows.Entry("Spooler", "Print Spooler")) };

        var text = Exporting.AsCsv(["serviceName", "displayName"], rows);

        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.column.name") + "," + Bws.Gui.Texts.Of("gui.column.displayName"),
            lines[0]);

        Assert.Equal("Spooler,Print Spooler", lines[1]);
    }


    /// <summary>
    /// A list narrowed to nothing writes a file with a heading and no rows.
    ///
    /// <b>The degenerate case named in this packet's four states, and it was a claim until this
    /// test.</b> A file of one line is true and readable - a person who exported a search that
    /// matched nothing gets a file saying which columns they were looking at, rather than an empty
    /// file that reads like a failure.
    /// </summary>
    [Fact]
    public void A_list_narrowed_to_nothing_writes_a_heading_and_no_rows()
    {
        var text = Exporting.AsCsv(["serviceName"], []);

        Assert.Equal(Bws.Gui.Texts.Of("gui.column.name") + Environment.NewLine, text);
    }

    /// <summary>
    /// A value carrying a comma, a quote or a line break is quoted the way the format says.
    ///
    /// <b>The ordinary case rather than the careful one.</b> Service descriptions on this machine run
    /// to twelve hundred characters and contain all three - a file that split those into columns
    /// would be a file that says something the window never did.
    /// </summary>
    [Fact]
    public void A_value_that_would_break_the_format_is_quoted_and_its_quotes_are_doubled()
    {
        var awkward = Rows.Entry("Spooler", "Prints, \"quickly\"\r\nand quietly");

        var text = Exporting.AsCsv(["displayName"], [EntryRow.Of(awkward)]);

        Assert.Contains("\"Prints, \"\"quickly\"\"\r\nand quietly\"", text, StringComparison.Ordinal);

        // And nothing that does not need it is quoted, because a file where every field is wrapped
        // is one nobody can read in a text editor.
        Assert.DoesNotContain("\"Spooler\"", Exporting.AsCsv(["serviceName"], [EntryRow.Of(awkward)]), StringComparison.Ordinal);
    }

    /// <summary>
    /// A value a spreadsheet would run is made into something it reads.
    ///
    /// <b>THE VALUES IN THIS FILE ARE WRITTEN BY WHOEVER INSTALLED THE SERVICE, which is the thing
    /// being audited.</b> A display name, a launch path, an account and a description all come from
    /// there. A spreadsheet treats a cell starting with one of five characters as a formula, so a
    /// service named <c>=cmd|'/c calc'!A1</c> arrived in an administrator's sheet as something to
    /// run. Quoting to RFC 4180 does not stop it - the quotes come off on the way in and the
    /// formula is what is left.
    ///
    /// Owner's decision, 2026-08-26, with the cost said out loud: this CHANGES the value, so the
    /// file stops being a character for character copy of the screen.
    /// </summary>
    [Theory]
    [InlineData("=cmd|'/c calc'!A1")]
    [InlineData("+1+1")]
    [InlineData("-1+1")]
    [InlineData("@SUM(A1)")]
    public void A_value_a_spreadsheet_would_evaluate_is_made_inert(string dangerous)
    {
        var text = Exporting.AsCsv(["displayName"], [EntryRow.Of(Rows.Entry("Spooler", dangerous))]);

        Assert.Contains("'" + dangerous, text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_ordinary_value_is_left_exactly_as_it_was()
    {
        // The half that must not be bought with the one above. Nearly every value in this file is
        // ordinary, and a file where they all carried a stray apostrophe would be a file nobody
        // could paste anywhere.
        var text = Exporting.AsCsv(["displayName"], [EntryRow.Of(Rows.Entry("Spooler", "Print Spooler"))]);

        Assert.Contains("Print Spooler", text, StringComparison.Ordinal);
        Assert.DoesNotContain("'Print Spooler", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The columns are the ones that are ON, in the order they are on SCREEN.
    ///
    /// <b>Read through the window rather than through the catalogue</b>, because the two disagree the
    /// moment somebody drags a heading - and a file in the catalogue's order would look right in
    /// every test and wrong on the machine of anybody who had rearranged anything.
    /// </summary>
    [Fact]
    public void The_columns_are_the_ones_on_screen_in_the_order_they_are_on_screen()
    {
        _ = WpfHost.Resources;

        var window = WpfHost.On(() => new MainWindow(WpfHost.Nowhere()));

        var shown = WpfHost.On(window.ShownColumns);

        Assert.Equal(
            Columns.All.Where(column => column.ShownAtFirst).Select(column => column.Id),
            shown);

        // Drag one to the front, and the file follows the window rather than the catalogue.
        //
        // IT WAS processId UNTIL 2026-09-02 AND THAT COLUMN WENT OFF BY DEFAULT THAT DAY, so
        // dragging it proved nothing about a list it was no longer in. The account is the last of
        // the five a first run shows, which makes it the same test: something from the far end
        // arriving at the front.
        WpfHost.On(() => window.Entries.Columns
            .First(column => column.SortMemberPath == "account").DisplayIndex = 0);

        Assert.Equal("account", WpfHost.On(window.ShownColumns)[0]);

        WpfHost.On(window.Close);
    }
    /// <summary>
    /// The rows are the ones on screen, in the order somebody sorted them into.
    ///
    /// <b>The view rather than the model, and this is the half that would have been missed.</b> The
    /// model holds the entries the query left, in the order the machine handed them over. The order
    /// a person sees is a comparer on the view over those entries - so a file built from the model
    /// would hold the right rows in an order nobody chose, and would look correct in every way
    /// except the one it promises.
    /// </summary>
    [Fact]
    public async Task The_rows_are_in_the_order_somebody_sorted_them_into()
    {
        _ = WpfHost.Resources;

        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Bravo"), Rows.Entry("Alpha")), new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.On(() => new MainWindow(WpfHost.Nowhere(), model));

        WpfHost.On(() => window.Entries.ItemsSource = model.Rows);
        WpfHost.Settled();

        // As the machine handed them over, which is not the order anybody asked for.
        Assert.Equal(["Bravo", "Alpha"], WpfHost.On(() => window.InTheOrderOnScreen().Select(row => row.ServiceName)));

        var heading = WpfHost.On(() => window.Entries.Columns
            .First(column => column.SortMemberPath == "serviceName"));

        WpfHost.On(() => ListSorting.WhenAHeadingIsClicked(
            window.Entries, new DataGridSortingEventArgs(heading)));

        WpfHost.Settled();

        Assert.Equal(["Alpha", "Bravo"], WpfHost.On(() => window.InTheOrderOnScreen().Select(row => row.ServiceName)));

        WpfHost.On(window.Close);
    }
    /// <summary>
    /// The file on disk is what a spreadsheet can read, mark and all.
    ///
    /// <b>The bytes rather than the string, because the difference is three of them.</b> AtomicFile
    /// writes UTF-8 without a mark, deliberately, for a snapshot our own reader opens - and a
    /// spreadsheet opening a CSV without one guesses the encoding. On this machine the display names
    /// are full of characters it guesses wrongly.
    ///
    /// <b>What this does NOT reach is the dialog that asks where to put it.</b> Measured 2026-08-25:
    /// a save dialog opened from a window started in the background never appears at all, which is
    /// the same limit interact.ps1 refuses on. Everything either side of it is held here.
    /// </summary>
    [Fact]
    public async Task What_lands_on_disk_carries_the_mark_a_spreadsheet_needs()
    {
        _ = WpfHost.Resources;

        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler", "Print Spooler")), new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.On(() => new MainWindow(WpfHost.Nowhere(), model));

        WpfHost.On(() => window.Entries.ItemsSource = model.Rows);
        WpfHost.Settled();

        var path = Path.Combine(Path.GetTempPath(), $"bws-export-{Guid.NewGuid():N}.csv");

        try
        {
            Assert.Null(WpfHost.On(() => window.WriteShownTo(path)));

            var bytes = await File.ReadAllBytesAsync(path);

            Assert.True(
                bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "The file has no byte order mark, so a spreadsheet will guess the encoding.");

            var lines = await File.ReadAllLinesAsync(path);

            Assert.Equal(2, lines.Length);
            Assert.Contains("Spooler", lines[1], StringComparison.Ordinal);
            Assert.Contains("Print Spooler", lines[1], StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
            WpfHost.On(window.Close);
        }
    }

    /// <summary>
    /// A file that was written is SAID to have been written, with how many rows went in and the
    /// name it went to - UX-GUI-016, owner's decision, 2026-09-24, reversing a window that said
    /// nothing when it worked.
    ///
    /// <b>And the sentence goes when somebody asks for something else</b>, because "wrote one entry"
    /// under a list somebody has since narrowed to twelve is a sentence about a list nobody is
    /// looking at.
    /// </summary>
    [Fact]
    public async Task A_written_file_is_said_with_its_rows_and_its_name_until_somebody_moves_on()
    {
        _ = WpfHost.Resources;

        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler", "Print Spooler")), new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.On(() => new MainWindow(WpfHost.Nowhere(), model));

        WpfHost.On(() => window.Entries.ItemsSource = model.Rows);
        WpfHost.Settled();

        var path = Path.Combine(Path.GetTempPath(), $"bws-export-{Guid.NewGuid():N}.csv");

        try
        {
            Assert.Equal(string.Empty, model.Says.Done);

            Assert.Null(WpfHost.On(() => window.WriteShownTo(path)));

            Assert.Equal(Exporting.Wrote(1, Path.GetFileName(path)), model.Says.Done);
            Assert.Contains(Path.GetFileName(path), model.Says.Done, StringComparison.Ordinal);
            Assert.Equal(string.Empty, model.Says.Problem);

            WpfHost.On(() => model.QueryText = "spool");

            Assert.Equal(string.Empty, model.Says.Done);
        }
        finally
        {
            File.Delete(path);
            WpfHost.On(window.Close);
        }
    }

    /// <summary>
    /// What a finished action did and what a refused one could not do share the second line of the
    /// foot of the window, so the newer puts the older away - the row holds two lines and no more.
    /// </summary>
    [Fact]
    public void A_finished_action_and_a_refused_one_put_each_other_away()
    {
        var says = new Says();

        says.Did(Exporting.Wrote(3, "services.csv"));
        says.CouldNotDo("the disk is full");

        Assert.Equal(string.Empty, says.Done);
        Assert.NotEqual(string.Empty, says.Problem);

        says.Did(Exporting.Wrote(3, "services.csv"));

        Assert.Equal(string.Empty, says.Problem);
        Assert.NotEqual(string.Empty, says.Done);
    }

    /// <summary>
    /// The name the save dialog offers is the list's, not "services" on every tab - UX-GUI-016. A
    /// file is named once and read by people who never saw the window it came from.
    /// </summary>
    [Fact]
    public void The_name_offered_is_the_list_somebody_is_looking_at()
    {
        var names = new[] { EntryScope.Services, EntryScope.Drivers, EntryScope.Everything }
            .Select(Exporting.FileName)
            .ToList();

        Assert.Equal("services.csv", names[0]);
        Assert.Equal(3, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, name => Assert.EndsWith(".csv", name, StringComparison.Ordinal));
    }

    /// <summary>One row is "entry" and every other number is "entries" - the plural pair the language file keeps.</summary>
    [Fact]
    public void The_sentence_counts_one_row_in_the_singular()
    {
        Assert.Contains("1 entry to", Exporting.Wrote(1, "drivers.csv"), StringComparison.Ordinal);
        Assert.Contains("0 entries to", Exporting.Wrote(0, "drivers.csv"), StringComparison.Ordinal);
    }
}
