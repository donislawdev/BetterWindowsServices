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
    /// The four fields this window does not read are named, and the panel says why.
    ///
    /// <b>Rule 8 in the place a details panel breaks it most easily.</b> Leaving them out entirely
    /// would let the panel read as complete when it is not - somebody looking for a signature
    /// would conclude the tool cannot see one, rather than that this window did not look. They are
    /// not columns for the opposite reason: in a cell they would write "nobody looked" eight
    /// hundred times.
    /// </summary>
    [Fact]
    public void The_four_fields_nobody_looked_at_are_named_and_the_panel_says_why()
    {
        var sections = Details.Of(Rows.Entry("Spooler"));
        var admitted = sections.Single(section => section.Note.Length > 0);

        Assert.Equal(4, admitted.Lines.Count);
        Assert.All(admitted.Lines, line => Assert.Equal(Texts.Of("gui.details.notRead"), line.Value));

        // The sentence has to point somewhere, or it is an admission with no way out of it.
        Assert.Contains("--signatures", admitted.Note, StringComparison.Ordinal);
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
