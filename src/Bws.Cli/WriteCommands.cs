using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// Which commands change the machine, and which ask each of them is.
///
/// <b>Its own file since 2026-08-25, and the size ratchet is what asked</b> - naming the arm that
/// used to be a discard took CommandLine.cs past the longest file in the product. The seam is a
/// subject: everything left there is about reading what somebody typed, and this is about what two
/// of those words MEAN once they have been read.
/// </summary>
internal static class WriteCommands
{
    /// <summary>
    /// Whether a command changes anything at all.
    ///
    /// <b>Four commands since 2026-08-25, and the fourth changes a SETTING rather than moving a
    /// service.</b> Everything downstream of this answer treats them alike on purpose - a write is
    /// a write, it goes through a plan, and `ADR-11` knows no other kind. Where the difference
    /// matters it is asked about by name rather than through this: the switch table refuses
    /// --timeout and --dependents on it, and the plan builder gives it no cascade.
    /// </summary>
    internal static bool Writes(CommandKind kind) =>
        kind is CommandKind.Stop or CommandKind.Start or CommandKind.Restart
            or CommandKind.SetStartType or CommandKind.Kill;

    /// <summary>
    /// Which ask a write command is. Read only where <see cref="Writes"/> is already true.
    ///
    /// <b>The discard used to mean restart</b>, so a fourth command of this family would have been
    /// carried out as one - four steps on a real machine that nobody asked for. It refuses now, and
    /// the caller that reads it has already checked which command this is.
    ///
    /// <b>The fourth command arrived a fortnight later and this arm is what it landed on</b>, which
    /// is the discard having been worth writing. Under the old shape <c>bws start-type X disabled</c>
    /// would have restarted X.
    /// </summary>
    /// <param name="restart">
    /// Whether the forcing verb was asked to bring the entry back afterwards.
    ///
    /// <b>A switch decides which ASK this is, which no other verb here needs</b> - and that is
    /// the price of one verb covering both. The alternative was a second verb, and a tool with
    /// eight of them is how nobody finds the one they want.
    /// </param>
    internal static ActionKind AskedFor(CommandKind kind, bool restart = false) => kind switch
    {
        CommandKind.Kill => restart ? ActionKind.ForceRestart : ActionKind.ForceStop,
        CommandKind.Stop => ActionKind.Stop,
        CommandKind.Start => ActionKind.Start,
        CommandKind.Restart => ActionKind.Restart,
        CommandKind.SetStartType => ActionKind.SetStartType,
        _ => throw new InvalidOperationException(
            "This command does not change anything, so it has no action. Writes says which do.")
    };

    /// <summary>
    /// Whether a write command needs a start type as well as a name.
    ///
    /// <b>Asked as a question of its own rather than read off the verb at three call sites.</b> One
    /// of the three is the parser, which has to know whether a second bare word belongs to this
    /// command or is a mistake, and the other two are the two refusals a person can run into. Three
    /// copies of "is it that verb" would be three places to miss when a fifth arrives.
    /// </summary>
    internal static bool NeedsAStartType(CommandKind kind) => kind == CommandKind.SetStartType;

    /// <summary>
    /// The startup setting somebody named, or nothing when the word was not one.
    ///
    /// <b>The reading itself is in the core, beside the writer that renders it.</b> A word this tool
    /// accepts and a word this tool prints have to be the same word - that is the whole argument of
    /// EquivalentCommand, and a table here would be the second answer it warns about.
    /// </summary>
    internal static StartSetting? Named(string word) => StartTypeWords.Read(word);
}
