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

        _ => Texts.Of("cli.query.badNumber", problem.Text, problem.Field!)
    };

    private static string Join(IReadOnlyList<string> values) => string.Join(", ", values);
}
