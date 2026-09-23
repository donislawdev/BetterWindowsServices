using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bws.Architecture.Tests;

/// <summary>
/// Tests of the MEASURE in <see cref="CodeShape"/>, not of the code it measures.
///
/// Written before the ceilings were filled in rather than after, and that order is the lesson of
/// both projects this guard follows. The Python one recorded a depth measure that walked an
/// <c>if</c> body without adding its level, so everything inside an <c>if</c> measured one short -
/// caught by a test like the ones here. The first draft of THIS measure scored four stacked
/// <c>fixed</c> statements as four levels. A ceiling standing on a measure that lies is worse
/// than none, because the suite is green and somebody believes it.
///
/// <b>Every rule has both halves.</b> "A comment does not move the number" is satisfied perfectly
/// by a measure that counts nothing, and "an else-if chain is flat" by one that returns zero for
/// everything. So each is paired with the case that MUST move the number.
/// </summary>
public sealed class CodeShapeMetricTests
{
    [Fact]
    public void Comments_doc_comments_and_blank_lines_are_not_code()
    {
        var bare = Unit("void F() {\n int x = 1;\n x++;\n}", "C.F");
        var padded = Unit("/// <summary>Doc.</summary>\n[Obsolete]\nvoid F() {\n // one\n\n /* two\n three */\n int x = 1;\n#pragma warning disable CA1031\n x++;\n}", "C.F");

        Assert.Equal(bare.CodeLines, padded.CodeLines);
    }

    [Fact]
    public void Seven_more_lines_of_code_move_the_length_by_exactly_seven()
    {
        var bare = Unit("void F() {\n int x = 1;\n}", "C.F");
        var longer = Unit("void F() {\n int x = 1;\n" + string.Concat(Enumerable.Repeat(" x++;\n", 7)) + "}", "C.F");

        Assert.Equal(7, longer.CodeLines - bare.CodeLines);
    }

    [Fact]
    public void A_string_over_several_lines_counts_every_line_it_covers()
    {
        var unit = Unit("void F() {\n var s = \"\"\"\n  a\n  b\n  \"\"\";\n}", "C.F");

        Assert.Equal(6, unit.CodeLines);
    }

    [Fact]
    public void A_local_function_counts_towards_its_method_and_is_a_unit_of_its_own()
    {
        var source = "void F() {\n G();\n void G() {\n  if (true) { }\n }\n}";

        Assert.Equal(6, Unit(source, "C.F").CodeLines);
        Assert.Equal(3, Unit(source, "C.F>G").CodeLines);
    }

    [Fact]
    public void Markup_comments_are_free_and_markup_is_not()
    {
        const string Bare = "<Grid>\n  <Border />\n</Grid>\n";
        const string Padded = "<!-- one\n  two -->\n<Grid>\n\n  <!-- three --> <Border />\n</Grid>\n";
        const string Longer = "<Grid>\n  <Border />\n  <Border />\n</Grid>\n";

        Assert.Equal(3, CodeShape.MarkupLines(Bare));
        Assert.Equal(3, CodeShape.MarkupLines(Padded));
        Assert.Equal(4, CodeShape.MarkupLines(Longer));
    }

    [Fact]
    public void Markup_inside_cdata_is_content_even_when_it_looks_like_a_comment()
    {
        Assert.Equal(3, CodeShape.MarkupLines("<Code><![CDATA[\n<!-- not a comment\n]]></Code>\n"));
    }

    [Fact]
    public void Straight_line_code_forks_nowhere()
    {
        Assert.Equal(1, Unit("void F() { int a = 1; a++; }", "C.F").Complexity);
    }

    [Fact]
    public void Every_decision_forks_once()
    {
        Assert.Equal(2, Complexity("if (a) { } else { }"));
        Assert.Equal(4, Complexity("if (a) { } else if (b) { } else if (c) { } else { }"));
        Assert.Equal(4, Complexity("switch (n) { case 1: break; case 2: break; case 3: break; default: break; }"));
        Assert.Equal(4, Complexity("var x = n switch { 1 => 1, 2 => 2, 3 => 3, _ => 0 };"));
        Assert.Equal(3, Complexity("var x = a && b || c;"));
        Assert.Equal(2, Complexity("var x = s ?? \"\";"));
        Assert.Equal(3, Complexity("try { } catch (IOException e) when (e.HResult == 1) { }"));
        Assert.Equal(5, Complexity("for (;;) { } foreach (var i in l) { } while (a) { } do { } while (a);"));
    }

    [Fact]
    public void Every_other_kind_of_decision_forks_once_too()
    {
        // The seven the test above left out, found by review on 2026-09-23. Each is in the list of
        // forks in ShapeMeasures.cs, and the rule this class lives by is that every entry has a
        // case that must move the number - otherwise deleting it from the list changes nothing here.
        Assert.Equal(2, Complexity("var x = a ? 1 : 2;"));
        Assert.Equal(2, Complexity("s ??= \"\";"));
        Assert.Equal(2, Complexity("var x = n is > 0 and < 5;"));
        Assert.Equal(2, Complexity("var x = n is 1 or 2;"));
        Assert.Equal(4, Complexity("switch (o) { case int i: break; case string t when t.Length > 0: break; }"));
        Assert.Equal(3, Complexity("var x = n switch { > 0 when flag => 1, _ => 0 };"));
        Assert.Equal(2, Complexity("foreach (var (a, b) in pairs) { }"));
    }

    [Fact]
    public void A_null_conditional_is_not_a_fork()
    {
        Assert.Equal(1, Complexity("var x = s?.Length;"));
    }

    [Fact]
    public void A_local_function_forks_on_its_own_and_a_lambda_forks_in_its_method()
    {
        var source = "void F() {\n Action a = () => { if (x) { } };\n void G() { if (y) { } }\n}";

        Assert.Equal(2, Unit(source, "C.F").Complexity);
        Assert.Equal(2, Unit(source, "C.F>G").Complexity);
    }

    [Fact]
    public void An_else_if_chain_is_one_level_however_long_it_is()
    {
        Assert.Equal(1, Depth("if (a) { x(); } else if (b) { x(); } else if (c) { x(); } else { x(); }"));
    }

    [Fact]
    public void Catch_and_finally_stand_at_the_level_of_their_try()
    {
        Assert.Equal(1, Depth("try { x(); } catch { x(); } finally { x(); }"));
        Assert.Equal(2, Depth("try { x(); } catch { foreach (var i in l) { x(); } }"));
    }

    [Fact]
    public void Real_nesting_is_counted_level_by_level()
    {
        Assert.Equal(0, Depth("x(); y();"));
        Assert.Equal(4, Depth("for (;;) { foreach (var a in l) { if (a) { using (d) { x(); } } } }"));
        Assert.Equal(1, Depth("for (;;) { } for (;;) { }"));
        Assert.Equal(2, Depth("if (a) { x(); } else { if (b) { x(); } }"));
    }

    [Fact]
    public void Stacked_using_fixed_and_lock_are_one_level_and_braces_between_them_are_two()
    {
        Assert.Equal(1, Depth("fixed (char* a = s) fixed (char* b = s) fixed (char* c = s) { x(); }"));
        Assert.Equal(1, Depth("using (a) using (b) { x(); }"));
        Assert.Equal(1, Depth("lock (a) lock (b) { x(); }"));
        Assert.Equal(2, Depth("using (a) { using (b) { x(); } }"));
    }

    [Fact]
    public void A_switch_is_two_levels_and_a_block_lambda_is_one()
    {
        Assert.Equal(2, Depth("switch (n) { case 1: x(); break; }"));
        Assert.Equal(2, Depth("Run(() => { if (a) { x(); } });"));
        Assert.Equal(0, Depth("Run(() => x());"));
    }

    [Fact]
    public void A_local_function_is_measured_on_its_own_and_says_where_its_deepest_line_is()
    {
        var source = "void F() {\n x();\n void G() {\n  if (a) {\n   if (b) { x(); }\n  }\n }\n}";

        Assert.Equal(0, Unit(source, "C.F").Depth);
        Assert.Equal(2, Unit(source, "C.F>G").Depth);

        // Line 6 and not 5: the sample is wrapped in `class C {` on a line of its own.
        Assert.Equal(6, Unit(source, "C.F>G").DeepestLine);
    }

    [Fact]
    public void Parameters_are_counted_where_somebody_lines_arguments_up()
    {
        var signatures = Signatures(
            "class C(int a, int b) { void M(int a, int b, int c) { void L(int x) { } } int this[int i, int j] => 0; }\n" +
            "delegate void D(int a, int b, int c, int d);\n" +
            "record R(int a, int b, int c, int d, int e);");

        Assert.Equal(2, signatures["C..ctor"]);
        Assert.Equal(3, signatures["C.M"]);
        Assert.Equal(1, signatures["C.M>L"]);
        Assert.Equal(2, signatures["C.this[]"]);
        Assert.Equal(4, signatures["D"]);
        Assert.False(signatures.ContainsKey("R..ctor"), "a positional record's parameters are its state, not a signature");
    }

    [Fact]
    public void A_class_is_measured_across_every_file_it_is_split_into()
    {
        var tally = new TypeTally();
        tally.Add(Root("namespace N; partial class C { int _a; void M() { } int P { get; set; } }"), "src/One", "one.cs");
        tally.Add(Root("namespace N; partial class C { static int s_b; void M2() { } int Q { get => 1; set { } } }"), "src/One", "two.cs");

        var row = Assert.Single(tally.Rows());

        Assert.Equal("src/One:N.C", row.Key);
        Assert.Equal(2, row.Files.Count);
        Assert.Equal(4, row.Methods);
        Assert.Equal(3, row.State);
    }

    [Fact]
    public void State_is_what_can_differ_between_two_objects_or_change_after_start()
    {
        var tally = new TypeTally();
        tally.Add(
            Root("namespace N; class C(int p) { const int K = 1; static readonly int S = 1; readonly int _r; event Action E; int Auto { get; } int Computed => 1; interface I { int X { get; } } record R(int a, int b); }"),
            "src/One",
            "one.cs");

        var rows = tally.Rows().ToDictionary(row => row.Key, StringComparer.Ordinal);

        Assert.Equal(4, rows["src/One:N.C"].State);
        Assert.Equal(1, rows["src/One:N.C"].Methods);
        Assert.Equal(2, rows["src/One:N.C+R"].State);
        Assert.False(rows.ContainsKey("src/One:N.C+I"), "an interface holds no state and no behaviour");
    }

    [Fact]
    public void The_same_name_in_two_projects_is_two_types()
    {
        var tally = new TypeTally();
        tally.Add(Root("namespace N; class C { void M() { } }"), "tests/A", "a.cs");
        tally.Add(Root("namespace N; class C { void M() { } }"), "tests/B", "b.cs");

        Assert.Equal(2, tally.Rows().Count);
    }

    [Fact]
    public void Top_level_statements_are_a_unit_and_the_types_beside_them_are_not_part_of_it()
    {
        var root = Root("if (args.Length > 0) {\n return 1;\n}\nreturn 0;\n\nenum ExitCode { Ok, Failed }\nclass Helper { void M() { if (a) { if (b) { } } } }");

        var unit = CodeUnit.Of(root, "src/Bws.Cli/Program.cs");

        Assert.Equal("Bws.Cli." + CodeUnit.TopLevel, unit.Name);
        Assert.Equal(4, unit.CodeLines);
        Assert.Equal(2, unit.Complexity);
        Assert.Equal(1, unit.Depth);
    }

    [Fact]
    public void Units_are_named_the_way_a_person_looks_for_them()
    {
        var root = Root("namespace N.M; class C { class D { int P { get { return 1; } } D() { } void F() { void G() { } } } }");
        var names = root.DescendantNodes().Where(CodeUnit.IsUnit).Select(Naming.Of).ToArray();

        Assert.Equal(new[] { "N.M.C+D.P.get", "N.M.C+D..ctor", "N.M.C+D.F", "N.M.C+D.F>G" }, names);
    }

    private static int Complexity(string body) => Unit($"void F() {{ {body} }}", "C.F").Complexity;

    private static int Depth(string body) => Unit($"unsafe void F() {{ {body} }}", "C.F").Depth;

    private static CodeUnit Unit(string members, string name)
    {
        var root = Root($"class C {{\n{members}\n}}");
        var units = root.DescendantNodes().Where(CodeUnit.IsUnit).Select(node => CodeUnit.Of(node, "C.cs")).ToArray();

        return units.Single(unit => unit.Name == name);
    }

    private static Dictionary<string, int> Signatures(string source) =>
        Root(source).DescendantNodes()
            .Select(node => Signature.Of(node, "S.cs"))
            .OfType<Signature>()
            .ToDictionary(signature => signature.Name, signature => signature.Parameters, StringComparer.Ordinal);

    private static CompilationUnitSyntax Root(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, CodeShape.Language);
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        // A sample that does not parse measures the parser's recovery, not the rule it is named after.
        Assert.True(errors.Length == 0, "The sample does not parse: " + string.Join("; ", errors.Select(e => e.ToString())));

        return (CompilationUnitSyntax)tree.GetRoot();
    }
}
