using System.Text.Json.Nodes;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The searches somebody made lately, newest first - shown over the questions to start from when
/// they come to an empty search box. The owner's first choice of 2026-11-16: "the empty box shows
/// the last five queries above the examples, kept in the same file as the column layout".
///
/// <b>The rules live here rather than in the window or the file</b>, so they can be held without
/// either: how many are kept, how long one may be, what counts as the same query, and how a file
/// somebody edited by hand is read. <see cref="KeptColumns"/> keeps the list and writes it, and the
/// list under the box reads it.
/// </summary>
internal static class RecentQueries
{
    /// <summary>How many are kept - the five the owner asked for.</summary>
    internal const int Kept = 5;

    /// <summary>
    /// The longest query kept, in characters. A query that reads can be any length, and a search
    /// box holding a pasted paragraph is a search somebody will not want back as a row they have to
    /// read. Chosen, not measured: far past anything typed by hand, and short enough that five of
    /// them keep the file small.
    /// </summary>
    internal const int LengthMost = 500;

    /// <summary>
    /// The list with this query first. Spaces around it are not part of it, a query already in the
    /// list moves to the front rather than standing twice, and the oldest goes when there are more
    /// than <see cref="Kept"/>. A query that is empty or longer than <see cref="LengthMost"/> leaves
    /// the list as it was.
    ///
    /// <b>The same query means the same characters</b>, case included. <c>status:Running</c> and
    /// <c>status:running</c> ask the same question, but they are what somebody typed, and the row
    /// gives back what they typed.
    /// </summary>
    internal static IReadOnlyList<string> With(IReadOnlyList<string> kept, string query)
    {
        var trimmed = query.Trim();

        if (trimmed.Length == 0 || trimmed.Length > LengthMost)
        {
            return kept;
        }

        return [trimmed, .. kept.Where(one => !string.Equals(one, trimmed, StringComparison.Ordinal)).Take(Kept - 1)];
    }

    /// <summary>
    /// Whether what the box holds is a search worth keeping: the model has read that very text, it
    /// reads, and it asks for something. A query with a mistake is not a search anybody made, a text
    /// the model has not read yet is not the one it answered, and <c>status:</c> reads while it is
    /// typed - the box passes it over - and asks for nothing.
    /// </summary>
    internal static bool Worth(string inTheBox, Bws.Core.Querying.QueryAsTyped typed) =>
        string.Equals(inTheBox, typed.Text, StringComparison.Ordinal)
        && typed.Parsed is { IsValid: true, Query.IsEmpty: false };

    /// <summary>The list without this query - somebody pressed Delete on its row.</summary>
    internal static IReadOnlyList<string> Without(IReadOnlyList<string> kept, string query) =>
        [.. kept.Where(one => !string.Equals(one, query, StringComparison.Ordinal))];

    /// <summary>
    /// Writes the list into the file's tree, or nothing when it is empty - the rule every optional
    /// key in that file follows: what is not there is what the program does on its own.
    /// </summary>
    internal static void Render(JsonObject tree, IReadOnlyList<string> kept)
    {
        if (kept.Count > 0)
        {
            tree["recentQueries"] = new JsonArray([.. kept.Select(one => (JsonNode)JsonValue.Create(one))]);
        }
    }

    /// <summary>
    /// The list read back from the file, or none at all.
    ///
    /// <b>Every element is asked for rather than assumed</b>, the rule the rest of the file follows
    /// (ColumnLayouts.Read): the file can be edited by hand, so a number where a query belongs, a
    /// query of a megabyte or forty of them are ordinary. What is not a query is passed over, and
    /// what is left is put through <see cref="With"/> oldest first - so it comes back trimmed,
    /// without repeats and no longer than the window would have kept it. Never an exception, never
    /// a refusal of the whole file over one bad element: this list is a convenience, and losing the
    /// column layout over it would be the wrong trade.
    /// </summary>
    internal static IReadOnlyList<string> From(JsonNode? node)
    {
        if (node is not JsonArray listed)
        {
            return [];
        }

        IReadOnlyList<string> kept = [];

        foreach (var element in listed.Take(Kept * 4).Reverse())
        {
            if (element is JsonValue value && value.TryGetValue<string>(out var query))
            {
                kept = With(kept, query);
            }
        }

        return kept;
    }
}
