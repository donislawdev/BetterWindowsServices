namespace Bws.Gui.ViewModels;

/// <summary>
/// What a finished run came to, as the sheet shows it - the look of the result at the top, and the
/// button under the plan that closes the sheet once there is nothing left to run.
///
/// <b>Its own file since 2026-11-16, when the owner said the finished sheet did not read as done.</b>
/// Until then the result was one sentence in the same weight as "Nothing has been done yet", under a
/// grey copy of the button that had just run - so the moment that matters most, the machine having
/// changed, looked like the moment before anything happened. The words and the outcome are read by
/// <see cref="FinishedRun"/>.
/// </summary>
public sealed partial class Planned
{
    /// <summary>
    /// Whether the sheet holds a result, so the button that ran the plan gives way to the one that
    /// closes the sheet.
    /// </summary>
    public bool HasResult => Showing && _run is not null;

    /// <summary>How the run came out, for the rule and the mark of the result block.</summary>
    public PlanOutcome Outcome => !Showing || _run is not { } run
        ? PlanOutcome.None
        : FinishedRun.Read(run, Named(run.Plan)).Outcome;
}
