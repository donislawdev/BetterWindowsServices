using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The foot of the plan sheet while a run is going - the three states the catalogue could not show
/// until 2026-09-30.
///
/// <b>Why they are here at all.</b> Until that day the foot of the sheet appeared in the catalogue
/// only BEFORE a run, and the two buttons that exist only during one - Interrupt, and since that day
/// the second way of stopping a run (backlog 497) - were on no sheet anybody could look at. GUI rule 4
/// asks for every component in every state, and these are states nothing but a run reaches.
///
/// <b>Its own file, because Catalogue.Views.cs stands a score of lines under the ceiling on file
/// length</b>, and this is one subject: a sheet with a run under way, told what a runner would tell
/// it, and never a runner. Nothing here can change a machine - there is no writer in this assembly
/// outside Carrying.cs, and PlanOnlyGuards holds that.
/// </summary>
public static partial class Catalogue
{
    /// <summary>
    /// A stop of Spooler under way: running, interrupted and waiting on its step, and interrupted long
    /// enough for the second level to be offered.
    /// </summary>
    private static async Task<(MainViewModel Running, MainViewModel Interrupted, MainViewModel Abandonable)> PrepareRunsAsync()
    {
        var models = new List<MainViewModel>();

        for (var state = 0; state < 3; state++)
        {
            var clock = new Wound();
            var model = new MainViewModel(new Frozen(Specimens()), clock) { Planned = new Planned { Clock = clock } };

            await model.LoadAsync().ConfigureAwait(false);
            await ShowPlanAsync(model, ActionKind.Stop, "Spooler").ConfigureAwait(false);

            model.Planned.Starting();
            model.Planned.Underway.Announce(model.Planned.Plan!.Steps.First(), 1);

            // The clock on the line reads as a step that has been going a while - which is what a
            // person sees on a sheet worth interrupting.
            clock.Wait(TimeSpan.FromSeconds(42));
            model.Planned.Underway.Tick();

            if (state > 0)
            {
                model.Planned.Underway.Interrupt(closing: state == 2);
            }

            if (state == 2)
            {
                clock.Wait(Underway.ArmsAfter);
                model.Planned.Underway.Tick();
            }

            models.Add(model);
        }

        return (models[0], models[1], models[2]);
    }

    /// <summary>
    /// A clock that waits by moving on - the one way to show a state a sheet reaches only after
    /// three seconds, without the sheet taking three seconds to build.
    /// </summary>
    private sealed class Wound : IClock
    {
        private TimeSpan _ahead;

        public DateTimeOffset Now => DateTimeOffset.UtcNow + _ahead;

        public TimeSpan Elapsed => System.Diagnostics.Stopwatch.GetElapsedTime(0) + _ahead;

        public void Wait(TimeSpan duration) => _ahead += duration;
    }
}
