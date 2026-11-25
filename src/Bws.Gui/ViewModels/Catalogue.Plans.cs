namespace Bws.Gui.ViewModels;

/// <summary>
/// The plan sheet on the catalogue (GUI rule 4) - the sheet, its foot, the heavy ask, and since 2026-11-24 the
/// recovery form at its top (backlog 555 part a2). A file of its own because Catalogue.Views.cs stands at the
/// length the size guard counts as near its ceiling.
/// </summary>
public static partial class Catalogue
{
    /// <summary>
    /// The plan sheet's views - the sheet, its foot, the heavy ask at the foot, and the recovery form.
    ///
    /// <b>Out of <see cref="Views"/></b>, which stands at the share of its ceiling the shape guard counts as
    /// near. One subject, so one place - and the next state of the sheet lands here rather than in the list of
    /// everything, as the recovery form did on 2026-11-24.
    /// </summary>
    private static Built[] PlanViews(Prepared ready) =>
    [
        View(() => new PlanView(),
            data: ready.WithPlan.Planned, wrong: ready.WithRefusedPlan.Planned, extreme: ready.WithBulkPlan.Planned),

        View(() => new PlanFooter(),
            data: ready.WithPlan.Planned, wrong: ready.WithRefusedPlan.Planned, extreme: ready.WithBulkPlan.Planned),

        // The same foot while a run goes, since 2026-09-30 (backlog 497): data is a run under way,
        // loading is the same run interrupted and waiting on its step, extreme is the second level
        // offered with the window waiting to close. Catalogue.Runs.cs builds them.
        View(() => new PlanFooter(), key: nameof(PlanFooter) + " while a run goes",
            data: ready.Runs.Running.Planned, loading: ready.Runs.Interrupted.Planned, extreme: ready.Runs.Abandonable.Planned),

        // The whole sheet once the run has ended, since 2026-11-16: data is every entry where it was
        // asked to go - the green result and the button that closes the sheet - wrong is the step
        // refused, in amber.
        View(() => new PlanView(), key: nameof(PlanView) + " after a run",
            data: ready.Runs.Arrived.Planned, wrong: ready.Runs.FellShort.Planned),

        // The heavy ask on its own, since 2026-09-29 (backlog 475): data is one critical entry
        // asking for its name, wrong is a selection holding it, refused with no box.
        View(() => new PlanConfirmation(),
            data: ready.AskingForName.Planned, wrong: ready.RefusingSelection.Planned),

        // The recovery form at the top of a recovery sheet (backlog 555 a2), in the states RecoveryFormSpecimens
        // builds: two parts edited, nothing set, two boxes that do not read, three entries that differ.
        View(() => new RecoveryFormView(),
            data: RecoveryFormSpecimens.Edited(), empty: RecoveryFormSpecimens.Empty(),
            wrong: RecoveryFormSpecimens.Mistyped(), extreme: RecoveryFormSpecimens.Differing(LongDescription))
    ];
}
