using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.ProcessStatus;
using Windows.Win32.System.Threading;

namespace Bws.Core;

/// <summary>
/// Asks the real processes what they are using.
///
/// Two calls per process, and the first one is the whole of what makes this safe to run on
/// somebody's production server: the handle is opened with
/// <see cref="PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION"/>, which allows
/// asking about a process and nothing else. It cannot read its memory, cannot write to it
/// and cannot stop it.
///
/// It is also the right that works. Measured on 2026-08-01 over the 110 processes behind
/// services: the limited right was refused by none, while the wider pair named first in the
/// documentation - PROCESS_QUERY_INFORMATION with PROCESS_VM_READ - was refused by seven,
/// all of them protected in one way or another. Reaching for the wider mask "to be safe"
/// would have lost those seven their answer and made it look like our bug.
/// </summary>
public sealed class WindowsProcessMemoryReader : IProcessMemoryReader
{
    public unsafe Reading<ProcessMemory> Read(int processId)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            bInheritHandle: false,
            (uint)processId);

        if (process.IsInvalid)
        {
            // Two different things arrive here and the number tells them apart. Access
            // denied is a protected process, and "no such process" is a service that ended
            // between the listing and this call - which is ordinary on a live machine and
            // is precisely why the number travels with the refusal.
            return Refused(Marshal.GetLastWin32Error());
        }

        // Sized for the extended structure, because the length handed over is what tells the
        // call how much to fill in. Asking with the shorter one would come back successful
        // with the commit figure left at zero, which is the worst shape of wrong answer
        // available here: a number, in the right place, meaning nothing.
        var buffer = new byte[sizeof(PROCESS_MEMORY_COUNTERS_EX)];

        if (!PInvoke.GetProcessMemoryInfo(process, buffer))
        {
            return Refused(Marshal.GetLastWin32Error());
        }

        fixed (byte* start = buffer)
        {
            var counters = *(PROCESS_MEMORY_COUNTERS_EX*)start;

            // SharedBy is filled in by the pass, which is the only place that knows how many
            // entries this process is running. One here means "not worked out yet" rather
            // than "not shared", and MemoryPass always replaces it.
            return Reading<ProcessMemory>.Present(new ProcessMemory(
                WorkingSet: (long)counters.WorkingSetSize,
                Commit: (long)counters.PrivateUsage,
                SharedBy: 1));
        }
    }

    private static Reading<ProcessMemory> Refused(int code) =>
        Reading<ProcessMemory>.Denied(code, new Win32Exception(code).Message);
}
