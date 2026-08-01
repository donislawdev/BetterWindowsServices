namespace Bws.Core.Querying;

/// <summary>What a query decided about one entry.</summary>
/// <param name="Matched">Whether the entry belongs in the result.</param>
/// <param name="Unreadable">
/// Whether the answer rests on a field that could not be read. The entry still gets a
/// yes or a no, because a filter has to decide, but the caller now knows the decision was
/// made with something missing.
/// </param>
/// <param name="TooCostly">
/// Whether an expression ran out of time on this entry. Separate from
/// <paramref name="Unreadable"/> because the way out of it is different: one calls for
/// more permissions, the other for a cheaper expression.
/// </param>
public readonly record struct QueryMatch(bool Matched, bool Unreadable, bool TooCostly);

/// <summary>
/// The entries a query selected, and how much of that answer is trustworthy.
/// </summary>
/// <param name="Entries">What matched, in the order they arrived.</param>
/// <param name="Unreadable">
/// How many entries were judged against a field that could not be read. Above zero, the
/// result is partial and has to say so. This is the rule the whole language leans on:
/// without it, a query on a machine without the right permissions returns a short list
/// that looks like a complete answer.
/// </param>
/// <param name="TooCostly">
/// How many entries an expression ran out of time on. Above zero, those entries were
/// never really judged, and a quiet absence of results would be the worst way to report it.
/// </param>
public sealed record QueryResult(IReadOnlyList<ScmEntry> Entries, int Unreadable, int TooCostly);

/// <summary>
/// A query that has been read and compiled, ready to be run against entries.
///
/// Compiled once and then only asked questions. Patterns and wildcards are turned into
/// matchers at parse time, not per entry, because in the interface this runs over the
/// whole listing on every keystroke.
///
/// Evaluation never reaches out to the system. It works on what has already been read,
/// and a member about something that has not been read yet counts as unreadable rather
/// than triggering a fetch.
/// </summary>
public sealed class Query
{
    private readonly IReadOnlyList<QueryTerm> _terms;

    internal Query(IReadOnlyList<QueryTerm> terms) => _terms = terms;

    /// <summary>True for a query with nothing in it, which selects everything.</summary>
    public bool IsEmpty => _terms.Count == 0;

    public QueryResult Filter(IReadOnlyList<ScmEntry> entries)
    {
        if (IsEmpty)
        {
            return new QueryResult(entries, Unreadable: 0, TooCostly: 0);
        }

        var selected = new List<ScmEntry>(entries.Count);
        var unreadable = 0;
        var tooCostly = 0;

        foreach (var entry in entries)
        {
            var match = Match(entry);

            if (match.Matched)
            {
                selected.Add(entry);
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

        return new QueryResult(selected, unreadable, tooCostly);
    }

    /// <summary>
    /// Judges one entry against every member.
    ///
    /// Deliberately without short circuits across members. The order a person typed their
    /// members in must not change the answer, and that includes whether the answer admits
    /// to being partial. Stopping at the first member that rejects would make the
    /// admission depend on typing order, which is exactly the kind of difference nobody
    /// would ever find.
    /// </summary>
    public QueryMatch Match(ScmEntry entry)
    {
        var matched = true;
        var unreadable = false;
        var tooCostly = false;

        foreach (var term in _terms)
        {
            var verdict = term.Field is null
                ? AcrossFreeSearch(term.Values, entry)
                : AnyValue(term.Field, term.Values, entry);

            unreadable |= verdict.Unreadable;
            tooCostly |= verdict.TooCostly;

            // An exclusion wins over anything that let the entry through. Somebody writes
            // an exclusion when they genuinely do not want to see the thing.
            //
            // An exclusion about a field nobody could read does not exclude, which is the
            // honest reading: there is no way to tell whether it applies. The entry stays
            // and the result reports itself as partial.
            matched &= term.Negated ? !verdict.Matched : verdict.Matched;
        }

        return new QueryMatch(matched, unreadable, tooCostly);
    }

    private static Verdict AnyValue(QueryField field, IReadOnlyList<IQueryValue> values, ScmEntry entry)
    {
        var verdict = Verdict.NoMatch;

        foreach (var value in values)
        {
            verdict = verdict.Or(value.Test(field, entry));
        }

        return verdict;
    }

    private static Verdict AcrossFreeSearch(IReadOnlyList<IQueryValue> values, ScmEntry entry)
    {
        var verdict = Verdict.NoMatch;

        foreach (var field in QueryFields.FreeSearch)
        {
            verdict = verdict.Or(AnyValue(field, values, entry));
        }

        return verdict;
    }
}
