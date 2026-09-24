using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the details panel says about one entry - the half that needs no window.
///
/// <b>The panel is the column catalogue applied to one row, and these guards are what holds it to
/// that.</b> The alternative was a second list of fields written out beside the columns, and this
/// project has already measured what two lists of one thing cost: they agree on the day they are
/// written and disagree the first time only one of them is touched, with nothing in a green build
/// to say so.
/// </summary>
public sealed class DetailsGuards
{
    /// <summary>
    /// Every column the picker offers appears in the panel, exactly once.
    ///
    /// <b>Whether or not somebody turned it on</b>, which is the difference between the panel and
    /// the list: the list is what you chose to see, the panel is everything there is about one
    /// entry. A field that appeared only when its column was on would be a panel that answers
    /// differently depending on a decision made about a different screen.
    /// </summary>
    [Fact]
    public void Every_column_the_picker_offers_has_a_line_in_the_panel_exactly_once()
    {
        var lines = Details.Of(Rows.Entry("Spooler")).SelectMany(section => section.Lines).ToList();

        foreach (var column in Columns.All)
        {
            var named = lines.Count(line => line.Label == Texts.Of(column.LabelKey));

            Assert.True(
                named == 1,
                $"The panel names {column.Id} {named} times, and a field belongs in exactly one "
                + "place - the panel is the whole catalogue applied to one entry.");
        }
    }

    /// <summary>
    /// A line says exactly what that column's own cell says, character for character - for every
    /// column whose cell shows the value as the machine holds it.
    ///
    /// The guard for the one design decision this class rests on. Two paths to one answer are how
    /// a field comes to say "no access" in a cell and nothing at all in the panel.
    ///
    /// <b>The column that translates what it shows is the sibling test's subject</b>, since
    /// 2026-09-15: its line carries the cell's words AND the spelling behind them, and asserting
    /// equality with the cell here would be asserting that the panel hides the spelling.
    /// </summary>
    [Fact]
    public void A_line_says_exactly_what_that_columns_cell_says()
    {
        var entry = Rows.Entry("Spooler");
        var lines = Details.Of(entry).SelectMany(section => section.Lines).ToList();
        var plain = Columns.All.Where(column => column.Holds is null).ToList();

        Assert.True(plain.Count >= Columns.All.Count - 1, "More than one column translates, so this test covers less than it says.");

        foreach (var column in plain)
        {
            var line = lines.First(shown => shown.Label == Texts.Of(column.LabelKey));

            Assert.Equal(column.Reads(entry), line.Value);
        }
    }

    /// <summary>
    /// The account line carries the name the cell shows and, in brackets, the spelling the machine
    /// holds - so the panel and a copy give somebody the value <c>account:</c> matches without
    /// disagreeing with the cell above them.
    ///
    /// <b>Literal on both sides</b>, rather than composed through the same call the panel uses,
    /// because a test built from the language file's format would pass over a format that said
    /// nothing. The spelling is the mixed-case one 19 services on the owner's machine carry, so
    /// the bracket shows what was read and not a tidied version of it.
    /// </summary>
    [Fact]
    public void The_account_line_carries_the_name_and_the_spelling_behind_it()
    {
        var entry = Rows.Entry("AppIDSvc") with
        {
            Account = Reading<string>.Present(@"NT Authority\LocalService")
        };

        var line = Details.Of(entry)
            .SelectMany(section => section.Lines)
            .Single(shown => shown.Label == Texts.Of("gui.column.account"));

        Assert.Equal(@"Local Service (NT Authority\LocalService)", line.Value);

        // And the copy says the same, because it is built from these lines.
        Assert.Contains(@"Local Service (NT Authority\LocalService)", Details.AsText(entry), StringComparison.Ordinal);
    }

    /// <summary>
    /// An account outside the three the window names is a line like any other: the spelling, and
    /// no bracket repeating it.
    /// </summary>
    [Fact]
    public void An_account_the_window_does_not_name_is_shown_as_held_and_not_bracketed()
    {
        var entry = Rows.Entry("McmSvc") with
        {
            Account = Reading<string>.Present(@"NT SERVICE\McmSvc")
        };

        var line = Details.Of(entry)
            .SelectMany(section => section.Lines)
            .Single(shown => shown.Label == Texts.Of("gui.column.account"));

        Assert.Equal(@"NT SERVICE\McmSvc", line.Value);
    }

    /// <summary>
    /// THE FIELDS THIS PANEL USED TO APOLOGISE FOR ARE ORDINARY LINES NOW - backlog 21.
    ///
    /// <b>This replaces a test rather than joining the file, and the replacement is the whole
    /// record of what changed.</b> What stood here asserted that four fields were named with
    /// "nobody looked" and that the note pointed at `--signatures` on the command line. That was
    /// rule 8 done properly for a window that did not read them. The window reads them now, so the
    /// same assertion would be pinning an apology the product has stopped owing - and an apology
    /// nobody retracts is how a panel starts lying politely.
    ///
    /// What is asserted instead is the other half of the same rule: each of them carries a value
    /// worked out from the entry, and no section is left making an excuse.
    /// </summary>
    [Fact]
    public void The_fields_this_panel_once_apologised_for_carry_values_now()
    {
        var entry = Rows.Entry("Spooler");
        var sections = Details.Of(entry);
        var lines = sections.SelectMany(section => section.Lines).ToList();

        foreach (var id in new[] { "signature", "publisher", "fileVersion", "binaryHash", "memory" })
        {
            var column = Columns.Of(id);

            Assert.NotNull(column);

            var line = lines.Single(shown => shown.Label == Texts.Of(column!.LabelKey));

            Assert.Equal(column!.Reads(entry), line.Value);
        }

        // NO NOTE OVER A FILE ON THIS MACHINE, since 2026-09-24. From 2026-09-16 every section with
        // an unread line said "the window reads a field when its column is turned on" - which
        // stopped being true when the panel began reading for itself (UX-GUI-005). An unread line
        // on a local file is a reading not yet back, and the line itself says so.
        Assert.All(sections, section => Assert.Equal(string.Empty, section.Note));
    }

    /// <summary>
    /// A file on another machine is the one reason a signature line stays unread after the panel
    /// has read - the window does not reach over the network - and only there, and only under the
    /// section holding the signature, does the panel say so.
    /// </summary>
    [Fact]
    public void A_file_on_another_machine_is_the_one_unread_the_panel_explains()
    {
        var entry = Rows.Entry("Spooler") with { BinaryFile = Reading<string>.Present(@"\\server\share\spoolsv.exe") };
        var sections = Details.Shown(entry);

        var noted = Assert.Single(sections, section => section.Note.Length > 0);

        Assert.Equal(Texts.Of(Columns.Binary), noted.Heading);
        Assert.Equal(Texts.Of("gui.details.notRead.network"), noted.Note);
    }

    /// <summary>
    /// Every line sits under the heading its own column belongs to in the picker.
    ///
    /// One grouping decided in one place - <see cref="Columns"/> - so somebody who learned where a
    /// field lives while choosing columns finds it in the same place while reading about an entry.
    /// </summary>
    [Fact]
    public void Every_line_sits_under_the_heading_its_column_belongs_to()
    {
        var sections = Details.Of(Rows.Entry("Spooler"));

        foreach (var column in Columns.All)
        {
            var heading = Texts.Of(Columns.GroupOf(column.Id)!);
            var holding = sections.Single(section =>
                section.Lines.Any(line => line.Label == Texts.Of(column.LabelKey)));

            Assert.Equal(heading, holding.Heading);
        }
    }

    /// <summary>
    /// The fields where alignment carries meaning are marked to be drawn in a fixed width face.
    ///
    /// Glossary part 4: a launch path and a security descriptor are values where two different
    /// things must not be able to look the same. The list already draws them that way, and a panel
    /// that dropped it would be the more careful reading of the two losing the distinction.
    /// </summary>
    [Fact]
    public void A_path_and_a_descriptor_are_marked_for_a_fixed_width_face()
    {
        var lines = Details.Of(Rows.Entry("Spooler"))
            .SelectMany(section => section.Lines)
            .ToDictionary(line => line.Label, StringComparer.Ordinal);

        Assert.Equal("fixed", lines[Texts.Of("gui.column.binaryPath")].Wears);
        Assert.Equal("fixed", lines[Texts.Of("gui.column.securityDescriptor")].Wears);
        Assert.Equal("text", lines[Texts.Of("gui.column.displayName")].Wears);
        Assert.Equal("prose", lines[Texts.Of("gui.column.description")].Wears);
    }

    /// <summary>
    /// The lines the list marks with a shape carry the same shape code, under the same name the
    /// list's own mark styles bind to - so the panel draws the dot beside "Running" out of the
    /// style the list draws it with, and cannot show a running entry in a colour the list does
    /// not use. `docs/11` 3.1: a state is a shape, a colour and a word, and until 2026-09-16 the
    /// panel had the word alone.
    /// </summary>
    [Fact]
    public void The_status_and_the_start_type_wear_the_marks_the_list_draws()
    {
        var entry = Rows.Entry("Spooler");
        var lines = Details.Of(entry)
            .SelectMany(section => section.Lines)
            .ToDictionary(line => line.Label, StringComparer.Ordinal);

        var status = lines[Texts.Of("gui.column.status")];
        var start = lines[Texts.Of("gui.column.startType")];
        var account = lines[Texts.Of("gui.column.account")];

        Assert.Equal("status", status.Wears);
        Assert.Equal(CellFaces.StatusShape(entry.Status), status.StatusShape);
        Assert.Equal(string.Empty, status.StartShape);

        Assert.Equal("start", start.Wears);
        Assert.Equal(CellFaces.StartShape(entry, StartQualifiers.Of(entry)), start.StartShape);
        Assert.Equal(string.Empty, start.StatusShape);

        Assert.Equal(string.Empty, account.StatusShape);
        Assert.Equal(string.Empty, account.StartShape);
        Assert.Equal(string.Empty, account.AgainstShape);
    }

    /// <summary>
    /// Nothing is drawn as a word, not as a blank - backlog 367, found by the component catalogue
    /// the first day it drew this panel over a driver: "Account" and "PID" with nothing after them,
    /// which on a panel about one entry reads as "did not load". The cell's own words stay in
    /// Value, blank included, so the guard holding a line to its cell keeps holding - what is DRAWN
    /// is Shown, and it carries the word.
    /// </summary>
    [Fact]
    public void A_field_with_genuinely_nothing_in_it_says_none_rather_than_showing_a_blank()
    {
        var driver = Rows.Entry("disk") with
        {
            Account = Reading<string>.Absent(),
            ProcessId = Reading<int>.Absent()
        };

        var lines = Details.Shown(driver)
            .SelectMany(section => section.Lines)
            .ToDictionary(line => line.Label, StringComparer.Ordinal);

        var account = lines[Texts.Of("gui.column.account")];

        Assert.Equal(string.Empty, account.Value);
        Assert.Equal(Texts.Of("gui.details.none"), account.Shown);
        Assert.True(account.Missing);

        Assert.Equal(Texts.Of("gui.details.none"), lines[Texts.Of("gui.column.processId")].Shown);

        var status = lines[Texts.Of("gui.column.status")];

        Assert.Equal(status.Value, status.Shown);
        Assert.False(status.Missing);
    }

    /// <summary>
    /// A field the window has not asked the machine for yet says so - "not read", the glossary's
    /// word, in the colour of a label - and never "unknown", which is what the machine says when
    /// it answered with something this code cannot name. The panel says it six times over an
    /// entry somebody is looking at, so the two sentences had to come apart.
    /// </summary>
    [Fact]
    public void A_field_nobody_has_asked_for_yet_says_not_read_and_is_drawn_as_a_state()
    {
        var lines = Details.Shown(Rows.Entry("Spooler"))
            .SelectMany(section => section.Lines)
            .ToDictionary(line => line.Label, StringComparer.Ordinal);

        var signature = lines[Texts.Of("gui.column.signature")];

        Assert.Equal(ReadOutcome.NotRead, signature.Outcome);
        Assert.Equal(Texts.Of("gui.cell.notRead"), signature.Shown);
        Assert.True(signature.Missing);

        Assert.Equal(ReadOutcome.Present, lines[Texts.Of("gui.column.startType")].Outcome);
    }

    /// <summary>
    /// The panel does not repeat its own head: the two names it shows above the sections are not
    /// lines of the first section as well. A copy has no head, so it keeps them.
    /// </summary>
    [Fact]
    public void The_panel_leaves_the_two_names_to_its_head_and_a_copy_keeps_them()
    {
        var entry = Rows.Entry("Spooler", "Print Spooler");
        var shown = Details.Shown(entry).SelectMany(section => section.Lines).Select(line => line.Label).ToList();
        var copied = Details.Of(entry).SelectMany(section => section.Lines).Select(line => line.Label).ToList();

        Assert.DoesNotContain(Texts.Of("gui.column.name"), shown);
        Assert.DoesNotContain(Texts.Of("gui.column.displayName"), shown);
        Assert.Contains(Texts.Of("gui.column.name"), copied);
        Assert.Contains(Texts.Of("gui.column.displayName"), copied);
        Assert.Equal(copied.Count - 2, shown.Count);

        Assert.Contains("Print Spooler", Details.AsText(entry), StringComparison.Ordinal);
    }

    /// <summary>
    /// A field nobody could read says so in the panel, in the same words the cell uses.
    ///
    /// The four read states reach the panel because the values come through the catalogue. Without
    /// this the class above could pass against a panel that renders a refusal as an empty line,
    /// which is the one thing an empty line must never mean.
    /// </summary>
    [Fact]
    public void A_field_that_could_not_be_read_says_so_rather_than_showing_nothing()
    {
        var refused = Rows.Entry("Spooler") with
        {
            Account = Bws.Core.Reading<string>.Denied(5, "Access is denied.")
        };

        var line = Details.Of(refused)
            .SelectMany(section => section.Lines)
            .First(shown => shown.Label == Texts.Of("gui.column.account"));

        Assert.False(string.IsNullOrWhiteSpace(line.Value));
        Assert.Equal(Columns.Of("account")!.Reads(refused), line.Value);
    }
}
