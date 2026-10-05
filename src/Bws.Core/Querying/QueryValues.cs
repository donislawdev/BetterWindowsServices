using System.Text.RegularExpressions;

namespace Bws.Core.Querying;

/// <summary>
/// What one value said about one entry.
///
/// More than a yes and a no, and the extras are the reason this is a type rather than a
/// boolean. "I could not read the field" is not a no: folding the two together would let
/// a query on a machine without the right permissions return a short list that looks like
/// a complete answer, which in an audit tool is the worst thing that can happen, being
/// believable and untrue at once. "Your expression cost too much to finish" is a third
/// thing again, and it needs saying because the way out of it is different.
///
/// Carrying the reservations as separate flags rather than ranking them means combining
/// two verdicts never needs a precedence rule, and a precedence rule is where this would
/// otherwise go quietly wrong.
/// </summary>
internal readonly record struct Verdict(bool Matched, bool Unreadable, bool TooCostly)
{
    internal static readonly Verdict Match = new(Matched: true, Unreadable: false, TooCostly: false);

    internal static readonly Verdict NoMatch = new(Matched: false, Unreadable: false, TooCostly: false);

    /// <summary>No, and the no rests on a field nobody was allowed to read.</summary>
    internal static readonly Verdict CouldNotRead = new(Matched: false, Unreadable: true, TooCostly: false);

    /// <summary>No, and nothing was really checked, because the expression ran out of time.</summary>
    internal static readonly Verdict RanOutOfTime = new(Matched: false, Unreadable: false, TooCostly: true);

    internal static Verdict Of(bool matched) => matched ? Match : NoMatch;

    /// <summary>
    /// Combines the verdicts of the alternatives inside one member. A match wins, and the
    /// reservations gathered along the way are kept either way, so the answer does not
    /// depend on which alternative was written first.
    /// </summary>
    internal Verdict Or(Verdict other) => new(
        Matched || other.Matched,
        Unreadable || other.Unreadable,
        TooCostly || other.TooCostly);
}

/// <summary>One value inside a member, already compiled and ready to be asked about entries.</summary>
internal interface IQueryValue
{
    Verdict Test(QueryField field, ScmEntry entry);

    /// <summary>
    /// Whether this value only bounds a quantity from one side - <c>&gt;</c>, <c>&gt;=</c>,
    /// <c>&lt;</c>, <c>&lt;=</c>. Asked by <c>QueryParser.Fold</c>, because two bounds written
    /// as two members narrow rather than widen. Every other kind of value answers no.
    /// </summary>
    bool IsOpenComparison => false;
}

internal static class QueryValue
{
    /// <summary>
    /// Whether this field has anything to say about this entry, or the answer would be a guess.
    ///
    /// <b>Two states, and until 2026-08-03 only one of them was asked about.</b> Refused is
    /// obvious. Not read is the quieter one: the field has no value because nobody looked, so
    /// saying "no" is a confident answer to a question nobody put to the machine.
    ///
    /// It mattered in a way nothing announced. With <c>--follow-network</c> off, an entry whose
    /// binary sits on a share never gets its signature read - so <c>publisher:microsoft</c>
    /// dropped it from the result and did not count it among the entries the answer is unsure
    /// about. A shorter list that looks complete, which is rule 8 of CLAUDE.md in the field
    /// where it costs most.
    ///
    /// The enumeration fields have always done this - <c>FieldSymbols.Nothing</c> carries
    /// "incomplete" for exactly this case - so <c>signed:no</c> reported itself as partial while
    /// <c>publisher:x</c> beside it did not. The three value kinds here now agree with them - and
    /// the reserved words did not until 2026-10-05, which is the story at <see cref="OutcomeValue"/>.
    /// </summary>
    internal static bool Unanswerable(QueryField field, ScmEntry entry) =>
        field.OutcomeOf(entry) is ReadOutcome.Denied or ReadOutcome.NotRead;
}

/// <summary>
/// Two of the reserved words: <c>none</c> and <c>any</c>. They ask about the reading itself
/// rather than about the value, which is how the four states a field can be in stay
/// expressible instead of only the two that have a value.
///
/// <b>UNTIL 2026-10-05 THESE WERE THE ONE KIND OF VALUE THAT DID NOT AGREE WITH THE OTHERS</b>,
/// and the comment on <see cref="QueryValue.Unanswerable"/> claimed they all did. On an entry
/// whose field was refused or never read, <c>trigger:any</c> said a confident no - so the entry
/// left the result without being counted among the ones the answer is unsure about, and
/// <c>!trigger:any</c> kept it with full confidence (stability report Q-4). Whether a field nobody
/// could read is empty is exactly the question nobody could answer, so it is answered as unsure,
/// the way every other value answers it. Owner's decision, 2026-09-30.
/// </summary>
internal sealed class OutcomeValue(ReadOutcome wanted) : IQueryValue
{
    public Verdict Test(QueryField field, ScmEntry entry) =>
        QueryValue.Unanswerable(field, entry)
            ? Verdict.CouldNotRead
            : Verdict.Of(field.OutcomeOf(entry) == wanted);
}

/// <summary>
/// The third reserved word, <c>?</c>: the field has no answer - refused, or never read.
///
/// <b>Refused only, until 2026-10-05.</b> A field nobody read - a trigger list on a listing that
/// never asked for one, a signature on a share with <c>--follow-network</c> off - answered no to
/// <c>none</c>, to <c>any</c> and to <c>?</c> at once, so the window's chip for "could not check"
/// left those entries out and nothing in the language could find them. Owner's decision,
/// 2026-09-30: both states that have no answer are one question, and three reserved words stay
/// three.
///
/// <b>Never unsure itself</b>, because whether a field was answered is the one thing about it
/// that is always known.
/// </summary>
internal sealed class UnansweredValue : IQueryValue
{
    public Verdict Test(QueryField field, ScmEntry entry) =>
        Verdict.Of(QueryValue.Unanswerable(field, entry));
}

internal enum TextOperator
{
    Contains,
    Exact,
    Pattern
}

/// <summary>
/// A value compared against text.
///
/// Contains is the default, because a person typing into a search box is typing a
/// fragment. Case is always folded: Windows treats service names that way, and the same
/// service is written differently in different places, so honouring case would make
/// results depend on somebody else's typing.
/// </summary>
internal sealed class TextValue(TextOperator operation, string text, Regex? pattern) : IQueryValue
{
    public Verdict Test(QueryField field, ScmEntry entry)
    {
        if (QueryValue.Unanswerable(field, entry))
        {
            return Verdict.CouldNotRead;
        }

        if (field.TextsOf is not null)
        {
            return Across(field.TextsOf(entry));
        }

        return Against(field.TextOf!(entry));
    }

    /// <summary>
    /// A field holding several values. Any one of them matching is a match, and a value
    /// running out of time is carried out whole rather than being turned into a no - the
    /// same rule the alternatives inside a member follow.
    /// </summary>
    private Verdict Across(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return Verdict.NoMatch;
        }

        var verdict = Verdict.NoMatch;

        foreach (var value in values)
        {
            verdict = verdict.Or(Against(value));
        }

        return verdict;
    }

    private Verdict Against(string? value)
    {
        if (value is null)
        {
            return Verdict.NoMatch;
        }

        // Both of the plain comparisons work in place. Lowering a copy of every string for
        // every comparison would be the obvious way and would allocate once per entry per
        // member, on a list that is evaluated on every keystroke in the interface.
        if (operation == TextOperator.Contains)
        {
            return Verdict.Of(value.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        if (operation == TextOperator.Exact)
        {
            return Verdict.Of(value.Equals(text, StringComparison.OrdinalIgnoreCase));
        }

        try
        {
            return Verdict.Of(pattern!.IsMatch(value));
        }
        catch (RegexMatchTimeoutException)
        {
            // Only reachable for expressions the linear engine would not take, where the
            // time limit is the net. Letting this escape would end the run with a stack
            // trace, and a quiet no would be worse still: an expression that ran out of
            // time has not answered, and saying so is the whole point of having a limit.
            return Verdict.RanOutOfTime;
        }
    }
}

/// <summary>
/// A value compared against an enumeration, expressed as the symbols it stands for. One
/// spelling can stand for several, which is how <c>type:driver</c> covers both driver kinds.
/// </summary>
internal sealed class SymbolValue(IReadOnlyList<string> wanted) : IQueryValue
{
    public Verdict Test(QueryField field, ScmEntry entry)
    {
        var present = field.SymbolsOf!(entry);

        foreach (var symbol in present.Symbols)
        {
            for (var index = 0; index < wanted.Count; index++)
            {
                if (string.Equals(symbol, wanted[index], StringComparison.Ordinal))
                {
                    return Verdict.Match;
                }
            }
        }

        // Saying no about a field that was only partly read would be a confident answer
        // to a question nobody asked the system.
        return present.Incomplete ? Verdict.CouldNotRead : Verdict.NoMatch;
    }
}

internal enum NumberOperator
{
    Equal,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
    Range
}

internal static class NumberOperators
{
    /// <summary>
    /// Whether this comparison bounds a quantity from one side only. An exact value and a closed
    /// range each pick out values of their own, a bound does not - which is the whole difference
    /// <c>QueryParser.Fold</c> needs.
    /// </summary>
    internal static bool IsOpen(NumberOperator operation) =>
        operation is NumberOperator.Greater
            or NumberOperator.GreaterOrEqual
            or NumberOperator.Less
            or NumberOperator.LessOrEqual;
}

/// <summary>
/// A value compared against a number. The range is closed at both ends, matching how the
/// port ranges in networking tools read, because that is where people have seen this
/// notation before.
/// </summary>
internal sealed class NumberValue(NumberOperator operation, int low, int high) : IQueryValue
{
    public bool IsOpenComparison => NumberOperators.IsOpen(operation);

    public Verdict Test(QueryField field, ScmEntry entry)
    {
        if (QueryValue.Unanswerable(field, entry))
        {
            return Verdict.CouldNotRead;
        }

        var value = field.NumberOf!(entry);

        // A stopped service has no process. pid:>1000 about it is false, not an error:
        // a member that cannot be judged simply does not match and never breaks the query.
        if (value is null)
        {
            return Verdict.NoMatch;
        }

        return Verdict.Of(operation switch
        {
            NumberOperator.Equal => value == low,
            NumberOperator.Greater => value > low,
            NumberOperator.GreaterOrEqual => value >= low,
            NumberOperator.Less => value < low,
            NumberOperator.LessOrEqual => value <= low,
            _ => value >= low && value <= high
        });
    }
}

/// <summary>
/// A value compared against a quantity of bytes.
///
/// Its own class rather than a wider <see cref="NumberValue"/>, because the two are not the
/// same question wearing different units. A process above two gigabytes does not fit in the
/// integer the other one uses, and the operand is written with a unit that has to be read
/// before anything can be compared.
/// </summary>
internal sealed class SizeValue(NumberOperator operation, long low, long high) : IQueryValue
{
    public bool IsOpenComparison => NumberOperators.IsOpen(operation);

    public Verdict Test(QueryField field, ScmEntry entry)
    {
        if (QueryValue.Unanswerable(field, entry))
        {
            return Verdict.CouldNotRead;
        }

        var value = field.SizeOf!(entry);

        // A stopped service has no process and therefore no memory. memory:>500MB about it
        // is false rather than an error, the same as pid:>1000 on the same entry.
        if (value is null)
        {
            return Verdict.NoMatch;
        }

        return Verdict.Of(operation switch
        {
            NumberOperator.Equal => value == low,
            NumberOperator.Greater => value > low,
            NumberOperator.GreaterOrEqual => value >= low,
            NumberOperator.Less => value < low,
            NumberOperator.LessOrEqual => value <= low,
            _ => value >= low && value <= high
        });
    }
}
