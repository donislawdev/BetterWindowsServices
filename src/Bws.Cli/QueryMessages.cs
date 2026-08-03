using Bws.Core.Querying;

namespace Bws.Cli;

/// <summary>
/// Turns what the core found wrong with a query into a sentence.
///
/// The split is deliberate. The core reports a kind of problem and the facts around it,
/// and this file decides the words, because a core that phrases messages has decided what
/// every interface says. The same problem will read differently in a window than it does
/// in a terminal, and neither of them belongs in the part that parses.
///
/// Every message names what was written and what would have worked. "Unknown value" on
/// its own leaves somebody guessing, and the list of what is accepted ends the guessing
/// on the spot.
/// </summary>
internal static class QueryMessages
{
    internal static string Of(QueryProblem problem) => problem.Kind switch
    {
        QueryProblemKind.UnknownField => Texts.Of(
            "cli.query.unknownField", problem.Text, Join(problem.Alternatives)),

        QueryProblemKind.UnknownValue when problem.Nearest is not null => Texts.Of(
            "cli.query.unknownValueNearest",
            problem.Text, problem.Field!, problem.Nearest, Join(problem.Alternatives)),

        QueryProblemKind.UnknownValue => Texts.Of(
            "cli.query.unknownValue", problem.Text, problem.Field!, Join(problem.Alternatives)),

        QueryProblemKind.BadPattern => Texts.Of(
            "cli.query.badPattern", problem.Text, problem.Detail ?? string.Empty),

        QueryProblemKind.UnclosedQuote => Texts.Of(
            "cli.query.unclosedQuote", problem.Text),

        // Its own sentence rather than the number one. Somebody who wrote memory:>500 wrote
        // a perfectly good number and left off the unit, and being told their number is not
        // a number would send them to check the digits.
        QueryProblemKind.BadSize => Texts.Of("cli.query.badSize", problem.Text, problem.Field!),

        // Its own sentence, because "there is nothing here" is not a kind of typo and
        // naming it one would send somebody looking at characters that are correct.
        QueryProblemKind.EmptyTerm => Texts.Of("cli.query.emptyTerm", problem.Text),

        // Shortened, unlike every other message here, and that is what the shortening is for:
        // the text that causes this is about two thousand characters long, so echoing it whole
        // would bury the one sentence that says what to do about it.
        //
        // The engine's own words are deliberately left out. They are about the size of an
        // automaton, which explains nothing to anybody who did not pick the engine.
        QueryProblemKind.PatternTooComplex => Texts.Of(
            "cli.query.patternTooComplex", Excerpt(problem.Text)),

        _ => Texts.Of("cli.query.badNumber", problem.Text, problem.Field!)
    };

    /// <summary>Enough of a value to recognise it, for the one message whose value can be enormous.</summary>
    private static string Excerpt(string text) =>
        text.Length <= 40 ? text : text[..40] + "...";

    private static string Join(IReadOnlyList<string> values) => string.Join(", ", values);
}
