using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;

namespace Bws.Core;

/// <summary>
/// The privilege to shut the system down, on for as long as this lives and back as it was after - for the one
/// write of a recovery list that restarts the computer (<see cref="WindowsScmControl.ConfigureRecovery"/>). Since
/// 2026-11-24, backlog 555 part (a2), the owner's decision to allow such a list.
///
/// <b>Turned on, never added</b>: a token holds the privileges it was given, and this only switches one of them.
/// An administrator's token holds it, and so does an ordinary user's. A token without it is refused here, before
/// anything is written, with Windows' own number for a privilege that is not held - not the 5 the manager would
/// answer (measured on the throwaway machine that day, <c>tools/vm-service/recovery-write-probe.ps1</c>).
///
/// <b>Its own file beside the writer</b>, because it is the one piece of this product that touches its own token,
/// and "what can it do to me" should find that in a file named for it.
/// </summary>
internal sealed unsafe class ShutdownRight : IDisposable
{
    private readonly SafeHandle _token;
    private TOKEN_PRIVILEGES _before;

    private ShutdownRight(SafeHandle token, TOKEN_PRIVILEGES before)
    {
        _token = token;
        _before = before;
    }

    /// <summary>The privilege switched on, or nothing and the refusal saying why.</summary>
    internal static ShutdownRight? TurnOn(out ControlAnswer? refusal)
    {
        using var process = PInvoke.GetCurrentProcess_SafeHandle();

        if (!PInvoke.OpenProcessToken(
                process, TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
        {
            refusal = Refused(Marshal.GetLastWin32Error());
            return null;
        }

        if (!PInvoke.LookupPrivilegeValue(null, PInvoke.SE_SHUTDOWN_NAME, out var luid))
        {
            refusal = Refused(Marshal.GetLastWin32Error());
            token.Dispose();
            return null;
        }

        var wanted = new TOKEN_PRIVILEGES { PrivilegeCount = 1 };
        wanted.Privileges[0] = new LUID_AND_ATTRIBUTES
        {
            Luid = luid,
            Attributes = TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED
        };

        var before = default(TOKEN_PRIVILEGES);
        var (adjusted, code) = Adjust(token, &wanted, &before);

        // ADJUSTED AND NOT ASSIGNED IS A YES THAT MEANS NO: the call succeeds and says through the last error that
        // the token does not hold the privilege (Microsoft's page on AdjustTokenPrivileges).
        if (!adjusted || code == (int)WIN32_ERROR.ERROR_NOT_ALL_ASSIGNED)
        {
            refusal = Refused(adjusted ? (int)WIN32_ERROR.ERROR_PRIVILEGE_NOT_HELD : code);
            token.Dispose();
            return null;
        }

        refusal = null;
        return new ShutdownRight(token, before);
    }

    public void Dispose()
    {
        // A count of zero says nothing changed - the privilege was on already - so there is nothing to put back.
        if (_before.PrivilegeCount > 0)
        {
            fixed (TOKEN_PRIVILEGES* before = &_before)
            {
                _ = Adjust(_token, before, null);
            }
        }

        _token.Dispose();
    }

    private static ControlAnswer Refused(int code) => ControlAnswer.Refused(code, ManagerTerms.Describe(code));

    /// <summary>
    /// One adjustment, and the last error read straight after it - the call that takes pointers takes a raw
    /// handle, so the safe one is held for its length rather than let go while the call runs.
    /// </summary>
    private static (bool Adjusted, int Code) Adjust(SafeHandle token, TOKEN_PRIVILEGES* state, TOKEN_PRIVILEGES* before)
    {
        var held = false;

        try
        {
            token.DangerousAddRef(ref held);

            var length = 0u;
            var adjusted = PInvoke.AdjustTokenPrivileges(
                new HANDLE(token.DangerousGetHandle()),
                false,
                state,
                before is null ? 0 : (uint)sizeof(TOKEN_PRIVILEGES),
                before,
                before is null ? null : &length);

            return (adjusted, Marshal.GetLastWin32Error());
        }
        finally
        {
            if (held)
            {
                token.DangerousRelease();
            }
        }
    }
}
