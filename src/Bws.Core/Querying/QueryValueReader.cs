using System.Globalization;

namespace Bws.Core.Querying;

/// <summary>
/// What one value inside a member means, once the member has said which field it is about.
///
/// <b>Moved out of QueryParser on 2026-08-03 because the size ratchet said so, for the fourth
/// time in two days.</b> Saying whether a piece of query text is finished or still being typed
/// pushed that file thirty six lines past a ceiling that may only ever go down.
///
/// The seam is the one the language already has. What stays behind reads MEMBERS: where one ends,
/// whether it is negated, which field it names, and what to do when it produces nothing. What is
/// here reads VALUES: the reserved words, the four shapes text can take, an enumeration spelling,
/// a number, a size. The two halves meet at exactly one call, and the split was available from
/// the day the language was written - it took a ceiling to make anybody look for it.
/// </summary>
internal static class QueryValueReader
{
    /// <summary>
    /// A bare word read as a regular expression, which is what the regex switch beside the
    /// search box means.
    ///
    /// Nothing else is tried first - not the slashes, not the equals sign, not the wildcards.
    /// With the switch on, the text is the expression and every character in it means what it
    /// means to a regular expression, which is the only reading that does not need a person to
    /// remember a second set of rules for when the switch is down.
    /// </summary>
    internal static IQueryValue? ReadExpressionValue(ScannedText value, List<QueryProblem> problems)
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

    internal static IQueryValue? ReadValue(QueryField field, ScannedText value, List<QueryProblem> problems)
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

    internal static IQueryValue? ReadTextValue(ScannedText value, bool forFreeSearch, List<QueryProblem> problems)
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
