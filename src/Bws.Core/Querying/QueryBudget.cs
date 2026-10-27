using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Bws.Core.Querying;

/// <summary>
/// How long ONE walk over a listing may spend inside patterns the linear engine would not take - since
/// 2026-10-27, logic report R10, owner's decision "a budget for the whole query".
///
/// <b>Why one value's limit was not enough, measured.</b> Each match on the backtracking engine has fifty
/// milliseconds (<see cref="QueryPatterns"/>), and nothing bounded their sum: on 2026-10-26, elevated, over
/// 804 entries, <c>bws list --query 'display:/(?=.)((\w|\s)+)+#/'</c> took 37.5 s and reported 582 entries
/// it ran out of time on - one run, a showing that it happens rather than a comparison. In the window the
/// same walk runs on its own thread at every keystroke and every tick that moved something.
///
/// <b>Only the backtracking engine is charged.</b> The linear one cannot run away - it was chosen for that
/// - so a walk spending its time there is a walk over many entries, not a pattern costing too much, and a
/// budget on it would refuse ordinary questions on a large machine. Once <see cref="Whole"/> is spent, every
/// further backtracking match in the same walk is not tried at all and answers that it ran out of time, the
/// verdict the language already turns into "never checked" in both interfaces. So a walk costs about this
/// much plus the one match that crossed the line, which has its own fifty milliseconds.
///
/// <b>Measured on the wall clock, like the limit per value</b>, and with the same price: a machine busy with
/// something else spends this budget too. The alternative is processor time, which .NET does not offer per
/// match, and the budget exists to keep a window answering, which is wall clock.
/// </summary>
internal sealed class QueryBudget
{
    /// <summary>All the backtracking one walk may do - the figure the owner chose, "about 200 ms" for the list.</summary>
    internal static readonly TimeSpan Whole = TimeSpan.FromMilliseconds(200);

    private readonly Func<TimeSpan> _ruler;
    private readonly TimeSpan _whole;
    private TimeSpan _spent;

    internal QueryBudget()
        : this(() => Stopwatch.GetElapsedTime(0), Whole)
    {
    }

    /// <param name="ruler">A count that only goes forward - <see cref="IClock.Elapsed"/>'s kind - so a test can hand one in.</param>
    /// <param name="whole">How much backtracking the walk may do.</param>
    internal QueryBudget(Func<TimeSpan> ruler, TimeSpan whole)
    {
        _ruler = ruler;
        _whole = whole;
    }

    /// <summary>
    /// The pattern asked about the value, paid for out of this walk - or not asked at all, once the walk has
    /// nothing left, and then it ran out of time without anybody having checked.
    /// </summary>
    internal Verdict Match(Regex pattern, string value)
    {
        if (_spent >= _whole)
        {
            return Verdict.RanOutOfTime;
        }

        var began = _ruler();

        try
        {
            return Verdict.Of(pattern.IsMatch(value));
        }
        catch (RegexMatchTimeoutException)
        {
            // Only reachable for expressions the linear engine would not take, where the time limit is the
            // net. Letting this escape would end the run with a stack trace, and a quiet no would be worse
            // still: an expression that ran out of time has not answered, and saying so is the whole point.
            return Verdict.RanOutOfTime;
        }
        finally
        {
            _spent += _ruler() - began;
        }
    }
}
