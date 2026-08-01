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
    public ControlAnswer Request(string serviceName, StepOperation operation) =>
        operation == StepOperation.Stop
            ? Stop(serviceName)
            : Start(serviceName);

    public ControlAnswer Read(string serviceName)
    {
        using var handle = Open(serviceName, PInvoke.SERVICE_QUERY_STATUS, out var refusal);

        if (handle is null)
        {
            return refusal!;
        }

        return ReadProgress(handle);
    }

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
