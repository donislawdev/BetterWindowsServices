using System.Diagnostics.CodeAnalysis;
using Bws.Core.Snapshots;

namespace Bws.Cli;

/// <summary>
/// The snapshot as a file on somebody's disk, in this front end's words and exit codes.
///
/// <b>Moved out of Program.cs on 2026-08-03 because the size ratchet said so, for the fifth
/// time.</b> The seam is between the run and the disk. What stays behind reads the command line,
/// chooses the verb and produces the document. What is here turns whatever is at the other end of a
/// path into a snapshot or into a sentence and a code.
///
/// <b>THE RULES THEMSELVES LEFT FOR THE CORE ON 2026-10-14</b>, when the window began taking and
/// comparing snapshots (<see cref="SnapshotOnDisk"/>). The window references the core and nothing
/// else, and two copies of "only a snapshot is ever replaced" would answer the same file two ways the
/// first time one was changed. What stayed here is what only the command line has: the sentences on
/// standard error and the code a run ends with. Every sentence and every code is the one this file
/// produced before the move - the faults were drawn along the lines the old catches already drew.
/// </summary>
internal static class SnapshotFiles
{
    /// <summary>Where the snapshot goes when nobody said - see <see cref="SnapshotOnDisk.Target"/>.</summary>
    internal static string Target(string given, SnapshotMetadata metadata) =>
        SnapshotOnDisk.Target(given, metadata);

    /// <summary>
    /// Whether this file may be written over, and the sentence when it may not - the rule is
    /// <see cref="SnapshotOnDisk.WhyNotWritten"/>.
    /// </summary>
    internal static bool MayWrite(string target, bool force, out string? refusal)
    {
        refusal = SnapshotOnDisk.WhyNotWritten(target, force) switch
        {
            null => null,
            { Fault: DiskFault.IsFolder } => Texts.Of("cli.snapshot.isFolder", target),
            { Fault: DiskFault.AlreadyThere } => Texts.Of("cli.snapshot.fileExists", target),
            var other => Said(other) + " " + Texts.Of("cli.snapshot.notReplaced")
        };

        return refusal is null;
    }

    /// <summary>
    /// Reads one snapshot from disk, or says what is wrong with what was pointed at.
    /// </summary>
    /// <param name="code">
    /// How the run should end if this returns false. Its own answer rather than a code the caller
    /// assumes, because the two reasons a read fails are not the same person's fault: a file that is
    /// not there, a path that cannot be spelled, bytes that are not UTF-8 and a file that is not a
    /// snapshot are what somebody pointed at (usage), and a disk that went away, a file another
    /// process holds or a folder this account may not enter are the machine failing (runtime).
    /// </param>
    internal static bool Load(string path, out Snapshot? snapshot, out int code)
    {
        code = ExitCode.Ok;

        if (SnapshotOnDisk.TryLoad(path, out snapshot, out var refusal))
        {
            return true;
        }

        // Names the file. Two are being read and a message about neither of them would leave
        // somebody checking both.
        Console.Error.WriteLine(Said(refusal));

        code = refusal.Fault == DiskFault.Unreadable ? ExitCode.Runtime : ExitCode.Usage;

        return false;
    }

    /// <summary>
    /// Compares two snapshots, or says which side stopped it and why.
    ///
    /// <b>Here rather than beside the verb it serves, and the reason is the size ratchet.</b>
    /// Program.cs stood at 549 lines against a ceiling of 552 that may only ever go down, so the
    /// branch this needs would have reddened the build. This file already owns "turn a snapshot the
    /// person pointed at into either a document or a sentence and a code", and a comparison refusing
    /// one of its two sides is the same sentence arriving one step later.
    ///
    /// <b>Unreachable through the command line today, and that is written down rather than left to be
    /// discovered.</b> Both sides of a two-file comparison have been through
    /// <c>SnapshotJson.TryRead</c>, which refuses these documents already, and --live builds its side
    /// from what the manager handed over, which cannot hold one service twice.
    /// </summary>
    /// <param name="difference">
    /// Annotated rather than left for the caller to assert with an exclamation mark. The compiler then
    /// knows what the return value already meant.
    /// </param>
    internal static bool Compare(
        Snapshot before, Snapshot after, [NotNullWhen(true)] out SnapshotDiff? difference, out int code)
    {
        code = ExitCode.Ok;

        if (SnapshotDiff.TryBetween(before, after, out difference, out var failure))
        {
            return true;
        }

        Console.Error.WriteLine(Texts.Of("cli.diff.cannotCompare", failure ?? string.Empty));

        // The same code a file that is not a snapshot gets, for the same reason: what is wrong is
        // the document somebody pointed at, and the machine did everything it was asked.
        code = ExitCode.Usage;

        return false;
    }

    /// <summary>
    /// The sentence for a file that could not be read as a snapshot - the four the command line has
    /// said since 2026-08-03, chosen by the fault rather than by the exception that used to carry it.
    /// A file that is not there and one the machine would not hand over share a sentence, and differ
    /// only in the code <see cref="Load"/> ends with.
    /// </summary>
    private static string Said(DiskRefusal refusal) => refusal.Fault switch
    {
        DiskFault.TooBig => Texts.Of("cli.diff.tooBig", refusal.Path, SnapshotOnDisk.Biggest / (1024 * 1024)),
        DiskFault.NotUtf8 => Texts.Of("cli.diff.notUtf8", refusal.Path),
        DiskFault.NotASnapshot => Texts.Of("cli.diff.notASnapshot", refusal.Path, refusal.Detail),
        _ => Texts.Of("cli.diff.cannotRead", refusal.Path, refusal.Detail)
    };
}
