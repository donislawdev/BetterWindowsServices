using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bws.Architecture.Tests;

/// <summary>
/// Finds code in the product that nothing alive reaches any more.
///
/// Added 2026-09-23 on the owner's decision, after the same guard in two of his other projects - a
/// Rust one and a Python one. The method transfers: a test is not a consumer, and life spreads from
/// roots to a fixed point, so two definitions that only name each other are both found. Three of the
/// details were measured here rather than copied:
///
///   - <b>Definitions and mentions come from the syntax tree.</b> The Rust version reads lines, and
///     its comments record four ways that went wrong - a constructor taken for a definition, every
///     public constant skipped, a mention credited to whatever definition happened to start above it.
///     A parser answers each of those exactly, and this project already has one.
///   - <b>A comment is NOT life.</b> The Rust version counts comments, so that prose still naming a
///     symbol keeps it. Measured on this tree the day this was written: counting them hid four of
///     the twenty-six findings, and one of the four, Query.Excludes, was alive only because its own
///     comment described a caller that had gone. A doc-comment reference is not life either.
///   - <b>Only names that can be references count</b> - never the name a declaration introduces.
///
/// <b>WHAT THIS DOES NOT SEE, said before anybody trusts a green run:</b>
///
///   - Names, not symbols. A member called Status survives on any other Status in the tree. That is
///     the safe direction - a miss leaves dead code, it never accuses living code.
///   - A record's positional members. They are data the serialiser reads, which names nothing, and
///     the snapshot and JSON contracts are built from them.
///   - Anything reached by a string - reflection, a binding path assembled in code.
///   - Private members, on purpose: IDE0051 and IDE0052 hold those in the build.
///   - Definitions under tests/, and site/Bws.Site, which is outside src/.
///   - A WPF attached property used only from markup would look dead here, because markup spells it
///     Owner.Name and never GetName or NameProperty. The owner's Rust project paid for that. There is
///     no attached property in the product today, so the case is named rather than handled.
/// </summary>
internal static class DeadCode
{
    /// <summary>A mention from product code outside any definition, or from markup. Life starts here.</summary>
    internal const int Root = -1;

    /// <summary>A mention from a test. Read, recorded, and never counted as life.</summary>
    internal const int Test = -2;

    /// <summary>
    /// Members the language or the framework reaches by contract, so no line in the product names
    /// them. Exact names rather than a pattern, so a future member cannot slip in by being called
    /// something similar. Each one is here because a construct reaches it without spelling it:
    ///
    ///   Convert, ConvertBack   a binding calls them on a converter named in markup.
    ///   GetErrors, HasErrors   INotifyDataErrorInfo, which a class carries whole or not at all.
    ///   Dispose, DisposeAsync  a using statement calls them.
    ///   GetEnumerator          a foreach calls it.
    ///   GetAwaiter             an await calls it.
    ///   Deconstruct            a deconstructing assignment calls it.
    /// </summary>
    internal static readonly HashSet<string> CalledByContract = new(StringComparer.Ordinal)
    {
        "Convert", "ConvertBack", "GetErrors", "HasErrors", "Dispose", "DisposeAsync", "GetEnumerator", "GetAwaiter", "Deconstruct",
    };

    /// <summary>
    /// The pure half: life spreads from the roots to a fixed point, and whatever it never reaches is
    /// returned by index.
    ///
    /// <b>From the roots, not from the leaves, and the direction is the whole point.</b> Crossing out
    /// what has no living mention handles a chain but not a cycle: two methods naming only each other
    /// each see one mention and both survive for ever. Spreading from roots finds the cycle. A test
    /// site is never a root, and a definition naming itself is recursion, not life.
    /// </summary>
    internal static int[] Unreached(IReadOnlyList<string> names, IReadOnlyDictionary<string, List<int>> sites)
    {
        var alive = names.Select(name => SitesOf(sites, name).Contains(Root)).ToArray();
        var grew = true;
        while (grew)
        {
            grew = false;
            for (var index = 0; index < names.Count; index++)
            {
                if (!alive[index] && SitesOf(sites, names[index]).Any(site => site >= 0 && site != index && alive[site]))
                {
                    alive[index] = true;
                    grew = true;
                }
            }
        }

        return [.. Enumerable.Range(0, names.Count).Where(index => !alive[index])];
    }

    /// <summary>The name a node introduces, when it is a definition this scan follows, or nothing.</summary>
    internal static string? NameOf(SyntaxNode node) => node switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
        VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax } field => field.Identifier.ValueText,
        EnumMemberDeclarationSyntax member => member.Identifier.ValueText,
        MemberDeclarationSyntax member when !AnswersSomebodyElse(member) => MemberName(member),
        _ => null,
    };

    /// <summary>How a definition is named in a message and on the list of exceptions: its type, then itself.</summary>
    internal static string KeyOf(SyntaxNode node, string name) => node switch
    {
        BaseTypeDeclarationSyntax type => Naming.TypeOf(type),
        _ when node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault() is { } owner => $"{Naming.TypeOf(owner)}.{name}",
        _ => name,
    };

    private static string? MemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        EventDeclarationSyntax @event => @event.Identifier.ValueText,
        _ => null,
    };

    /// <summary>
    /// A member somebody else calls by its place rather than by its name: an override, an explicit
    /// interface member, a method with no body (abstract, an interface's own, a partial or native
    /// declaration), and the contract names above. Its body still counts - a mention inside it is
    /// credited to the type that holds it.
    /// </summary>
    private static bool AnswersSomebodyElse(MemberDeclarationSyntax member) =>
        member.Modifiers.Any(SyntaxKind.OverrideKeyword)
        || member is BasePropertyDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
        || member is MethodDeclarationSyntax method && Unnamed(method)
        || CalledByContract.Contains(MemberName(member) ?? string.Empty);

    private static bool Unnamed(MethodDeclarationSyntax method) =>
        method.ExplicitInterfaceSpecifier is not null || method is { Body: null, ExpressionBody: null };

    private static IEnumerable<int> SitesOf(IReadOnlyDictionary<string, List<int>> sites, string name) =>
        sites.TryGetValue(name, out var found) ? found : [];
}

/// <summary>One definition the scan follows.</summary>
internal sealed record Definition(string Key, string Name, string File, int Line, SyntaxNode Node)
{
    internal string Where => $"{Key} ({File}:{Line})";
}

/// <summary>
/// One reading of the product: every definition, and every place each name is mentioned from.
/// Built from syntax roots and markup text rather than from paths, so the tests of the scan itself
/// can hand it three lines of source instead of the tree.
/// </summary>
internal sealed class DeadCodeScan
{
    private static readonly Regex Word = new("[A-Za-z_][A-Za-z0-9_]*", RegexOptions.None, Sources.Ceiling);

    private readonly List<Definition> _definitions = [];
    private readonly Dictionary<SyntaxNode, int> _index = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<int>> _sites = new(StringComparer.Ordinal);

    private DeadCodeScan()
    {
    }

    internal IReadOnlyList<Definition> Definitions => _definitions;

    internal int FilesRead { get; private set; }

    internal static DeadCodeScan Of(
        IEnumerable<(string File, SyntaxNode Root)> product, IEnumerable<SyntaxNode> tests, IEnumerable<string> markup)
    {
        var scan = new DeadCodeScan();
        var shipped = product.ToList();
        shipped.ForEach(file => scan.Define(file.File, file.Root));
        shipped.ForEach(file => scan.Mention(file.Root, fromTests: false));
        foreach (var root in tests)
        {
            scan.Mention(root, fromTests: true);
        }

        foreach (var text in markup)
        {
            scan.MentionWords(text);
        }

        return scan;
    }

    /// <summary>
    /// Every definition nothing alive reaches, once per name and place - the outermost only, so a dead
    /// type is one finding rather than one per member.
    /// </summary>
    internal IReadOnlyList<Definition> Unreached()
    {
        var dead = DeadCode.Unreached([.. _definitions.Select(definition => definition.Name)], _sites).ToHashSet();

        return [.. _definitions
            .Where((definition, index) => dead.Contains(index) && !InsideOneOf(definition, dead))
            .DistinctBy(definition => definition.Key)];
    }

    /// <summary>Whether a test names this definition - for the message, which is worded differently.</summary>
    internal bool NamedByTests(Definition definition) =>
        _sites.TryGetValue(definition.Name, out var sites) && sites.Contains(DeadCode.Test);

    private void Define(string file, SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes())
        {
            if (DeadCode.NameOf(node) is { } name)
            {
                _index[node] = _definitions.Count;
                _names.Add(name);
                _definitions.Add(new Definition(DeadCode.KeyOf(node, name), name, file, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, node));
            }
        }
    }

    /// <summary>
    /// Every name the code can use as a reference - a simple name, never the one a declaration
    /// introduces, and never inside a comment or a doc-comment reference, which are trivia.
    /// </summary>
    private void Mention(SyntaxNode root, bool fromTests)
    {
        FilesRead++;
        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            var site = fromTests ? DeadCode.Test : SiteOf(name);
            Add(name.Identifier.ValueText, site);

            // [Obsolete] names ObsoleteAttribute. The language drops the suffix, so this puts it back.
            if (name.FirstAncestorOrSelf<AttributeSyntax>() is { } attribute && attribute.Name.Span.Contains(name.Span))
            {
                Add(name.Identifier.ValueText + "Attribute", site);
            }
        }
    }

    /// <summary>Markup names code by bindings, handlers and types, and every word of it counts as a root.</summary>
    private void MentionWords(string text)
    {
        FilesRead++;
        foreach (Match word in Word.Matches(text))
        {
            Add(word.Value, DeadCode.Root);
        }
    }

    /// <summary>The definition a mention stands inside, or a root when it stands inside none.</summary>
    private int SiteOf(SyntaxNode mention) =>
        mention.Ancestors().Select(ancestor => _index.TryGetValue(ancestor, out var index) ? index : DeadCode.Root)
            .FirstOrDefault(index => index != DeadCode.Root, DeadCode.Root);

    private bool InsideOneOf(Definition definition, HashSet<int> dead) =>
        definition.Node.Ancestors().Any(ancestor => _index.TryGetValue(ancestor, out var index) && dead.Contains(index));

    private void Add(string name, int site)
    {
        if (!_names.Contains(name))
        {
            return;
        }

        if (!_sites.TryGetValue(name, out var sites))
        {
            _sites[name] = sites = [];
        }

        sites.Add(site);
    }
}
