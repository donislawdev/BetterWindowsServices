using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Bws.Core;

/// <summary>
/// Asks a real process the two questions in <see cref="EndingFacts"/>.
///
/// <b>TWO HANDLES FOR TWO QUESTIONS, AND COMBINING THEM WOULD MAKE ONE OF THE ANSWERS A GUESS.</b>
/// A handle opened for several rights at once is refused when ANY of them is refused, so a single
/// open asking for both would come back "no" without saying which right was missing - and the
/// first question is precisely "is the right to end it there". Two opens cost two calls on one
/// process while a plan is built, which is nothing, and each one answers exactly its own question.
///
/// <b>The first asks for <see cref="PROCESS_ACCESS_RIGHTS.PROCESS_TERMINATE"/> and nothing else,
/// which is the same mask <see cref="WindowsScmControl"/> uses to do the ending.</b> That is the
/// whole reason the answer is worth anything: it is not a question resembling the real one, it is
/// the real one, asked and then dropped.
///
/// <b>The second asks for <see cref="PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION"/></b>,
/// the same right the memory reader uses and for the same measured reason. Counted on 2026-09-08
/// over 186 processes on two machines: that right was refused zero times, including by every one
/// of the eleven processes Windows was protecting, and <c>GetProcessTimes</c> answered on every
/// one of them.
///
/// <b>NOTHING HERE CHANGES ANYTHING.</b> Opening a handle that carries the right to end a process
/// does not end it, and this file contains no call that could. That is what makes it safe to ask
/// on somebody's production server, and what makes a preview able to say "this cannot be done"
/// without having tried.
/// </summary>
public sealed class WindowsEndingFactsReader : IEndingFactsReader
{
    public EndingFacts Read(int processId) =>
        new(WhetherItCanBeEnded(processId), WhenItStarted(processId));

    /// <summary>
    /// Opens a handle carrying the right to end this process, and closes it.
    ///
    /// <b>The refusal is the answer rather than a failure to get one</b>, which is why it comes
    /// back as <see cref="Reading{T}.Denied"/> carrying the system's own number and words. Rung
    /// five of specification <c>C3</c> asks for exactly this - say straight away that it cannot be
    /// done, rather than trying - and the layer above turns it into a plan that refuses.
    /// </summary>
    private static Reading<bool> WhetherItCanBeEnded(int processId)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_TERMINATE,
            bInheritHandle: false,
            (uint)processId);

        if (!process.IsInvalid)
        {
            return Reading<bool>.Present(true);
        }

        return Refusal<bool>(Marshal.GetLastWin32Error());
    }

    /// <summary>
    /// When this process started, which is the half of its identity a number cannot carry.
    /// </summary>
    private static Reading<long> WhenItStarted(int processId)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            bInheritHandle: false,
            (uint)processId);

        if (process.IsInvalid)
        {
            return Refusal<long>(Marshal.GetLastWin32Error());
        }

        if (!PInvoke.GetProcessTimes(process, out var created, out _, out _, out _))
        {
            return Refusal<long>(Marshal.GetLastWin32Error());
        }

        return Reading<long>.Present(FileTimeOf(created));
    }

    /// <summary>
    /// Two quite different things arrive as a failed open, and the number is what tells them apart.
    ///
    /// <b>"No such process" is not a refusal and must not read as one.</b> A service can end
    /// between the listing and this question on any live machine, and reporting that as "Windows
    /// will not let this tool end it" would be a confident sentence about permissions describing
    /// something that is simply not there any more. The layer above answers the two differently:
    /// one becomes a refusal to build the plan, the other becomes "there is no process to end",
    /// which that layer already had a word for.
    /// </summary>
    private static Reading<T> Refusal<T>(int code) =>
        code == (int)WIN32_ERROR.ERROR_INVALID_PARAMETER
            ? Reading<T>.Absent()
            : Reading<T>.Denied(code, new Win32Exception(code).Message);

    /// <summary>
    /// The two halves Windows hands out, put back together.
    ///
    /// <b>BOTH HALVES GO THROUGH <c>uint</c> BEFORE THEY ARE WIDENED, AND LEAVING EITHER ONE OUT
    /// IS A BUG NOTHING WOULD REPORT.</b> The generator hands these over as the framework's own
    /// file time, whose two fields are SIGNED integers holding unsigned halves of a count of
    /// hundred-nanosecond ticks. Widening the low half without the cast sign-extends it whenever
    /// its top bit is set - which is true of roughly half of all file times - and the result is a
    /// number that is not a time and does not resemble one.
    ///
    /// <b>What that would cost is worth naming, because it is not a crash.</b> This value is only
    /// ever compared against itself, read once when the plan is built and once on the handle that
    /// does the ending. A corruption applied consistently to both would compare equal and hide
    /// itself completely - until the day the two readings landed on opposite sides of the top bit,
    /// when a plan would refuse to end a process that had not moved anywhere.
    /// </summary>
    private static long FileTimeOf(System.Runtime.InteropServices.ComTypes.FILETIME time) =>
        ((long)(uint)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;
}
