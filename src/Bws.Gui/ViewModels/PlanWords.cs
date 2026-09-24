using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Everything the plan panel SAYS about one value from the core, in one place.
///
/// <b>Its own file since 2026-08-19, and the size ratchet is what asked - for the fourth time in
/// this window and the fourth time pointing at a real seam.</b> <see cref="Planned"/> is about the
/// three states a person sees: nothing done, doing it, done. This is about turning one warning, one
/// refusal, one failed step or one verb into a sentence, and it grew by half when the panel learned
/// to report a run.
///
/// <b>The core carries no words at all and that is deliberate rather than an omission.</b> A warning
/// holds a kind and the facts, because the same warning reads differently in a window and in a
/// terminal - so the sentences live beside the interface that says them, and this is that place for
/// the window.
///
/// <b>EVERY KEY IS INSIDE ITS OWN Texts.Of CALL, and that is not a style rule.</b> A key travelling
/// as the value of an expression is invisible to TextKeyGuards, so a switch choosing a key and one
/// call around it produces strings that never reach a screen and nothing goes red. This project has
/// the lesson from ListState.Say and walked into it again on 2026-08-18.
/// </summary>
internal static class PlanWords
{
    /// <summary>
    /// The verb as a person reads it, with the key INSIDE each call.
    ///
    /// <b>Written first as one Texts.Of around a switch that chose the key, and TextKeyGuards
    /// reddened - correctly.</b> A key travelling as the value of an expression is invisible to it, so
    /// all three came back as strings that never reach a screen. This project has the lesson recorded
    /// already, from ListState.Say, and the recorded answer is this one rather than a wider pattern: a
    /// key visible where it is chosen is better for a reader too.
    /// </summary>
    /// <b>THE DISCARD USED TO MEAN RESTARTING, AND A PROBE CAUGHT IT ON A LIVE WINDOW ON
    /// 2026-08-25.</b> The first run of the start type menu put "What restarting Adobe Acrobat
    /// Update Service would do" over a step reading "set AdobeARMservice to Disabled" - the title of
    /// the panel, which is the line somebody reads before changing a machine, describing an
    /// operation nobody asked for. Every named kind has its own arm now and an unnamed one refuses.
    internal static string Doing(ActionKind kind) => kind switch
    {
        ActionKind.Stop => Texts.Of("gui.plan.doing.stop"),
        ActionKind.Start => Texts.Of("gui.plan.doing.start"),
        ActionKind.Restart => Texts.Of("gui.plan.doing.restart"),
        ActionKind.SetStartType => Texts.Of("gui.plan.doing.setStartType"),
        ActionKind.ForceStop => Texts.Of("gui.plan.doing.forceStop"),
        ActionKind.ForceRestart => Texts.Of("gui.plan.doing.forceRestart"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind, Bws.Core.Planning.EquivalentCommand.Unhandled)
    };

    /// <summary>Why a step is there, with the key inside each call for the reason above.</summary>
    internal static string Reason(StepReason reason) => reason switch
    {
        StepReason.Requested => Texts.Of("gui.plan.reason.requested"),
        StepReason.Cascade => Texts.Of("gui.plan.reason.cascade"),
        StepReason.SharesTheProcess => Texts.Of("gui.plan.reason.sharesTheProcess"),

        // NAMED RATHER THAN LEFT TO THE DISCARD, WHICH USED TO SAY "put back". A step that
        // ends a process is the opposite of one that gives something back, and the discard
        // would have labelled the most dangerous line in the panel with the gentlest word
        // this file has.
        StepReason.Escalation => Texts.Of("gui.plan.reason.escalation"),

        _ => Texts.Of("gui.plan.reason.restore")
    };

    /// <summary>
    /// A warning in words. The kinds come from the core and the sentences belong here.
    ///
    /// <b>Written out rather than composed from the name of the value</b>, for the reason the command
    /// line gives about the same six: flattening a name to lower case works for as long as every one
    /// is a single word and then quietly asks for a key nobody wrote.
    ///
    /// <b>TWO KEYS APIECE WHEREVER A SENTENCE COUNTS SOMETHING OR POINTS AT A GROUP, ADDED
    /// 2026-08-19.</b> The command line has had exactly this pair since it learned to warn, and its
    /// own comment says why - a warning reading "1 other entries" spends the trust the warning
    /// needs. These sentences were written later, from the same facts, and arrived with the plural
    /// half only. Nothing went red, because no guard in this project reads prose.
    ///
    /// <b>The pair is spelled out per branch rather than through a helper that appends ".one" or
    /// ".many", which is what the command line does.</b> That helper cannot live here: a key built
    /// as an expression is invisible to TextKeyGuards, so both halves would be reported as text no
    /// screen ever shows, and the missing one would render on screen as its own key. The head of
    /// this file carries the same lesson from Doing, one switch earlier.
    /// </summary>
    internal static string Describe(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.Cascade => warning.Related.Count == 1
            ? Texts.Of(
                "gui.plan.warning.cascade.one", warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of(
                "gui.plan.warning.cascade.many", warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.DependentsInTheWay => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.inTheWay.one", warning.ServiceName, Listed(warning.Related))
            : Texts.Of("gui.plan.warning.inTheWay.many", warning.ServiceName, Listed(warning.Related)),

        PlanWarningKind.SharedProcess => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.sharedProcess.one", warning.ServiceName, Listed(warning.Related))
            : Texts.Of("gui.plan.warning.sharedProcess.many", warning.ServiceName, Listed(warning.Related)),

        PlanWarningKind.ReturnsAfterReboot => Texts.Of("gui.plan.warning.returnsAfterReboot", warning.ServiceName),

        PlanWarningKind.CascadeUnreadable => Texts.Of("gui.plan.warning.cascadeUnreadable", warning.ServiceName),

        // THE LITERAL SITS INSIDE Texts.Of RATHER THAN IN A TERNARY HANDED TO IT, and the first
        // attempt did the second - TextKeyGuards found both halves and was right to. Its patterns
        // read the argument of a call, so a key chosen one line earlier is a key nobody can find by
        // searching for it, which is the same failure that file records against a key assembled at
        // run time. The five arms above are all written this way and now so is this one.
        PlanWarningKind.DoesNotAcceptStop => warning.Related.Count == 1
            ? Texts.Of(
                "gui.plan.warning.doesNotAcceptStop.one",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of(
                "gui.plan.warning.doesNotAcceptStop.many",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.TerminationTakesWithIt => warning.Related.Count == 1
            ? Texts.Of(
                "gui.plan.warning.takesWithIt.one",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of(
                "gui.plan.warning.takesWithIt.many",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.CriticalService => warning.Related.Count == 1
            ? Texts.Of(
                "gui.plan.warning.critical.one",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of(
                "gui.plan.warning.critical.many",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        // SAME NAMES, DIFFERENT WHEN - see the terminal's own arm for the whole of the argument.
        PlanWarningKind.CriticalStartType => warning.Related.Count == 1
            ? Texts.Of(
                "gui.plan.warning.criticalStartType.one",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of(
                "gui.plan.warning.criticalStartType.many",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.AlreadyThere => Texts.Of("gui.plan.warning.alreadyThere", warning.ServiceName),

        // The offer to stop it too is not in this sentence - it stands under it as a button, which
        // PlanWarningLine decides. The terminal's sentence names --stop instead, having no button.
        PlanWarningKind.KeepsRunning => Texts.Of("gui.plan.warning.keepsRunning", warning.ServiceName),

        PlanWarningKind.StartsAtNextBoot => Texts.Of("gui.plan.warning.startsAtNextBoot", warning.ServiceName),

        // NAMED ARMS AND A REFUSAL, SINCE 2026-09-06, AND THE WILDCARD THAT WAS HERE IS WHY. Every
        // kind but one used to fall through to "is already in that state, so nothing would change" -
        // a warning added without a sentence would have said something confident and wrong about a
        // machine rather than nothing at all. The terminal's own switch had the same shape and was
        // changed the same day.
        _ => throw new ArgumentOutOfRangeException(
            nameof(warning), warning.Kind, EquivalentCommand.Unhandled)
    };

    /// <summary>
    /// A refusal in words, with the entry it belongs to named first.
    ///
    /// <b>The same singular and plural pair as the warnings above, and for the same reason.</b> A
    /// refusal is read by somebody deciding whether to press, so a sentence pointing at "these
    /// drivers" when there is one of them is a sentence they have to check against the list.
    ///
    /// <b>NAMED ARMS AND A REFUSAL SINCE 2026-09-07, AND THE WILDCARD THAT WAS HERE HAD ALREADY
    /// SHIPPED A FALSE SENTENCE ON THE OTHER SURFACE.</b> The two refusals a forcing plan can
    /// produce arrived in the core on 2026-09-06 with no words on either interface, and both
    /// switches ended in a discard rather than a throw - so instead of saying nothing they each
    /// said the last thing they knew how to say. Measured rather than reasoned:
    /// <c>bws kill ALG --dry-run</c> answered <i>"ALG is a driver. This tool shows drivers but does
    /// not start or stop them."</i> about a WIN32_OWN_PROCESS service that was merely stopped,
    /// confirmed against <c>sc qc ALG</c>. This switch was one step behind the same fault: it would
    /// have reached a window the moment the window could build such a plan, wearing the sentence
    /// about a disabled entry that cannot come back.
    ///
    /// <b>A discard that PICKS one of the remaining kinds is worse than one that says nothing</b>,
    /// which is the lesson the warning switch above recorded a day earlier and this one did not
    /// get. Nothing went red either time, because no guard asked whether two kinds shared a
    /// sentence - <c>PluralGuards</c> asks only whether a key is missing, and a wildcard is never
    /// missing.
    /// </summary>
    internal static string Describe(PlanProblem problem) => problem.Kind switch
    {
        // SPELLED .one SINCE 2026-08-25, WHEN THESE TWO GAINED A PLURAL. Both used to be the only
        // sentence their kind had, so neither carried a suffix - and a lone .many beside a bare key
        // is the shape the plural guard is written to refuse, because a missing half renders on
        // screen as its own key and nothing else in this project reports that.
        PlanProblemKind.UnknownService =>
            Texts.Of("gui.plan.problem.unknownService.one", problem.ServiceName),

        PlanProblemKind.NotOperable => Texts.Of("gui.plan.problem.notOperable.one", problem.ServiceName),

        PlanProblemKind.CascadeNotOperable => problem.Related.Count == 1
            ? Texts.Of("gui.plan.problem.cascadeNotOperable.one", problem.ServiceName, Listed(problem.Related))
            : Texts.Of("gui.plan.problem.cascadeNotOperable.many", problem.ServiceName, Listed(problem.Related)),

        PlanProblemKind.CannotComeBack => problem.Related.Count == 1
            ? Texts.Of("gui.plan.problem.cannotComeBack.one", problem.ServiceName, Listed(problem.Related))
            : Texts.Of("gui.plan.problem.cannotComeBack.many", problem.ServiceName, Listed(problem.Related)),

        // NO COUNT AND NO PLURAL ON EITHER OF THE NEXT TWO, and that is a fact about them rather
        // than an omission. Both speak about the one entry somebody named - there is no list to
        // grow, so there is no second sentence for a second length.
        PlanProblemKind.NoProcessToEnd =>
            Texts.Of("gui.plan.problem.noProcessToEnd", problem.ServiceName),

        // Two keys for one reason, and the second is not defensive padding - see the same switch in
        // the command line's PlanText for the argument. A refusal from the operating system carries
        // its words, and one from anywhere else must not promise them.
        PlanProblemKind.ProcessCannotBeEnded => problem.Error is { } said
            ? Texts.Of(
                "gui.plan.problem.processCannotBeEnded",
                problem.ServiceName, problem.ProcessId, said, problem.ErrorCode)
            : Texts.Of(
                "gui.plan.problem.processCannotBeEnded.plain",
                problem.ServiceName, problem.ProcessId),

        PlanProblemKind.CascadeUnreadable =>
            Texts.Of("gui.plan.problem.cascadeUnreadable", problem.ServiceName),

        // The group is named because it is the one fact somebody could act on.
        PlanProblemKind.CannotStartLate =>
            Texts.Of("gui.plan.problem.cannotStartLate", problem.ServiceName, Listed(problem.Related)),

        _ => throw new ArgumentOutOfRangeException(
            nameof(problem), problem.Kind, EquivalentCommand.Unhandled)
    };

    internal static string Listed(IReadOnlyList<string> names) => string.Join(", ", names);

    /// <summary>
    /// Every refusal in a plan as a person reads them, with the ones that say the same thing
    /// gathered into one sentence and a count.
    ///
    /// <b>SELECTING FOUR HUNDRED DRIVERS USED TO PRODUCE FOUR HUNDRED SENTENCES DIFFERING ONLY BY A
    /// NAME, and it is a fault the scope bar created.</b> A refusal belongs to its own entry and the
    /// rest of the selection carries on - the owner's decision of 2026-08-18, and the right one -
    /// but while drivers were hidden behind a query, picking them by the hundred was not something
    /// anybody could do by accident. Backlog 217.
    ///
    /// <b>Only the kinds whose sentence is a name and one fixed reason.</b> A cascade refusal and an
    /// entry that could not come back both carry a list of OTHER entries, so two of them are two
    /// different facts however alike they read. Folding those together would take away the part that
    /// says what exactly was refused, which is rule 8 of the untouchable list.
    ///
    /// <b>THE NAMES ARE ALL STILL THERE, in one sentence rather than in four hundred.</b> The
    /// complaint is the reason repeated four hundred times, not the names - and a count with the
    /// names after it is the shape this panel already uses for the entries nobody picked. A ceiling
    /// with "and 400 more" behind it was the other candidate and is not built: it hides part of an
    /// answer to make a screen tidier, and the screen belongs to somebody about to change a machine.
    ///
    /// The order is where each group first appeared, so the panel still reads in the order somebody
    /// picked things rather than in an order this method invented.
    /// </summary>
    internal static IReadOnlyList<string> Describe(IReadOnlyList<PlanProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        return
        [
            .. problems
                .Select((problem, at) => (problem, at))
                // The kinds that gather share a key, and every other refusal gets one of its own so
                // it comes out untouched. The prefix is what keeps a position from ever colliding
                // with the name of a kind.
                .GroupBy(one => SaysOnlyAName(one.problem.Kind)
                    ? one.problem.Kind.ToString()
                    : $"on its own {one.at}")
                .OrderBy(group => group.Min(one => one.at))
                .Select(group => group.Count() == 1
                    ? Describe(group.First().problem)
                    : Gathered(group.First().problem.Kind, [.. group.Select(one => one.problem.ServiceName)]))
        ];
    }

    /// <summary>
    /// Whether every refusal of this kind says a name and then the same thing.
    ///
    /// <b>Named as a question rather than read off the presence of a Related list</b>, because those
    /// two are not the same question and would drift the day a kind carries a list it does not put
    /// in its sentence.
    /// </summary>
    private static bool SaysOnlyAName(PlanProblemKind kind) =>
        kind is PlanProblemKind.UnknownService or PlanProblemKind.NotOperable;

    /// <summary>
    /// One sentence for a group of refusals that all say the same thing.
    ///
    /// <b>Keys written out rather than built from the name of the kind</b>, and two guards asked for
    /// it in the same run: a key assembled at run time reaches no screen that a source scan can see,
    /// so the text guard reports it as a string with no audience - which is the same blindness that
    /// would hide a key nobody ever wrote.
    ///
    /// <b>The discard throws rather than picking the likelier of two.</b> Only two kinds arrive
    /// here today and <see cref="SaysOnlyAName"/> is what keeps the rest away, so an arm answering
    /// for a third would be a sentence about drivers shown for something that is not one. That is
    /// the shape this product spent 2026-08-25 taking out of nine other places.
    /// </summary>
    private static string Gathered(PlanProblemKind kind, IReadOnlyList<string> names) => kind switch
    {
        PlanProblemKind.UnknownService =>
            Texts.Of("gui.plan.problem.unknownService.many", names.Count, Listed(names)),

        PlanProblemKind.NotOperable =>
            Texts.Of("gui.plan.problem.notOperable.many", names.Count, Listed(names)),

        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "Refusals of this kind are not gathered, so there is no sentence here for a group of "
            + "them. SaysOnlyAName decides which kinds arrive.")
    };

    /// <summary>
    /// One failed step in words, with the manager's own message carried through rather than
    /// replaced.
    ///
    /// <b>Timing out is its own sentence and not a failure worded softly.</b> The manager took the
    /// request and the entry may well have arrived after we stopped looking, so telling somebody it
    /// failed would be a claim about something nobody saw - the same distinction StepOutcome makes,
    /// arriving on a screen.
    /// </summary>
    internal static string Describe(StepResult result) =>
        result.Step.Operation == StepOperation.SetStartType

            // A SENTENCE OF ITS OWN, because the shared one does not survive this verb: "Spooler
            // would not set the start type of" is what the template above would produce. A timeout
            // cannot arrive here at all - writing a setting is done when it returns, so there is
            // nothing to watch and nothing to give up on.
            ? Texts.Of(
                "gui.plan.failure.refusedStartType",
                result.Step.ServiceName,
                result.Error ?? string.Empty)
            : Ordinary(result);

    private static string Ordinary(StepResult result) => result.Outcome == StepOutcome.TimedOut
        ? TimedOut(result)
        : Texts.Of(
            "gui.plan.failure.refused",
            result.Step.ServiceName,
            Word(result.Step.Operation),
            result.Error ?? string.Empty);

    /// <summary>
    /// A step that was watched and did not arrive, naming the process still holding the entry.
    ///
    /// <b>The one outcome in this window with nowhere else to send somebody, and until 2026-09-06
    /// it ended at "it may arrive by itself".</b> That sentence is true and it is the whole of what
    /// there was: the entry has been asked, has not moved, and asking again does nothing. Naming
    /// the process is what turns a dead end into somewhere to look.
    ///
    /// <b>Two sentences rather than one with a gap in it.</b> The reading has three states and only
    /// one is a number - the manager can say there is no process, and nobody may have got an answer
    /// at all. Both of those read correctly as the shorter sentence and neither is worth its own.
    /// </summary>
    private static string TimedOut(StepResult result) => result.ProcessId.IsPresent
        ? Texts.Of(
            "gui.plan.failure.timedOut.process",
            result.Step.ServiceName,
            Word(result.Step.Operation),
            result.ProcessId.Value)
        : Texts.Of(
            "gui.plan.failure.timedOut",
            result.Step.ServiceName,
            Word(result.Step.Operation));

    /// <summary>
    /// What one step DOES, in one word, said in one place.
    ///
    /// <b>Four copies of this stood in this window until 2026-08-25, and every one of them was a
    /// ternary</b> - "a stop, otherwise a start". A third kind of step would have been called a
    /// start on four screens at once, in a panel whose whole purpose is telling somebody what is
    /// about to happen to their machine. The core has the same change at the same time, including
    /// in the one place that asks the manager to move something.
    ///
    /// <b>It refuses rather than guessing, and a compile error was not available:</b> a switch
    /// covering every named member of an enum still needs a discard - CS8524 - because the variable
    /// can hold a number nobody named.
    /// </summary>
    internal static string Word(StepOperation operation) => operation switch
    {
        StepOperation.Stop => Texts.Of("gui.plan.operation.stop"),
        StepOperation.Start => Texts.Of("gui.plan.operation.start"),
        StepOperation.SetStartType => Texts.Of("gui.plan.operation.setStartType"),
        StepOperation.Terminate => Texts.Of("gui.plan.operation.terminate"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(operation), operation, Bws.Core.Planning.EquivalentCommand.Unhandled)
    };

    /// <summary>
    /// The step on screen, with how long it has been going and how long it has left.
    ///
    /// <b>`docs/11` SECTION 7 ASKS FOR THIS ABOVE TEN SECONDS AND IT WAS NOT THERE</b> - backlog
    /// 330, and the pre-release audit's RELEASE-008. The line said which step was happening and
    /// nothing about how long it had been happening, so a stop that had been going fifty seconds
    /// and one that had been going two looked the same. A single StartService on Windows Server
    /// 2025 has been measured here at 30 375 - 30 450 ms, so the half minute this is about is not
    /// hypothetical.
    ///
    /// <b>Both numbers, because one of them alone is the wrong sentence.</b> Elapsed on its own
    /// says how long somebody has been waiting and not whether waiting is nearly over. The ceiling
    /// on its own is the thing they could already have read off the box. Together they say the one
    /// thing worth knowing at that moment: whether this is about to be given up on.
    ///
    /// <b>Whole seconds, rounded down.</b> A tenth of a second changing under somebody's eye is
    /// motion carrying no information, and this line sits under a list they are trying to read.
    ///
    /// <b>Past the ceiling is a real state rather than an impossible one.</b> The ceiling caps our
    /// watching, not the manager's answering - PlanRunner says so - so a step can sit at seventy of
    /// sixty while the entry's own wait hint is still being honoured. The words have to survive
    /// that rather than pretend it cannot happen.
    /// </summary>
    internal static string StillWaiting(string step, TimeSpan waited, int ceiling)
    {
        var seconds = (int)waited.TotalSeconds;

        return seconds < 1
            ? step
            : Texts.Of("gui.plan.progress.waiting", step, seconds, ceiling);
    }
}
