using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Bws.Architecture.Tests;

/// <summary>One line of prose somebody wrote, where it stands, and what it says.</summary>
internal sealed record ProseLine(string File, int Line, string Text)
{
    internal string Where => $"{File}:{Line}";
}

/// <summary>
/// Reads the prose out of the files in git: comments in C# and XAML, the Markdown at the root, and
/// the comments in the workflows.
///
/// <b>Comments come from the syntax tree, never from a search for two slashes</b> - the lesson the
/// owner's Rust project wrote down twice: a search for a comment marker finds one inside a string
/// literal, and misses one trailing a line of code. Trivia is what the parser knows is a comment.
///
/// <b>Code inside prose is not prose.</b> A <c>code</c> block is blanked before the lines are split,
/// keeping its line breaks so every line keeps its number. What stands inline - a <c>c</c> element,
/// backticks, a quotation, an entity, an address - is taken out by <see cref="Unquoted"/>, and only
/// by the rule that needs it: a quotation is exactly where a sentence in another language lives.
/// </summary>
internal static class Prose
{
    private static readonly Regex CodeBlock = new("<code>.*?</code>", RegexOptions.Singleline, Sources.Ceiling);

    private static readonly Regex MarkupComment = new("<!--.*?-->", RegexOptions.Singleline, Sources.Ceiling);

    private static readonly Regex Inline = new(@"<c>.*?</c>|`[^`]*`|""[^""]*""|&#?\w+;|https?://\S+", RegexOptions.None, Sources.Ceiling);

    /// <summary>Every line of every comment in one C# file.</summary>
    internal static IEnumerable<ProseLine> InCode(string file, SyntaxNode root) =>
        root.DescendantTrivia().Where(IsComment).SelectMany(trivia =>
            Lines(file, trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1, trivia.ToFullString()));

    /// <summary>Every line of every comment in one XAML file.</summary>
    internal static IEnumerable<ProseLine> InMarkup(string file, string text) =>
        MarkupComment.Matches(text).SelectMany(comment =>
            Lines(file, LineAt(text, comment.Index), comment.Value));

    /// <summary>
    /// Every line of a Markdown file outside a fence or an indented block.
    ///
    /// <b>A fence closes only on its own marker</b> - the same character, at least as long, and nothing
    /// after it, which is CommonMark's rule. The first version toggled on any line that began with three
    /// backticks, so the inside of a four-backtick fence around a three-backtick one read as prose, and
    /// a tilde fence was not a fence at all. The review of the pull request that brought this said so.
    /// </summary>
    internal static IEnumerable<ProseLine> InMarkdown(string file, string[] lines)
    {
        Fence? open = null;
        for (var index = 0; index < lines.Length; index++)
        {
            var marker = Fence.On(lines[index]);
            var prose = open is null && marker is null && !lines[index].StartsWith("    ", StringComparison.Ordinal);
            open = open is null ? marker : open.ClosedBy(marker) ? null : open;
            if (prose)
            {
                yield return new ProseLine(file, index + 1, lines[index]);
            }
        }
    }

    /// <summary>Every whole-line comment of a workflow. A trailing one is left alone - a hash inside a value is not one.</summary>
    internal static IEnumerable<ProseLine> InWorkflow(string file, string[] lines) =>
        lines.Select((line, index) => new ProseLine(file, index + 1, line))
            .Where(line => line.Text.TrimStart().StartsWith('#'));

    /// <summary>A line with everything that is code, a quotation, an entity or an address taken out.</summary>
    internal static string Unquoted(string line) => Inline.Replace(line, " ");

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.Kind() is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia
            or SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia;

    private static IEnumerable<ProseLine> Lines(string file, int first, string text) =>
        CodeBlock.Replace(text, block => new string('\n', block.Value.Count(character => character == '\n')))
            .Split('\n')
            .Select((line, offset) => new ProseLine(file, first + offset, line.TrimEnd('\r')));

    private static int LineAt(string text, int index) => 1 + text.AsSpan(0, index).Count('\n');
}

/// <summary>
/// One Markdown fence marker: three or more backticks or tildes after at most three spaces.
/// </summary>
internal sealed record Fence(char Mark, int Length, bool Bare)
{
    /// <summary>The marker a line is, or nothing. A backtick marker may not carry a backtick after it.</summary>
    internal static Fence? On(string line)
    {
        var rest = line.TrimStart(' ');
        var mark = rest.Length > 0 ? rest[0] : ' ';
        var length = rest.TakeWhile(character => character == mark).Count();
        var after = rest[length..];

        return line.Length - rest.Length <= 3 && mark is '`' or '~' && length >= 3 && !(mark == '`' && after.Contains('`', StringComparison.Ordinal))
            ? new Fence(mark, length, after.Trim().Length == 0)
            : null;
    }

    /// <summary>Whether a marker closes this fence: the same character, at least as long, and bare.</summary>
    internal bool ClosedBy(Fence? marker) => marker is { Bare: true } && marker.Mark == Mark && marker.Length >= Length;
}
