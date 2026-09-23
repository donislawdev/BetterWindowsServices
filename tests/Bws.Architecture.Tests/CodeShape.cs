using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bws.Architecture.Tests;

/// <summary>
/// Reads the shape of the code - how long, how branching, how deep, how wide, how big a class -
/// from the C# syntax tree, once, for every guard that asks.
///
/// Added 2026-09-23 on the owner's decision, on the model of two of his other projects: a Python
/// suite that ratchets logic lines, branching, indent depth and class size, and a Rust one that
/// guards the lints around clippy's numbers. The method transfers, none of the numbers do - they
/// live in <see cref="ShapeCeilings"/> and come from measuring this tree.
///
/// <b>WHY A PARSER AND NOT LINE COUNTS.</b> Sixty-three per cent of the lines under src/ are
/// comments and blank lines - measured the day this was written, 37 254 lines against 13 886 of
/// code. That is deliberate, it is where the reasons live, and a ceiling on raw lines is a ceiling
/// on explaining: the size ratchet recorded at least three sessions shortening a fresh comment to
/// fit under a number. So only lines that carry a token count, and a comment is never a token.
///
/// <b>WHY ONE MEASURE WRITTEN ONCE, AND TESTED ON ITSELF.</b> The first draft of the depth measure
/// scored four stacked <c>fixed (...)</c> statements as four levels when the screen shows one, and
/// the Python suite this is modelled on recorded the same class of mistake with <c>elif</c>. A
/// ceiling standing on a measure that lies is worse than no ceiling, because it looks guarded -
/// which is why <see cref="CodeShapeMetricTests"/> exists and was written before the numbers.
///
/// <b>What this does NOT see, said before anybody trusts it:</b> whether a method does one thing,
/// whether its name is honest, nesting inside EXPRESSIONS (switch expressions, initialisers, LINQ
/// chains), and code behind an inactive <c>#if</c>, which the parser reads as a comment. There is
/// no <c>#if</c> under src/ today.
/// </summary>
internal static class CodeShape
{
    /// <summary>
    /// The language the build compiles. Latest rather than Preview on purpose: a file using syntax
    /// this package does not know parses with errors, and the canary guard turns that into a red
    /// test naming the file instead of a measure that quietly counts it wrong.
    /// </summary>
    internal static CSharpParseOptions Language { get; } = new(LanguageVersion.Latest);

    private static readonly Lazy<ShapeReport> ShippedReport = new(() => ShapeReport.Of(Sources.Shipped()));
    private static readonly Lazy<ShapeReport> TestingReport = new(() => ShapeReport.Of(Sources.Testing()));

    /// <summary>Everything under src/, read once for every guard in the run.</summary>
    internal static ShapeReport Shipped => ShippedReport.Value;

    /// <summary>Everything under tests/, held to its own numbers like the file ratchet always was.</summary>
    internal static ShapeReport Testing => TestingReport.Value;

    private static readonly Lazy<IReadOnlyList<ShapeFile>> MarkupFiles = new(() =>
        [.. Sources.ShippedMarkup()
            .Order(StringComparer.Ordinal)
            .Select(path => new ShapeFile(NameOf(path), MarkupLines(File.ReadAllText(path)), [], []))]);

    /// <summary>Every XAML file under src/, with its lines of markup.</summary>
    internal static IReadOnlyList<ShapeFile> Markup => MarkupFiles.Value;

    /// <summary>A path the way every message here prints it: from the repository root, forward slashes.</summary>
    internal static string NameOf(string path) => Path.GetRelativePath(SourceTree.Root(), path).Replace('\\', '/');

    /// <summary>Lines of code in C# source: any line a token starts on, ends on or spans.</summary>
    internal static int CodeLines(SyntaxNode root) => LinesOf(root.DescendantTokens()).Count;

    /// <summary>The line numbers that carry at least one token, zero-based.</summary>
    internal static HashSet<int> LinesOf(IEnumerable<SyntaxToken> tokens)
    {
        var lines = new HashSet<int>();
        foreach (var token in tokens.Where(token => token.Span.Length > 0))
        {
            var span = token.GetLocation().GetLineSpan();
            for (var line = span.StartLinePosition.Line; line <= span.EndLinePosition.Line; line++)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    /// <summary>
    /// Lines of code in XAML: any line with something on it outside a comment.
    ///
    /// A character scan is enough, and the reason is XML's own grammar rather than luck: a
    /// <c>&lt;</c> cannot stand inside an attribute value, so outside a CDATA section a
    /// <c>&lt;!--</c> is always the start of a comment. CDATA content counts as code.
    /// </summary>
    internal static int MarkupLines(string text)
    {
        var lines = new HashSet<int>();
        var line = 0;
        var closer = string.Empty;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                continue;
            }

            var step = MarkupStep(text, index, ref closer);
            if (step.Counts)
            {
                lines.Add(line);
            }

            index += step.Skip;
        }

        return lines.Count;
    }

    /// <summary>
    /// One step of the markup scan. <paramref name="closer"/> is what ends the section the scan
    /// is inside: empty for plain markup, <c>--&gt;</c> inside a comment, <c>]]&gt;</c> inside CDATA.
    /// </summary>
    private static (bool Counts, int Skip) MarkupStep(string text, int index, ref string closer)
    {
        var inComment = closer == CommentEnd;
        if (closer.Length > 0 && At(text, index, closer))
        {
            closer = string.Empty;
            return (!inComment, (inComment ? CommentEnd : CdataEnd).Length - 1);
        }

        if (closer.Length == 0 && (At(text, index, CommentStart) || At(text, index, CdataStart)))
        {
            closer = At(text, index, CommentStart) ? CommentEnd : CdataEnd;
            return (closer == CdataEnd, (closer == CommentEnd ? CommentStart : CdataStart).Length - 1);
        }

        return (!inComment && !char.IsWhiteSpace(text[index]), 0);
    }

    private const string CommentStart = "<!--";
    private const string CommentEnd = "-->";
    private const string CdataStart = "<![CDATA[";
    private const string CdataEnd = "]]>";

    private static bool At(string text, int index, string word) =>
        string.CompareOrdinal(text, index, word, 0, word.Length) == 0;
}

/// <summary>What one reading of a set of files found, file by file, unit by unit, type by type.</summary>
internal sealed class ShapeReport
{
    private ShapeReport(
        IReadOnlyList<ShapeFile> files,
        IReadOnlyList<CodeUnit> units,
        IReadOnlyList<Signature> signatures,
        IReadOnlyList<CodeType> types)
    {
        Files = files;
        Units = units;
        Signatures = signatures;
        Types = types;
    }

    internal IReadOnlyList<ShapeFile> Files { get; }

    internal IReadOnlyList<CodeUnit> Units { get; }

    internal IReadOnlyList<Signature> Signatures { get; }

    internal IReadOnlyList<CodeType> Types { get; }

    internal static ShapeReport Of(IEnumerable<string> paths)
    {
        var files = new List<ShapeFile>();
        var units = new List<CodeUnit>();
        var signatures = new List<Signature>();
        var types = new TypeTally();

        foreach (var path in paths.OrderBy(path => path, StringComparer.Ordinal))
        {
            var name = CodeShape.NameOf(path);
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), CodeShape.Language, path);
            var root = tree.GetRoot();
            var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

            files.Add(new ShapeFile(name, CodeShape.CodeLines(root), [.. errors.Select(e => e.ToString())], [.. Escape.In(root, name)]));
            units.AddRange(root.DescendantNodes().Where(CodeUnit.IsUnit).Select(node => CodeUnit.Of(node, name)));
            if (root is CompilationUnitSyntax compilation && CodeUnit.HasTopLevel(compilation))
            {
                units.Add(CodeUnit.Of(compilation, name));
            }
            signatures.AddRange(root.DescendantNodes().Select(node => Signature.Of(node, name)).OfType<Signature>());
            types.Add(root, Project(name), name);
        }

        return new ShapeReport(files, units, signatures, types.Rows());
    }

    /// <summary>
    /// The project a file belongs to - the first two segments, src/Bws.Core or tests/Bws.Gui.Tests.
    /// Part of a type's identity because a partial type cannot cross an assembly, and two test
    /// projects may each carry a type of the same full name.
    /// </summary>
    private static string Project(string name) => string.Join('/', name.Split('/').Take(2));
}

/// <summary>One file: its lines of code, whatever the parser could not read, and its ways around the compiler.</summary>
internal sealed record ShapeFile(string Name, int CodeLines, IReadOnlyList<string> SyntaxErrors, IReadOnlyList<Escape> Escapes);

/// <summary>
/// One place where the code steps around what the compiler would otherwise say: a warning disabled
/// by <c>#pragma</c>, a <c>[SuppressMessage]</c>, or <c>#nullable disable</c>.
///
/// <b>Read from the syntax tree, never from the text, and the difference was measured on the day.</b>
/// A text search for <c>#pragma warning disable</c> over this tree counted three that are not there:
/// one in a guard's regular expression, and two inside the sample strings the shape measure is tested
/// on. A directive is trivia the parser knows is a directive - a string that spells one is a string.
/// </summary>
internal sealed record Escape(string Rule, string File, int Line)
{
    internal string Where => $"{File}:{Line}";

    internal static IEnumerable<Escape> In(SyntaxNode root, string file)
    {
        foreach (var trivia in root.DescendantTrivia())
        {
            var line = trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            foreach (var rule in RulesOf(trivia.GetStructure()))
            {
                yield return new Escape(rule, file, line);
            }
        }

        foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>().Where(Suppresses))
        {
            var rule = attribute.ArgumentList?.Arguments.Skip(1).FirstOrDefault()?.ToString().Trim('"').Split(':')[0] ?? "(no rule)";
            yield return new Escape($"[SuppressMessage] {rule}", file, attribute.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
        }
    }

    private static IEnumerable<string> RulesOf(SyntaxNode? directive) => directive switch
    {
        PragmaWarningDirectiveTriviaSyntax { DisableOrRestoreKeyword.RawKind: (int)SyntaxKind.DisableKeyword } pragma =>
            pragma.ErrorCodes.Count == 0
                ? ["#pragma warning disable (every warning)"]
                : pragma.ErrorCodes.Select(code => $"#pragma warning disable {code}"),
        NullableDirectiveTriviaSyntax { SettingToken.RawKind: (int)SyntaxKind.DisableKeyword } => ["#nullable disable"],
        _ => [],
    };

    private static bool Suppresses(AttributeSyntax attribute) =>
        attribute.Name.ToString().Split('.')[^1] is "SuppressMessage" or "SuppressMessageAttribute"
            or "UnconditionalSuppressMessage" or "UnconditionalSuppressMessageAttribute";
}

/// <summary>
/// One piece of code a reader reads as a whole: a method, constructor, destructor, operator,
/// conversion, accessor with a body, expression-bodied property or indexer, or local function.
/// A lambda belongs to the unit it stands in.
/// </summary>
internal sealed record CodeUnit(string Name, string File, int Line, int CodeLines, int Complexity, int Depth, int DeepestLine)
{
    /// <summary>
    /// The name of a file's top-level statements, which are a unit like any method.
    ///
    /// They were invisible to the first draft of this reader, and the file that proved it matters
    /// is the command line's entry point: Program.cs is written as top-level statements, so the
    /// whole of what `bws` does before handing off would have been outside every method ceiling.
    /// </summary>
    internal const string TopLevel = "<top-level statements>";

    internal string Where => $"{File}:{Line} {Name}";

    /// <summary>A file's top-level statements, when it has any.</summary>
    internal static bool HasTopLevel(CompilationUnitSyntax root) => root.Members.Any(member => member is GlobalStatementSyntax);

    internal static bool IsUnitOrTopLevel(SyntaxNode node) => node is CompilationUnitSyntax || IsUnit(node);

    /// <summary>
    /// Whether a walk measuring <paramref name="unit"/> may go down into <paramref name="node"/>.
    /// Not into a local function, which is measured on its own - and, for top-level statements, not
    /// into the types and namespaces that share the file with them.
    /// </summary>
    internal static bool Inside(SyntaxNode unit, SyntaxNode node) =>
        node == unit || node is not (LocalFunctionStatementSyntax or BaseTypeDeclarationSyntax or BaseNamespaceDeclarationSyntax);

    internal static bool IsUnit(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax => true,
        LocalFunctionStatementSyntax => true,
        AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null,
        BasePropertyDeclarationSyntax property => ArrowOf(property) is not null,
        _ => false,
    };

    internal static CodeUnit Of(SyntaxNode unit, string file)
    {
        var (depth, deepest) = Nesting.Of(unit);

        // Top-level statements have no namespace to be named by, so the project stands in for one
        // - Bws.Cli.<top-level statements> - and stays unique the day a second project has some.
        var name = unit is CompilationUnitSyntax ? $"{file.Split('/').Skip(1).FirstOrDefault() ?? file}.{TopLevel}" : Naming.Of(unit);

        return new CodeUnit(
            name,
            file,
            unit.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            LengthOf(unit),
            Branching.Of(unit),
            depth,
            deepest);
    }

    /// <summary>
    /// Lines of code in the unit, without the attributes above it. A local function counts here
    /// AND as a unit of its own - a reader reads it in place, which is what the Python suite this
    /// follows does with a nested def.
    /// </summary>
    internal static int LengthOf(SyntaxNode unit)
    {
        if (unit is CompilationUnitSyntax root)
        {
            return CodeShape.LinesOf(root.Members.OfType<GlobalStatementSyntax>().SelectMany(global => global.DescendantTokens())).Count;
        }

        var attributes = AttributesOf(unit).Select(list => list.FullSpan).ToArray();

        return CodeShape
            .LinesOf(unit.DescendantTokens().Where(token => !attributes.Any(span => span.Contains(token.Span))))
            .Count;
    }

    private static SyntaxList<AttributeListSyntax> AttributesOf(SyntaxNode unit) => unit switch
    {
        MemberDeclarationSyntax member => member.AttributeLists,
        LocalFunctionStatementSyntax local => local.AttributeLists,
        AccessorDeclarationSyntax accessor => accessor.AttributeLists,
        _ => default,
    };

    private static ArrowExpressionClauseSyntax? ArrowOf(BasePropertyDeclarationSyntax property) => property switch
    {
        PropertyDeclarationSyntax plain => plain.ExpressionBody,
        IndexerDeclarationSyntax indexer => indexer.ExpressionBody,
        _ => null,
    };
}

/// <summary>
/// One parameter list somebody has to line arguments up against: a method, constructor, operator,
/// local function, indexer, delegate, or the primary constructor of a class or struct.
///
/// <b>A record's positional parameters are NOT here.</b> Nobody writes that constructor - they are
/// the record's data, and they count towards its state in <see cref="CodeType"/> instead. The
/// Python suite makes the same cut: a dataclass has no written <c>__init__</c> for its rule to see.
/// </summary>
internal sealed record Signature(string Name, string File, int Line, int Parameters)
{
    internal string Where => $"{File}:{Line} {Name}";

    internal static Signature? Of(SyntaxNode node, string file)
    {
        BaseParameterListSyntax? list = node switch
        {
            BaseMethodDeclarationSyntax method => method.ParameterList,
            LocalFunctionStatementSyntax local => local.ParameterList,
            IndexerDeclarationSyntax indexer => indexer.ParameterList,
            DelegateDeclarationSyntax @delegate => @delegate.ParameterList,
            TypeDeclarationSyntax type when type is not RecordDeclarationSyntax => type.ParameterList,
            _ => null,
        };

        return list is null
            ? null
            : new Signature(Naming.Of(node), file, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, list.Parameters.Count);
    }
}

/// <summary>
/// One type, with every partial part summed: the unit that holds state, and the thing splitting a
/// FILE cannot shrink. Measured the day this was written: MainWindow in eleven files, fifty-nine
/// methods, every file comfortably under the file ceiling.
/// </summary>
internal sealed record CodeType(string Key, IReadOnlyList<string> Files, int Methods, int State)
{
    internal string Where => $"{Key} ({Files.Count} file(s))";
}
