namespace Bws.Cli;

/// <summary>
/// What the person asked the tool to do.
///
/// <b>HERE SINCE 2026-08-25, AND THE SIZE RATCHET IS WHAT ASKED.</b> It lived in CommandLine.cs,
/// which was the longest shipped file in the product at exactly its ceiling - so the fourth write
/// verb could not be added anywhere in that file until something came out of it. The seam was found
/// rather than invented: everything left there reads what somebody typed, and the list of commands
/// belongs beside <see cref="OptionSurface.Spelling"/> and <see cref="OptionSurface.Verbs"/>, which
/// are the two other places that answer "what is this command called".
///
/// <b>tools/audit/audit.ps1 reads this enumeration out of the source</b> to check that every command
/// the code knows has a column in the switch table of docs/02, and it was pointed at the old file.
/// A seam that moves a shape a script matches on is the fault that script has already paid for once,
/// so it moved in the same change.
/// </summary>
internal enum CommandKind
{
    /// <summary>Nothing recognisable. Print how to use it.</summary>
    None,
    List,

    /// <summary>
    /// Everything this tool knows about one named entry.
    ///
    /// <b>The command line twin of the window's details panel, and it reads MORE than that panel
    /// does.</b> The panel takes no expensive reading, because it renders one row out of a listing
    /// of 798 and paying for signatures there costs 947-999 ms. This verb is about a single entry,
    /// where the same reading costs milliseconds, so it takes all of it and asks for none of it.
    ///
    /// <b>It sits beside List rather than under it, and the reason is the parser.</b> A verb that
    /// takes a NAME and a verb that takes a QUERY are two different shapes, and that line - not
    /// read against write - is what decides whether a name nobody can find is an empty result or
    /// a mistake. This one answers code 2, the same as stop does.
    /// </summary>
    Show,

    Stop,
    Start,
    Restart,

    /// <summary>
    /// Write what the manager does with an entry at the next boot.
    ///
    /// <b>Spelled with a hyphen - <c>bws start-type NAME manual</c> - which no other verb here is.</b>
    /// Owner's decision, 2026-08-25, with <c>bws config NAME --start-type manual</c> as the
    /// alternative on the table. It follows the field name the glossary binds, the way
    /// <c>--follow-network</c> follows its own, and it is the first verb in this tool that takes a
    /// VALUE as well as a name.
    ///
    /// <b>The price is that it sits one character from the verb "start".</b> Somebody typing
    /// <c>bws start type Spooler manual</c> is asking to start a service called type, and gets code
    /// 2 with a sentence about it rather than anything happening - which is the whole of what
    /// protects that mistake.
    ///
    /// <b>AND UNTIL 2026-09-01 THAT SENTENCE WAS "Unknown option: Spooler, manual", which is the
    /// protection being there and saying the wrong thing.</b> Neither word is an option and both
    /// are spelled correctly, so the answer sent somebody to look for a typo in a line that had
    /// none. Backlog 233 - the words now have their own list and the answer names how many the
    /// verb has room for. The code was always 2, and the code was never the part that helped.
    /// </summary>
    SetStartType,

    /// <summary>
    /// Stop it, and end the process behind it if that does not work.
    ///
    /// <b>Spelled the way `E1` of the specification wrote it from the start - <c>bws kill NAME</c> -
    /// and the word is the one every Windows administrator already has for this.</b> taskkill
    /// documents its own /f as "processes be forcefully ended" and says outright that it replaces
    /// the kill tool, and PowerShell's alias for Stop-Process is literally <c>kill</c>.
    ///
    /// <b>A verb rather than a switch on <see cref="Stop"/>, for two reasons that are not taste.</b>
    /// The word carries the blast radius: somebody reading a shell history, a change ticket or a
    /// runbook sees what happened without reading the flags. And <c>-Force</c> on a Windows SERVICE
    /// already means something else - Stop-Service -Force is documented as "even if it has dependent
    /// services", which is what <c>--dependents</c> does here, so a stop wearing that flag would be
    /// confidently wrong rather than merely unfamiliar.
    /// </summary>
    Kill,

    /// <summary>
    /// Freeze the state of every entry into a file.
    ///
    /// Spelled as two words on the command line - <c>bws snapshot create</c> - because `E1`
    /// writes it that way and because more will live under that noun: diff and restore are
    /// both promised there. One word now would have to become two later, and a verb that
    /// changes spelling after release costs somebody a runbook.
    /// </summary>
    SnapshotCreate,

    /// <summary>
    /// Say what changed between two snapshots.
    ///
    /// Spelled the way `E1` writes it. It was nearly spelled <c>bws diff</c> instead, on the
    /// strength of a question that offered the choice without mentioning that the
    /// specification had already made it - and the OptionSurface.Surface of the command line is a frozen
    /// contract, so that would have been a breaking change bought by accident.
    /// </summary>
    SnapshotDiff,

    /// <summary>
    /// What this program is licensed under, and what it carries that somebody else wrote.
    ///
    /// <b>The one verb here that reads nothing at all</b> - no service manager, no disk, no
    /// network. It answers out of a register compiled into the executable, which is the whole
    /// point of it: an administrator on a machine with no internet, holding a 98 MB file they
    /// are about to run with administrator rights, can ask what is inside it and get an answer
    /// from the file itself rather than from a web page.
    ///
    /// <b>Spelled the American way while this repository writes the noun the British way.</b>
    /// The guard next door is LicenceNoticeGuards and the field in every bill of materials is
    /// spelled `license`, as is the flag every other command line tool offers. The command is
    /// the word a person types, so it follows the tools rather than our prose.
    ///
    /// <b>A verb rather than a switch, owner's decision 2026-09-23.</b> The alternative on the
    /// table was <c>bws --licenses</c> beside --help and --version. Thirty lines of answer under
    /// a flag reads as an option, and this is a question with two depths - the notice, and the
    /// full register under --components.
    /// </summary>
    License
}

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

        // Listing only, and NOT the same switch as --dependents further down. That one is an
        // instruction to a write verb - take the services standing on this one with you. This is
        // a reading, and it costs a call per entry: 236-259 ms over 313 services.
        ("--required-by", [CommandKind.List]),

        // The two verbs that resolve a launch path against the disk. A plan does not - it
        // works from names the manager already gave it - and a comparison of two files never
        // touches a machine at all, so on either of those this would be a switch that does
        // nothing.
        //
        // Off by default and this is the only switch here where the default is a safety
        // decision rather than a cost one. Measured 2026-08-02: one unreachable share costs
        // 21 053 ms against a one second budget, and the connection carries the token of
        // whoever ran the tool. See NetworkPaths in the core for the whole argument.
        ("--follow-network", [CommandKind.List, CommandKind.Show, CommandKind.SnapshotCreate]),

        // The one verb that writes a file somebody keeps. Nothing else here overwrites
        // anything, so nothing else has an existing file to be asked about.
        // TWO VERBS AND ONE MEANING, WHICH IS WHY IT COULD BE REUSED AT ALL. On both it says the
        // same thing: skip the safeguard this tool would otherwise give you. On snapshot create the
        // safeguard is "I will not write over your file", and on kill it is "I will ask the service
        // politely first". A switch that meant two unrelated things would have needed two names.
        ("--force", [CommandKind.SnapshotCreate, CommandKind.Kill]),

        // Only on the verb that takes something down without asking. Every other write verb here
        // either brings a service back already - restart - or was never going to take one down.
        ("--restart", [CommandKind.Kill]),

        // Only on the verb that sets a start type, and the refusals narrow it further to the one
        // word it means anything beside - "disabled", the setting the plan says leaves a running
        // entry running. Spec C4, the owner's decision of 2026-09-24.
        //
        // --timeout stays off this verb even though --stop makes the plan move something: the stop
        // step waits the default ceiling, a minute. Accepting --timeout here would make it a word
        // that means something beside --stop and nothing without it, which is a rule nobody could
        // guess - and adding it later is an addition, where taking it back would not be.
        ("--stop", [CommandKind.SetStartType]),

        // Only where there is a field that can be empty rather than merely unread. It says
        // "print the ones that are genuinely absent as well", which every other verb here either
        // has no fields for or prints in full anyway.
        //
        // What it does NOT govern is the fields nobody could read. Those are printed in both
        // modes, because rule 8 of CLAUDE.md forbids swallowing a failed read - and a switch that
        // could hide one would be exactly that, spelled as an option.
        ("--full", [CommandKind.Show]),

        ("--json", [CommandKind.List, CommandKind.Show, CommandKind.Stop, CommandKind.Start, CommandKind.Restart, CommandKind.SetStartType, CommandKind.SnapshotCreate, CommandKind.SnapshotDiff, CommandKind.Kill]),

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
        ("--timing", [CommandKind.List, CommandKind.Show, CommandKind.Stop, CommandKind.Start, CommandKind.Restart, CommandKind.SetStartType, CommandKind.SnapshotCreate, CommandKind.SnapshotDiff, CommandKind.Kill]),

        ("--dry-run", [CommandKind.Stop, CommandKind.Start, CommandKind.Restart, CommandKind.SetStartType,
            CommandKind.Kill]),

        // Not on start, and this one was missed the first time round. Starting is not the
        // mirror of stopping: the manager brings up whatever the entry needs by itself, and
        // nothing that merely depends on it has to move. The plan says so and ignores the
        // word - so accepting it here would be the same silence one level further down,
        // where somebody writing "start it and everything under it" gets one step and no
        // hint that the rest of their sentence was dropped.
        ("--dependents", [CommandKind.Stop, CommandKind.Restart, CommandKind.Kill]),

        // NOT ON start-type, AND THAT IS THE SAME SENTENCE AS --dependents ARRIVING AT IT FROM THE
        // OTHER SIDE. Both of these are about a service MOVING: one asks what may be taken down
        // with it, the other asks how long to watch it arrive. Writing a start type moves nothing -
        // the manager answers when the configuration is written and there is no state to wait for -
        // so either switch here would be a word that does nothing, which is the silence this table
        // was built to end. The stop that --stop adds does move an entry, and why --timeout still
        // stays off even beside it is written at --stop above.
        ("--timeout", [CommandKind.Stop, CommandKind.Start, CommandKind.Restart, CommandKind.Kill]),

        // The only verb with anything to say about components, and NOT accepted anywhere else
        // even though a listing could be imagined to carry one. It turns the notice into the
        // whole register: every component with its version, its licence and where it came from.
        //
        // NOT --json, and that omission is a decision rather than an oversight. The
        // machine-readable rendering of these exact facts is the SPDX document published beside
        // every archive, and a second JSON shape for one set of facts is a second public
        // contract to keep true. Adding it later is additive and breaks nothing - taking it away
        // would not be.
        ("--components", [CommandKind.License])
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

        // Spelled rather than lower-cased for the same reason as the two above: the name of the
        // value reads "setstarttype", which is not a thing anybody can type - and it would go out
        // in the sentence telling somebody where a switch DOES work, so the answer to "then where"
        // would be a word that does not exist.
        CommandKind.SetStartType => "start-type",
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
    ///
    /// <b>start-type sits AFTER start, and the order is doing work here rather than reading well.</b>
    /// A mistyped word equally close to both is offered the earlier one, and "start" is the verb
    /// somebody is far likelier to have meant - it is three words shorter and it is what people
    /// come to this tool for.
    /// </summary>
    /// <remarks>
    /// <b>license is LAST, and the order is doing the same work it does for start-type above.</b>
    /// A mistyped word equally close to two commands is offered the earlier one, and nothing
    /// somebody types at three in the morning on a server was meant to be this. It is also the
    /// only verb here that is read far more often than it is typed.
    /// </remarks>
    internal static IReadOnlyList<string> Verbs =>
        ["list", "show", "stop", "start", "restart", "start-type", "kill", "snapshot", "license"];

    /// <summary>
    /// Whether the command is about ONE entry somebody named, rather than about whatever a query
    /// selects.
    ///
    /// <b>This line, not read against write, is the one the parser and the exit code both need.</b>
    /// A bare word after a verb belongs to the verb only when the verb takes a name - after "list"
    /// there is no such word at all, and taking one there would mean `bws list Spooler` quietly
    /// printed the whole machine. And a name that matches nothing is a mistake worth code 2, while
    /// a query that matches nothing is an answer worth code 0.
    ///
    /// <b>Checked against a second case, as rule 12 asks:</b> `snapshot diff` on a file that is not
    /// there is not an empty comparison either, and it does not report one.
    /// </summary>
    internal static bool TakesAName(CommandKind kind) =>
        kind == CommandKind.Show || WriteCommands.Writes(kind);

    /// <summary>
    /// How many bare words a command has room for, as the key of a sentence saying so.
    ///
    /// <b>Built for backlog 233, and the sentence it feeds is the whole repair.</b> A word a verb
    /// has no room for used to be reported as an unknown option. Saying "there is no such option"
    /// about <c>Spooler</c> is wrong twice: it is not an option, and it is spelled correctly. What
    /// somebody needs instead is the shape of the command they are one word away from.
    ///
    /// <b>A key rather than a sentence, and a phrase rather than a number.</b> A number would want
    /// a plural beside it - backlog 207 is that same trap elsewhere in this file's neighbours - and
    /// the phrase carries what the number could not anyway: that <c>list</c> takes no name because
    /// it narrows with a query, and that <c>start-type</c> takes two words rather than one.
    ///
    /// <b>Written out per value rather than derived.</b> The reason <see cref="Spelling"/> gives
    /// one screen up holds here too: anything worked out from the name of the value asks for a key
    /// nobody wrote, and asks for it at the moment somebody is already being told they got
    /// something wrong.
    /// </summary>
    internal static string TakesWhat(CommandKind kind) => kind switch
    {
        CommandKind.List => "cli.takes.query",
        CommandKind.SetStartType => "cli.takes.nameAndStartType",
        CommandKind.SnapshotCreate => "cli.takes.oneFile",
        CommandKind.SnapshotDiff => "cli.takes.twoFiles",

        // The only verb here that takes no words at all, so the sentence it feeds says that
        // rather than naming a shape. Somebody typing `bws license GPL` is asking a question the
        // verb cannot narrow - the answer is the same either way and pretending otherwise would
        // be worse than saying so.
        CommandKind.License => "cli.takes.nothing",

        // Show, stop, start and restart. Not a default arm that guesses, for the reason For gives
        // in the window: a fifth shape must fail here loudly rather than quietly claim to take one
        // name when it does not.
        CommandKind.Show or CommandKind.Stop or CommandKind.Start or CommandKind.Restart
            or CommandKind.Kill =>
            "cli.takes.oneName",

        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind, "This is not a command, so there is nothing it takes.")
    };
}
