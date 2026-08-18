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
    /// A line says exactly what that column's own cell says, character for character.
    ///
    /// The guard for the one design decision this class rests on. Two paths to one answer are how
    /// a field comes to say "no access" in a cell and nothing at all in the panel.
    /// </summary>
    [Fact]
    public void A_line_says_exactly_what_that_columns_cell_says()
    {
        var entry = Rows.Entry("Spooler");
        var lines = Details.Of(entry).SelectMany(section => section.Lines).ToList();

        foreach (var column in Columns.All)
        {
            var line = lines.First(shown => shown.Label == Texts.Of(column.LabelKey));

            Assert.Equal(column.Reads(entry), line.Value);
        }
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

        Assert.All(sections, section => Assert.Equal(string.Empty, section.Note));
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

        Assert.True(lines[Texts.Of("gui.column.binaryPath")].FixedWidth);
        Assert.True(lines[Texts.Of("gui.column.securityDescriptor")].FixedWidth);
        Assert.False(lines[Texts.Of("gui.column.displayName")].FixedWidth);
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
