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

    /// <summary>
    /// What has to be read before this query can be answered, beyond what a listing reads.
    ///
    /// Asked before the listing is filtered, so that somebody writing <c>signed:no</c> gets
    /// an answer rather than an empty list. Without this the only honest reply to a query
    /// about an unread field is nothing at all - which reads exactly like "there are none",
    /// and is the failure this whole language is arranged to avoid.
    ///
    /// Per family rather than a single flag, because the families are nothing alike: one is
    /// measured in seconds and the other in fractions of a millisecond, so answering a
    /// question about memory by verifying every signature on the machine would be paying
    /// four thousand times over for something nobody asked about.
    /// </summary>
    public ExtraRead Needs =>
        _terms.Aggregate(ExtraRead.None, (needs, term) => needs | (term.Field?.Needs ?? ExtraRead.None));

    /// <summary>
    /// Whether this query carries an exclusion of one value of one field, written the way a
    /// person would write it.
    /// </summary>
    /// <remarks>
    /// Exists so that a control standing for a member can tell whether that member is in the
    /// text - the "show drivers" switch asks <c>Excludes("type", "driver")</c>. Without it the
    /// window would have to find members in the text itself, which means a second copy of the
    /// scanner living in the interface and drifting from this one without a sound.
    ///
    /// Asks about the value as written rather than about what it compiled to, and that is the
    /// point: <c>type:driver</c> becomes two symbols, neither of them the word that was
    /// clicked. Spelling is folded the same way the language folds it everywhere else, so
    /// <c>!TYPE:Driver</c> answers yes.
    ///
    /// One member, one value. <b>Kept as its own name after the general question below arrived
    /// on 2026-08-11</b>, because <c>Excludes("type", "driver")</c> reads as what the drivers
    /// switch means and <c>Carries("type", "driver", negated: true)</c> reads as machinery.
    /// It is one line and it delegates, so there is no second rule to drift.
    /// </remarks>
    public bool Excludes(string field, string value) => Carries(field, value, negated: true);

    /// <summary>
    /// Whether this query carries one value of one field, written the way a person would write
    /// it, on the side asked about.
    /// </summary>
    /// <remarks>
    /// <b>The general form the comment above said could not be designed yet</b> - it said the
    /// clickable filters of <c>A5</c> would want more, in a shape nobody could choose before
    /// there were chips to choose it for. There are now, and the shape they want is this: a
    /// chip stands for one member and has to know whether that member is already in the text,
    /// on the same side. <c>status:stopped</c> and <c>!status:stopped</c> are different chips
    /// and a question that could not tell them apart would light the wrong one.
    ///
    /// Asks about the value as WRITTEN rather than about what it compiled to, and that is the
    /// point rather than a shortcut: <c>type:driver</c> becomes two symbols, neither of them
    /// the word that was clicked, so a chip labelled "drivers" would answer to an exclusion of
    /// one driver kind. Spelling is folded the way the language folds it everywhere else, so
    /// <c>!TYPE:Driver</c> answers yes to <c>("type", "driver", true)</c>.
    /// </remarks>
    public bool Carries(string field, string value, bool negated)
    {
        var wanted = QueryFields.Find(field);

        if (wanted is null)
        {
            return false;
        }

        var spelling = QueryFields.Normalise(value);

        foreach (var term in _terms)
        {
            if (term.Negated != negated || term.Field != wanted)
            {
                continue;
            }

            foreach (var written in term.Written)
            {
                if (QueryFields.Normalise(written) == spelling)
                {
                    return true;
                }
            }
        }

        return false;
    }

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
