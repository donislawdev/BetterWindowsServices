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
    /// <summary>Whether a command changes anything at all.</summary>
    internal static bool Writes(CommandKind kind) =>
        kind is CommandKind.Stop or CommandKind.Start or CommandKind.Restart;

    /// <summary>
    /// Which ask a write command is. Read only where <see cref="Writes"/> is already true.
    ///
    /// <b>The discard used to mean restart</b>, so a fourth command of this family would have been
    /// carried out as one - four steps on a real machine that nobody asked for. It refuses now, and
    /// the caller that reads it has already checked which command this is.
    /// </summary>
    internal static ActionKind AskedFor(CommandKind kind) => kind switch
    {
        CommandKind.Stop => ActionKind.Stop,
        CommandKind.Start => ActionKind.Start,
        CommandKind.Restart => ActionKind.Restart,
        _ => throw new InvalidOperationException(
            "This command does not change anything, so it has no action. Writes says which do.")
    };
}
