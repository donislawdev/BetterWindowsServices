namespace Bws.Core.Tests.Fakes;

/// <summary>
/// A process that can be told what to say about itself before anybody ends it.
///
/// <b>The one shape a real machine will not produce on demand.</b> Windows refuses to hand out the
/// right to end a process for exactly three processes on the machine this was written on, all of
/// them Defender or the account manager, and none of them is a thing to point a test at. The
/// answer that matters most - "the system said no" - is therefore unreachable in a test without
/// something like this, which is the same argument <see cref="FakeScmControl"/> makes about a
/// service that sits in StopPending for forty seconds.
///
/// <b>Its default is the ordinary case</b>: the handle opens, and the process has a creation time.
/// So a test that says nothing about a process gets the machine as it usually is, and every
/// interesting shape is one named call away.
/// </summary>
internal sealed class FakeEndingFacts : IEndingFactsReader
{
    /// <summary>
    /// A creation time that is a plausible file time rather than a small number, because a value
    /// that could be mistaken for a count of anything is a value somebody eventually compares
    /// against the wrong thing. This is roughly 2026 in ticks since 1601.
    /// </summary>
    internal const long ATimeLikeAnyOther = 134_000_000_000_000_000L;

    private readonly Dictionary<int, Reading<bool>> _rights = [];
    private readonly Dictionary<int, Reading<long>> _created = [];
    private readonly Dictionary<int, Reading<bool>> _critical = [];
    private readonly Dictionary<string, Reading<IReadOnlyList<RecoveryItem>>> _recovery = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every entry whose recovery list was asked for, in order.</summary>
    internal List<string> AskedRecovery { get; } = [];

    /// <summary>Windows marks this process critical - ending it stops the machine.</summary>
    internal FakeEndingFacts Critical(int processId)
    {
        _critical[processId] = Reading<bool>.Present(true);
        return this;
    }

    /// <summary>Nobody could read whether this process is critical.</summary>
    internal FakeEndingFacts CriticalUnreadable(int processId, int errorCode = 5)
    {
        _critical[processId] = Reading<bool>.Denied(errorCode, "Access is denied.");
        return this;
    }

    /// <summary>What the manager does to this entry when its process dies, item by item, each at once.</summary>
    internal FakeEndingFacts Recovering(string serviceName, params RecoveryAction[] actions) =>
        Recovering(serviceName, [.. actions.Select(action => new RecoveryItem(action, TimeSpan.Zero))]);

    /// <summary>The same with the delay of each item, for the sentences that say when (backlog 501).</summary>
    internal FakeEndingFacts Recovering(string serviceName, params RecoveryItem[] items)
    {
        _recovery[serviceName] = Reading<IReadOnlyList<RecoveryItem>>.Present(items);
        return this;
    }

    /// <summary>The manager will not say what it does to this entry.</summary>
    internal FakeEndingFacts RefusingRecovery(string serviceName, int errorCode = 5)
    {
        _recovery[serviceName] = Reading<IReadOnlyList<RecoveryItem>>.Denied(errorCode, "Access is denied.");
        return this;
    }

    /// <summary>
    /// The entry has gone from the manager, which is what an entry uninstalled between the listing and the
    /// question answers.
    /// </summary>
    internal FakeEndingFacts GoneFromTheManager(string serviceName)
    {
        _recovery[serviceName] = Reading<IReadOnlyList<RecoveryItem>>.Absent();
        return this;
    }

    public Reading<IReadOnlyList<RecoveryItem>> ReadRecovery(string serviceName)
    {
        AskedRecovery.Add(serviceName);

        // An entry nobody scripted has no recovery at all - the ordinary answer is a list with no items.
        return _recovery.TryGetValue(serviceName, out var recovery)
            ? recovery
            : Reading<IReadOnlyList<RecoveryItem>>.Present([]);
    }

    /// <summary>Every process this was asked about, in order, so a test can see it was asked once.</summary>
    internal List<int> Asked { get; } = [];

    /// <summary>Windows will not hand over the right to end this one.</summary>
    internal FakeEndingFacts Refusing(int processId, int errorCode = 5, string reason = "Access is denied.")
    {
        _rights[processId] = Reading<bool>.Denied(errorCode, reason);
        return this;
    }

    /// <summary>
    /// There is no such process any more - it went away between the listing and the question.
    ///
    /// <b>A different answer from the one above and it has to stay different.</b> One is about
    /// permission and the other is about existence, and a tool that says "Windows will not let me"
    /// about something that is simply not there is confidently describing the wrong subject.
    /// </summary>
    internal FakeEndingFacts Gone(int processId)
    {
        _rights[processId] = Reading<bool>.Absent();
        _created[processId] = Reading<long>.Absent();
        return this;
    }

    /// <summary>Nobody could read when this one started, though it can still be ended.</summary>
    internal FakeEndingFacts WithNoCreationTime(int processId)
    {
        _created[processId] = Reading<long>.NotRead();
        return this;
    }

    /// <summary>Started at a particular moment, for a test that cares which.</summary>
    internal FakeEndingFacts StartedAt(int processId, long fileTime)
    {
        _created[processId] = Reading<long>.Present(fileTime);
        return this;
    }

    public EndingFacts Read(int processId)
    {
        Asked.Add(processId);

        return new EndingFacts(
            _rights.TryGetValue(processId, out var rights) ? rights : Reading<bool>.Present(true),
            _created.TryGetValue(processId, out var created)
                ? created
                : Reading<long>.Present(ATimeLikeAnyOther),
            _critical.TryGetValue(processId, out var critical) ? critical : Reading<bool>.Present(false));
    }
}
