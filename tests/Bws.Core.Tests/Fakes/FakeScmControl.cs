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
internal sealed class FakeScmControl : IScmControl
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
            }
        }

        return ControlAnswer.Done();
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

        if (entry.RequestRefusedWith is { } refused)
        {
            if (entry.BecomesOnRefusal is { } becomes)
            {
                entry.Status = becomes;
            }

            return ControlAnswer.Refused(refused, $"refused with {refused}");
        }

        entry.Moving = true;

        // Nothing scripted means it is there by the time anybody looks, which is what most
        // services do and what most of these tests are not about.
        if (entry.AfterRequest is null)
        {
            entry.Status = operation == StepOperation.Stop ? EntryStatus.Stopped : EntryStatus.Running;
        }

        return ControlAnswer.Done();
    }

    public ControlAnswer Read(string serviceName)
    {
        var entry = Entry(serviceName);

        if (entry.ReadRefusedWith is { } refused)
        {
            return ControlAnswer.Refused(refused, $"refused with {refused}");
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

        internal EntryStatus Status { get; set; } = EntryStatus.Running;

        internal Queue<ServiceProgress>? AfterRequest { get; set; }

        internal bool Moving { get; set; }

        internal int? RequestRefusedWith { get; set; }

        internal EntryStatus? BecomesOnRefusal { get; set; }

        internal int? ReadRefusedWith { get; set; }
    }
}
