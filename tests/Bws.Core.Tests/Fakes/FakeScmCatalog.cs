namespace Bws.Core.Tests.Fakes;

/// <summary>
/// A service control manager that is not one.
///
/// This is the seam ADR-10 exists for: the real manager cannot be persuaded to produce a
/// particular situation on demand, so anything that reasons about entries is tested
/// against this instead. Two situations matter enough on their own to justify it.
///
/// The refusal path cannot be reached on the machine these were captured from at all. 05-PRZYPADKI-BRZEGOWE
/// records that reading configuration through the manager was refused zero times out of
/// 811 without administrator rights, and says in as many words that the refusal path
/// remains unverified on a real system. Here it is one line.
///
/// The other is timing. An entry that is halfway through starting exists for a second or
/// two on a real machine and never when a test wants one.
/// </summary>
internal sealed class FakeScmCatalog : IScmCatalog
{
    private readonly IReadOnlyList<ScmEntry> _entries;
    private readonly Dictionary<string, IReadOnlyList<string>> _dependents =
        new(StringComparer.OrdinalIgnoreCase);

    internal FakeScmCatalog(params ScmEntry[] entries) => _entries = entries;

    internal FakeScmCatalog(IEnumerable<ScmEntry> entries) => _entries = [.. entries];

    /// <summary>How many times somebody asked for the listing. Reading is not meant to repeat.</summary>
    internal int Reads { get; private set; }

    /// <summary>Names the double was asked about, in order, so a test can say how the cascade walked.</summary>
    internal List<string> DependentsAsked { get; } = [];

    /// <summary>Services refused when asked about, for the path a real machine will not produce on demand.</summary>
    internal HashSet<string> RefuseDependentsFor { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Teaches the double who breaks when a service stops.
    ///
    /// Stated rather than derived from what the entries declare, on purpose: the real
    /// manager answers this itself and knows things the declarations do not, so a double
    /// that inferred it would be agreeing with our own reasoning instead of standing in
    /// for the system.
    /// </summary>
    internal FakeScmCatalog DependedOnBy(string serviceName, params string[] dependents)
    {
        _dependents[serviceName] = dependents;
        return this;
    }

    /// <summary>How many times somebody asked only for what is moving.</summary>
    internal int StatusReads { get; private set; }

    public IReadOnlyList<ScmEntry> ReadAll()
    {
        Reads++;
        return _entries;
    }

    /// <summary>
    /// Taken from the same entries, because here the status genuinely is one of their fields
    /// and reading it twice from one fixture is not the double inferring anything. Anything
    /// that needs the status to <b>change</b> between two calls wants a different double -
    /// this one stands still on purpose, so a test that sees a change knows where it came from.
    /// </summary>
    public IReadOnlyList<ScmStatus> ReadStatuses()
    {
        StatusReads++;

        return [.. _entries.Select(entry => new ScmStatus(entry.ServiceName, entry.Status, entry.ProcessId))];
    }

    public Reading<IReadOnlyList<string>> ReadDependents(string serviceName)
    {
        DependentsAsked.Add(serviceName);

        if (RefuseDependentsFor.Contains(serviceName))
        {
            return Reading<IReadOnlyList<string>>.Denied(Entries.AccessDenied, "access denied");
        }

        return _dependents.TryGetValue(serviceName, out var dependents)
            ? Reading<IReadOnlyList<string>>.Present(dependents)
            : Reading<IReadOnlyList<string>>.Absent();
    }
}
