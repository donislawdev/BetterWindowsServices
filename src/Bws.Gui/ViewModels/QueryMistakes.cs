using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Where the mistakes in a query stand, and the text they were read from - what the search box
/// marks under the characters the sentence about the mistake is about. The owner's third choice
/// of 2026-11-16: the sentence said what was wrong, and nothing in the box said where.
///
/// <b>The text travels with the places, and that is the whole of what keeps a mark honest.</b> The
/// model reads the box 400 ms behind it, so for as long as somebody is typing the places describe a
/// text the box no longer holds. Whoever draws them draws nothing until the two texts are the same
/// again - a mark under the wrong letters is worse than a mark that comes a moment late.
/// </summary>
/// <param name="Text">The query the places were read from, exactly as it was parsed.</param>
/// <param name="Places">Each mistake's member as written (QueryProblem.At), in the order they were written.</param>
public sealed record QueryMistakes(string Text, IReadOnlyList<Range> Places)
{
    /// <summary>A query with nothing wrong in it, or no query at all.</summary>
    public static QueryMistakes None { get; } = new(string.Empty, []);

    /// <summary>
    /// The places of everything wrong with the text as typed, each place once. A problem with no
    /// place is left out rather than guessed at - every problem the parser raises has one, and
    /// ParserPropertyTests holds that over twenty thousand texts.
    ///
    /// <b>Once, because the parser gives every bad value of a member that member's place</b> -
    /// <c>status:a,b,c</c> is three sentences about one member. Three lines on the same letters look
    /// like one, and each is a wave built and drawn again on every frame the box changes.
    /// </summary>
    internal static QueryMistakes Of(QueryAsTyped typed) =>
        typed.Parsed.IsValid
            ? None
            : new(typed.Text, [.. typed.Parsed.Problems.Where(problem => problem.At is not null).Select(problem => problem.At!.Value).Distinct()]);
}
