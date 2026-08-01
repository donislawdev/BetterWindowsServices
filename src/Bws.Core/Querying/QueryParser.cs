using System.Globalization;

namespace Bws.Core.Querying;

/// <summary>One member of a query, compiled and grouped, ready to be asked about entries.</summary>
internal sealed record QueryTerm
{
    /// <summary>Null for a bare word, which is searched across every free-search field.</summary>
    internal QueryField? Field { get; init; }

    internal required IReadOnlyList<IQueryValue> Values { get; init; }

    internal bool Negated { get; init; }
}

/// <summary>What came of reading a query: either something that can filter, or what is wrong with it.</summary>
public sealed record QueryParseResult
{
    internal QueryParseResult(Query? query, IReadOnlyList<QueryProblem> problems)
    {
        Query = query;
        Problems = problems;
    }

    /// <summary>Null when <see cref="Problems"/> is not empty.</summary>
    public Query? Query { get; }

    public IReadOnlyList<QueryProblem> Problems { get; }

    public bool IsValid => Problems.Count == 0 && Query is not null;
}

/// <summary>
/// Reads query text into something that can be run against entries.
///
/// Two rules shape the error handling and both come from the query language document. A
/// query with a mistake in it filters nothing and says what is wrong, rather than
/// returning an empty list, because an empty list is an answer and here there is none. An
/// unfinished member, on the other hand, is not a mistake: the interface validates on
/// every keystroke and would spend most of its time showing red if half-typed text were
/// an error, so a member with nothing after the colon is simply ignored.
/// </summary>
public static class QueryParser
{
    /// <summary>
    /// The version of the syntax this build understands.
    ///
    /// A saved query is stored text, so it is a promise that has to hold for years. The
    /// syntax may grow and may not change meaning, and when a saved query starts carrying
    /// this number it will be able to keep behaving the way it did on the day it was
    /// written, including which fields a bare word searched back then.
    /// </summary>
    public const int SyntaxVersion = 1;

    /// <summary>An empty query means everything, which has to be said out loud because the
    /// alternative - an empty search box showing an empty list - would be absurd.</summary>
    public static QueryParseResult Parse(string? query)
    {
        var problems = new List<QueryProblem>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return new QueryParseResult(new Query([]), problems);
        }

        if (!QueryScanner.TryScan(query, out var members, out var unclosedQuoteAt))
        {
            problems.Add(new QueryProblem
            {
                Kind = QueryProblemKind.UnclosedQuote,
                Text = query[unclosedQuoteAt..]
            });

            return new QueryParseResult(null, problems);
        }

        var terms = new List<QueryTerm>();

        foreach (var member in members)
        {
            var term = ReadMember(member, problems);

            if (term is not null)
            {
                terms.Add(term);
            }
        }

        return problems.Count > 0
            ? new QueryParseResult(null, problems)
            : new QueryParseResult(new Query(Fold(terms)), problems);
    }

    /// <summary>
    /// Collapses repeated mentions of one field into an alternative.
    ///
    /// <c>status:running status:stopped</c> means either, not both. Without this rule,
    /// ticking two status boxes in the interface would produce a query that can never
    /// match anything, and ticking two boxes is the most ordinary thing a person does. So
    /// the rule is forced by the promise that clicking filters writes a query, not chosen
    /// for elegance.
    /// </summary>
    private static List<QueryTerm> Fold(List<QueryTerm> terms)
    {
        var folded = new List<QueryTerm>(terms.Count);
        var positionOf = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var term in terms)
        {
            // Bare words stay separate: two of them mean "contains both", and exclusions
            // are already absolute, so neither gains anything from being merged.
            if (term.Field is null || term.Negated)
            {
                folded.Add(term);
                continue;
            }

            if (positionOf.TryGetValue(term.Field.Name, out var position))
            {
                folded[position] = folded[position] with
                {
                    Values = [.. folded[position].Values, .. term.Values]
                };

                continue;
            }

            positionOf[term.Field.Name] = folded.Count;
            folded.Add(term);
        }

        return folded;
    }

    private static QueryTerm? ReadMember(ScannedText member, List<QueryProblem> problems)
    {
        var negated = member.StartsWithSpecial('!');
        var body = negated ? member.Slice(1) : member;

        if (body.Length == 0)
        {
            return null;
        }

        var colon = body.IndexOfSpecial(':');

        // No field name means no field. A member that merely contains a colon, or opens
        // with one, is a bare word: paths and times have colons in them and none of that
        // is an attempt to name a field.
        if (colon <= 0)
        {
            var free = ReadTextValue(body, forFreeSearch: true, problems);
            return free is null ? null : new QueryTerm { Values = [free], Negated = negated };
        }

        var fieldName = body.Slice(0, colon).Text;
        var field = QueryFields.Find(fieldName);

        if (field is null)
        {
            problems.Add(new QueryProblem
            {
                Kind = QueryProblemKind.UnknownField,
                Text = fieldName,
                Alternatives = [.. QueryFields.All.Select(known => known.Name)]
            });

            return null;
        }

        var values = new List<IQueryValue>();

        foreach (var part in body.Slice(colon + 1).SplitOnSpecial(','))
        {
            if (part.Length == 0)
            {
                continue;
            }

            var value = ReadValue(field, part, problems);

            if (value is not null)
            {
                values.Add(value);
            }
        }

        // Nothing after the colon at all. Half-typed, not wrong, so the rest of the query
        // still answers and the member waits for the person to finish.
        return values.Count == 0 ? null : new QueryTerm { Field = field, Values = values, Negated = negated };
    }

    private static IQueryValue? ReadValue(QueryField field, ScannedText value, List<QueryProblem> problems)
    {
        var reserved = ReadReservedValue(value);

        if (reserved is not null)
        {
            return reserved;
        }

        return field.Kind switch
        {
            QueryFieldKind.Text => ReadTextValue(value, forFreeSearch: false, problems),
            QueryFieldKind.Enumeration => ReadSymbolValue(field, value, problems),
            _ => ReadNumberValue(field, value, problems)
        };
    }

    /// <summary>
    /// The three words that ask about the reading rather than the value. Only the bare
    /// form counts, so <c>name:"none"</c> looks for a service called none and <c>name:"?"</c>
    /// looks for a question mark.
    /// </summary>
    private static IQueryValue? ReadReservedValue(ScannedText value)
    {
        if (!value.IsBare())
        {
            return null;
        }

        // Deliberately ahead of the wildcard rule, where a lone ? would otherwise mean
        // "any single character". Asking what could not be read is worth the collision,
        // and a one-character wildcard on its own is not a query anybody writes.
        if (value.Text == QueryFields.Unreadable)
        {
            return new OutcomeValue(ReadOutcome.Denied);
        }

        return QueryFields.Normalise(value.Text) switch
        {
            QueryFields.None => new OutcomeValue(ReadOutcome.Absent),
            QueryFields.Any => new OutcomeValue(ReadOutcome.Present),
            _ => null
        };
    }

    private static IQueryValue? ReadTextValue(ScannedText value, bool forFreeSearch, List<QueryProblem> problems)
    {
        // A bare word is text and nothing else. Reserved words need a field to be about,
        // so treating a lone "none" as a search for the word is the only reading that means
        // anything.
        if (!forFreeSearch && value.Length == 0)
        {
            return null;
        }

        if (value.Length >= 2 && value.IsSpecial(0, '/') && value.IsSpecial(value.Length - 1, '/'))
        {
            var pattern = value.Text[1..^1];

            if (!QueryPatterns.TryPattern(pattern, out var compiled, out var failure))
            {
                problems.Add(new QueryProblem
                {
                    Kind = QueryProblemKind.BadPattern,
                    Text = pattern,
                    Detail = failure
                });

                return null;
            }

            return new TextValue(TextOperator.Pattern, pattern, compiled);
        }

        if (value.StartsWithSpecial('='))
        {
            return new TextValue(TextOperator.Exact, value.Slice(1).Text, null);
        }

        if (value.HasSpecial('*') || value.HasSpecial('?'))
        {
            return new TextValue(TextOperator.Pattern, value.Text, QueryPatterns.Wildcard(value));
        }

        return new TextValue(TextOperator.Contains, value.Text, null);
    }

    private static IQueryValue? ReadSymbolValue(QueryField field, ScannedText value, List<QueryProblem> problems)
    {
        var wanted = QueryFields.Normalise(value.Text);

        foreach (var accepted in field.Values)
        {
            if (QueryFields.Normalise(accepted.Text) == wanted)
            {
                return new SymbolValue(accepted.Symbols);
            }
        }

        var alternatives = field.Values.Select(accepted => accepted.Text).ToArray();

        problems.Add(new QueryProblem
        {
            Kind = QueryProblemKind.UnknownValue,
            Text = value.Text,
            Field = field.Name,
            Alternatives = alternatives,

            // A typo in an enumeration must never come back as an empty list. An empty list
            // reads as an answer, and "there are no running services" is a very different
            // sentence from "you wrote runing".
            Nearest = Nearest(wanted, alternatives)
        });

        return null;
    }

    private static IQueryValue? ReadNumberValue(QueryField field, ScannedText value, List<QueryProblem> problems)
    {
        var text = value.Text;

        (NumberOperator Operation, string Bound)? shape = text switch
        {
            _ when text.StartsWith(">=", StringComparison.Ordinal) => (NumberOperator.GreaterOrEqual, text[2..]),
            _ when text.StartsWith("<=", StringComparison.Ordinal) => (NumberOperator.LessOrEqual, text[2..]),
            _ when text.StartsWith('>') => (NumberOperator.Greater, text[1..]),
            _ when text.StartsWith('<') => (NumberOperator.Less, text[1..]),
            _ => null
        };

        if (shape is { } comparison)
        {
            return TryNumber(comparison.Bound, out var bound)
                ? new NumberValue(comparison.Operation, bound, bound)
                : Reject(field, value, problems);
        }

        var dash = text.IndexOf('-', StringComparison.Ordinal);

        if (dash > 0)
        {
            return TryNumber(text[..dash], out var low) && TryNumber(text[(dash + 1)..], out var high)
                ? new NumberValue(NumberOperator.Range, low, high)
                : Reject(field, value, problems);
        }

        return TryNumber(text, out var exact)
            ? new NumberValue(NumberOperator.Equal, exact, exact)
            : Reject(field, value, problems);
    }

    private static IQueryValue? Reject(QueryField field, ScannedText value, List<QueryProblem> problems)
    {
        problems.Add(new QueryProblem
        {
            Kind = QueryProblemKind.BadNumber,
            Text = value.Text,
            Field = field.Name
        });

        return null;
    }

    private static bool TryNumber(string text, out int number) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    /// <summary>
    /// The closest accepted spelling, when one is close enough to be worth offering. Too
    /// generous a threshold turns a helpful hint into a confusing one, so a suggestion has
    /// to be nearer than half the word.
    /// </summary>
    private static string? Nearest(string wanted, IReadOnlyList<string> alternatives)
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
