using System.Globalization;

namespace Bws.Core.Planning;

/// <summary>
/// How long one step of a plan may be watched for - the number both interfaces ask a person for,
/// and the one rule for reading it.
///
/// <b>ONE PLACE SINCE 2026-09-15, AND UNTIL THEN THERE WERE TWO.</b> The terminal read
/// <c>--timeout</c> in <c>Arguments.Seconds</c> and the window read its "Wait up to" box in the
/// setter of <c>Planned.Waiting</c>, each with its own sixty and its own idea of what a number of
/// seconds is. They agreed by copying, which holds until the day one of them is edited. The owner's
/// decision for the window's box was "the same rule as the terminal, no ceiling" - and the cheapest
/// way to make two interfaces keep one rule is for there to be one function. `docs/11` 4.4 is the
/// same argument about queries.
///
/// <b>Whole seconds and at least one, nothing else.</b> "30s" means thirty seconds to whoever wrote
/// it, and reading it as sixty because the spelling was not understood is the quiet substitution
/// that turns up in a runbook months later - the terminal's own comment, kept. There is no ceiling:
/// a service somebody knows takes ten minutes is given ten minutes, and the report says where the
/// entry was left if it takes longer.
///
/// <b>Invariant culture and digits only, on purpose.</b> A timeout is typed by whoever wrote the
/// runbook or is sitting at the window, and a number that means sixty on one machine and nothing on
/// another because of a decimal separator or a thousands mark is what rule 3 of the untouchable
/// rules exists to stop. Whitespace is refused here too, so that the rule itself has no leniency in
/// it - a caller that wants to forgive a typed space trims before asking, and says so where it does.
/// </summary>
public static class StepCeiling
{
    /// <summary>
    /// A minute, which is what both interfaces use when nobody says otherwise - `E1` of the
    /// specification uses the number in its own example. It is a cap on the watching rather than a
    /// deadline: an entry that keeps reporting progress is given the time it asks for, and this
    /// only stops a plan sitting on a screen forever when the entry never finishes what it keeps
    /// saying it is doing.
    /// </summary>
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The number of seconds a piece of text names, or null when it names none.
    ///
    /// <b>Null rather than a default standing in</b>, so that every caller has to decide what to do
    /// with text that is not a number - the terminal refuses the command, the window greys the
    /// button and says why under the box. Neither may run with sixty because somebody typed
    /// something else.
    /// </summary>
    public static int? Seconds(string? typed) =>
        typed is not null
        && int.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
        && seconds >= 1
            ? seconds
            : null;
}
