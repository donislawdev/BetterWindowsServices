using Bws.Core.Planning;

namespace Bws.Core.Tests.Fakes;

/// <summary>
/// A manager that can be told how to behave while a plan runs.
///
/// The half of ADR-10's seam that writes, doubled for the same reason as the half that
/// reads, and with more to gain. A service that sits in StopPending raising its check point
/// for forty seconds and then stops exists on a real machine for exactly as long as it
/// exists, cannot be summoned, and is the single case the waiting rules are built around.
/// Here it is one line.
///
/// Every method records what it was asked, because half of what these tests check is what
/// the runner did <i>not</i> do: a step reported as skipped that quietly went to the manager
/// anyway would be a preview that lied, which is the failure this whole pattern exists to
/// prevent.
/// </summary>
/// <param name="clock">
/// The ruler an entry made with <see cref="Arriving"/> keeps time by - the same one the runner
/// under test is given. Only those entries need it.
/// </param>
internal sealed partial class FakeScmControl(IClock? clock = null) : IScmControl
{
    private readonly Dictionary<string, Behaviour> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Names the manager was asked to change, in order.</summary>
    internal List<string> Requested { get; } = [];

    /// <summary>
    /// What was written, in order, as pairs of name and start type.
    ///
    /// <b>Kept apart from <see cref="Requested"/> on purpose.</b> Half of what these tests check is
    /// what the runner did NOT do, and "asked the manager to move it" and "wrote a setting on it"
    /// are two different wrong answers to check for.
    /// </summary>
    internal List<(string Name, StartSetting To)> Configured { get; } = [];

    /// <summary>
    /// Processes this was asked to end, in order.
    ///
    /// <b>A third list rather than more of <see cref="Requested"/>, and the reason is the reason
    /// these lists exist at all.</b> Half of what the tests check is what the runner did NOT do,
    /// and "asked the manager to move it", "wrote a setting on it" and "ended the process behind
    /// it" are three different wrong answers to look for. An empty list here is the assertion that
    /// carries the most weight in this file.
    /// </summary>
    internal List<int> Ended { get; } = [];

    /// <summary>
    /// The creation time handed down with each ending, in the same order as <see cref="Ended"/>.
    ///
    /// <b>Recorded rather than acted on, because the real check cannot live above this seam.</b>
    /// Comparing the identity is <see cref="Bws.Core.WindowsScmControl"/>'s job and it does it on
    /// the handle it kills with, which is the whole point of it - nothing a fake does here could
    /// stand in for that. What a test CAN ask on this side is whether the plan carried the second
    /// half of the identity down at all, and this is what it asks.
    /// </summary>
    internal List<long?> EndedWith { get; } = [];

    private int? _terminateRefusedWith;

    private readonly HashSet<int> _survives = [];

    /// <summary>A process the system will not let anybody open for ending - protected, or gone.</summary>
    internal FakeScmControl RefusingToEnd(int errorCode)
    {
        _terminateRefusedWith = errorCode;
        return this;
    }

    /// <summary>
    /// A process that takes the request and does not die of it.
    ///
    /// <b>Win32 documents this and it is not a curiosity:</b> ending a process is asynchronous, and
    /// a process with pending driver work cannot exit until that work finishes or is cancelled. So
    /// a successful call and an entry that never reaches Stopped is a real pair, and the tool has to
    /// report it as a step that ran out of time rather than as one that worked.
    /// </summary>
    internal FakeScmControl SurvivingTermination(int processId)
    {
        _survives.Add(processId);
        return this;
    }

    public ControlAnswer Terminate(int processId, long? createdAt)
    {
        Ended.Add(processId);
        EndedWith.Add(createdAt);

        if (_terminateRefusedWith is { } refused)
        {
            return ControlAnswer.Refused(refused, "Access is denied.");
        }

        if (!_survives.Contains(processId))
        {
            // Every entry running in that process, because that is what ending it does. The whole
            // point of the neighbours being steps is that a test can see them arrive here without
            // having been asked.
            foreach (var entry in _entries.Values.Where(entry => entry.ProcessId == processId))
            {
                entry.Status = EntryStatus.Stopped;

                // AND THE SCRIPT IS TORN UP, WHICH THE FIRST VERSION FORGOT. A scripted entry
                // keeps handing out its last reading for as long as anybody looks, which is how
                // "stuck" is expressed here - so an entry scripted to sit in StopPending went on
                // saying so after its process had gone, and the tool was reported as failing to
                // notice a death this double never modelled.
                entry.Moving = false;
                entry.AfterRequest = null;

                if (entry.ComesBack is { } back)
                {
                    entry.AfterRequest = back;
                    entry.Moving = true;
                }
            }
        }

        return ControlAnswer.Done();
    }

    /// <summary>
    /// An entry the manager starts again once its process dies - a recovery list saying "restart the
    /// service" - handing out these readings after the ending, the last one repeating. Since 2026-09-30.
    /// </summary>
    internal FakeScmControl ComingBack(string serviceName, params ServiceProgress[] readings)
    {
        Entry(serviceName).ComesBack = new Queue<ServiceProgress>(readings);
        return this;
    }

    /// <summary>Which process an entry runs in, for the tests that end one.</summary>
    internal FakeScmControl RunningIn(string serviceName, int processId)
    {
        Entry(serviceName).ProcessId = processId;
        return this;
    }

    /// <summary>An entry whose configuration the manager will not write.</summary>
    internal FakeScmControl RefusingConfiguration(string serviceName, int errorCode)
    {
        Entry(serviceName).ConfigureRefusedWith = errorCode;
        return this;
    }

    public ControlAnswer Configure(string serviceName, StartSetting wanted)
    {
        Configured.Add((serviceName, wanted));

        var entry = Entry(serviceName);

        return entry.ConfigureRefusedWith is { } refused
            ? ControlAnswer.Refused(refused, "Access is denied.")
            : ControlAnswer.Done();
    }

    /// <summary>An entry that is where it is, and that arrives the moment it is asked to move.</summary>
    internal FakeScmControl At(string serviceName, EntryStatus status)
    {
        Entry(serviceName).Status = status;
        return this;
    }

    /// <summary>
    /// An entry that takes its time. The readings are handed out one per look, and the last
    /// one repeats for as long as anybody keeps looking - which is how "stuck" is expressed.
    /// </summary>
    internal FakeScmControl Reaching(string serviceName, params ServiceProgress[] readings)
    {
        Entry(serviceName).AfterRequest = new Queue<ServiceProgress>(readings);
        return this;
    }

    /// <summary>
    /// An entry that gets there a set time after it was asked, however often anybody looks in
    /// between - and until then says <paramref name="meanwhile"/>.
    ///
    /// <b>Time rather than a count of looks, since 2026-09-29</b>, because the runner no longer
    /// looks at a steady pace. <see cref="Reaching"/> hands out one reading per look, so it can say
    /// "arrives at the third look" and cannot say "arrives after 40 ms", which is the only way to
    /// ask how soon an arrival is noticed.
    /// </summary>
    internal FakeScmControl Arriving(string serviceName, TimeSpan after, ServiceProgress meanwhile)
    {
        var ruler = clock
            ?? throw new InvalidOperationException("An entry that arrives in time needs the clock the runner is given.");

        Entry(serviceName).Arrival = new Arrival(ruler, after, meanwhile);
        return this;
    }

    /// <summary>
    /// An entry already on its way somewhere when the run begins - somebody else asked - that gets
    /// there <paramref name="after"/> from now, and until then says <paramref name="meanwhile"/>. Where
    /// it is going follows from the pending state it is in: stopping ends in Stopped, anything else in
    /// Running.
    ///
    /// <b>Added 2026-09-30 for stability report W-1 and W-7.</b> Until then every entry here moved only
    /// when this tool asked it to, so the one shape the report was about - a step meeting an entry
    /// still stopping - could not be written down at all.
    /// </summary>
    internal FakeScmControl OnItsWay(string serviceName, TimeSpan after, ServiceProgress meanwhile)
    {
        var ruler = clock
            ?? throw new InvalidOperationException("An entry on its way needs the clock the runner is given.");

        var entry = Entry(serviceName);
        entry.Arrival = new Arrival(ruler, after, meanwhile);
        entry.Moving = true;
        entry.AskedAt = ruler.Elapsed;
        entry.Heading = meanwhile.Status == EntryStatus.StopPending ? EntryStatus.Stopped : EntryStatus.Running;
        entry.Status = meanwhile.Status;
        return this;
    }

    /// <summary>
    /// An entry whose manager sits on every request for <paramref name="by"/> before answering it -
    /// the shape measured on Windows Server 2025, where one start took half a minute to come back.
    /// </summary>
    internal FakeScmControl SlowToAnswer(string serviceName, TimeSpan by)
    {
        _ = clock ?? throw new InvalidOperationException("A slow answer needs the clock the runner is given.");
        Entry(serviceName).AnswersAfter = by;
        return this;
    }

    /// <summary>
    /// How many times anybody asked where an entry is, every entry counted.
    ///
    /// For the one question the other lists cannot answer: whether looking sooner turned into
    /// asking the manager all the time.
    /// </summary>
    internal int Reads { get; private set; }

    /// <summary>An entry the manager will not move, optionally because it has moved itself.</summary>
    internal FakeScmControl RefusingRequests(string serviceName, int errorCode, EntryStatus? becomes = null)
    {
        var entry = Entry(serviceName);
        entry.RequestRefusedWith = errorCode;
        entry.BecomesOnRefusal = becomes;
        return this;
    }

    /// <summary>An entry that cannot even be looked at.</summary>
    internal FakeScmControl RefusingReads(string serviceName, int errorCode)
    {
        Entry(serviceName).ReadRefusedWith = errorCode;
        return this;
    }

    public ControlAnswer Request(string serviceName, StepOperation operation)
    {
        Requested.Add(serviceName);

        var entry = Entry(serviceName);

        if (entry.AnswersAfter is { } slow)
        {
            clock!.Wait(slow);
        }

        if (Refusal(entry, operation) is { } refused)
        {
            return refused;
        }

        entry.AskedAgain();
        entry.Moving = true;

        if (entry.Arrival is { } arrival)
        {
            entry.AskedAt = arrival.Clock.Elapsed;
            entry.Heading = operation == StepOperation.Stop ? EntryStatus.Stopped : EntryStatus.Running;

            return ControlAnswer.Done();
        }

        // Nothing scripted means it is there by the time anybody looks, which is what most
        // services do and what most of these tests are not about.
        if (entry.AfterRequest is null)
        {
            entry.Status = operation == StepOperation.Stop ? EntryStatus.Stopped : EntryStatus.Running;
        }

        return ControlAnswer.Done();
    }

    /// <summary>
    /// What the manager says no to before anything moves: an entry told to refuse, and - since
    /// 2026-09-30 - an entry still on its way somewhere, as a real manager refuses a stop to an entry
    /// already stopping and a start to one still stopping. Before that day nothing here was ever on its
    /// way when it was asked. Its own method because the shape guard counts the forks of a test method.
    /// </summary>
    private static ControlAnswer? Refusal(Behaviour entry, StepOperation operation)
    {
        if (entry.RequestRefusedWith is { } refused)
        {
            if (entry.BecomesOnRefusal is { } becomes)
            {
                entry.Status = becomes;
            }

            return ControlAnswer.Refused(refused, $"refused with {refused}");
        }

        if (entry.Moving
            && entry.Arrival is { } onItsWay
            && onItsWay.Clock.Elapsed - entry.AskedAt < onItsWay.After)
        {
            var code = operation == StepOperation.Stop ? CannotAcceptControl : AlreadyRunning;
            return ControlAnswer.Refused(code, $"refused with {code}");
        }

        return null;
    }

    public ControlAnswer Read(string serviceName)
    {
        Reads++;

        var entry = Entry(serviceName);

        if (entry.ReadRefusedWith is { } refused)
        {
            return ControlAnswer.Refused(refused, $"refused with {refused}");
        }

        if (entry.Moving && entry.Arrival is { } arrival)
        {
            if (arrival.Clock.Elapsed - entry.AskedAt < arrival.After)
            {
                entry.Status = arrival.Meanwhile.Status;
                return ControlAnswer.At(arrival.Meanwhile);
            }

            // Arrived, and from here on it is an entry that is where it is.
            entry.Status = entry.Heading;
            entry.Arrival = null;
        }

        if (!entry.Moving || entry.AfterRequest is null)
        {
            return ControlAnswer.At(
                new ServiceProgress(entry.Status, CheckPoint: 0, TimeSpan.Zero, Held(entry)));
        }

        // The last reading stays on the table. An entry that has arrived should keep saying
        // so, and one that is stuck should keep being stuck however long anybody watches.
        var reading = entry.AfterRequest.Count > 1
            ? entry.AfterRequest.Dequeue()
            : entry.AfterRequest.Peek();

        entry.Status = reading.Status;

        return ControlAnswer.At(reading);
    }

    /// <summary>
    /// A process for an entry that is anywhere but stopped, and none for one that is.
    ///
    /// A number rather than nothing, because the fake exists to produce the shapes a real
    /// manager produces - and a running entry with no process behind it is not one of them.
    /// The value itself carries no meaning beyond being the same one every time, so a test
    /// asserting on it is asserting that the reading was carried rather than invented.
    /// </summary>
    internal const uint FakeProcess = 4812;

    /// <summary>ERROR_SERVICE_CANNOT_ACCEPT_CTRL - what a stop to an entry already stopping gets.</summary>
    internal const int CannotAcceptControl = 1061;

    /// <summary>ERROR_SERVICE_ALREADY_RUNNING - what a start to an entry that is not stopped gets.</summary>
    internal const int AlreadyRunning = 1056;

    private static uint Held(Behaviour entry) =>
        entry.Status == EntryStatus.Stopped ? 0 : (uint)entry.ProcessId;

    private Behaviour Entry(string serviceName)
    {
        if (!_entries.TryGetValue(serviceName, out var entry))
        {
            entry = new Behaviour();
            _entries[serviceName] = entry;
        }

        return entry;
    }

    /// <summary>How one entry behaves while a plan runs.</summary>
    private sealed class Behaviour
    {
        /// <summary>
        /// The process this entry runs in. Shared with any other entry given the same number,
        /// which is what ending one of them has to take down with it.
        /// </summary>
        internal int ProcessId { get; set; } = (int)FakeProcess;

        internal int? ConfigureRefusedWith { get; set; }

        internal TimeSpan? AnswersAfter { get; set; }

        /// <summary>
        /// A SECOND REQUEST AFTER THE SCRIPT HAS PLAYED OUT starts from where the entry stands, rather
        /// than handing out the last reading of the FIRST request's journey for ever - so a restart of
        /// an entry scripted to stop slowly can be put back. Since 2026-09-30.
        /// </summary>
        internal void AskedAgain()
        {
            if (Moving && AfterRequest is { Count: 1 })
            {
                AfterRequest = null;
            }
        }

        internal EntryStatus Status { get; set; } = EntryStatus.Running;

        internal Queue<ServiceProgress>? AfterRequest { get; set; }

        /// <summary>What the entry says once its process has been ended, when the manager brings it back.</summary>
        internal Queue<ServiceProgress>? ComesBack { get; set; }

        internal bool Moving { get; set; }

        internal int? RequestRefusedWith { get; set; }

        internal EntryStatus? BecomesOnRefusal { get; set; }

        internal int? ReadRefusedWith { get; set; }

        internal Arrival? Arrival { get; set; }

        /// <summary>When the manager was asked to move it, on the ruler of <see cref="Arrival"/>.</summary>
        internal TimeSpan AskedAt { get; set; }

        /// <summary>Where the request sent it, which is where it ends up once it arrives.</summary>
        internal EntryStatus Heading { get; set; }
    }

    private sealed record Arrival(IClock Clock, TimeSpan After, ServiceProgress Meanwhile);
}
