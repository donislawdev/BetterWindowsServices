namespace Bws.Cli;

/// <summary>
/// What the start-type verb was asked for, before anything is made of it.
///
/// <b>Two facts in one place because they are one ask</b> - "set it to disabled, and stop it" - and
/// because the record that carries every switch stood at its ceiling of fields the day --stop
/// arrived (2026-09-24). The seam is a subject rather than a count: nothing else on the command line
/// means anything to this verb, and nothing here means anything to any other.
/// </summary>
/// <param name="Word">
/// The startup setting somebody named, as they wrote it. Empty when they named none.
///
/// <b>The word rather than the value, and it is kept that way all the way to the refusal.</b> A word
/// that names no start type has to appear in the sentence that says so - "manuel is not a start
/// type" is an answer, and "that is not a start type" sends somebody back to look at a line they
/// have already read twice. Reading it into a value here would throw away the only half of it worth
/// saying.
/// </param>
/// <param name="AlsoStop">
/// Whether --stop was given: stop the entry the setting leaves running, as a second step of the same
/// plan. Spec C4, the owner's decision of 2026-09-24. Only beside "disabled" - the refusals turn it
/// back anywhere else, because that is the one setting the plan says leaves a running entry running.
/// </param>
internal sealed record StartTypeAsk(string Word, bool AlsoStop)
{
    /// <summary>Nothing asked - every verb but one.</summary>
    internal static StartTypeAsk None { get; } = new(string.Empty, AlsoStop: false);
}
