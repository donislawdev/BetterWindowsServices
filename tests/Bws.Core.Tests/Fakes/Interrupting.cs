using Bws.Core.Planning;

namespace Bws.Core.Tests.Fakes;

/// <summary>
/// A manager during whose work somebody presses Stop - after it takes a request, or while it is being read
/// for who lives in a process.
///
/// <b>Since 2026-10-26</b> (logic report R09). Until then the tests pressed Stop from the progress call the
/// runner makes just before a step, and the runner sent that step's request anyway - the very thing R09
/// closed. Pressed there, a step is held now, so the shape these tests are about - a step already asked for,
/// which the runner promises to watch to its end - has to be made after the request instead.
/// </summary>
/// <param name="whileReadingStatuses">
/// Pull the lever when the whole listing is read - just before a process is ended - rather than after the first
/// request the manager takes.
/// </param>
internal sealed class Interrupting(
    FakeScmControl inner,
    CancellationTokenSource lever,
    bool whileReadingStatuses = false) : IScmControl
{
    private int _requests;

    public ControlAnswer Request(string serviceName, StepOperation operation)
    {
        var answer = inner.Request(serviceName, operation);

        if (++_requests == 1 && !whileReadingStatuses)
        {
            lever.Cancel();
        }

        return answer;
    }

    public ControlAnswer Configure(string serviceName, StartSetting wanted) => inner.Configure(serviceName, wanted);

    public ControlAnswer Terminate(int processId, long? createdAt) => inner.Terminate(processId, createdAt);

    public ControlAnswer Read(string serviceName) => inner.Read(serviceName);

    public Reading<IReadOnlyList<ScmStatus>> ReadStatuses()
    {
        if (whileReadingStatuses)
        {
            lever.Cancel();
        }

        return inner.ReadStatuses();
    }

    public Reading<IReadOnlyList<string>> ReadDependents(string serviceName) => inner.ReadDependents(serviceName);

    public Reading<StartType> ReadStartType(string serviceName) => inner.ReadStartType(serviceName);
}
