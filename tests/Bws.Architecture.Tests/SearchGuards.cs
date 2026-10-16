using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bws.Architecture.Tests;

/// <summary>
/// That every random property is in the weekly search, and reports how much it searched.
///
/// The weekly search (tests/search/search.ps1, .github/workflows/search.yml) lists the tests
/// carrying the trait search=weekly and runs each of them for minutes. A property it does not list
/// is never searched, and nothing says so - the run is green and smaller. A property that samples
/// CsCheck directly is searched and cannot say how many times, because the count is kept by
/// tests/Searching.cs. Both are silences of the kind rule 8 forbids, in an instrument rather than in
/// the product, so they are held here rather than left to whoever writes the next property.
///
/// <b>Read from the syntax tree, not from text,</b> because a property is a method and the trait is
/// an attribute on it - and the one test file with a local function called Sample would match any
/// pattern loose enough to find the real calls.
///
/// <b>What this does not see, said rather than left to be trusted:</b> a property in a file that
/// does not import CsCheck and reaches it through a helper of its own, and a test that draws random
/// input with System.Random by hand (QueryPropertyTests does, with a fixed seed, and that is not a
/// search). Neither exists in a form that would hide from the weekly run today.
/// </summary>
public sealed class SearchGuards
{
    private const string TraitKey = "search";
    private const string TraitValue = "weekly";

    /// <summary>The one file allowed to sample CsCheck for everybody.</summary>
    private const string Door = "Searching.cs";

    /// <summary>
    /// Tests that use CsCheck and are not in the weekly search, one reason each. The list only
    /// shrinks - a new entry is a decision, made here where the next reader sees it.
    /// </summary>
    private static readonly Dictionary<string, string> NotASearch = new(StringComparer.Ordinal)
    {
        ["DiffPropertyTests.The_generated_pairs_reach_every_outcome_these_properties_are_about"] =
            "It asks whether the GENERATOR reaches every outcome, so that the properties beside it " +
            "cannot pass on nothing. Running it for minutes finds nothing a fraction of a second " +
            "does not, and it samples on one thread with counters that are not a property at all.",

        ["SearchCanaryTests.A_property_that_fails_is_red_and_says_how_to_reproduce_it"] =
            "The weekly runner's own canary. It fails only when the runner asks it to, to prove a red " +
            "property reaches the verdict with its seed - so it is run by name, never listed."
    };

    [Fact]
    public void Only_the_door_samples_random_input()
    {
        var direct = Methods()
            .Where(method => method.File != Door && method.SamplesDirectly)
            .Where(method => !NotASearch.ContainsKey(method.Name))
            .Select(method => $"{method.Name} ({method.File})")
            .ToList();

        Assert.True(
            direct.Count == 0,
            "These tests sample CsCheck directly. Go through Searched in tests/Searching.cs, so the " +
            "weekly search can say how many times the property ran - or name the test in " +
            "SearchGuards.NotASearch with the reason it is not a search:" +
            Environment.NewLine + string.Join(Environment.NewLine, direct));
    }

    [Fact]
    public void Every_property_through_the_door_is_in_the_weekly_search()
    {
        var unlisted = Methods()
            .Where(method => method.CallsTheDoor && !method.Weekly && !NotASearch.ContainsKey(method.Name))
            .Select(method => method.Name)
            .ToList();

        Assert.True(
            unlisted.Count == 0,
            $"These tests go through Searched and do not carry [Trait(\"{TraitKey}\", \"{TraitValue}\")], " +
            "so the weekly search never lists them and nobody is told:" +
            Environment.NewLine + string.Join(Environment.NewLine, unlisted));
    }

    [Fact]
    public void Nothing_is_in_the_weekly_search_that_does_not_search()
    {
        // The other direction. A test carrying the trait without sampling anything would be run for
        // a weekly budget and report no calls, which the runner calls NOT SEARCHED - right, and a
        // red Sunday about a label.
        var empty = Methods()
            .Where(method => method.Weekly && !method.CallsTheDoor)
            .Select(method => method.Name)
            .ToList();

        Assert.True(
            empty.Count == 0,
            "These tests carry the weekly trait and never call Searched:" +
            Environment.NewLine + string.Join(Environment.NewLine, empty));
    }

    [Fact]
    public void The_trait_is_spelled_the_one_way_the_runner_lists()
    {
        // A trait the runner filters by is a key and a value compared exactly. "Weekly" with a
        // capital would be a property quietly left out of every Sunday.
        var misspelt = Methods()
            .SelectMany(method => method.SearchTraitValues.Select(value => (method.Name, value)))
            .Where(found => found.value != TraitValue)
            .Select(found => $"{found.Name}: \"{found.value}\"")
            .ToList();

        Assert.True(
            misspelt.Count == 0,
            $"The trait \"{TraitKey}\" has one value, \"{TraitValue}\":" +
            Environment.NewLine + string.Join(Environment.NewLine, misspelt));
    }

    [Fact]
    public void The_exceptions_still_name_tests_that_use_CsCheck()
    {
        // An entry naming a test that is gone, or that stopped using CsCheck, reads as an argument
        // still being made - the same rot BroadCatchGuards guards its list against.
        var users = Methods()
            .Where(method => method.SamplesDirectly || method.CallsTheDoor)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        var gone = NotASearch.Keys.Where(name => !users.Contains(name)).ToList();

        Assert.True(
            gone.Count == 0,
            "SearchGuards.NotASearch names tests that no longer sample anything. Remove them:" +
            Environment.NewLine + string.Join(Environment.NewLine, gone));
    }

    [Fact]
    public void The_reading_finds_the_properties_it_exists_for()
    {
        // Every assertion above passes on zero methods, which is what a reader that stopped finding
        // anything would return. A floor rather than an exact count, so adding a property does not
        // turn this red, and set from the count on the day it was last raised: sixteen weekly
        // properties on 2026-10-16, twenty-one once the five over shaped query text arrived.
        var methods = Methods();

        Assert.True(
            methods.Count(method => method.Weekly) >= 21,
            $"Only {methods.Count(method => method.Weekly)} weekly properties found, against 21 when " +
            "this floor was last raised. Either properties were removed on purpose and this floor goes " +
            "down with them, or the reading stopped seeing them.");

        Assert.Contains(methods, method => method.File == Door && method.SamplesDirectly);
    }

    [Theory]
    [InlineData("using CsCheck; class T { void M() { Gen.Int.Sample(x => true); } }", true)]
    [InlineData("class T { void M() { CsCheck.Gen.Int.Sample(x => true); } }", true)]
    [InlineData("class T { void M() { global::CsCheck.Gen.Int.Sample(x => true); } }", true)]
    [InlineData("class T { void M() { var e = Sample(); } static int Sample() => 1; }", false)]
    [InlineData("class T { void M() { new Random(1).Sample(); } }", false)]
    public void The_reading_sees_a_sample_however_CsCheck_is_named(string source, bool samples)
    {
        // The reading on its own, over five small files whose answer is known - the two shapes
        // that reach CsCheck, the fully qualified one review found, and two that only share the
        // word Sample. Every guard above stands on this reading.
        var root = CSharpSyntaxTree.ParseText(source, CodeShape.Language).GetRoot();

        Assert.Equal(samples, Read(root, "Specimen.cs").Single(method => method.Name == "T.M").SamplesDirectly);
    }

    private static List<TestMethod> Methods() =>
        Sources.Testing()
            .SelectMany(Read)
            .ToList();

    private static IEnumerable<TestMethod> Read(string path) =>
        Read(CodeShape.TreeOf(path).GetRoot(), Path.GetFileName(path));

    /// <summary>
    /// The methods of one file. A file reaches CsCheck when the name stands anywhere in it - the
    /// using directive, or a call written out in full such as <c>CsCheck.Gen.Int[0, 9].Sample(...)</c>
    /// with no directive at all. Only the directive counted until the review of PR #73, and the
    /// second form would have sampled past the door without a word.
    /// </summary>
    private static IEnumerable<TestMethod> Read(SyntaxNode root, string file)
    {
        var usesCsCheck = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(name => name.Identifier.Text == "CsCheck");

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var type = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "?";
            var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(call => call.Expression)
                .OfType<MemberAccessExpressionSyntax>()
                .Select(access => access.Name.Identifier.Text)
                .ToList();

            yield return new TestMethod
            {
                Name = $"{type}.{method.Identifier.Text}",
                File = file,
                SamplesDirectly = usesCsCheck && calls.Any(name => name.StartsWith("Sample", StringComparison.Ordinal)),
                CallsTheDoor = calls.Contains("Searched", StringComparer.Ordinal),
                SearchTraitValues = SearchTraits(method).ToList()
            };
        }
    }

    /// <summary>The values of every [Trait("search", ...)] on a method, written as literals.</summary>
    private static IEnumerable<string> SearchTraits(MethodDeclarationSyntax method) =>
        method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(attribute => attribute.Name.ToString() is "Trait" or "TraitAttribute" or "Xunit.Trait")
            .Select(attribute => attribute.ArgumentList?.Arguments.Select(argument => argument.Expression).ToList() ?? [])
            .Where(arguments => arguments.Count == 2 && Literal(arguments[0]) == TraitKey)
            .Select(arguments => Literal(arguments[1]) ?? arguments[1].ToString());

    private static string? Literal(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal ? literal.Token.ValueText : null;

    private sealed class TestMethod
    {
        public required string Name { get; init; }

        public required string File { get; init; }

        public required bool SamplesDirectly { get; init; }

        public required bool CallsTheDoor { get; init; }

        public required IReadOnlyList<string> SearchTraitValues { get; init; }

        public bool Weekly => SearchTraitValues.Count > 0;
    }
}
