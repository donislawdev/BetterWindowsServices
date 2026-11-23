using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// SERVICES.MSC'S RECOVERY TAB, FIVE COLUMNS AND ONE HEADING - backlog 555, 2026-11-23, the owner's decision
/// to show everything that tab shows. Off until somebody turns them on, like every column the list does not
/// need at a glance, and in the details panel under "When it fails" for every entry. Read with the listing,
/// on the configuration's handle, so none of them declares a family.
///
/// <b>Its own file from the first day</b>, because the catalogue in Columns.cs stood at its size ceiling and
/// five more declarations took it fifty lines past. They are one subject - one reading, one heading - which
/// is the seam the ratchet asks for.
/// </summary>
internal static partial class Columns
{
    /// <summary>
    /// The five, in the order of services.msc's tab. A method rather than a field, so the order in which the
    /// files of this class are initialised cannot leave <see cref="All"/> holding a null.
    ///
    /// <b>One shape five times, so made from a list rather than written out five times</b> (GUI rule 8): they
    /// differ in their name, their width and the words they say, and in nothing else. The label keys stay
    /// written in full, so a search for one finds the column that uses it.
    /// </summary>
    private static Column[] RecoveryColumns() =>
    [
        RecoveryColumn("recovery", "gui.column.recovery", "ColumnRecovery", RecoveryWords.Actions),
        RecoveryColumn("recoveryReset", "gui.column.recoveryReset", "ColumnRecoveryReset", RecoveryWords.Reset),
        RecoveryColumn(
            "recoveryOnNonCrashFailures", "gui.column.recoveryOnNonCrashFailures", "ColumnRecoveryNonCrash",
            RecoveryWords.OnNonCrashFailures),

        // The one of the five worth a look on its own: the program runs under the service's account, so a
        // command added here is how something stays on a machine.
        RecoveryColumn("recoveryCommand", "gui.column.recoveryCommand", "ColumnRecoveryCommand", RecoveryWords.Command),
        RecoveryColumn(
            "recoveryRebootMessage", "gui.column.recoveryRebootMessage", "ColumnRecoveryMessage",
            RecoveryWords.RebootMessage)
    ];

    private static Column RecoveryColumn(string id, string label, string width, Func<Reading<RecoverySettings>, string> words) => new()
    {
        Id = id,
        LabelKey = label,
        WidthKey = width,
        Face = ColumnFace.Text,
        ShownAtFirst = false,
        Reads = entry => words(entry.Recovery)
    };
}
