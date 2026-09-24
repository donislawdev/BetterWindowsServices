using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Services;
using Windows.Win32.System.Threading;
using Bws.Core.Planning;

namespace Bws.Core;

/// <summary>
/// The half of the real manager that changes things.
///
/// Three calls, and they are the entire list of ways this tool can alter a machine. Kept in
/// a class of its own rather than beside the reading, so that "what can it do to me" is
/// answerable by opening one short file.
///
/// Rights are asked for one operation at a time. A stop asks for the right to stop and
/// nothing else, so a machine where somebody may stop a service but not start it behaves
/// the way its administrator set it up, rather than failing on a right we took for
/// convenience.
/// </summary>
public sealed class WindowsScmControl : IScmControl
{
    public ControlAnswer Request(string serviceName, StepOperation operation) => operation switch
    {
        StepOperation.Stop => Stop(serviceName),
        StepOperation.Start => Start(serviceName),

        // THE MOST IMPORTANT OF THE NINE, and the reason all nine were changed. This one asks the
        // service manager to MOVE something. A step of a kind nobody taught it used to arrive here
        // and be sent as a start - a real machine changed by a step whose preview said something
        // else, which is the one fault the whole plan mechanism stands against.
        _ => throw new ArgumentOutOfRangeException(
            nameof(operation), operation, Planning.EquivalentCommand.Unhandled)
    };

    /// <summary>
    /// Ends a process. The one call in this project that nothing can refuse on the machine's behalf.
    ///
    /// <b>PROCESS_TERMINATE, and one more right ONLY when there is an identity to check</b>, which
    /// is the same rule the three above follow: ask for what this operation needs and not a bit
    /// more. Without a creation time the handle carries the right to end the process and nothing
    /// else - it cannot read a byte of that process and cannot write one. With a creation time it
    /// also carries PROCESS_QUERY_LIMITED_INFORMATION, which allows asking about a process and
    /// still cannot read, write or touch anything inside it.
    ///
    /// <b>That second right costs nothing, and it is measured rather than assumed.</b> A handle
    /// asking for two rights is refused when either is refused, so the honest worry is that adding
    /// it would lose the ending on machines that would otherwise have allowed it. Counted on
    /// 2026-09-08 over 186 processes behind services on two machines: that right was refused zero
    /// times, including by all eleven processes Windows was protecting.
    ///
    /// <b>THE HANDLE IS WHAT MAKES THE CHECK WORTH ANYTHING, AND THIS PARAGRAPH USED TO SAY THE
    /// OPPOSITE.</b> It said a window remains between the caller's last reading of the process
    /// number and this call, that closing it needs an identity Windows does not hand out in one
    /// piece, and that pretending otherwise would be the kind of sentence this project warns about.
    /// The first half is still true and the conclusion was wrong. Windows does not reuse a process
    /// number while somebody holds a handle to it - so a check made THROUGH this handle, before
    /// ending through the same one, has nothing that can slip between the two. The identity is
    /// still handed out in two pieces. They just do not have to be collected at the same moment.
    /// Backlog 323.
    /// </summary>
    public ControlAnswer Terminate(int processId, long? createdAt)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(
            createdAt is null
                ? PROCESS_ACCESS_RIGHTS.PROCESS_TERMINATE
                : PROCESS_ACCESS_RIGHTS.PROCESS_TERMINATE
                    | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            bInheritHandle: false,
            (uint)processId);

        if (process.IsInvalid)
        {
            // A process that has already gone, no such number at all, or a live process whose own
            // access control list says no. NOT "a protected process", which is what this comment
            // said until two machines disagreed with it on 2026-09-08 - PROCESS_TERMINATE is not
            // among the rights Windows withholds from a protected process, so protection on its
            // own never arrives here. The number travels with the refusal because it is the only
            // thing that tells the three apart, and the layer above asks the entry where it is
            // before calling any of them a failure.
            return Refusal();
        }

        if (createdAt is { } expected && Started(process) != expected)
        {
            // EVERY WAY OF NOT GETTING A MATCH ENDS HERE, INCLUDING NOT GETTING AN ANSWER AT ALL,
            // and that is the only safe shape for the one operation in this product that cannot be
            // undone. An identity that cannot be confirmed is not a reason to go ahead.
            return ControlAnswer.Refused(0, ProcessIsNotTheSameOne);
        }

        // The exit code a killed process reports. One rather than zero, because zero is what a
        // process that finished its own work reports, and anything reading an exit code afterwards
        // would otherwise be told this one shut down cleanly.
        return PInvoke.TerminateProcess(process, uExitCode: 1)
            ? ControlAnswer.Done()
            : Refusal();
    }

    /// <summary>
    /// When the process behind this handle started, or a value nothing can match.
    ///
    /// <b>A failure to read comes back as a number no file time can be rather than as a separate
    /// answer</b>, because the caller has exactly one question - is this the same process - and
    /// "I could not tell" is a no. Zero is the value chosen for it: a real creation time is ticks
    /// since 1601 and is never zero for a process that exists.
    /// </summary>
    /// <remarks>
    /// Written as a statement rather than as one expression, and that is not a style choice.
    /// <c>NativeCallGuards</c> reads source rather than meaning, and its own header names this
    /// exact blind spot: a native call beginning a continuation line inside a larger expression
    /// looks to it like a call whose answer was dropped. The answer IS read here. Putting the call
    /// where the guard can see it read costs three lines and keeps a guard that has caught this
    /// project's most repeated interop mistake from having to be argued with.
    /// </remarks>
    private static long Started(SafeHandle process)
    {
        if (!PInvoke.GetProcessTimes(process, out var created, out _, out _, out _))
        {
            return 0;
        }

        return ((long)(uint)created.dwHighDateTime << 32) | (uint)created.dwLowDateTime;
    }

    /// <summary>
    /// Said in the plainest words available, and it is one of the two refusals in this project that
    /// are ours rather than the system's - the other is the caller's, for a process the entry no
    /// longer reports at all. This one is narrower and stranger: the entry still names this number,
    /// and the number no longer names the same process.
    /// </summary>
    private const string ProcessIsNotTheSameOne =
        "The process behind this entry is no longer the one the plan named - the number has been "
        + "given to something else. Nothing was ended. Ask again to build a plan against the "
        + "machine as it is now.";

    public ControlAnswer Read(string serviceName)
    {
        using var handle = Open(serviceName, PInvoke.SERVICE_QUERY_STATUS, out var refusal);

        if (handle is null)
        {
            return refusal!;
        }

        return ReadProgress(handle);
    }


    /// <summary>
    /// Writes the startup setting - the start type and the late start flag - and nothing else.
    ///
    /// <b>SERVICE_NO_CHANGE in every other field, which is the manager's own way of saying "leave
    /// that one alone".</b> The call takes the whole configuration - the binary path, the account,
    /// the load order group, the dependencies - and a tool that passed what it had read a moment
    /// ago would rewrite all of them from a copy that may already be stale. That is the quietest
    /// possible way to break somebody's machine, and it is what this constant exists to prevent.
    ///
    /// <b>THE FLAG IS WRITTEN TOO SINCE 2026-09-24, and until then it was the one field this left
    /// alone on purpose.</b> Our "Automatic" and the Automatic sc.exe and services.msc write were not
    /// the same write: sc.exe clears the flag on every start type, this did not, so choosing
    /// Automatic on a delayed entry kept it delayed (backlog 231, measured 2026-08-25). Every setting
    /// now writes both halves, as sc.exe does - the owner's decision, and the reason a way back can
    /// be an exact inverse.
    ///
    /// <b>ONE HANDLE, TWO CALLS, AND THE ORDER DEPENDS ON WHERE THE ENTRY IS GOING.</b> Two writes
    /// are not one, so something can land between them. The order makes the only possible half a
    /// harmless one: the flag does nothing unless the entry is automatic (Microsoft's page on
    /// SERVICE_DELAYED_AUTO_START_INFO, and measured on the throwaway machine). Going TO automatic,
    /// the flag goes first, while the old type still makes it inert. Going AWAY from automatic, the
    /// type goes first, which makes the flag inert before it is touched. Automatic to automatic is
    /// the one case where the flag is the whole change, and there the second call writes the type
    /// the entry already has.
    ///
    /// <b>An entry in a load order group refuses the flag with 87</b> - measured on Spooler and
    /// SCardSvr, and sc.exe gets the same answer. The plan refuses that before anybody presses, so
    /// reaching it here means the group could not be read. Going to automatic, the flag is the first
    /// call, so nothing has changed when it is refused.
    ///
    /// <b>The right asked for is SERVICE_CHANGE_CONFIG and only that</b>, the same rule the two
    /// requests above follow: a machine where somebody may change a setting but not stop a service
    /// behaves the way its administrator set it up. Both calls need exactly that right.
    /// </summary>
    public ControlAnswer Configure(string serviceName, StartSetting wanted)
    {
        if (!Enum.IsDefined(wanted))
        {
            return ControlAnswer.Refused(0, StartSettings.NoSuchSetting);
        }

        var (type, delayed) = StartSettings.Written(wanted);
        var code = Numbered(type)!.Value;

        using var handle = Open(serviceName, ChangeConfig, out var refusal);

        if (handle is null)
        {
            return refusal!;
        }

        var flagFirst = StartSettings.StartsAtBoot(wanted);

        if (!(flagFirst ? WriteFlag(handle, delayed) : WriteType(handle, code)))
        {
            return FirstRefused(delayed && flagFirst);
        }

        return (flagFirst ? WriteType(handle, code) : WriteFlag(handle, delayed))
            ? ControlAnswer.Done()
            : HalfWritten(flagFirst);
    }

    /// <summary>
    /// The start type half. The overload WITHOUT the tag identifier, and that is a choice rather
    /// than the shorter line: the other one hands back a tag through an out parameter, and a tag is
    /// part of the load order group this call is being told to leave alone.
    /// </summary>
    private static bool WriteType(SafeHandle handle, SERVICE_START_TYPE code) => PInvoke.ChangeServiceConfig(
            handle,
            (ENUM_SERVICE_TYPE)NoChange,
            code,
            (SERVICE_ERROR)NoChange,
            lpBinaryPathName: null!,
            lpLoadOrderGroup: null!,
            lpDependencies: null!,
            lpServiceStartName: null!,
            lpPassword: null!,
            lpDisplayName: null!);

    /// <summary>The late start half, at the one information level that holds nothing else.</summary>
    private static unsafe bool WriteFlag(SafeHandle handle, bool delayed)
    {
        var info = new SERVICE_DELAYED_AUTO_START_INFO { fDelayedAutostart = delayed };

        return PInvoke.ChangeServiceConfig2W(handle, SERVICE_CONFIG.SERVICE_CONFIG_DELAYED_AUTO_START_INFO, &info);
    }

    /// <summary>
    /// The first write refused, so nothing is on the machine. The manager's own words, except for
    /// the one refusal whose words say nothing: 87 on the late start flag is "the parameter is
    /// incorrect", and what it means there is a load order group.
    /// </summary>
    private static ControlAnswer FirstRefused(bool wasTheLateFlag)
    {
        var code = Marshal.GetLastWin32Error();

        return wasTheLateFlag && code == InvalidParameter
            ? ControlAnswer.Refused(code, CannotStartLate)
            : ControlAnswer.Refused(code, ManagerTerms.Describe(code));
    }

    /// <summary>
    /// The second write refused after the first landed. Said in full, because a refusal reads as
    /// "nothing happened" and here something did - which half, and that the half is inert.
    /// </summary>
    private static ControlAnswer HalfWritten(bool flagWent)
    {
        var code = Marshal.GetLastWin32Error();
        var half = flagWent ? OnlyTheFlag : OnlyTheType;

        return ControlAnswer.Refused(code, half + " " + ManagerTerms.Describe(code));
    }

    /// <summary>ERROR_INVALID_PARAMETER, the manager's answer to a late start in a load order group.</summary>
    private const int InvalidParameter = 87;

    private const string CannotStartLate =
        "Windows does not let this entry start late - it belongs to a load order group, and a "
        + "delayed entry cannot.";

    private const string OnlyTheFlag =
        "The late start mark was written and the start type was not. The mark only does anything on "
        + "an entry that is already automatic.";

    private const string OnlyTheType =
        "The start type was written and the late start mark was not cleared. The mark does nothing "
        + "on an entry that is not automatic.";

    /// <summary>The manager's own word for "leave this field as it is".</summary>
    private const uint NoChange = 0xFFFFFFFF;

    /// <summary>
    /// The right to write a service's configuration.
    ///
    /// <b>Spelled here rather than asked of the generator</b>, because the generator does not offer
    /// it: the three rights beside it in NativeMethods.txt come through as constants and this one
    /// is not among the names that file can request. It is documented as 0x0002 and has been since
    /// Windows NT.
    /// </summary>
    private const uint ChangeConfig = 0x0002;

    /// <summary>
    /// The number the manager uses for a start type, or nothing for one it cannot be told.
    ///
    /// <b>Boot and System have no number here</b>, and since 2026-09-24 nothing can ask for them:
    /// <see cref="StartSetting"/> has no value that writes either. Those two belong to entries this
    /// tool will not operate on, and writing one onto a service is a machine that may not come back.
    /// </summary>
    private static SERVICE_START_TYPE? Numbered(StartType wanted) => wanted switch
    {
        StartType.Automatic => SERVICE_START_TYPE.SERVICE_AUTO_START,
        StartType.Manual => SERVICE_START_TYPE.SERVICE_DEMAND_START,
        StartType.Disabled => SERVICE_START_TYPE.SERVICE_DISABLED,
        _ => null
    };
    private static ControlAnswer Stop(string serviceName)
    {
        using var handle = Open(serviceName, PInvoke.SERVICE_STOP, out var refusal);

        if (handle is null)
        {
            return refusal!;
        }

        // The returned status is deliberately dropped. It is what the manager knew at the
        // moment it took the request, which is a snapshot already going stale, and mixing it
        // with the answers from the watching loop would give two sources for one question.
        return PInvoke.ControlService(handle, PInvoke.SERVICE_CONTROL_STOP, out _)
            ? ControlAnswer.Done()
            : Refusal();
    }

    private static ControlAnswer Start(string serviceName)
    {
        using var handle = Open(serviceName, PInvoke.SERVICE_START, out var refusal);

        if (handle is null)
        {
            return refusal!;
        }

        // No arguments. Windows passes these to the service as command line arguments for
        // this start only, and nothing in this tool has any to pass - the specification puts
        // arguments under editing a service's own configuration, which is a different thing
        // and a later slice.
        return PInvoke.StartService(handle, default)
            ? ControlAnswer.Done()
            : Refusal();
    }

    private static unsafe ControlAnswer ReadProgress(SafeHandle handle)
    {
        // Fixed size structure, so asking for the length first would only double the calls,
        // and this one runs repeatedly while a step is watched.
        var buffer = new byte[sizeof(SERVICE_STATUS_PROCESS)];

        if (!PInvoke.QueryServiceStatusEx(handle, SC_STATUS_TYPE.SC_STATUS_PROCESS_INFO, buffer, out _))
        {
            return Refusal();
        }

        fixed (byte* start = buffer)
        {
            var status = *(SERVICE_STATUS_PROCESS*)start;

            return ControlAnswer.At(new ServiceProgress(
                ManagerTerms.Status(status.dwCurrentState),
                status.dwCheckPoint,
                TimeSpan.FromMilliseconds(status.dwWaitHint),
                status.dwProcessId));
        }
    }

    /// <summary>
    /// Opens one entry for one purpose, or explains why not.
    ///
    /// Null with a refusal rather than an exception, because being turned away is ordinary
    /// here: without elevation the manager refuses the right to stop anything, and a service
    /// can be set up to refuse even an administrator. That is a fact to report, not an
    /// accident to throw over.
    /// </summary>
    private static SafeHandle? Open(string serviceName, uint rights, out ControlAnswer? refusal)
    {
        var manager = PInvoke.OpenSCManager(
            lpMachineName: null!,
            lpDatabaseName: null!,
            dwDesiredAccess: PInvoke.SC_MANAGER_CONNECT);

        using (manager)
        {
            if (manager.IsInvalid)
            {
                refusal = Refusal();
                return null;
            }

            var service = PInvoke.OpenService(manager, serviceName, rights);

            if (service.IsInvalid)
            {
                refusal = Refusal();
                service.Dispose();
                return null;
            }

            refusal = null;
            return service;
        }
    }

    private static ControlAnswer Refusal()
    {
        var code = Marshal.GetLastWin32Error();

        return ControlAnswer.Refused(code, ManagerTerms.Describe(code));
    }
}
