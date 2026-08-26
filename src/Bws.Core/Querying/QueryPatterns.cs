using System.Text;
using System.Text.RegularExpressions;

namespace Bws.Core.Querying;

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
    /// <b>THE BACKTRACKING FALLBACK ONLY, SINCE 2026-08-26. Until that day it was on the
    /// linear engine too, above a comment saying it could not be reached there - and it was
    /// reached.</b> .NET measures this against the WALL CLOCK rather than against processor
    /// time, so a pause the match did not cause is charged to the pattern. Two tests caught
    /// it and both blamed the query rather than the machine: an expression that is perfectly
    /// fine came back as "ran out of time".
    ///
    /// That sentence is the part that matters, because it is not only a test. A person
    /// filtering a listing on a busy machine was told their expression was too costly, which
    /// reads as "your filter is wrong" when it means "something else was running".
    ///
    /// <b>The linear engine does not need a limit, and not needing one is the entire reason
    /// it was chosen</b> - no input can make it run away. Measured on 2026-08-26 with the
    /// classic explosion, <c>(a+)+b</c> against forty a characters: the ordinary engine timed
    /// out five times out of five at 59-66 ms, the linear engine answered in 0.0-1.0 ms.
    ///
    /// <b>What the old arrangement bought, and how it is paid for now.</b> It turned a mistake
    /// in the engine choice into a red test rather than a run that never ends, found by making
    /// exactly that mistake on 2026-08-01. That is held directly now, by a test that asks which
    /// engine a pattern was given to. A claim about the choice beats a stopwatch that happens
    /// to notice the choice, because the stopwatch also noticed the machine.
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
            // InfiniteMatchTimeout written out rather than taking the two-argument overload,
            // because a missing limit reads as an oversight and this one is a decision. The
            // reason is at Ceiling: a wall-clock limit on an engine that cannot run away buys
            // nothing and costs a wrong answer when the machine is busy.
            compiled = new Regex(
                pattern,
                Shared | RegexOptions.NonBacktracking,
                Regex.InfiniteMatchTimeout);

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
            // NO TIME LIMIT SINCE 2026-08-26, AND IT IS THE SAME DECISION AS THE ONE AT Ceiling.
            // This is the linear engine, which cannot run away, and a wall-clock limit on it
            // charges the pattern for pauses it did not cause.
            //
            // THIS IS THE PATH WHERE THAT WOULD BE SEEN MOST, which is why it is called out
            // here rather than left to the shared reason. A wildcard is typed into a search box
            // and evaluated over every entry on every keystroke, so a limit that fires once in
            // a while fires here first - and the person reading the result is told their filter
            // was too costly while they are still typing it.
            //
            // The limit lived here from 2026-08-03 to 2026-08-26 and the argument for adding it
            // was sound: this call was the one place in the language that left it off, and an
            // inconsistency in a safety net is worth removing. What changed is which way the
            // consistency runs, not whether there should be one.
            //
            // SINGLELINE SINCE 2026-08-12, AND WITHOUT IT A WILDCARD COULD NEVER MATCH A VALUE
            // WITH A LINE BREAK IN IT. This pattern is anchored at both ends, and by default a dot
            // does not cross a newline - so `*printer*` against a two-line value asks the dot after
            // "printer" to reach the end of a string it cannot get to, and the answer is silently
            // NOTHING. Found by the description arriving on 2026-08-12: it is the first field a
            // value with a line break can come out of, two entries of 819 on this machine have one,
            // and until then no field could reach this at all.
            //
            // ONLY the wildcard, deliberately. A wildcard is this language's own idea and `*` means
            // "any characters", newline included. A user's own expression between slashes is
            // ordinary regular expression syntax, where a dot not matching a newline is what
            // everybody who writes one expects - changing that would rewrite the meaning of
            // patterns people have already written.
            compiled = new Regex(
                pattern.Append('$').ToString(),
                Shared | RegexOptions.NonBacktracking | RegexOptions.Singleline,
                Regex.InfiniteMatchTimeout);
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
