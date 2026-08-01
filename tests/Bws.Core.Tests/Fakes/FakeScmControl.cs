using Bws.Core;
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
            return ControlAnswer.At(new ServiceProgress(entry.Status, CheckPoint: 0, TimeSpan.Zero));
        }

        // The last reading stays on the table. An entry that has arrived should keep saying
        // so, and one that is stuck should keep being stuck however long anybody watches.
        var reading = entry.AfterRequest.Count > 1
            ? entry.AfterRequest.Dequeue()
            : entry.AfterRequest.Peek();

        entry.Status = reading.Status;

        return ControlAnswer.At(reading);
    }

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
        internal EntryStatus Status { get; set; } = EntryStatus.Running;

        internal Queue<ServiceProgress>? AfterRequest { get; set; }

        internal bool Moving { get; set; }

        internal int? RequestRefusedWith { get; set; }

        internal EntryStatus? BecomesOnRefusal { get; set; }

        internal int? ReadRefusedWith { get; set; }
    }
}
