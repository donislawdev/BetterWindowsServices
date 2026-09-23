using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bws.Architecture.Tests;

// The four measures CodeShape.cs stands on - what a type holds, where a path forks, how deep a
// block is indented, and what a unit is called. Split out of that file on the day both were
// written, when the file ratchet put the reader third among the longest test files in the tree
// with eight types in it. What each one counts is written above it.

/// <summary>
/// Adds up the partial parts of every type. Interfaces are left out: they hold no state, and a
/// member without a body carries no behaviour for a reader to hold.
/// </summary>
internal sealed class TypeTally
{
    private readonly Dictionary<string, (HashSet<string> Files, int Methods, int State)> _rows = new(StringComparer.Ordinal);

    internal void Add(SyntaxNode root, string project, string file)
    {
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(type => type is not InterfaceDeclarationSyntax))
        {
            var key = $"{project}:{Naming.TypeOf(type)}";
            var row = _rows.TryGetValue(key, out var found) ? found : ([], 0, 0);
            row.Files.Add(file);
            _rows[key] = (row.Files, row.Methods + MethodsOf(type), row.State + StateOf(type));
        }
    }

    internal IReadOnlyList<CodeType> Rows() =>
        [.. _rows.Select(row => new CodeType(row.Key, [.. row.Value.Files.Order(StringComparer.Ordinal)], row.Value.Methods, row.Value.State))];

    /// <summary>
    /// Members with code in them, declared in this type itself: every unit whose nearest type is
    /// this one, local functions excepted because they belong to their method.
    /// </summary>
    internal static int MethodsOf(TypeDeclarationSyntax type) =>
        type.DescendantNodes(node => node == type || node is not BaseTypeDeclarationSyntax)
            .Where(CodeUnit.IsUnit)
            .Count(node => node is not LocalFunctionStatementSyntax && HasCode(node));

    /// <summary>
    /// What an object of this type carries: instance fields, static fields that can change, instance
    /// auto-properties and properties over the <c>field</c> keyword, instance field-like events,
    /// and the parameters of a primary constructor or a positional record.
    ///
    /// An upper bound, knowingly: a primary constructor parameter used only in an initialiser never
    /// becomes a field, and this counts it anyway.
    /// </summary>
    internal static int StateOf(TypeDeclarationSyntax type) =>
        type.Members.Sum(StateOf) + (type.ParameterList?.Parameters.Count ?? 0);

    private static int StateOf(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field when Changes(field.Modifiers) => field.Declaration.Variables.Count,
        EventFieldDeclarationSyntax @event when !Static(@event.Modifiers) => @event.Declaration.Variables.Count,
        PropertyDeclarationSyntax property when !Static(property.Modifiers) && Stores(property) => 1,
        _ => 0,
    };

    private static bool Changes(SyntaxTokenList modifiers) =>
        !modifiers.Any(SyntaxKind.ConstKeyword)
        && (!Static(modifiers) || !modifiers.Any(SyntaxKind.ReadOnlyKeyword));

    private static bool Static(SyntaxTokenList modifiers) => modifiers.Any(SyntaxKind.StaticKeyword);

    private static bool Stores(PropertyDeclarationSyntax property) =>
        property.AccessorList is { } accessors
        && (accessors.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null)
            || property.DescendantNodes().OfType<FieldExpressionSyntax>().Any());

    private static bool HasCode(SyntaxNode unit) => unit switch
    {
        BaseMethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
        _ => true,
    };
}

/// <summary>
/// McCabe's count: one, plus one for every point where the path through the unit can fork.
///
/// <b>What forks:</b> <c>if</c>, <c>?:</c>, the four loops, every <c>case</c> label, every switch
/// expression arm other than <c>_</c>, <c>catch</c>, a <c>when</c> filter, <c>&amp;&amp;</c>,
/// <c>||</c>, <c>??</c>, <c>??=</c> and the pattern combinators <c>and</c> and <c>or</c>.
///
/// <b><c>?.</c> does not</b>, and that is a choice rather than an oversight: it is shorthand for a
/// null check a reader does not follow as a branch. A local function is measured on its own and
/// adds nothing to the method around it. A lambda adds to the unit it stands in.
/// </summary>
internal static class Branching
{
    private static readonly HashSet<SyntaxKind> Forks =
    [
        SyntaxKind.IfStatement, SyntaxKind.ConditionalExpression,
        SyntaxKind.ForStatement, SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement,
        SyntaxKind.WhileStatement, SyntaxKind.DoStatement,
        SyntaxKind.CaseSwitchLabel, SyntaxKind.CasePatternSwitchLabel,
        SyntaxKind.CatchClause, SyntaxKind.CatchFilterClause, SyntaxKind.WhenClause,
        SyntaxKind.LogicalAndExpression, SyntaxKind.LogicalOrExpression,
        SyntaxKind.CoalesceExpression, SyntaxKind.CoalesceAssignmentExpression,
        SyntaxKind.AndPattern, SyntaxKind.OrPattern,
    ];

    internal static int Of(SyntaxNode unit) =>
        1 + unit.DescendantNodes(node => CodeUnit.Inside(unit, node)).Count(IsFork);

    private static bool IsFork(SyntaxNode node) =>
        Forks.Contains(node.Kind()) || node is SwitchExpressionArmSyntax { Pattern: not DiscardPatternSyntax };
}

/// <summary>
/// How deep the deepest block in a unit is indented ON SCREEN - which is not how deep it sits in
/// the syntax tree, and every rule below is a place where the two disagree:
///
///   - an <c>else if</c> chain is one level, however long, and so is its <c>else</c>,
///   - <c>catch</c> and <c>finally</c> stand at the level of their <c>try</c>,
///   - a <c>switch</c> is two levels, the statement and its section,
///   - stacked <c>using</c>, <c>fixed</c> and <c>lock</c> without braces between them are one,
///   - a lambda with a block body is a level, one with an expression body is not,
///   - a local function is measured on its own and adds nothing to its method.
///
/// Returns the depth and the line it is reached on, so a red test can point at it.
/// </summary>
internal static class Nesting
{
    private static readonly HashSet<SyntaxKind> Openers =
    [
        SyntaxKind.ForStatement, SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement,
        SyntaxKind.WhileStatement, SyntaxKind.DoStatement, SyntaxKind.TryStatement,
        SyntaxKind.UsingStatement, SyntaxKind.FixedStatement, SyntaxKind.LockStatement,
        SyntaxKind.CheckedStatement, SyntaxKind.UncheckedStatement, SyntaxKind.UnsafeStatement,
        SyntaxKind.SwitchStatement, SyntaxKind.SwitchSection,
    ];

    private static readonly HashSet<SyntaxKind> Stacks =
        [SyntaxKind.UsingStatement, SyntaxKind.FixedStatement, SyntaxKind.LockStatement];

    internal static (int Depth, int Line) Of(SyntaxNode unit) => Walk(unit, (0, LineOf(unit)));

    private static (int Depth, int Line) Walk(SyntaxNode node, (int Depth, int Line) at)
    {
        var deepest = at;
        foreach (var child in node.ChildNodes())
        {
            deepest = Deeper(deepest, Step(child, at.Depth));
        }

        return deepest;
    }

    private static (int Depth, int Line) Step(SyntaxNode child, int depth)
    {
        if (child is LocalFunctionStatementSyntax or BaseTypeDeclarationSyntax or BaseNamespaceDeclarationSyntax)
        {
            return (depth, 0);
        }

        if (child is IfStatementSyntax chain)
        {
            return Chain(chain, depth + 1);
        }

        return Opens(child)
            ? Walk(child, (Stacked(child) ? depth : depth + 1, LineOf(child)))
            : Walk(child, (depth, 0));
    }

    private static bool Opens(SyntaxNode node) =>
        Openers.Contains(node.Kind())
        || node is BlockSyntax { Parent: BlockSyntax }
        || node is AnonymousFunctionExpressionSyntax { Block: not null };

    private static bool Stacked(SyntaxNode node) =>
        Stacks.Contains(node.Kind()) && node.Parent is { } parent && Stacks.Contains(parent.Kind());

    /// <summary>An if, its else-ifs and its else: every body at the same depth.</summary>
    private static (int Depth, int Line) Chain(IfStatementSyntax first, int depth)
    {
        var deepest = (depth, LineOf(first));
        for (IfStatementSyntax? link = first; link is not null; link = link.Else?.Statement as IfStatementSyntax)
        {
            deepest = Deeper(deepest, Walk(link.Condition, (depth - 1, 0)));
            deepest = Deeper(deepest, Body(link.Statement, depth));
            if (link.Else is { Statement: not IfStatementSyntax } last)
            {
                deepest = Deeper(deepest, Body(last.Statement, depth));
            }
        }

        return deepest;
    }

    private static (int Depth, int Line) Body(StatementSyntax statement, int depth) =>
        statement is BlockSyntax ? Walk(statement, (depth, 0)) : Step(statement, depth);

    private static (int Depth, int Line) Deeper((int Depth, int Line) held, (int Depth, int Line) found) =>
        found.Depth > held.Depth ? found : held;

    private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}

/// <summary>
/// Names a unit, a signature and a type the way a person looks for them: namespace, containing
/// types joined by <c>+</c>, then the member. Accessors end in <c>.get</c> or <c>.set</c>, a local
/// function in <c>&gt;name</c>, a constructor in <c>.ctor</c>.
/// </summary>
internal static class Naming
{
    internal static string Of(SyntaxNode node)
    {
        var owner = node as BaseTypeDeclarationSyntax ?? node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        var prefix = owner is null ? string.Empty : TypeOf(owner) + ".";

        return prefix + MemberOf(node);
    }

    internal static string TypeOf(BaseTypeDeclarationSyntax type)
    {
        var chain = type.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().Reverse().Select(Arity);
        var space = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();

        return (space is null ? string.Empty : space + ".") + string.Join('+', chain);
    }

    private static string Arity(BaseTypeDeclarationSyntax type) =>
        type is TypeDeclarationSyntax { TypeParameterList: { } generic }
            ? $"{type.Identifier.Text}`{generic.Parameters.Count}"
            : type.Identifier.Text;

    private static string MemberOf(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Identifier.Text,
        ConstructorDeclarationSyntax => ".ctor",
        DestructorDeclarationSyntax => "~",
        OperatorDeclarationSyntax op => "operator " + op.OperatorToken.Text,
        ConversionOperatorDeclarationSyntax conversion => "operator " + conversion.Type,
        LocalFunctionStatementSyntax local => MemberOf(local.Ancestors().First(CodeUnit.IsUnitOrTopLevel)) + ">" + local.Identifier.Text,
        CompilationUnitSyntax => CodeUnit.TopLevel,
        AccessorDeclarationSyntax accessor => PropertyOf(accessor) + "." + accessor.Keyword.Text,
        PropertyDeclarationSyntax property => property.Identifier.Text,
        IndexerDeclarationSyntax => "this[]",
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.Text,
        TypeDeclarationSyntax => ".ctor",
        _ => node.Kind().ToString(),
    };

    private static string PropertyOf(AccessorDeclarationSyntax accessor) => accessor.Parent?.Parent switch
    {
        PropertyDeclarationSyntax property => property.Identifier.Text,
        EventDeclarationSyntax @event => @event.Identifier.Text,
        IndexerDeclarationSyntax => "this[]",
        _ => "?",
    };
}
