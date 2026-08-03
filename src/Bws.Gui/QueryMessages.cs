using Bws.Core.Querying;

namespace Bws.Gui;

/// <summary>
/// Turns what the core found wrong with a query into a sentence for the window.
///
/// A second file doing what <c>Bws.Cli</c> does, and deliberately not shared code. The core
/// reports a kind of problem and the facts around it, and each interface decides the words -
/// a core that phrases messages has decided what every interface says. The two phrasings can
/// then differ where the situations differ, and one of them already does: the terminal tells
/// somebody a mistyped query printed nothing, and the window has a list still sitting there.
///
/// Every message names what was written and what would have worked. "Unknown value" on its
/// own leaves somebody guessing at a keyboard, and here they are guessing on every keystroke.
/// </summary>
internal static class QueryMessages
{
    internal static string Of(QueryProblem problem) => problem.Kind switch
    {
        QueryProblemKind.UnknownField => Texts.Of(
            "gui.query.unknownField", problem.Text, Join(problem.Alternatives)),

        QueryProblemKind.UnknownValue when problem.Nearest is not null => Texts.Of(
            "gui.query.unknownValueNearest",
            problem.Text, problem.Field!, problem.Nearest, Join(problem.Alternatives)),

        QueryProblemKind.UnknownValue => Texts.Of(
            "gui.query.unknownValue", problem.Text, problem.Field!, Join(problem.Alternatives)),

        QueryProblemKind.BadPattern => Texts.Of(
            "gui.query.badPattern", problem.Text, problem.Detail ?? string.Empty),

        QueryProblemKind.UnclosedQuote => Texts.Of(
            "gui.query.unclosedQuote", problem.Text),

        QueryProblemKind.BadSize => Texts.Of("gui.query.badSize", problem.Text, problem.Field!),

        // Its own sentence, because "there is nothing here" is not a kind of typo and
        // naming it one would send somebody looking at characters that are correct.
        QueryProblemKind.EmptyTerm => Texts.Of("gui.query.emptyTerm", problem.Text),

        // Shortened, unlike every other message here. The text that causes this runs to about
        // two thousand characters, and this one goes into a single line under a list.
        QueryProblemKind.PatternTooComplex => Texts.Of(
            "gui.query.patternTooComplex", Excerpt(problem.Text)),

        _ => Texts.Of("gui.query.badNumber", problem.Text, problem.Field!)
    };

    /// <summary>Enough of a value to recognise it, for the one message whose value can be enormous.</summary>
    private static string Excerpt(string text) =>
        text.Length <= 40 ? text : text[..40] + "...";

    private static string Join(IReadOnlyList<string> values) => string.Join(", ", values);
}
