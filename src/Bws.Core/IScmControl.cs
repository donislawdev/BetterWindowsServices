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
/// <param name="ProcessId">
/// The process behind the entry, or zero when there is none - raw, exactly as the manager
/// reported it, the same way <paramref name="CheckPoint"/> is. It comes out of the same
/// structure as everything else here and costs nothing to carry.
///
/// <b>Here so that a step which gave up can name what it gave up on.</b> "Still stopping" and
/// "still stopping, process 4812" are the same fact with and without somewhere to go next.
/// Zero becomes an absence one layer up, where the four read states live.
/// </param>
public readonly record struct ServiceProgress(
    EntryStatus Status, uint CheckPoint, TimeSpan WaitHint, uint ProcessId);

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

    /// <summary>
    /// Write an entry's start type - what the manager does with it at boot.
    ///
    /// <b>The first thing on this interface that changes CONFIGURATION rather than asking the
    /// manager to move something</b>, and the difference is the whole reason it is a second method
    /// rather than a third value passed to the first. A request is taken now and arrives later, so
    /// the caller waits and watches. This is done when it returns: there is no half-configured
    /// state to watch for, no wait hint, and nothing to poll.
    /// </summary>
    ControlAnswer Configure(string serviceName, StartType wanted);

    /// <summary>
    /// End a process, without asking anything whether it minds.
    ///
    /// <b>THE ONLY METHOD ON THIS INTERFACE THAT DOES NOT TOUCH THE SERVICE CONTROL MANAGER, and it
    /// lives here anyway on purpose.</b> The read-only mode section F promises is the absence of
    /// this interface - one thing not to hold. Putting the one call that can end a process behind a
    /// second interface would make that promise a pair of things to remember, and a promise you
    /// have to remember twice is one somebody eventually keeps once.
    ///
    /// <b>It takes a process rather than a service, which is the narrowest surface that can do the
    /// job.</b> Deciding WHICH process belongs to an entry is a reading, and readings are worked
    /// out above this seam where a test can drive them - the same line every other method here
    /// draws. Checking that the number still means what the plan said is the caller's job for the
    /// same reason.
    ///
    /// <b>Returning does not mean the process is gone.</b> Win32 documents the call as
    /// asynchronous: it starts the ending and comes back, and a process with pending driver work
    /// cannot exit until that work is finished or cancelled. So this answers whether the request
    /// was accepted, and where the entry ended up is a separate question asked afterwards - the
    /// same shape as <see cref="Request"/>.
    ///
    /// <b>Access denied means two different things and only a reading tells them apart.</b> A
    /// process that has already gone refuses to be opened for this - Win32 documents error 5 for a
    /// terminate on a process that has ended - and so does a live process whose own access control
    /// list says no. Asking the entry where it is afterwards answers which happened.
    ///
    /// <b>WHAT IT DOES NOT MEAN IS "PROTECTED", AND THAT IS EXACTLY WHAT THIS PARAGRAPH SAID
    /// UNTIL SOMEBODY MEASURED IT.</b> Windows publishes the rights it withholds from a protected
    /// process and PROCESS_TERMINATE is not among them, so protection on its own never refuses
    /// this. Counted on two machines on 2026-09-08: seven protected service processes here and
    /// three refusals, four there and two - and one program answering opposite ways on the two
    /// machines at the same protection level. A protection level is a reason to show a person
    /// beside a refusal, never the refusal itself.
    /// </summary>
    ControlAnswer Terminate(int processId);

    /// <summary>Where the entry is now. The answer says whether it could be read at all.</summary>
    ControlAnswer Read(string serviceName);
}
