using System.Globalization;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the search over a table of the audit finds - owner's decision AS-b of 2026-10-20: plain text,
/// every word somewhere in the row, upper and lower case the same.
/// </summary>
public sealed class RowSearchTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Nothing_typed_or_only_spaces_is_no_words_and_finds_every_row(string? typed)
    {
        var words = RowSearch.Words(typed);

        Assert.Empty(words);
        Assert.True(RowSearch.Finds("anything", words));
    }

    [Fact]
    public void Words_are_split_on_any_white_space_and_said_back_one_space_apart()
    {
        // Pasted text brings tabs and line breaks - the sentence about the search must not.
        var words = RowSearch.Words("  bits\tSYSTEM\r\n2026-10 ");

        Assert.Equal(["bits", "SYSTEM", "2026-10"], words);
        Assert.Equal("bits SYSTEM 2026-10", RowSearch.Typed(words));
    }

    [Fact]
    public void Every_word_has_to_be_somewhere_in_the_row_in_any_case()
    {
        var said = RowSearch.Said("BITS", "Background Intelligent Transfer Service", @"NT AUTHORITY\SYSTEM");

        Assert.True(RowSearch.Finds(said, RowSearch.Words("bits system")));
        Assert.True(RowSearch.Finds(said, RowSearch.Words("TRANSFER")));
        Assert.False(RowSearch.Finds(said, RowSearch.Words("bits spooler")));
    }

    [Fact]
    public void A_word_never_matches_across_the_edge_of_two_values()
    {
        // Glued together, "Spooler" and "BITS" would hold "rbi".
        var said = RowSearch.Said("Spooler", "BITS");

        Assert.False(RowSearch.Finds(said, RowSearch.Words("rbi")));
    }

    [Fact]
    public void Values_that_are_not_there_are_left_out()
    {
        Assert.Equal("a\nb", RowSearch.Said("a", null, string.Empty, "b"));
    }

    [Fact]
    public void Case_is_the_same_whatever_the_culture_of_the_machine()
    {
        // Rule 26: under Turkish rules a lower "i" pairs with a dotted capital, so a culture's comparison
        // would not find BITS for "bits". The list's bare word is ordinal and case blind, and so is this.
        var before = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.True(RowSearch.Finds("BITS", RowSearch.Words("bits")));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
