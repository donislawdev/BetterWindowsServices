namespace Bws.Core.Tests.Fakes;

/// <summary>
/// Who lives in a process and who depends on them, as the step that ends a process reads it just before it
/// does. Since 2026-09-30, stability report W-6, package B2.
///
/// <b>Answered from the same entries the rest of this double moves</b>, so an entry the double stopped holds
/// no process here either, and a neighbour a plan's own step stopped never looks like a stranger. A second
/// double for these two questions would be two machines in one test that could disagree.
/// </summary>
internal sealed partial class FakeScmControl
{
    private readonly Dictionary<string, List<string>> _dependents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _dependentsRefusedWith = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Reading<StartType>> _startTypes = new(StringComparer.OrdinalIgnoreCase);
    private int? _statusesRefusedWith;

    /// <summary>
    /// The start type the manager holds for an entry now - since 2026-10-26, for the run's look before its first
    /// step (logic report R03). An entry nobody set is Manual, which is what lets every older test go on as it was.
    /// </summary>
    internal FakeScmControl StartingAs(string serviceName, StartType startType)
    {
        _startTypes[serviceName] = Reading<StartType>.Present(startType);
        return this;
    }

    /// <summary>The manager will not say how an entry starts.</summary>
    internal FakeScmControl RefusingStartType(string serviceName, int errorCode)
    {
        _startTypes[serviceName] = Reading<StartType>.Denied(errorCode, $"refused with {errorCode}");
        return this;
    }

    /// <summary>How many times anybody asked how an entry starts.</summary>
    internal int StartTypeReads { get; private set; }

    public Reading<StartType> ReadStartType(string serviceName)
    {
        StartTypeReads++;

        return _startTypes.GetValueOrDefault(serviceName, Reading<StartType>.Present(StartType.Manual));
    }

    /// <summary>Entries that depend on this one, whatever they are doing.</summary>
    internal FakeScmControl DependedOnBy(string serviceName, params string[] dependents)
    {
        _dependents[serviceName] = [.. dependents];
        return this;
    }

    /// <summary>The manager will not enumerate anything.</summary>
    internal FakeScmControl RefusingStatuses(int errorCode)
    {
        _statusesRefusedWith = errorCode;
        return this;
    }

    /// <summary>The manager will not say who depends on this entry.</summary>
    internal FakeScmControl RefusingDependents(string serviceName, int errorCode)
    {
        _dependentsRefusedWith[serviceName] = errorCode;
        return this;
    }

    public Reading<IReadOnlyList<ScmStatus>> ReadStatuses()
    {
        if (_statusesRefusedWith is { } refused)
        {
            return Reading<IReadOnlyList<ScmStatus>>.Denied(refused, $"refused with {refused}");
        }

        return Reading<IReadOnlyList<ScmStatus>>.Present(
        [
            .. _entries.Select(pair => new ScmStatus(
                pair.Key,
                pair.Value.Status,
                Held(pair.Value) == 0 ? Reading<int>.Absent() : Reading<int>.Present((int)Held(pair.Value))))
        ]);
    }

    public Reading<IReadOnlyList<string>> ReadDependents(string serviceName)
    {
        if (_dependentsRefusedWith.TryGetValue(serviceName, out var refused))
        {
            return Reading<IReadOnlyList<string>>.Denied(refused, $"refused with {refused}");
        }

        return Reading<IReadOnlyList<string>>.Present(
            _dependents.TryGetValue(serviceName, out var dependents) ? dependents : []);
    }
}
