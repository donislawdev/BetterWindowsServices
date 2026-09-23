using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Bws.Architecture.Tests;

/// <summary>
/// The dead-code scan's own correctness, on source written for the purpose.
///
/// Not on a live symbol, and that is the one thing changed from the owner's Python project, which
/// used a real definition as its probe - a probe that works for exactly as long as that definition
/// exists and then goes quietly green. Each rule below is one <see cref="DeadCode"/> states, and a
/// scan that stopped keeping it would read exactly like a scan that found nothing.
/// </summary>
public sealed class DeadCodeScanTests
{
    [Fact]
    public void A_definition_only_its_tests_name_is_reported()
    {
        var sites = new Dictionary<string, List<int>> { ["Helper"] = [DeadCode.Test, DeadCode.Test] };

        Assert.Equal(new[] { 0 }, DeadCode.Unreached(["Helper"], sites));
    }

    [Fact]
    public void Two_definitions_that_only_name_each_other_are_both_reported()
    {
        var sites = new Dictionary<string, List<int>> { ["Ping"] = [1], ["Pong"] = [0] };

        Assert.Equal(new[] { 0, 1 }, DeadCode.Unreached(["Ping", "Pong"], sites));
    }

    [Fact]
    public void A_type_only_a_test_names_is_reported_by_the_scan()
    {
        // The same rule as the first test, asked of the whole scan rather than of the pure half: that
        // one hands Unreached a site already marked as a test, and cannot see whether reading a test
        // file still marks it so.
        var found = UnreachedBesideTests(
            "public static class OnlyTested { public static int Answer() => 42; }",
            "var answer = OnlyTested.Answer();");

        Assert.Equal(["OnlyTested"], found);
    }

    [Fact]
    public void A_definition_that_only_names_itself_is_reported()
    {
        var sites = new Dictionary<string, List<int>> { ["Again"] = [0] };

        Assert.Equal(new[] { 0 }, DeadCode.Unreached(["Again"], sites));
    }

    [Fact]
    public void Life_reaches_down_a_chain_from_a_root()
    {
        var sites = new Dictionary<string, List<int>> { ["Entry"] = [DeadCode.Root], ["Middle"] = [0], ["Leaf"] = [1] };

        Assert.Empty(DeadCode.Unreached(["Entry", "Middle", "Leaf"], sites));
    }

    [Fact]
    public void A_comment_or_a_documentation_reference_keeps_nothing_alive()
    {
        // The reference stands on a LIVING type on purpose. Written on Forgotten itself, as the first
        // draft had it, a counted reference would be Forgotten naming itself - recursion, not life -
        // and the test stayed green with the rule broken. The mutation run on 2026-09-23 found that.
        var found = Unreached(
            """
            // Forgotten is kept for the next session, which will want it.
            /// <summary>Took over from <see cref="Forgotten"/>.</summary>
            public static class Used { public static void Go() { } }

            public static class Forgotten { }
            """,
            "Used.Go();");

        Assert.Equal(["Forgotten"], found);
    }

    [Fact]
    public void A_mention_from_inside_dead_code_keeps_nothing_alive()
    {
        var found = Unreached(
            """
            public static class Orphan { public static int Ask() => Helper.Answer(); }
            public static class Helper { public static int Answer() => 42; }
            """,
            string.Empty);

        Assert.Equal(["Orphan", "Helper"], found);
    }

    [Fact]
    public void An_override_and_a_member_called_by_contract_are_not_definitions()
    {
        var found = Unreached(
            """
            public sealed class Thing : System.IDisposable
            {
                public override string ToString() => "thing";
                public void Dispose() { }
            }
            """,
            "using var thing = new Thing();");

        Assert.Empty(found);
    }

    [Fact]
    public void Markup_and_an_attribute_each_keep_a_name_alive()
    {
        var found = Unreached(
            """
            public sealed class ShownInMarkup { }
            public sealed class MarkedAttribute : System.Attribute { }
            """,
            "[assembly: Marked]",
            "<local:ShownInMarkup Width=\"10\" />");

        Assert.Empty(found);
    }

    /// <summary>The names the scan reports, for one file of types, one of top-level statements and one of markup.</summary>
    private static string[] Unreached(string types, string program, string markup = "") =>
        Names(DeadCodeScan.Of([("Program.cs", Parse(program)), ("Types.cs", Parse(types))], [], [markup]));

    /// <summary>The names the scan reports, for one file of types and one of test code beside it.</summary>
    private static string[] UnreachedBesideTests(string types, string tests) =>
        Names(DeadCodeScan.Of([("Types.cs", Parse(types))], [Parse(tests)], []));

    private static string[] Names(DeadCodeScan scan) => [.. scan.Unreached().Select(definition => definition.Name)];

    private static SyntaxNode Parse(string text) => CSharpSyntaxTree.ParseText(text, CodeShape.Language).GetRoot();
}
