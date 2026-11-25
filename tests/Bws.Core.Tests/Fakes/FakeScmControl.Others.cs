namespace Bws.Core.Tests.Fakes;

/// <summary>
/// Entries somebody else is moving while a plan runs, and the request the manager takes and loses - the two
/// shapes the tests of waiting for a pause need (PauseWaitingTests).
/// </summary>
internal sealed partial class FakeScmControl
{
    /// <summary>
    /// An entry somebody else is moving, read one reading per look from the very FIRST look - the readings
    /// <see cref="Reaching"/> hands out after a request, handed out before any. The last one repeats, and a
    /// request once they have played out starts from where the entry stands, as after <see cref="Reaching"/>.
    /// </summary>
    internal FakeScmControl MovedByOthers(string serviceName, params ServiceProgress[] readings)
    {
        var entry = Entry(serviceName);
        entry.AfterRequest = new Queue<ServiceProgress>(readings);
        entry.Moving = true;
        return this;
    }

    /// <summary>
    /// A request that arrives while the entry was last read in <paramref name="status"/> is taken and does
    /// nothing - measured on the throwaway machine on 2026-11-25 for a stop sent during PausePending, which the
    /// manager answered with success and the entry ended up Paused.
    /// </summary>
    internal FakeScmControl LosingRequestsWhile(string serviceName, EntryStatus status)
    {
        Entry(serviceName).LosesWhile = status;
        return this;
    }
}
