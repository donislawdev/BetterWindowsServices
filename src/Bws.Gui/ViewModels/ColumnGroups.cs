namespace Bws.Gui.ViewModels;

/// <summary>
/// Which heading each column sits under in the picker.
///
/// <b>Split out of Columns.cs on 2026-09-02 because the size ratchet said so, and the seam it
/// pointed at was named in that file already:</b> it calls itself the CATALOGUE of columns, and
/// CellOrder was taken out of it on 2026-08-31 on exactly this argument - what a column IS and
/// how two rows compare by one are different subjects. A grouping is a third: it says nothing
/// about any column and everything about how a person is offered all of them.
///
/// <b>Partial rather than a new type, so that not one call site had to move.</b> The window is
/// already split this way across three files, so this is the pattern this project uses rather
/// than a new one - and Columns.Basics and Columns.GroupOf still read the way they read before.
/// </summary>
internal static partial class Columns
{
    /// <summary>
    /// Which heading in the picker each column sits under.
    ///
    /// <b>One map rather than a field on every declaration, and that is not a saving.</b> A
    /// grouping is only useful if somebody can see the whole of it at once and ask whether it makes
    /// sense - spread across the entries it becomes one local decision each, and nobody notices
    /// when a group is left with one item in it.
    ///
    /// <b>THIS MAP IS WHY TWO NEW COLUMNS COST SIX RED TESTS ON 2026-08-17 AND NOT ONE, and that is
    /// the design working.</b> Adding a column to the catalogue without adding it here left it
    /// under no heading at all - and the picker, the details panel and the button's menu all went
    /// red about it separately, because each of them is built from this map rather than from a
    /// default. The sentence below promised exactly that outcome before it happened.
    ///
    /// <b>The groups are about what a person came looking for, not about where the data comes
    /// from.</b> The first is what the list shows without being asked. The rest are three questions
    /// somebody arrives with: who is this running as and is it behaving, what does it run, and the
    /// details you go looking for once you already suspect something.
    ///
    /// A column missing here is a failed test rather than a default, because a default would put it
    /// quietly under whichever heading was least wrong.
    /// </summary>
    private static readonly Dictionary<string, string> Groups = new(StringComparer.Ordinal)
    {
        ["serviceName"] = Basics,
        ["displayName"] = Basics,
        ["description"] = Basics,
        ["status"] = Basics,
        ["startType"] = Basics,

        // Beside the start type rather than under About, because it is a QUALIFIER on that column
        // and not a separate fact about the entry - the start cell says "Automatic (delayed)" from
        // the same field. Somebody who turns this on is refining what the start column already
        // told them, which is what Basics is.
        ["delayedAuto"] = Basics,

        ["account"] = Basics,
        ["processId"] = Basics,

        // Beside the process id in meaning, and the grouping follows meaning rather than cost -
        // this is the only column here that is a MEASUREMENT of a running process rather than a
        // setting, and the process id is the other half of that pair.
        ["memory"] = Basics,

        ["entryType"] = About,

        // Beside the entry type because it answers a different question about the same bits, and
        // the two are misleading apart: a per-user template that shares a process reads as an
        // ordinary SharedProcess without this.
        ["perUserRole"] = About,

        ["runsAgainstItsStartType"] = About,
        ["runsWhileDisabled"] = About,
        ["sidType"] = About,

        ["binaryPath"] = Binary,
        ["binaryFile"] = Binary,

        // Under Binary rather than under a heading of their own, because every one of them is a
        // fact about the FILE the entry runs rather than about the entry - which is exactly what
        // this heading means. Backlog 21.
        ["signature"] = Binary,
        ["publisher"] = Binary,
        ["fileVersion"] = Binary,
        ["binaryHash"] = Binary,

        // The third one this heading has wanted since it was drawn. "What does it run" is answered
        // by a command, by the file that command resolves to, and by whether that file is there -
        // and the last of the three was the one folded into another column as the words
        // "file missing".
        ["binaryOnDisk"] = Binary,

        ["loadOrderGroup"] = Advanced,
        ["errorControl"] = Advanced,
        ["dependsOn"] = Advanced,
        ["triggers"] = Advanced,
        ["requiredPrivileges"] = Advanced,
        ["securityDescriptor"] = Advanced
    };

    internal const string Basics = "gui.columns.group.basics";
    internal const string About = "gui.columns.group.about";
    internal const string Binary = "gui.columns.group.binary";
    internal const string Advanced = "gui.columns.group.advanced";

    /// <summary>The heading a column sits under, or null when nobody gave it one.</summary>
    internal static string? GroupOf(string id) => Groups.GetValueOrDefault(id);
}
