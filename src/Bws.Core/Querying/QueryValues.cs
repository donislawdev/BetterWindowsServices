using System.Globalization;
using System.Text;
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
}

/// <summary>
/// The reserved words: <c>none</c>, <c>any</c> and <c>?</c>. They ask about the reading
/// itself rather than about the value, which is how the four states a field can be in
/// stay expressible instead of only the two that have a value.
/// </summary>
internal sealed class OutcomeValue(ReadOutcome wanted) : IQueryValue
{
    public Verdict Test(QueryField field, ScmEntry entry) =>
        Verdict.Of(field.OutcomeOf(entry) == wanted);
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
        if (field.OutcomeOf(entry) == ReadOutcome.Denied)
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

/// <summary>
/// A value compared against a number. The range is closed at both ends, matching how the
/// port ranges in networking tools read, because that is where people have seen this
/// notation before.
/// </summary>
internal sealed class NumberValue(NumberOperator operation, int low, int high) : IQueryValue
{
    public Verdict Test(QueryField field, ScmEntry entry)
    {
        if (field.OutcomeOf(entry) == ReadOutcome.Denied)
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
    public Verdict Test(QueryField field, ScmEntry entry)
    {
        if (field.OutcomeOf(entry) == ReadOutcome.Denied)
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

/// <summary>
/// Reads a quantity of bytes written with a unit.
///
/// The unit is required, and that is the whole design. <c>memory:&gt;500</c> read as bytes
/// would match every running service on the machine while looking exactly like a filter
/// that worked, and read as megabytes it would be this code guessing at what somebody meant.
/// Refusing it costs one retry and a message naming the forms that work.
/// </summary>
internal static class QuerySizes
{
    /// <summary>
    /// Powers of 1024, because that is what Windows means when it writes MB. Task Manager,
    /// the file properties dialog and <c>Get-Process</c> all divide by 1024, so matching the
    /// disk-drive meaning of the word would put us at odds with everything a person could
    /// check us against.
    /// </summary>
    private static readonly (string Suffix, long Multiplier)[] Units =
    [
        ("KB", 1024L),
        ("MB", 1024L * 1024),
        ("GB", 1024L * 1024 * 1024),
        ("TB", 1024L * 1024 * 1024 * 1024),

        // Last, so that the two-letter suffixes are tried first - otherwise every one of
        // them would match here on its final character and be read as a count of bytes.
        ("B", 1L)
    ];

    internal static bool TryRead(string text, out long bytes)
    {
        bytes = 0;

        foreach (var (suffix, multiplier) in Units)
        {
            if (!text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var number = text[..^suffix.Length];

            if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
            {
                return false;
            }

            // A quantity large enough to overflow is a mistake worth reporting rather than
            // silently wrapping into a small number that matches everything.
            if (quantity > long.MaxValue / multiplier)
            {
                return false;
            }

            bytes = quantity * multiplier;
            return true;
        }

        return false;
    }
}

/// <summary>
/// Builds the regular expressions the language needs, for wildcards and for patterns a
/// person wrote.
/// </summary>
internal static class QueryPatterns
{
    /// <summary>
    /// How long one value may be matched against before the attempt is abandoned.
    ///
    /// Generous on purpose: the job is to stop a hang, not to enforce the evaluation
    /// budget, and a limit tight enough to do the second would fire on patterns that are
    /// merely slow. A single value here is a service name, a display name or an account,
    /// so fifty milliseconds is far more than any sane pattern needs.
    ///
    /// Applied to the linear engine as well, where in principle it cannot be reached.
    /// That is not belt and braces, it earns its place: it is what turns a mistake in the
    /// engine choice into a test that goes red rather than a test run that never ends.
    /// Found by making exactly that mistake on 2026-08-01 - the suite hung instead of
    /// failing, and a hang tells nobody anything.
    /// </summary>
    private static readonly TimeSpan Ceiling = TimeSpan.FromMilliseconds(50);

    private const RegexOptions Shared = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>
    /// Compiles a pattern a person typed.
    ///
    /// Tries the non-backtracking engine first. Patterns here come from whoever is at the
    /// keyboard and run over hundreds of entries on every keystroke, and that engine
    /// cannot be made to run away no matter what it is given, which beats noticing
    /// afterwards that it did. Constructs it does not support - backreferences, lookaround -
    /// fall back to the ordinary engine, where the time limit is the net instead.
    /// </summary>
    internal static bool TryPattern(string pattern, out Regex? compiled, out string? failure)
    {
        try
        {
            compiled = new Regex(pattern, Shared | RegexOptions.NonBacktracking, Ceiling);
            failure = null;
            return true;
        }
        catch (NotSupportedException)
        {
            // Valid expression, just not one the linear engine handles.
        }
        catch (ArgumentException badPattern)
        {
            compiled = null;
            failure = badPattern.Message;
            return false;
        }

        try
        {
            compiled = new Regex(pattern, Shared, Ceiling);
            failure = null;
            return true;
        }
        catch (ArgumentException badPattern)
        {
            compiled = null;
            failure = badPattern.Message;
            return false;
        }
    }

    /// <summary>
    /// Turns a wildcard into a pattern anchored at both ends, because a wildcard describes
    /// the whole value while the plain form already means "contains".
    ///
    /// <b>Returns rather than throws, and until 2026-08-03 it threw.</b> The linear engine
    /// refuses a pattern whose automaton would exceed ten thousand nodes, and a wildcard is
    /// the one shape in this language where a person can reach that by accident: about a
    /// thousand repetitions of <c>*a</c> - a two thousand character paste - is enough.
    /// <c>bws list --query "name:*a*a..."</c> ended with an unhandled
    /// <see cref="NotSupportedException"/>, a stack trace, and exit code <c>0xE0434352</c>,
    /// which is not in the table of exit codes at all. In the window the same text arrives on
    /// the interface thread through a binding, on every keystroke.
    ///
    /// <b>Refused rather than sent to the other engine</b>, which is what <see cref="TryPattern"/>
    /// does with a construct the linear engine cannot express. The two cases are not alike. A
    /// backreference is a thing somebody meant, and the ordinary engine plus the time limit is
    /// the honest way to serve it. A wildcard of this size is a paste accident, and the ordinary
    /// engine would meet it with exactly the backtracking shape - <c>.*a.*a.*a</c> - that runs
    /// away, so the trade would be a crash swapped for a window frozen for as long as the
    /// timeout allows, times every entry in the listing. The linear engine is the promise this
    /// language makes about patterns typed into a search box, and this keeps it.
    /// </summary>
    internal static bool TryWildcard(ScannedText value, out Regex? compiled, out string? failure)
    {
        var pattern = new StringBuilder(value.Length + 8).Append('^');

        for (var index = 0; index < value.Length; index++)
        {
            var character = value.Text[index];

            if (value.IsSpecial(index, '*'))
            {
                pattern.Append(".*");
            }
            else if (value.IsSpecial(index, '?'))
            {
                pattern.Append('.');
            }
            else
            {
                pattern.Append(Regex.Escape(character.ToString()));
            }
        }

        try
        {
            // The time limit is here for the same reason it is on every other pattern in this
            // file, and the reason is written at Ceiling: it is what turns a mistake in the
            // choice of engine into a red test rather than a run that never ends. This call
            // used to be the one place in the language that left it off.
            compiled = new Regex(pattern.Append('$').ToString(), Shared | RegexOptions.NonBacktracking, Ceiling);
            failure = null;

            return true;
        }
        catch (Exception refused) when (refused is NotSupportedException or ArgumentException)
        {
            compiled = null;
            failure = refused.Message;

            return false;
        }
    }
}
