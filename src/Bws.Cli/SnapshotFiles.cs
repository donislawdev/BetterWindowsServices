using Bws.Core.Snapshots;

namespace Bws.Cli;

/// <summary>
/// The snapshot as a file on somebody's disk: where it goes when they did not say, and what
/// comes back when they point at one.
///
/// <b>Moved out of Program.cs on 2026-08-03 because the size ratchet said so, for the fifth
/// time.</b> Telling a mistyped path apart from a disk that went away pushed that file fourteen
/// lines past a ceiling that may only ever go down, and the ceiling exists so that adding to the
/// longest file starts with looking for a seam.
///
/// The seam is between the run and the disk. What stays behind reads the command line, chooses
/// the verb and produces the document. What is here touches a path: it works one out when nobody
/// gave one, and it turns whatever is at the other end into a snapshot or into a sentence and a
/// code. <c>Execution</c> took the other half of the same file for the same reason, on the same
/// principle - the part that waits.
/// </summary>
internal static class SnapshotFiles
{
    /// <summary>
    /// Where the snapshot goes when nobody said.
    ///
    /// The machine and the moment, in the directory the person is standing in. E1's own example
    /// writes a snapshot without naming a file, so a name has to be worked out - and it has to
    /// be one somebody can recognise weeks later among a dozen others, which rules out anything
    /// clever. Sorted by name is sorted by time, because the stamp runs from the largest unit
    /// down.
    ///
    /// Not a directory of our own choosing. ADR-18 says the tool does not invent directories,
    /// and a file appearing somewhere in a profile is a file nobody finds.
    /// </summary>
    internal static string Target(string given, SnapshotMetadata metadata) =>
        given.Length > 0
            ? given
            : $"bws-snapshot-{metadata.Machine}-{metadata.TakenAt:yyyyMMdd-HHmmss}.json";

    /// <summary>
    /// Reads one snapshot from disk, or says what is wrong with what was pointed at.
    ///
    /// Deliberately narrow catches rather than a broad one. The ways a path can be wrong are a
    /// list somebody can finish - it is not there, it is a directory, it cannot be opened, it is
    /// not spellable - which is exactly the argument the broad catches in this project are
    /// allowed by, run backwards.
    /// </summary>
    /// <param name="code">
    /// How the run should end if this returns false. Its own answer rather than a code the
    /// caller assumes, because the two reasons a read fails are not the same person's fault.
    /// </param>
    internal static bool Load(string path, out Snapshot? snapshot, out int code)
    {
        snapshot = null;
        code = ExitCode.Ok;

        string content;

        try
        {
            content = File.ReadAllText(path);
        }

        // WHOSE FAULT IT WAS DECIDES THE CODE, and until 2026-08-03 every one of these was code 2 -
        // the one this tool reserves for what somebody typed wrongly. A file that is not there and a
        // path that cannot be spelled are indeed that. A disk that went away mid-read, a file another
        // process is holding open and a directory this account may not enter are not: they are the
        // machine failing, and calling them a typo teaches a monitor to ignore the code that says so.
        //
        // Step 5 of docs/12 asks exactly this question - can a script tell my fault from yours by
        // this number - and here the answer was no. `audit.ps1` is green either way, because it asks
        // whether a code is IN the table rather than whether it is the RIGHT one.
        catch (Exception mistyped) when (mistyped is FileNotFoundException or DirectoryNotFoundException or ArgumentException)
        {
            Console.Error.WriteLine(Texts.Of("cli.diff.cannotRead", path, mistyped.Message));
            code = ExitCode.Usage;

            return false;
        }
        catch (Exception broke) when (broke is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(Texts.Of("cli.diff.cannotRead", path, broke.Message));
            code = ExitCode.Runtime;

            return false;
        }

        if (SnapshotJson.TryRead(content, out snapshot, out var failure))
        {
            return true;
        }

        // Names the file. Two are being read and a message about neither of them would leave
        // somebody checking both. The reason is never null when the read failed, and saying so
        // out loud is cheaper than a nullable sentence in a message.
        Console.Error.WriteLine(Texts.Of("cli.diff.notASnapshot", path, failure ?? string.Empty));

        // A file that is not a snapshot is what somebody pointed at, so it keeps the usage code.
        // The machine did its part here - it handed over every byte that was asked for.
        code = ExitCode.Usage;

        return false;
    }
}
