namespace Bws.Cli;

/// <summary>
/// Which option belongs to which verb, and how each is spelled on the command line.
///
/// Moved out of CommandLine on 2026-08-02, when adding help and version pushed that file to 639
/// lines against a ceiling of 583. The ratchet asks for a split rather than a bigger number, and
/// this is the piece that came away whole: everything here answers "what does this tool accept
/// and what is it called", and nothing here reads an argument.
///
/// <b>The table below is a public contract twice over.</b> It is mirrored in docs/02, and
/// tools/audit/audit.ps1 is the only thing that can compare the two - a test in this repository
/// does not know the document exists. And people put these lines in runbooks, so an option that
/// moves between verbs breaks somebody's script without touching their machine.
/// </summary>
internal static class OptionSurface
{
    /// <summary>Which verbs each option belongs to. The whole surface, in one readable place.</summary>
    internal static readonly (string Option, CommandKind[] Verbs)[] Surface =
    [
        ("--query", [CommandKind.List]),

        // Listing only. A plan never asks who signed anything, so accepting it on a write
        // verb would be a switch that does nothing - the silence this table was built to
        // end. Measured at 4620-7656 ms over 810 entries and 544 files, which is why it is
        // asked for rather than assumed.
        ("--signatures", [CommandKind.List]),

        // Listing only, for the same reason: a plan is about what will happen to a service,
        // not about how much memory it is holding while it happens.
        ("--memory", [CommandKind.List]),

        // The two verbs that resolve a launch path against the disk. A plan does not - it
        // works from names the manager already gave it - and a comparison of two files never
        // touches a machine at all, so on either of those this would be a switch that does
        // nothing.
        //
        // Off by default and this is the only switch here where the default is a safety
        // decision rather than a cost one. Measured 2026-08-02: one unreachable share costs
        // 21 053 ms against a one second budget, and the connection carries the token of
        // whoever ran the tool. See NetworkPaths in the core for the whole argument.
        ("--follow-network", [CommandKind.List, CommandKind.SnapshotCreate]),

        // The one verb that writes a file somebody keeps. Nothing else here overwrites
        // anything, so nothing else has an existing file to be asked about.
        ("--force", [CommandKind.SnapshotCreate]),

        ("--json", [CommandKind.List, CommandKind.Stop, CommandKind.Start, CommandKind.Restart, CommandKind.SnapshotCreate, CommandKind.SnapshotDiff]),

        // Only where there is a snapshot to annotate. A note is the thing that makes a file
        // from three weeks ago mean something, so it belongs to the verb that writes one.
        ("--note", [CommandKind.SnapshotCreate]),

        // Only where there is something to find. Everywhere else the answer to "did it work"
        // is the whole of what a code can say, and this switch adds a second meaning to it.
        ("--exit-code", [CommandKind.SnapshotDiff]),

        // Only where there is a machine worth comparing against. Everywhere else the live
        // state is what the command already reads.
        ("--live", [CommandKind.SnapshotDiff]),

        // Diagnostic, and every command reads the manager before doing anything, so it
        // applies to every command. It used to be accepted everywhere and only honoured for
        // the listing, which is the same silence from the other side.
        ("--timing", [CommandKind.List, CommandKind.Stop, CommandKind.Start, CommandKind.Restart, CommandKind.SnapshotCreate, CommandKind.SnapshotDiff]),

        ("--dry-run", [CommandKind.Stop, CommandKind.Start, CommandKind.Restart]),

        // Not on start, and this one was missed the first time round. Starting is not the
        // mirror of stopping: the manager brings up whatever the entry needs by itself, and
        // nothing that merely depends on it has to move. The plan says so and ignores the
        // word - so accepting it here would be the same silence one level further down,
        // where somebody writing "start it and everything under it" gets one step and no
        // hint that the rest of their sentence was dropped.
        ("--dependents", [CommandKind.Stop, CommandKind.Restart]),

        ("--timeout", [CommandKind.Stop, CommandKind.Start, CommandKind.Restart])
    ];

    /// <summary>
    /// Whether a word is one of this tool's options, whichever verb it belongs to.
    ///
    /// Exists so that an option needing a value can tell "nobody gave me one" from "the next
    /// word is another switch". Until 2026-08-03 it could not: <c>bws list --query --json</c>
    /// took <c>--json</c> as the text to search for, found nothing, printed a table with no rows
    /// and ended with code 0 - so a script asking for JSON got a human table and a green light.
    /// That is the same silence the belonging table above exists to end, arriving from the other
    /// side.
    ///
    /// <b>Asked about known options rather than about a leading hyphen</b>, and the difference is
    /// somebody's note. <c>--note "-before the upgrade"</c> is a sentence a person would write,
    /// and refusing every value that opens with a hyphen would take it away to catch a mistake
    /// this catches exactly.
    ///
    /// Help and version are here as well as in the table, because they belong to no verb and are
    /// read before one is known - and they are just as wrong as a value.
    /// </summary>
    internal static bool IsOption(string argument) =>
        argument.Equals("--help", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("-h", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("--version", StringComparison.OrdinalIgnoreCase)
        || Surface.Any(entry => entry.Option.Equals(argument, StringComparison.OrdinalIgnoreCase));

    /// <summary>Where an option does work, for the message that says it does not work here.</summary>
    internal static IReadOnlyList<string> Accepts(string option) =>
    [
        .. Surface.Single(entry => entry.Option == option).Verbs.Select(Spelling)
    ];

    /// <summary>
    /// How a command is written on the command line.
    ///
    /// Not the name of the enumeration value. <see cref="CommandKind.SnapshotCreate"/>
    /// lower-cased reads "snapshotcreate", which is not a thing anybody can type - and it
    /// was going out in the message telling people where a switch does work, so the answer
    /// to "then where do I use it" was a word that does not exist. Same family as a switch
    /// missing its value reporting itself as unknown.
    /// </summary>
    internal static string Spelling(CommandKind kind) => kind switch
    {
        CommandKind.SnapshotCreate => "snapshot create",
        CommandKind.SnapshotDiff => "snapshot diff",
        _ => kind.ToString().ToLowerInvariant()
    };

    /// <summary>
    /// The words that can follow <c>snapshot</c>. E1 promises restore as well and it is not
    /// built, so the message offering these comes from here rather than from a sentence
    /// somebody has to remember to update.
    /// </summary>
    internal static IReadOnlyList<string> Subcommands => ["create", "diff"];

    /// <summary>
    /// Every command as somebody types it, in the order the usage text lists them.
    ///
    /// The order carries meaning: a suggestion for a word that is equally close to two commands
    /// offers the first, which is the one somebody is likelier to have read.
    /// </summary>
    internal static IReadOnlyList<string> Verbs => ["list", "stop", "start", "restart", "snapshot"];

}
