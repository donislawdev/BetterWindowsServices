using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What a query picked out of the rows, and what it could not judge.
///
/// Split out of <see cref="MainViewModel"/> on 2026-08-05, the third seam the size ratchet asked
/// that file for. The concept is its own: narrowing a list is a question about a query and some
/// rows, and it needs nothing that only a window knows.
///
/// <b>The two counts travel with the answer rather than being worked out afterwards, and that is
/// the point of the shape.</b> They are the only evidence that the answer is incomplete - an
/// entry judged on a field nobody could read may be in this list wrongly, or missing from it
/// wrongly - and rule 8 forbids handing back a result that looks whole when it is not. Computing
/// them in a second pass would mean walking every row twice to learn something the first walk
/// already knew.
/// </summary>
internal readonly record struct Narrowed
{
    /// <summary>The rows the query let through, in the order they came.</summary>
    public required List<EntryRow> Selected { get; init; }

    /// <summary>How many rows were judged on something nobody could read.</summary>
    public required int Unreadable { get; init; }

    /// <summary>How many the expression ran out of time on, so they were never judged at all.</summary>
    public required int TooCostly { get; init; }
}

internal static class Narrowing
{
    /// <summary>
    /// Applies a query to every row and counts what it had to admit.
    ///
    /// Runs on the interface thread on every keystroke, which is why the query language is
    /// compiled once and never reaches out to the system while it is being asked. The budget is
    /// 50 ms over the whole listing, from section 8.1 of the specification, and the measurement
    /// against it is 0.42-2.25 ms over 810 entries.
    /// </summary>
    public static Narrowed Of(Query query, IReadOnlyList<EntryRow> everything)
    {
        var selected = new List<EntryRow>(everything.Count);
        var unreadable = 0;
        var tooCostly = 0;

        foreach (var row in everything)
        {
            var match = query.Match(row.Entry);

            if (match.Matched)
            {
                selected.Add(row);
            }

            if (match.Unreadable)
            {
                unreadable++;
            }

            if (match.TooCostly)
            {
                tooCostly++;
            }
        }

        return new Narrowed { Selected = selected, Unreadable = unreadable, TooCostly = tooCostly };
    }
}
