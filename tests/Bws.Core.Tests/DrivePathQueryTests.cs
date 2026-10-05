using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// A path pasted into the search box is a search, not a field called C.
///
/// <b>Measured on the owner's machine before the repair</b>: <c>C:\Windows\System32\svchost.exe</c>
/// ended with a code of 2 and a sentence about a field named C, while the README says a bare word
/// searches the path (stability report Q-6). After it, the same text answers with 251 entries.
/// </summary>
public sealed class DrivePathQueryTests
{
    [Theory]
    [InlineData("C:\\WINDOWS\\System32\\spoolsv.exe")]
    [InlineData("c:\\windows")]
    [InlineData("!D:\\Games")]
    public void A_pasted_path_is_a_search_rather_than_a_field(string query)
    {
        var parsed = QueryParser.Parse(query);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));
        Assert.True(parsed.Query!.Match(Entries.Any).Matched);
    }

    [Fact]
    public void A_letter_and_a_colon_with_no_separator_after_it_still_names_a_field()
    {
        // The rule is the shape of a drive, not the length of the name. Without the separator it
        // is a field like any other, and an unknown one is still said rather than searched for.
        var problem = Assert.Single(QueryParser.Parse("x:abc").Problems);

        Assert.Equal(QueryProblemKind.UnknownField, problem.Kind);
    }

    [Fact]
    public void No_field_and_no_alias_has_a_name_of_one_letter()
    {
        // What the rule above stands on. The day a field or alias of one letter arrives, its
        // values starting with a separator would be read as paths - so this goes red first, and
        // whoever adds it has to decide which reading wins.
        var names = QueryFields.All.SelectMany(field => field.Aliases.Prepend(field.Name));

        Assert.DoesNotContain(names, name => name.Length == 1);
    }
}
