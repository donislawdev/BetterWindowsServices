using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bws.Architecture.Tests;

/// <summary>
/// Counts, file by file, every catch that drops what it caught - neither throwing nor reading the
/// exception - and holds the count exactly.
///
/// Added 2026-09-23 on the owner's decision, after an inventory of the same shape in his Python
/// project. <see cref="BroadCatchGuards"/> already says WHERE a catch-all is allowed, but it finds one
/// by the analyser suppression it needs, and a narrow catch needs none. Measured that day: 50 catch
/// clauses under src/, and 22 of them drop what they caught - every one invisible to every guard in
/// this project, so the twenty-third would arrive in silence.
///
/// <b>Nothing here says any of the 22 is wrong.</b> Most carry a comment arguing for themselves, and a
/// catch that turns a failure into a state the person sees is exactly what rule 8 asks for - this
/// cannot tell that from a catch that hides one, and does not try. It says the next one is a DECISION:
/// either the failure still reaches the person, argued beside the catch and the number here raised on
/// purpose - which is the owner's call, like any ceiling - or it goes where a person can see it.
///
/// <b>A filter that reads the exception does not count as reading it.</b> <c>when (e is IOException)</c>
/// narrows what is caught and says nothing about what happens to it.
/// </summary>
public sealed class SilentCatchGuards
{
    /// <summary>The floor under the catches read - fifty on the day. It catches a scan that read nothing.</summary>
    private const int FewestCatchesRead = 30;

    /// <summary>
    /// Catches that drop what they caught, per file, measured 2026-09-23. Exact: fewer means lower it in
    /// the same change, and a file absent from this list must have none.
    /// </summary>
    private static readonly Dictionary<string, int> Recorded = new(StringComparer.Ordinal)
    {
        ["src/Bws.Cli/Execution.cs"] = 1,
        ["src/Bws.Cli/SnapshotFiles.cs"] = 2,
        ["src/Bws.Core/BinaryPathResolver.cs"] = 1,
        ["src/Bws.Core/Querying/QueryPatterns.cs"] = 1,
        ["src/Bws.Core/Querying/QueryValues.cs"] = 1,
        ["src/Bws.Core/Snapshots/AtomicFile.cs"] = 2,
        ["src/Bws.Core/WindowsBinaryInspector.Publisher.cs"] = 1,
        ["src/Bws.Gui/Elevation.cs"] = 1,
        ["src/Bws.Gui/ListColumns.cs"] = 3,
        ["src/Bws.Gui/PreferencesFile.cs"] = 2,
        ["src/Bws.Gui/Texts.cs"] = 4,
        ["src/Bws.Gui/ViewModels/Catalogue.Samples.cs"] = 3,
    };

    [Fact]
    public void Every_catch_that_drops_what_it_caught_is_counted_file_by_file()
    {
        var clauses = Sources.Shipped()
            .SelectMany(path => CodeShape.TreeOf(path).GetRoot().DescendantNodes().OfType<CatchClauseSyntax>()
                .Select(clause => (File: CodeShape.NameOf(path), Drops: Catches.Drop(clause))))
            .ToArray();
        var counted = clauses.Where(clause => clause.Drops).GroupBy(clause => clause.File)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var wrong = counted.Keys.Union(Recorded.Keys, StringComparer.Ordinal)
            .Where(file => counted.GetValueOrDefault(file) != Recorded.GetValueOrDefault(file))
            .Select(file => $"  {file}: {counted.GetValueOrDefault(file)} in the code, {Recorded.GetValueOrDefault(file)} recorded")
            .ToArray();

        Assert.True(clauses.Length >= FewestCatchesRead, $"The scan read {clauses.Length} catch clauses - it is reading the wrong place.");
        Assert.True(
            wrong.Length == 0,
            "The count of catches that drop what they caught has moved. Fewer: lower the number here in the " +
            "same change. More: a catch neither throws nor reads its exception, which is the shape rule 8 calls " +
            "silence. Either the failure still reaches the person - argue it beside the catch and raise the " +
            "number here on purpose - or send it somewhere a person sees it:" +
            Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    [Theory]
    [InlineData("try { } catch (System.IO.IOException) { }", true)]
    [InlineData("try { } catch (System.IO.IOException) { return; }", true)]
    [InlineData("try { } catch (System.Exception e) when (e is System.IO.IOException) { return; }", true)]
    [InlineData("try { } catch (System.IO.IOException e) { Report(e); }", false)]
    [InlineData("try { } catch (System.IO.IOException) { throw; }", false)]
    [InlineData("try { } catch { throw new System.InvalidOperationException(); }", false)]
    public void A_catch_drops_what_it_caught_only_when_it_neither_throws_nor_reads_it(string code, bool drops)
    {
        var clause = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {code} }} }}", CodeShape.Language)
            .GetRoot().DescendantNodes().OfType<CatchClauseSyntax>().Single();

        Assert.Equal(drops, Catches.Drop(clause));
    }
}

/// <summary>What a catch clause does with what it caught, read from the syntax tree.</summary>
internal static class Catches
{
    /// <summary>Whether the clause neither throws nor reads the exception in its body.</summary>
    internal static bool Drop(CatchClauseSyntax clause) => !Throws(clause.Block) && !Reads(clause);

    private static bool Throws(BlockSyntax body) =>
        body.DescendantNodes().Any(node => node is ThrowStatementSyntax or ThrowExpressionSyntax);

    private static bool Reads(CatchClauseSyntax clause) =>
        clause.Declaration?.Identifier.ValueText is { Length: > 0 } caught
        && clause.Block.DescendantNodes().OfType<IdentifierNameSyntax>().Any(name => name.Identifier.ValueText == caught);
}
