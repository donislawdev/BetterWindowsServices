namespace Bws.Cli;

/// <summary>
/// The nearest thing the tool knows to a word somebody typed.
///
/// Written 2026-08-02, after an audit against clig.dev found that <c>bws lst</c> answered
/// "Unknown option: lst". Two faults in five words: <c>lst</c> is not an option, and the answer
/// offered nothing. clig.dev asks for the second half - "did you mean" - and this project's own
/// history asks for the first: a switch given without its value used to report itself as
/// unknown, sending somebody to hunt for a typo in a word they had spelled correctly.
///
/// Its own file rather than a few lines inside CommandLine, which stood at 499 lines against a
/// ratchet of 583. A helper that fits is not a reason to spend the last of a budget.
/// </summary>
internal static class Suggestions
{
    /// <summary>
    /// How far a typo may be from the word it was meant to be before offering it does more harm
    /// than good.
    ///
    /// Two, which covers a swap, a doubled letter and a missing one - "lst", "lsit", "listt" -
    /// and stops short of turning every unknown word into a guess. Offering a wrong correction
    /// is worse than offering none: it reads as though the tool understood.
    /// </summary>
    private const int Furthest = 2;

    /// <summary>
    /// The closest known word, or nothing when nothing is close.
    ///
    /// Ties go to the first candidate in the order given, which is the order they appear in the
    /// usage text - so when two are equally close the answer is the one somebody is likelier to
    /// have read.
    /// </summary>
    internal static string? Nearest(string typed, IReadOnlyList<string> known)
    {
        if (typed.Length == 0)
        {
            return null;
        }

        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in known)
        {
            var distance = Distance(typed, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return bestDistance <= Furthest ? best : null;
    }

    /// <summary>
    /// Levenshtein distance, case insensitive because the rest of the command line is.
    ///
    /// Two rows rather than a full matrix. Not for speed - the words here are five letters long
    /// and there are seven of them - but because the full matrix invites somebody to read
    /// intermediate rows out of it, and there is nothing there worth reading.
    /// </summary>
    private static int Distance(string first, string second)
    {
        var previous = new int[second.Length + 1];
        var current = new int[second.Length + 1];

        for (var index = 0; index <= second.Length; index++)
        {
            previous[index] = index;
        }

        for (var left = 1; left <= first.Length; left++)
        {
            current[0] = left;

            for (var right = 1; right <= second.Length; right++)
            {
                var same = char.ToLowerInvariant(first[left - 1]) == char.ToLowerInvariant(second[right - 1]);

                current[right] = Math.Min(
                    Math.Min(current[right - 1] + 1, previous[right] + 1),
                    previous[right - 1] + (same ? 0 : 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[second.Length];
    }
}
