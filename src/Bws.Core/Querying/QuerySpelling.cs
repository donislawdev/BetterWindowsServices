namespace Bws.Core.Querying;

/// <summary>
/// How close two spellings are, and which accepted one somebody probably meant.
///
/// <b>Moved out of <c>QueryParser</c> on 2026-08-03 because the size ratchet said so.</b>
/// Reporting a wildcard the engine will not build - a crash before that day - pushed that file
/// fifteen lines past a ceiling that may only ever go down, and the ceiling exists precisely so
/// that adding to the longest file starts with looking for a seam.
///
/// This is the seam, and it is a real one rather than a convenient line count. Everything left
/// behind reads query text: it decides where a member ends, which field it names and what its
/// values mean. Nothing here reads anything. It answers one question about two strings, and it
/// would answer it the same way for a field name, a value or a word from another language
/// entirely.
///
/// <b>There is a second implementation of this in <c>Bws.Cli/Suggestions.cs</c>, and they are
/// deliberately not merged.</b> That one measures a mistyped command against the five verbs, and
/// its threshold is tuned for words of four to seven letters. This one measures a value against
/// whatever an enumeration accepts. Sharing them would mean one of the two thresholds moving to
/// suit the other, and putting a public spelling utility in the core's contract for the sake of
/// one caller in another assembly. Written down so the next session knows it was decided rather
/// than missed.
/// </summary>
internal static class QuerySpelling
{
    /// <summary>
    /// The closest accepted spelling, when one is close enough to be worth offering. Too
    /// generous a threshold turns a helpful hint into a confusing one, so a suggestion has
    /// to be nearer than half the word.
    /// </summary>
    internal static string? Nearest(string wanted, IReadOnlyList<string> alternatives)
    {
        var best = default(string);
        var bestDistance = int.MaxValue;

        foreach (var candidate in alternatives)
        {
            var distance = Distance(wanted, QueryFields.Normalise(candidate));

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        var allowed = Math.Max(2, wanted.Length / 2);

        return bestDistance <= allowed ? best : null;
    }

    /// <summary>
    /// How many single character edits turn one string into the other.
    ///
    /// Two rows rather than the whole table, because only the previous one is ever read - which
    /// matters here for a reason that is not tidiness: the left side is text somebody typed and
    /// has no length limit, so a full table would be as large as that text times the longest
    /// accepted spelling.
    /// </summary>
    private static int Distance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= right.Length; column++)
            {
                var substitution = previous[column - 1] + (left[row - 1] == right[column - 1] ? 0 : 1);
                current[column] = Math.Min(Math.Min(current[column - 1] + 1, previous[column] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
