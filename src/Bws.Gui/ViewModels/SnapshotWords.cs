using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the window says about a snapshot file it could not read or write - the faults
/// <see cref="SnapshotOnDisk"/> names, in the window's words.
///
/// <b>The same faults the command line turns into its sentences</b>, decided once in the core, so the
/// window and <c>bws snapshot</c> never answer one file two ways. Only the words and the place they
/// appear differ: here they go to the line at the foot of the window.
/// </summary>
internal static class SnapshotWords
{
    /// <summary>Why a file somebody pointed at is not a snapshot this build reads.</summary>
    internal static string Of(DiskRefusal refusal)
    {
        var path = Printable.Of(refusal.Path);

        return refusal.Fault switch
        {
            DiskFault.IsFolder => Texts.Of("gui.snapshot.fault.isFolder", path),
            DiskFault.AlreadyThere => Texts.Of("gui.snapshot.fault.alreadyThere", path),
            DiskFault.TooBig => Texts.Of("gui.snapshot.fault.tooBig", path, SnapshotOnDisk.Biggest / (1024 * 1024)),
            DiskFault.NotUtf8 => Texts.Of("gui.snapshot.fault.notUtf8", path),
            DiskFault.NotASnapshot => Texts.Of("gui.snapshot.fault.notASnapshot", path, Printable.Of(refusal.Detail)),
            _ => Texts.Of("gui.snapshot.fault.unreadable", path, refusal.Detail)
        };
    }

    /// <summary>
    /// Why a snapshot was not written where somebody asked - and, when what stands there is not a
    /// snapshot, that it was left exactly as it was (S-8).
    /// </summary>
    internal static string NotWritten(DiskRefusal refusal) =>
        refusal.Fault is DiskFault.IsFolder or DiskFault.AlreadyThere
            ? Of(refusal)
            : Texts.Of("gui.snapshot.fault.notReplaced", Of(refusal));
}
