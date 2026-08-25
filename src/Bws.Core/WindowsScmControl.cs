using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Services;
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
    /// Writes the start type and nothing else.
    ///
    /// <b>SERVICE_NO_CHANGE in every other field, which is the manager's own way of saying "leave
    /// that one alone".</b> The call takes the whole configuration - the binary path, the account,
    /// the load order group, the dependencies - and a tool that passed what it had read a moment
    /// ago would rewrite all of them from a copy that may already be stale. That is the quietest
    /// possible way to break somebody's machine, and it is what this constant exists to prevent.
    ///
    /// <b>The right asked for is SERVICE_CHANGE_CONFIG and only that</b>, the same rule the two
    /// requests above follow: a machine where somebody may change a setting but not stop a service
    /// behaves the way its administrator set it up.
    /// </summary>
    public ControlAnswer Configure(string serviceName, StartType wanted)
    {
        var start = Numbered(wanted);

        if (start is not { } code)
        {
            return ControlAnswer.Refused(0, "There is no such start type to write.");
        }

        using var handle = Open(serviceName, ChangeConfig, out var refusal);

        if (handle is null)
        {
            return refusal!;
        }

        // The overload WITHOUT the tag identifier, and that is a choice rather than the shorter
        // line: the other one hands back a tag through an out parameter, and a tag is part of the
        // load order group this call is being told to leave alone.
        return PInvoke.ChangeServiceConfig(
            handle,
            (ENUM_SERVICE_TYPE)NoChange,
            code,
            (SERVICE_ERROR)NoChange,
            lpBinaryPathName: null!,
            lpLoadOrderGroup: null!,
            lpDependencies: null!,
            lpServiceStartName: null!,
            lpPassword: null!,
            lpDisplayName: null!)
            ? ControlAnswer.Done()
            : Refusal();
    }

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
    /// <b>Boot and System are refused rather than translated</b>, and that is the same decision the
    /// plan builder already makes about drivers: those two belong to entries this tool will not
    /// operate on, and writing one onto a service is a machine that may not come back. Unknown is
    /// refused because it is not a type at all - it is what a reading says when the manager did
    /// not answer.
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
                TimeSpan.FromMilliseconds(status.dwWaitHint)));
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
