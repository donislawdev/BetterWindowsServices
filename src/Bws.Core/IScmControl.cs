using Bws.Core.Planning;

namespace Bws.Core;

/// <summary>
/// Where an entry is, and whether it is still on its way somewhere.
///
/// The last two members are the entry's own account of itself and they are the reason
/// waiting is not a fixed sleep. Win32 documents the promise a service makes: before
/// <see cref="WaitHint"/> elapses it will either raise <see cref="CheckPoint"/> or change
/// state, and if it does neither, whoever asked may take it as gone wrong. So the entry
/// sets its own deadline, and a service that legitimately needs half a minute is not
/// reported as stuck by a tool that decided ten seconds was enough.
/// </summary>
public readonly record struct ServiceProgress(EntryStatus Status, uint CheckPoint, TimeSpan WaitHint);

/// <summary>
/// What the manager said. Facts only - the wording belongs to the layer above, same as
/// with a plan warning.
/// </summary>
/// <param name="Worked">Whether the manager did what was asked of it.</param>
/// <param name="ErrorCode">
/// The manager's own number for the refusal, or zero. Kept alongside the message because
/// the number is what a script keys on, and because the error decoder the specification
/// promises reads numbers rather than prose.
/// </param>
/// <param name="Error">The system's own words for that number, in the system language.</param>
/// <param name="Progress">Filled in by a read, never by a request.</param>
public sealed record ControlAnswer(bool Worked, int ErrorCode, string? Error, ServiceProgress? Progress)
{
    public static ControlAnswer Done() => new(true, 0, null, null);

    public static ControlAnswer At(ServiceProgress progress) => new(true, 0, null, progress);

    public static ControlAnswer Refused(int errorCode, string error) => new(false, errorCode, error, null);
}

/// <summary>
/// The half of the service control manager that changes things.
///
/// Separate from <see cref="IScmCatalog"/> rather than added to it, for two reasons that
/// are not tidiness. Reading and writing need different rights on a service, so a machine
/// where one works and the other does not is ordinary rather than odd. And the read-only
/// mode the specification promises in section F is then a matter of not holding one of
/// these at all, which is a far stronger promise than a flag inside a class that can also
/// write.
///
/// Deliberately primitive. Asking is one call, arriving is another, and the waiting in
/// between - which is where the interesting mistakes live - is worked out above this seam
/// where a test can drive it without a machine to break.
/// </summary>
public interface IScmControl
{
    /// <summary>
    /// Ask the manager to take an entry somewhere.
    ///
    /// Returns when the manager has taken the request, not when the entry has arrived.
    /// Taking it is not instant either: Win32 documents that the manager handles control
    /// codes one at a time and that this blocks for half a minute if another service is
    /// busy with one, failing with ERROR_SERVICE_REQUEST_TIMEOUT if it stays busy. So this
    /// call can sit for a while on its own, before any waiting the caller does.
    /// </summary>
    ControlAnswer Request(string serviceName, StepOperation operation);

    /// <summary>Where the entry is now. The answer says whether it could be read at all.</summary>
    ControlAnswer Read(string serviceName);
}
