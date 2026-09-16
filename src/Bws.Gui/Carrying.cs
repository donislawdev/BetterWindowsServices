using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui;

/// <summary>
/// The one place in this window that builds something able to change a machine.
///
/// <b>ITS OWN FILE AND ITS OWN NAME, AND BOTH WERE DECIDED BY PlanOnlyGuards RATHER THAN BY
/// TASTE.</b> That guard holds rule 1 of the project's untouchable rules - every write goes through
/// a plan - by listing the files allowed to construct the writer, and adding a file to that list is
/// a conversation with the owner rather than an edit. This is the second entry ever, added on
/// 2026-08-18 on the owner's decision, and it exists because the window learned to carry a plan out.
///
/// <b>Not called Execution, and that is not a preference either.</b> The command line's composition
/// point has that name, and the guard identifies files by their BARE NAME - so a second Execution.cs
/// would quietly overwrite the command line's entry in its list, leaving one permission covering two
/// files and the guard's second assertion unable to notice if either stopped needing it. Found by
/// reading the guard rather than by it going red, because it would not have gone red.
///
/// <b>What it does NOT do is the interesting half.</b> It decides nothing about the plan, adds no
/// step, and never asks the manager anything of its own. It builds a runner, hands it the plans the
/// preview showed, and hands back what came of them.
/// </summary>
internal static class Carrying
{
    // THE LONGEST ANY ONE STEP IS WATCHED FOR IS NO LONGER A NUMBER OF THIS WINDOW'S OWN. A sixty
    // stood here from 2026-08-18 to 2026-09-15, copied from the command line so that "it worked
    // from the terminal" could never be a true sentence about the same entry - and a copy agrees
    // only until one side is edited. Both interfaces read StepCeiling.Default in the core now, and
    // the box on the plan sheet reads the same rule for what a person types (Planned.Waiting).

    /// <summary>
    /// Carries a whole selection out, off the thread the window draws on.
    ///
    /// <b>OFF THE UI THREAD BECAUSE OF A MEASUREMENT, NOT A HABIT, and the measurement points the
    /// opposite way to the one that decided how a plan is BUILT.</b> Building a plan for twenty
    /// selected entries was measured at 37-40 ms on this machine, so it stayed on the calling thread
    /// and no background machinery was built - the lesson of backlog 21. Carrying one out is a
    /// different question with different evidence: a single StartService for a service that never
    /// reports itself took 30 375-30 450 ms across three runs on Windows Server 2025, recorded at
    /// PlanRun.OutranTheCeiling. Half a minute on one step, multiplied by the steps of a selection,
    /// is a window Windows paints "not responding" over.
    ///
    /// The shape is the one this window already uses twice for reading - Task.Run awaited by a
    /// caller that comes back to the UI thread - so nothing new is introduced to lose an exception
    /// in, which is what BackgroundWorkGuards is about.
    /// </summary>
    /// <param name="stopping">
    /// Asks the run to stop going forward. The steps that give back what earlier steps took are
    /// still carried out, which is the whole difference between this and closing the window.
    ///
    /// Only the first of the command line's two levels is offered. The second - stop and put nothing
    /// back - exists there because the alternative was somebody killing the process, and a killed
    /// process reports nothing. A window has no such person: closing it asks for this one and waits.
    /// </param>
    /// <param name="announce">
    /// Called before each step that is really attempted, with its place across the whole selection.
    ///
    /// <b>It arrives on the background thread, and whoever passes it in is responsible for getting
    /// back.</b> Said here because the failure is silent: touching a bound property from here works
    /// often enough to pass a test and throws on a collection.
    /// </param>
    internal static Task<BulkRun> Out(
        BulkPlan plan, TimeSpan ceiling, CancellationToken stopping, Action<PlanStep, int> announce) =>
        Task.Run(
            () =>
            {
                // THE WRITER, BUILT AND HANDED STRAIGHT TO A PLAN RUNNER IN ONE STATEMENT. That is
                // not a formatting choice - it is what PlanOnlyGuards asserts, and a runner built
                // from a manager handed in from somewhere else would put this construction in a
                // caller with no plan beside it, which is the first step towards a write that skips
                // one.
                var runner = new BulkRunner(new PlanRunner(new WindowsScmControl(), new SystemClock()));

                return runner.Run(plan, ceiling, stopping, abandonment: default, starting: announce);
            },
            CancellationToken.None);
}
