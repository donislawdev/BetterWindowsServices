using System.Globalization;

namespace Bws.Core.Querying;

/// <summary>One member of a query, compiled and grouped, ready to be asked about entries.</summary>
internal sealed record QueryTerm
{
    /// <summary>Null for a bare word, which is searched across every free-search field.</summary>
    internal QueryField? Field { get; init; }

    internal required IReadOnlyList<IQueryValue> Values { get; init; }

    internal bool Negated { get; init; }

    /// <summary>
    /// The values as they were written, before they were compiled into matchers.
    ///
    /// Kept so that something can ask whether a query contains a particular member without
    /// re-reading the text - see <see cref="Query.Excludes"/>. A compiled value cannot answer
    /// that: <c>type:driver</c> becomes two symbols and no longer knows it was spelled with
    /// the word the person clicked.
    /// </summary>
    internal IReadOnlyList<string> Written { get; init; } = [];
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

    /// <summary>
    /// An empty query means everything, which has to be said out loud because the
    /// alternative - an empty search box showing an empty list - would be absurd.
    /// </summary>
    /// <param name="bareWordsAreExpressions">
    /// Whether a member without a field reads as a regular expression rather than as text to
    /// be contained. This is the regex switch beside the search box in <c>A2</c>, and it lives
    /// here rather than being spelled out in the interface for one reason: working out which
    /// parts of the text are bare words is the scanner's job, and a second copy of that in the
    /// window would drift from this one silently.
    ///
    /// Only bare words change. <c>name:spool*</c> keeps its own operators either way, so the
    /// switch decides how the search half behaves and leaves the language half alone.
    ///
    /// <b>Not part of the query text</b>, which matters later rather than now: a saved set is
    /// stored text, so the day sets can be saved, this state has to be stored beside the text
    /// or folded into it. Recorded as item 18 of the backlog.
    /// </param>
    public static QueryParseResult Parse(string? query, bool bareWordsAreExpressions = false)
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
            // A member that produces nothing is dropped in silence, and that is DELIBERATE for
            // one shape only: a member still being typed. `status:` is what a search box holds
            // between the colon and the value, and making it an error would flash red after
            // every keystroke. Three tests hold that decision and a fourth holds it in the
            // window.
            //
            // A choke point here reporting every silent drop was written on 2026-08-03 and
            // reverted the same hour, because it broke all four. The narrower fix lives in
            // ReadMember: a member that is nothing but exclamation marks is not half typed, it
            // is finished and says nothing.
            var term = ReadMember(member, problems, bareWordsAreExpressions);

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

    private static QueryTerm? ReadMember(ScannedText member, List<QueryProblem> problems, bool asExpression)
    {
        var negated = member.StartsWithSpecial('!');
        var body = negated ? member.Slice(1) : member;

        // A member made of nothing but exclamation marks. Said rather than dropped, and until
        // 2026-08-03 it was dropped: `bws list --query "!!!"` came back with all 810 entries and
        // a code of success, so a script with a typo in its query got the whole machine and a
        // green light. Rule 8 broken in the place that rule is most about.
        //
        // Found by tools/user-journey/journey.ps1 on its first run and shrunk to one character
        // by the property test beside it.
        //
        // NARROW ON PURPOSE, and the boundary is the interesting part. `status:` and `=` are
        // also members that produce nothing, and they are left alone because they are what a
        // search box holds WHILE SOMEBODY IS TYPING - three tests and one in the window hold
        // that decision. An exclamation mark on its own is not half typed. It is finished and
        // it says nothing.
        // AN ATTEMPT TO ASK THE SCANNER INSTEAD WENT IN HERE AND CAME STRAIGHT BACK OUT, and it
        // is written down because the guess was reasonable and wrong. ScannedText carries a
        // Literal flag per character, and "no literal characters means no content" reads well -
        // but that flag marks characters whose special meaning was taken away by quoting, not
        // characters a person meant. An ordinary word has none of them. The condition turned 156
        // green tests red in one build.
        if (body.Length == 0 || body.Text.All(character => character == '!'))
        {
            problems.Add(new QueryProblem
            {
                Kind = QueryProblemKind.EmptyTerm,

                // Written back out when the member left no characters behind, because it did
                // leave something: a pair of quotes, which is what somebody typed and what the
                // message has to name. An empty fragment in a sentence about a fragment reads
                // as a message with a hole in it.
                Text = member.Text.Length == 0 ? "\"\"" : member.Text
            });

            return null;
        }

        var colon = body.IndexOfSpecial(':');

        // No field name means no field. A member that merely contains a colon, or opens
        // with one, is a bare word: paths and times have colons in them and none of that
        // is an attempt to name a field.
        if (colon <= 0)
        {
            var free = asExpression
                ? ReadExpressionValue(body, problems)
                : ReadTextValue(body, forFreeSearch: true, problems);

            return free is null
                ? null
                : new QueryTerm { Values = [free], Negated = negated, Written = [body.Text] };
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
        var written = new List<string>();

        foreach (var part in body.Slice(colon + 1).SplitOnSpecial(','))
        {
            if (part.Length == 0)
            {
                // Nothing after the colon is somebody mid-keystroke and is dropped below. An
                // empty PAIR OF QUOTES is not: they opened it and closed it, so the member is
                // finished and asks for a value that is the empty string - which nothing has.
                //
                // Until 2026-08-03 the two were the same absence, and `name:""` came back with
                // every entry on the machine and a code of success. Backlog item 66.
                if (part.IsExplicitlyEmpty)
                {
                    problems.Add(new QueryProblem
                    {
                        Kind = QueryProblemKind.EmptyTerm,
                        Text = member.Text
                    });

                    return null;
                }

                continue;
            }

            var value = ReadValue(field, part, problems);

            if (value is not null)
            {
                values.Add(value);
                written.Add(part.Text);
            }
        }

        // Nothing after the colon at all. Half-typed, not wrong, so the rest of the query
        // still answers and the member waits for the person to finish.
        return values.Count == 0
            ? null
            : new QueryTerm { Field = field, Values = values, Negated = negated, Written = written };
    }

    /// <summary>
    /// A bare word read as a regular expression, which is what the regex switch beside the
    /// search box means.
    ///
    /// Nothing else is tried first - not the slashes, not the equals sign, not the wildcards.
    /// With the switch on, the text is the expression and every character in it means what it
    /// means to a regular expression, which is the only reading that does not need a person to
    /// remember a second set of rules for when the switch is down.
    /// </summary>
    private static IQueryValue? ReadExpressionValue(ScannedText value, List<QueryProblem> problems)
    {
        if (QueryPatterns.TryPattern(value.Text, out var compiled, out var failure))
        {
            return new TextValue(TextOperator.Pattern, value.Text, compiled);
        }

        problems.Add(new QueryProblem
        {
            Kind = QueryProblemKind.BadPattern,
            Text = value.Text,
            Detail = failure
        });

        return null;
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
            QueryFieldKind.Size => ReadSizeValue(field, value, problems),
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
            var wanted = value.Slice(1);

            // An equals sign with nothing after it is somebody mid-keystroke, the same as a
            // colon with nothing after it. Taking it literally would ask for entries whose
            // name is the empty string and answer with nothing at all.
            return wanted.Length == 0
                ? null
                : new TextValue(TextOperator.Exact, wanted.Text, null);
        }

        if (value.HasSpecial('*') || value.HasSpecial('?'))
        {
            if (QueryPatterns.TryWildcard(value, out var wildcard, out var refused))
            {
                return new TextValue(TextOperator.Pattern, value.Text, wildcard);
            }

            // Said rather than thrown. Until 2026-08-03 this call could not fail as far as the
            // parser was concerned, so a wildcard the engine refused escaped as an exception -
            // out of Parse, out of the command line tool's Main, and onto somebody's screen as
            // a stack trace with an exit code that is not in the table.
            problems.Add(new QueryProblem
            {
                Kind = QueryProblemKind.PatternTooComplex,
                Text = value.Text,
                Detail = refused
            });

            return null;
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
            Nearest = QuerySpelling.Nearest(wanted, alternatives)
        });

        return null;
    }

    /// <summary>
    /// Which comparison a value opens with, and what is left after it.
    ///
    /// Shared by numbers and sizes because the shapes are the language's, not the unit's -
    /// a person who has learned <c>pid:&gt;1000</c> should not have to find out whether
    /// memory spells its comparisons the same way.
    /// </summary>
    private static (NumberOperator Operation, string Bound)? Comparison(string text) => text switch
    {
        _ when text.StartsWith(">=", StringComparison.Ordinal) => (NumberOperator.GreaterOrEqual, text[2..]),
        _ when text.StartsWith("<=", StringComparison.Ordinal) => (NumberOperator.LessOrEqual, text[2..]),
        _ when text.StartsWith('>') => (NumberOperator.Greater, text[1..]),
        _ when text.StartsWith('<') => (NumberOperator.Less, text[1..]),
        _ => null
    };

    /// <summary>
    /// A quantity of bytes, in the same shapes a number takes: exact, compared, or a closed
    /// range. The unit is not optional - see <see cref="QuerySizes"/> for why.
    /// </summary>
    private static IQueryValue? ReadSizeValue(QueryField field, ScannedText value, List<QueryProblem> problems)
    {
        var text = value.Text;

        if (Comparison(text) is { } comparison)
        {
            return QuerySizes.TryRead(comparison.Bound, out var bound)
                ? new SizeValue(comparison.Operation, bound, bound)
                : RejectSize(field, value, problems);
        }

        // Only a dash between two units is a range. Looking for the first dash anywhere, the
        // way the number field does, is safe here for the same reason: a size never opens
        // with one, because a negative quantity of bytes is not a thing anybody writes.
        var dash = text.IndexOf('-', StringComparison.Ordinal);

        if (dash > 0)
        {
            if (!QuerySizes.TryRead(text[..dash], out var low) || !QuerySizes.TryRead(text[(dash + 1)..], out var high))
            {
                return RejectSize(field, value, problems);
            }

            // Ends the wrong way round matches nothing, ever, so it is a mistake rather than
            // an empty answer - the same rule the number field follows.
            return low <= high
                ? new SizeValue(NumberOperator.Range, low, high)
                : RejectSize(field, value, problems);
        }

        return QuerySizes.TryRead(text, out var exact)
            ? new SizeValue(NumberOperator.Equal, exact, exact)
            : RejectSize(field, value, problems);
    }

    private static IQueryValue? RejectSize(QueryField field, ScannedText value, List<QueryProblem> problems)
    {
        problems.Add(new QueryProblem
        {
            Kind = QueryProblemKind.BadSize,
            Text = value.Text,
            Field = field.Name
        });

        return null;
    }

    private static IQueryValue? ReadNumberValue(QueryField field, ScannedText value, List<QueryProblem> problems)
    {
        var text = value.Text;

        var shape = Comparison(text);

        if (shape is { } comparison)
        {
            return TryNumber(comparison.Bound, out var bound)
                ? new NumberValue(comparison.Operation, bound, bound)
                : Reject(field, value, problems);
        }

        var dash = text.IndexOf('-', StringComparison.Ordinal);

        if (dash > 0)
        {
            if (!TryNumber(text[..dash], out var low) || !TryNumber(text[(dash + 1)..], out var high))
            {
                return Reject(field, value, problems);
            }

            // A range that ends before it starts matches nothing, ever. Letting it through
            // would answer a transposition with an empty list, and an empty list reads as
            // "there are none" rather than "you wrote the ends the wrong way round".
            return low <= high
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
}
