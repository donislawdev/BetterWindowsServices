using Bws.Core.Planning;

namespace Bws.Core.Tests.Fakes;

/// <summary>
/// A manager during whose work somebody presses Stop - after it takes a request, while it is being read for who
/// lives in a process, or while an entry's recovery is read again before it is written.
///
/// <b>Since 2026-10-26</b> (logic report R09). Until then the tests pressed Stop from the progress call the
/// runner makes just before a step, and the runner sent that step's request anyway - the very thing R09
/// closed. Pressed there, a step is held now, so the shape these tests are about - a step already asked for,
/// which the runner promises to watch to its end - has to be made after the request instead.
/// </summary>
/// <param name="when">When the lever is pulled - after the first request the manager takes unless said otherwise.</param>
internal sealed class Interrupting(
    FakeScmControl inner,
    CancellationTokenSource lever,
    Interrupting.Moment when = Interrupting.Moment.AfterTheFirstRequest) : IScmControl
{
    /// <summary>The moments a test can press Stop at.</summary>
    internal enum Moment
    {
        AfterTheFirstRequest,

        /// <summary>When the whole listing is read - just before a process is ended.</summary>
        WhileReadingStatuses,

        /// <summary>When an entry's recovery is read again - just before it is written, since 2026-11-25.</summary>
        WhileReadingRecovery
    }

    private int _requests;

    public ControlAnswer Request(string serviceName, StepOperation operation)
    {
        var answer = inner.Request(serviceName, operation);

        if (++_requests == 1 && when == Moment.AfterTheFirstRequest)
        {
            lever.Cancel();
        }

        return answer;
    }

    public ControlAnswer Configure(string serviceName, StartSetting wanted) => inner.Configure(serviceName, wanted);

    public ControlAnswer ConfigureRecovery(string serviceName, RecoveryChange change) =>
        inner.ConfigureRecovery(serviceName, change);

    public Reading<RecoverySettings> ReadRecovery(string serviceName)
    {
        if (when == Moment.WhileReadingRecovery)
        {
            lever.Cancel();
        }

        return inner.ReadRecovery(serviceName);
    }

    public ControlAnswer Terminate(int processId, long? createdAt) => inner.Terminate(processId, createdAt);

    public ControlAnswer Read(string serviceName) => inner.Read(serviceName);

    public Reading<IReadOnlyList<ScmStatus>> ReadStatuses()
    {
        if (when == Moment.WhileReadingStatuses)
        {
            lever.Cancel();
        }

        return inner.ReadStatuses();
    }

    public Reading<IReadOnlyList<string>> ReadDependents(string serviceName) => inner.ReadDependents(serviceName);

    public Reading<StartType> ReadStartType(string serviceName) => inner.ReadStartType(serviceName);
}
