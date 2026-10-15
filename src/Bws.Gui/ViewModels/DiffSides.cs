namespace Bws.Gui.ViewModels;

/// <summary>
/// What a comparison in the window set side by side. `D2` has three modes and the window reaches all
/// three: a snapshot against this machine now (W1), and two snapshots (W2, since 2026-10-15) - which is
/// also the third mode, machine A against machine B, done the way v1 does it, with two files.
///
/// <b>The words depend on it and nothing else does.</b> The engine compares two snapshots either way -
/// SnapshotDiff never learns that one side was read a second ago - so this travels with the sheet and
/// decides the title, the column headings, the rows for entries that came or went, and the caveats that
/// name the sides. A sentence saying "since the snapshot" about two files from Tuesday and Wednesday
/// would be the window describing a comparison nobody made.
/// </summary>
public enum DiffSides
{
    /// <summary>A snapshot somebody opened, against this machine read now.</summary>
    WithThisMachine,

    /// <summary>
    /// Two snapshots somebody opened, the one taken first on the left - owner's decision W2-a, so the
    /// order they were chosen in cannot turn an addition into a removal.
    /// </summary>
    TwoSnapshots
}

/// <summary>One side of a comparison as the head of the view says it: which snapshot, and its note.</summary>
/// <param name="About">Which snapshot, from which machine, when, by whom, and with what rights.</param>
/// <param name="Note">The snapshot's own note as a sentence, or empty when it carries none.</param>
public sealed record DiffSide(string About, string Note);
