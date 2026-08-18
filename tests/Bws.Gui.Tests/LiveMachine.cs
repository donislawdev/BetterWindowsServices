using Bws.Core;

namespace Bws.Gui.Tests;

/// <summary>
/// A machine whose services move while you are looking at them.
///
/// Its own double rather than the one in the core's tests, and the difference is the whole
/// reason it exists: that one stands still on purpose, and everything this slice is about
/// happens between two readings. Here a service can be stopped, started, installed or removed
/// between one call and the next, which a real machine will not do on demand.
///
/// It also counts which of the two readings it was asked for. "The window asks the cheap
/// question once a second and the expensive one almost never" is a claim about behaviour, and
/// a claim about behaviour needs something counting.
/// </summary>
internal sealed class LiveMachine : IScmCatalog
{
    private readonly Dictionary<string, ScmEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = [];

    internal LiveMachine(params ScmEntry[] entries)
    {
        foreach (var entry in entries)
        {
            _entries[entry.ServiceName] = entry;
            _order.Add(entry.ServiceName);
        }
    }

    /// <summary>How many full readings were asked for. Each one costs about half a second on a real machine.</summary>
    internal int FullReads { get; private set; }

    /// <summary>How many cheap readings were asked for.</summary>
    internal int StatusReads { get; private set; }

    /// <summary>Thrown by the next reading of either kind, then cleared.</summary>
    internal Exception? FailNext { get; set; }

    /// <summary>
    /// Holds every reading inside the manager until it is released.
    ///
    /// The only way to have two readings genuinely in flight at once from a test. Without it,
    /// a double is so fast that the first call has finished before the second is made, and a
    /// test about two overlapping readings would be a test about one.
    /// </summary>
    private readonly ManualResetEventSlim _gate = new(initialState: true);

    internal void HoldReadings() => _gate.Reset();

    internal void ReleaseReadings() => _gate.Set();

    internal void Stop(string serviceName) => _entries[serviceName] = _entries[serviceName] with
    {
        Status = EntryStatus.Stopped,
        ProcessId = Reading<int>.Absent()
    };

    internal void Start(string serviceName, int processId) => _entries[serviceName] = _entries[serviceName] with
    {
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(processId)
    };

    internal void Install(ScmEntry entry)
    {
        _entries[entry.ServiceName] = entry;
        _order.Add(entry.ServiceName);
    }

    internal void Remove(string serviceName)
    {
        _entries.Remove(serviceName);
        _order.Remove(serviceName);
    }

    /// <summary>Changes something a cheap reading cannot see, which is what tells the two apart.</summary>
    internal void Rename(string serviceName, string displayName) =>
        _entries[serviceName] = _entries[serviceName] with { DisplayName = displayName };

    public IReadOnlyList<ScmEntry> ReadAll()
    {
        FullReads++;
        _gate.Wait();
        Throw();

        return [.. _order.Select(name => _entries[name])];
    }

    public IReadOnlyList<ScmStatus> ReadStatuses()
    {
        StatusReads++;
        _gate.Wait();
        Throw();

        return [.. _order.Select(name => new ScmStatus(name, _entries[name].Status, _entries[name].ProcessId))];
    }

    private readonly Dictionary<string, IReadOnlyList<string>> _dependents =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Says that these entries break if that one stops.
    ///
    /// <b>Added 2026-08-18 for the plan panel, and the default below is deliberately unchanged.</b>
    /// Until then this double answered Absent to every such question, which is a machine where nothing
    /// depends on anything - fine for a listing and useless for a plan, because a cascade is the half
    /// of a plan worth previewing. Every test written before this one keeps the answer it had.
    ///
    /// <b>The transitive set, stated as the manager states it.</b> Measured on a real machine on
    /// 2026-08-01: the manager's own answer already reaches past the first hop, so a double that
    /// listed only direct dependants would be a double of something else.
    /// </summary>
    internal LiveMachine DependedOnBy(string serviceName, params string[] dependents)
    {
        _dependents[serviceName] = dependents;

        return this;
    }

    public Reading<IReadOnlyList<string>> ReadDependents(string serviceName) =>
        _dependents.TryGetValue(serviceName, out var dependents)
            ? Reading<IReadOnlyList<string>>.Present(dependents)
            : Reading<IReadOnlyList<string>>.Absent();

    private void Throw()
    {
        if (FailNext is null)
        {
            return;
        }

        var failure = FailNext;
        FailNext = null;

        throw failure;
    }
}

/// <summary>
/// A clock that only moves when told.
///
/// The highlight fades after a few seconds, and a test that waited those seconds would be a
/// test nobody runs. This one skips them.
/// </summary>
internal sealed class SteppedClock : IClock
{
    private DateTimeOffset _now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset Now => _now;

    public void Wait(TimeSpan duration) => _now += duration;

    internal void Advance(TimeSpan duration) => _now += duration;
}
