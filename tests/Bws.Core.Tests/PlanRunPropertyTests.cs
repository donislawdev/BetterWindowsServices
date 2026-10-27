using System.Globalization;
using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;
using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// A plan carried out on a machine that goes wrong in random places, checked against what the preview
/// showed - rule 5, "the preview shows exactly what carrying it out does", as a property.
///
/// <b>The plan is any plan PlanGraphPropertyTests can build</b>, and the machine is the same one, with
/// trouble drawn per entry: a request refused, a reading refused, an entry that moved between the
/// listing and the run, a process number handed to somebody else, a setting refused, an ending refused.
/// On top of that, Stop or Abandon pressed at a random step.
///
/// <b>Every write the runner sends is recorded with the step it was sent in</b>, by a decorator around
/// the double, and the oracle is stated over those writes and the steps the preview showed - never
/// over the runner's own account of what it did:
/// nothing is asked that the step being run does not show, nothing goes forward past a failure or past
/// Stop, nothing at all past Abandon, nothing is put back that the run never took down, and what the
/// run did take down is put back unless somebody abandoned it.
/// </summary>
public sealed class PlanRunPropertyTests
{
    private static readonly Gen<Trouble> Troubles =
        Gen.Select(Gen.Int[0, 15].Array[7], Gen.Int[1, 16], Gen.Int[1, 24])
            .Select(drawn => new Trouble(drawn.Item1, drawn.Item2, drawn.Item3));

    private static readonly Gen<(Graph Graph, Trouble Trouble)> Runs =
        Gen.Select(Graphs.Any, Troubles).Where(drawn => Built(drawn.Item1).IsRunnable);

    [Fact]
    [Trait("search", "weekly")]
    public void A_run_asks_only_what_the_preview_showed_and_puts_back_what_it_took_down()
    {
        Runs.Searched(
            drawn => Wrong(Carried(drawn.Graph, drawn.Trouble)) is null,
            new(3_000) { Print = drawn => Said(Carried(drawn.Graph, drawn.Trouble), drawn.Trouble, drawn.Graph) });
    }

    /// <summary>
    /// The input the first minute-long search of the property above found on 2026-10-17 (finding E,
    /// seed 0l4gUzq4NGe5, after 7.8 million draws), replayed on every pull request - the procedure in
    /// the header of tests/search/search.ps1. A housemate somebody else was stopping was only watched,
    /// given up on, and then started by the forced restart, which had never asked it to stop.
    ///
    /// <b>A seed names an input only while the generators stay as they are.</b> Changing Graphs or the
    /// trouble table turns this into a replay of something else, silently - which is why the shape is
    /// also written out by hand in PlanRunProgressTests, where no generator stands between it and the
    /// code.
    ///
    /// <b>Checked when the table grew dice 9 and 10 on 2026-10-26 (logic report R02, R03)</b>, by printing
    /// what the seed draws: dice 9 6 6 14 8 0 2. The 9, behaving until then, now falls on Alpha - stopped and
    /// in no step of the plan, so never asked and never read after a request, which is the only thing that
    /// trouble changes. The housemates on dice 6 are still watched and given up on, and nothing is put back.
    /// Grow the table again and this has to be printed again.
    /// </summary>
    [Fact]
    public void The_run_the_first_minute_long_search_found_puts_back_nothing_it_never_asked_to_stop()
    {
        Runs.Searched(
            drawn => Wrong(Carried(drawn.Graph, drawn.Trouble)) is null,
            new(1) { Seed = "0l4gUzq4NGe5", Print = drawn => Said(Carried(drawn.Graph, drawn.Trouble), drawn.Trouble, drawn.Graph) });
    }

    private static OperationPlan Built(Graph graph) =>
        new PlanBuilder(graph.Entries, graph.Catalog()).Build(graph.Ask);

    private static string Said(Ran ran, Trouble trouble, Graph graph) =>
        $"{Wrong(ran)} - run [{ran}] - {trouble} - {graph}";

    private static Ran Carried(Graph graph, Trouble trouble)
    {
        var plan = Built(graph);
        var clock = new FakeClock();
        var watched = new Watched(Machine(graph, trouble, clock));
        using var stop = new CancellationTokenSource();
        using var abandon = new CancellationTokenSource();
        int? stoppedAt = null;
        int? abandonedAt = null;

        var run = new PlanRunner(watched, clock).Run(
            plan,
            TimeSpan.FromSeconds(30),
            stop.Token,
            abandon.Token,
            (_, position) =>
            {
                watched.Position = position;
                watched.Attempted.Add(position);
                stoppedAt ??= Pressed(stop, position == trouble.StopAt, position);
                abandonedAt ??= Pressed(abandon, position == trouble.AbandonAt, position);
            });

        return new Ran(plan, run, watched, stoppedAt, abandonedAt);
    }

    private static int? Pressed(CancellationTokenSource source, bool now, int position)
    {
        if (!now)
        {
            return null;
        }

        source.Cancel();
        return position;
    }

    /// <summary>The double, standing where the listing said, with the trouble each entry drew.</summary>
    private static FakeScmControl Machine(Graph graph, Trouble trouble, FakeClock clock)
    {
        var control = new FakeScmControl(clock);

        for (var at = 0; at < graph.Entries.Count; at++)
        {
            Troubled(control, graph.Entries[at], trouble.Dice[at]);
        }

        foreach (var (name, answer) in graph.Answers)
        {
            if (answer.Outcome == ReadOutcome.Denied)
            {
                control.RefusingDependents(name, Entries.AccessDenied);
            }
            else if (answer.IsPresent)
            {
                control.DependedOnBy(name, [.. answer.Value!]);
            }
        }

        // The last die is about the machine as a whole: the ending refused, the statuses the ending checks
        // refused, or the manager no longer saying who depends on whom - each a change since the preview.
        return trouble.Dice[^1] switch
        {
            0 => control.RefusingToEnd(Entries.AccessDenied),
            1 => control.RefusingStatuses(Entries.AccessDenied),
            2 => Silenced(control, graph),
            _ => control
        };
    }

    private static FakeScmControl Silenced(FakeScmControl control, Graph graph)
    {
        foreach (var entry in graph.Entries)
        {
            control.RefusingDependents(entry.ServiceName, Entries.AccessDenied);
        }

        return control;
    }

    private static void Troubled(FakeScmControl control, ScmEntry entry, int die)
    {
        var name = entry.ServiceName;
        var settled = Settled(entry.Status);

        // Die 2: the entry moved between the listing and the run - the one change a preview cannot see.
        control.At(name, die == 2 ? Moved(settled) : settled);

        if (entry.ProcessId.IsPresent)
        {
            // Die 4: the number now belongs to somebody else, which the ending must refuse to end.
            control.RunningIn(name, entry.ProcessId.Value + (die == 4 ? 1 : 0));
        }

        if (die < Kinds.Length)
        {
            Kinds[die](control, name, settled);
        }
    }

    /// <summary>
    /// The trouble each die gives an entry, by its value - a table rather than a switch, so that adding
    /// one is a line rather than a fork (the shape ceilings count every arm). Past the end it behaves.
    /// </summary>
    private static readonly Action<FakeScmControl, string, EntryStatus>[] Kinds =
    [
        (control, name, _) => control.RefusingRequests(name, 1051),
        (control, name, _) => control.RefusingReads(name, Entries.AccessDenied),

        // 2 and 4 are dealt with where the status and the process are set, above.
        (_, _, _) => { },
        (control, name, _) => control.RefusingConfiguration(name, Entries.AccessDenied),
        (_, _, _) => { },

        // Stuck: asked, it answers Paused for ever with no progress, so the step runs out of time.
        (control, name, _) => control.Reaching(name, new ServiceProgress(EntryStatus.Paused, 0, TimeSpan.Zero, 0)),

        // On its way when the run reaches it, and there five seconds later - the step waits first.
        (control, name, settled) => control.OnItsWay(name, TimeSpan.FromSeconds(5), Pending(settled)),

        // Refused, and yet where it was asked to go - the manager refusing to stop what just stopped.
        (control, name, settled) => control.RefusingRequests(name, 1062, becomes: Moved(settled)),

        // Falls over: on its way up, then stopped with the exit code a crashed service leaves.
        (control, name, _) => control.Reaching(
            name,
            new ServiceProgress(EntryStatus.StartPending, 1, TimeSpan.FromSeconds(2), 0),
            new ServiceProgress(EntryStatus.Stopped, 0, TimeSpan.Zero, 0, ExitCode: 1067)),

        // Lost from sight: readable until the manager takes a request for it, refused after (R02, 2026-10-26).
        (control, name, _) => control.RefusingReadsOnceAsked(name, Entries.AccessDenied),

        // Set to Disabled since the preview, so a restart refuses before its first step (R03, 2026-10-26).
        (control, name, _) => control.StartingAs(name, StartType.Disabled)
    ];

    private static ServiceProgress Pending(EntryStatus settled) =>
        new(settled == EntryStatus.Stopped ? EntryStatus.StartPending : EntryStatus.StopPending, 1, TimeSpan.FromSeconds(2), 0);

    /// <summary>Where a moving entry has got to by the time the run reaches it.</summary>
    private static EntryStatus Settled(EntryStatus listed) => listed switch
    {
        EntryStatus.StopPending => EntryStatus.Stopped,
        EntryStatus.StartPending => EntryStatus.Running,
        _ => listed
    };

    private static EntryStatus Moved(EntryStatus settled) =>
        settled == EntryStatus.Stopped ? EntryStatus.Running : EntryStatus.Stopped;

    private static string? Wrong(Ran ran) =>
        Unaccounted(ran)
        ?? Unshown(ran)
        ?? PastAFailure(ran)
        ?? PastAPress(ran)
        ?? PutBackUntaken(ran)
        ?? LeftDown(ran);

    /// <summary>Every step the preview showed has one result, in the order shown.</summary>
    private static string? Unaccounted(Ran ran) =>
        ran.Run.Results.Count != ran.Plan.Steps.Count
            ? $"{ran.Run.Results.Count} results for {ran.Plan.Steps.Count} steps"
            : ran.Plan.Steps
                .Select((step, at) => ReferenceEquals(step, ran.Run.Results[at].Step) ? null : $"result {at + 1} is for another step")
                .FirstOrDefault(wrong => wrong is not null);

    /// <summary>Every write is the write the step being run shows, about the entry it shows.</summary>
    private static string? Unshown(Ran ran) =>
        ran.Machine.Writes.FirstOrDefault(write => write.Position == 0 || !Shown(ran.Plan.Steps[write.Position - 1], write))
            is { What: not null } stray
            ? $"{stray.Operation} {stray.What} sent during step {stray.Position}, which shows something else"
            : null;

    private static bool Shown(PlanStep step, Write write) =>
        step.Operation == write.Operation
        && (step.Operation == StepOperation.Terminate
            ? write.What == step.ProcessId?.ToString(CultureInfo.InvariantCulture)
            : string.Equals(write.What, step.ServiceName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Once a step that does not put something back has failed, no later step going forward is asked,
    /// and once a cascade step has failed, no later step standing behind the entry's own stop is asked
    /// either - a dependant still running on the process is what the manager's refusal protected.
    /// </summary>
    private static string? PastAFailure(Ran ran)
    {
        foreach (var failed in ran.Positions.Where(ran.Failed))
        {
            var cascade = ran.Step(failed).Reason == StepReason.Cascade;
            var asked = ran.Positions.FirstOrDefault(later => later > failed && ran.Wrote(later)
                && (Forward(ran.Step(later)) || (cascade && Behind(ran.Step(later)))));

            if (asked > 0)
            {
                return $"step {asked} was asked after step {failed} failed";
            }
        }

        return null;
    }

    /// <summary>After Stop only putting back is asked, and after Abandon nothing at all.</summary>
    private static string? PastAPress(Ran ran)
    {
        var asked = ran.Positions.FirstOrDefault(later => ran.Wrote(later) && (AfterStop(ran, later) || later > ran.AbandonedAt));

        return asked > 0 ? $"step {asked} was asked after Stop at {ran.StoppedAt} or Abandon at {ran.AbandonedAt}" : null;
    }

    private static bool AfterStop(Ran ran, int position) =>
        position > ran.StoppedAt && ran.Step(position).Reason != StepReason.Restore;

    /// <summary>
    /// Nothing is put back that the run did not take down by SENDING a stop or an ending.
    ///
    /// <b>A second half stood here from 2026-10-17 to 2026-10-26 and it is gone on the owner's decision of
    /// that day</b> (logic report R01). The first minute-long search (seed 0l4gUzq4NGe5) found a housemate
    /// already on its way down, watched, arrived, its step succeeded with nothing sent - and this oracle was
    /// widened to call that "taken down", so starting it again counted as the preview kept. It is somebody
    /// else's stop: the step is marked ArrivedUnasked and nothing puts the entry back.
    /// </summary>
    private static string? PutBackUntaken(Ran ran)
    {
        var untaken = ran.Positions.FirstOrDefault(at =>
            ran.Step(at).Reason == StepReason.Restore && ran.Wrote(at) && !TakenDownBefore(ran, at));

        return untaken > 0 ? $"{ran.Step(untaken).ServiceName} put back at step {untaken} though the run never took it down" : null;
    }

    private static bool TakenDownBefore(Ran ran, int at) =>
        ran.Positions.Any(before => before < at
            && ran.Wrote(before)
            && TakesDown(ran.Step(before), ran.Step(at).ServiceName));

    private static bool TakesDown(PlanStep step, string name) =>
        (step.Operation is StepOperation.Stop or StepOperation.Terminate
            && string.Equals(step.ServiceName, name, StringComparison.OrdinalIgnoreCase))
        || (step.Operation == StepOperation.Terminate && (step.TakesWithIt ?? []).Contains(name, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// What the run stopped is put back, after a failure and after Stop alike - only Abandon leaves it
    /// down. The promise the restart's own comment makes, found broken once by Ctrl+C on a virtual
    /// machine in 2026-08.
    /// </summary>
    private static string? LeftDown(Ran ran)
    {
        var left = ran.Positions.FirstOrDefault(at => Owed(ran, at)
            && ran.Result(at).SkippedBecause is SkipReason.Cancelled or SkipReason.EarlierStepFailed or SkipReason.NothingToPutBack);

        return left > 0 ? $"{ran.Step(left).ServiceName} was stopped and not put back: {ran.Result(left).SkippedBecause}" : null;
    }

    /// <summary>A step putting back an entry an earlier step of this run stopped, with nobody having abandoned the run.</summary>
    private static bool Owed(Ran ran, int at) =>
        ran.Step(at).Reason == StepReason.Restore
        && !(at >= ran.AbandonedAt)
        && ran.Positions.Any(before => before < at && StoppedHere(ran, before, ran.Step(at).ServiceName));

    /// <summary>A stop this run sent and saw arrive - an arrival it only watched is somebody else's (R01).</summary>
    private static bool StoppedHere(Ran ran, int position, string name) =>
        ran.Step(position).Operation == StepOperation.Stop
        && string.Equals(ran.Step(position).ServiceName, name, StringComparison.OrdinalIgnoreCase)
        && ran.Result(position) is { Outcome: StepOutcome.Succeeded, ArrivedUnasked: false };

    private static bool Forward(PlanStep step) => step.Reason is StepReason.Requested or StepReason.Cascade;

    private static bool Behind(PlanStep step) => step.Reason is StepReason.Escalation or StepReason.SharesTheProcess;
}

/// <summary>
/// What goes wrong on the machine: a die per entry (0 refuses requests, 1 refuses readings, 2 moved since
/// the listing, 3 refuses settings, 4 handed its process number to somebody else, 5 stuck, 6 on its way
/// when the run reaches it, 7 refused and there anyway, 8 falls over while starting, the rest behave), a
/// last die for the machine as a whole (0 refuses the ending, 1 the statuses, 2 who depends on whom), and
/// the steps at which Stop and Abandon are pressed - often past the end, which is the run nobody interrupts.
/// </summary>
internal sealed record Trouble(int[] Dice, int StopAt, int AbandonAt)
{
    public override string ToString() =>
        $"dice [{string.Join(" ", Dice)}], Stop at {StopAt}, Abandon at {AbandonAt}";
}

/// <summary>One write the runner sent, and the step being run when it did.</summary>
internal readonly record struct Write(int Position, StepOperation Operation, string What);

/// <summary>
/// The double, with every write recorded against the step being run. Reads pass through unrecorded:
/// rule 5 is about what changes the machine, and the runner reads on purpose before and after acting.
/// </summary>
internal sealed class Watched(IScmControl inner) : IScmControl
{
    internal int Position { get; set; }

    internal HashSet<int> Attempted { get; } = [];

    internal List<Write> Writes { get; } = [];

    public ControlAnswer Request(string serviceName, StepOperation operation)
    {
        Writes.Add(new Write(Position, operation, serviceName));
        return inner.Request(serviceName, operation);
    }

    public ControlAnswer Configure(string serviceName, StartSetting wanted)
    {
        Writes.Add(new Write(Position, StepOperation.SetStartType, serviceName));
        return inner.Configure(serviceName, wanted);
    }

    public ControlAnswer Terminate(int processId, long? createdAt)
    {
        Writes.Add(new Write(Position, StepOperation.Terminate, processId.ToString(CultureInfo.InvariantCulture)));
        return inner.Terminate(processId, createdAt);
    }

    public ControlAnswer Read(string serviceName) => inner.Read(serviceName);

    public Reading<IReadOnlyList<ScmStatus>> ReadStatuses() => inner.ReadStatuses();

    public Reading<IReadOnlyList<string>> ReadDependents(string serviceName) => inner.ReadDependents(serviceName);

    public Reading<StartType> ReadStartType(string serviceName) => inner.ReadStartType(serviceName);
}

/// <summary>A plan, its run, and what the machine was sent - with positions counted from one, as the runner counts them.</summary>
internal sealed record Ran(OperationPlan Plan, PlanRun Run, Watched Machine, int? StoppedAt, int? AbandonedAt)
{
    internal IEnumerable<int> Positions => Enumerable.Range(1, Plan.Steps.Count);

    internal PlanStep Step(int position) => Plan.Steps[position - 1];

    internal StepResult Result(int position) => Run.Results[position - 1];

    internal bool Wrote(int position) => Machine.Writes.Any(write => write.Position == position);

    /// <summary>Tried, not there at the end, and not a step putting something back.</summary>
    internal bool Failed(int position) =>
        Machine.Attempted.Contains(position) && !Result(position).Arrived && Step(position).Reason != StepReason.Restore;

    /// <summary>The run step by step - what each step was, how it ended, and what it sent - for a failure's message.</summary>
    public override string ToString() =>
        string.Join(" | ", Positions.Select(at =>
            $"{at} {Step(at).Operation} {Step(at).ServiceName} {Step(at).Reason}: {Result(at).Outcome}"
            + (Result(at).SkippedBecause is { } skipped ? $" {skipped}" : string.Empty)
            + $" sent [{string.Join(" ", Machine.Writes.Where(write => write.Position == at).Select(write => $"{write.Operation} {write.What}"))}]"));
}
