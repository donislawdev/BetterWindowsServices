using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Services;

namespace Bws.Core;

/// <summary>
/// Walking the manager's own listing, one turn at a time.
///
/// <b>Split out of WindowsScmCatalog.cs on 2026-09-02 because the size ratchet said so, and the
/// seam is a subject rather than a line count.</b> Everything left in that file asks the manager
/// about a machine or about one entry and gets an answer. This asks REPEATEDLY, and the whole of
/// its difficulty is in that: a handle the manager owns says where it had got to, two calls a turn
/// share it, and every reason to stop comes from the other side.
///
/// <b>Partial rather than a new type, so no caller moved.</b> The window is already split this way
/// across three files, so this is the pattern this project uses rather than a new one.
/// </summary>
public sealed partial class WindowsScmCatalog
{
    // A list rather than a sequence, because the caller needs a count before it starts and an
    // index while it runs. It was already building one internally - the sequence was hiding
    // that behind a type that promised less than it delivered.
    private static unsafe List<EnumeratedEntry> Enumerate(SafeHandle manager)
    {
        uint resume = 0;
        var results = new List<EnumeratedEntry>(capacity: 1024);

        while (true)
        {
            // Where the manager said it had got to when this turn started. Both calls below take
            // the handle by reference and may move it, so the comparison at the foot of the loop
            // has to hold the value from before either of them touched it.
            var before = resume;

            // Ask with an empty buffer first. The call is expected to fail and to report
            // how much room it wants, which is the documented way to size this.
            var probed = PInvoke.EnumServicesStatusEx(
                manager, SC_ENUM_TYPE.SC_ENUM_PROCESS_INFO, AllEntryTypes,
                ENUM_SERVICE_STATE.SERVICE_STATE_ALL, default,
                out var needed, out _, ref resume, null!);

            var probeError = Marshal.GetLastWin32Error();

            // NOTHING LEFT AND REFUSED LOOKED THE SAME FROM HERE UNTIL 2026-08-03, and this
            // took both to mean the first. Any failure that does not set the size - a refusal,
            // a manager shutting down between two turns of this loop, resources running out -
            // came back reporting no room needed, the loop broke, and ReadAll returned however
            // many entries it happened to have collected by then. With a code of success, and a
            // listing that looks exactly like a complete one.
            //
            // That is rule 8 of CLAUDE.md, in the one place in this file that had no guard
            // against it.
            //
            // THE SENTENCE THAT STOOD HERE UNTIL 2026-08-26 WAS FALSE, and saying so is worth more
            // than quietly replacing it. It said that ReadDependents had
            // "the same buffer shape and the right check since it was written" and named this one
            // as the odd one out. The buffer shape was the same. The check was not: that call's
            // return value was discarded and the decision was made on the error code alone, which
            // is the exact mistake the paragraph below describes. A comment claiming a guard exists
            // is worse than no comment, because the next reader stops looking.
            //
            // ASKED THROUGH THE RETURN VALUE, NOT THROUGH THE ERROR CODE ALONE, and the
            // difference is not style. Windows does not clear the last error on success, so a
            // call that genuinely has nothing left to hand over can leave whatever the previous
            // call in this thread put there - and a check reading only the number would throw
            // on a stale one, turning a working listing into a failure. The return value is the
            // only thing that says whether this call worked.
            if (!probed && probeError != (int)WIN32_ERROR.ERROR_MORE_DATA)
            {
                throw new Win32Exception(probeError, "Enumerating the service control manager failed.");
            }

            if (needed == 0)
            {
                break;
            }

            var buffer = new byte[needed];

            // Pinned across the call and the reading, for the reason set out in full at
            // ReadConfiguration: each record here carries two absolute pointers into this very
            // block, and the array is free to move the instant the interop wrapper returns.
            fixed (byte* pinned = buffer)
            {
                var read = PInvoke.EnumServicesStatusEx(
                    manager, SC_ENUM_TYPE.SC_ENUM_PROCESS_INFO, AllEntryTypes,
                    ENUM_SERVICE_STATE.SERVICE_STATE_ALL, new Span<byte>(pinned, buffer.Length),
                    out _, out var returned, ref resume, null!);

                var error = Marshal.GetLastWin32Error();

                if (!read && error != (int)WIN32_ERROR.ERROR_MORE_DATA)
                {
                    throw new Win32Exception(error, "Enumerating the service control manager failed.");
                }

                results.AddRange(ManagerBlocks.ReadEnumerationBuffer(buffer, returned));

                // THE ONE LOOP IN THIS PROJECT WHOSE ENDING IS DECIDED ENTIRELY BY SOMEBODY ELSE,
                // and until 2026-09-02 nothing here insisted that it end. Backlog 304. Both exits
                // come from the manager: a size of zero above and a resume handle of zero below.
                // A turn that hands over no record and does not move the handle has made no
                // progress, and repeating it makes none either - so the loop would spin, taking a
                // fresh buffer every time, with the command line hung and the window silently
                // stuck on a reading that never returns.
                //
                // NOBODY HAS SEEN THIS AND THAT IS WRITTEN DOWN RATHER THAN GLOSSED. The manager
                // answers in one or two turns. What earns the check is the asymmetry: three lines
                // against a hang on somebody's server, in the only place here where the condition
                // to continue is a number a different process chose.
                if (returned == 0 && resume == before)
                {
                    throw new Win32Exception(
                        (int)WIN32_ERROR.ERROR_INVALID_DATA,
                        "The service control manager stopped making progress through its own listing.");
                }
            }

            // A resume handle of zero means the manager has nothing left to hand over.
            if (resume == 0)
            {
                break;
            }
        }

        return results;
    }
}
