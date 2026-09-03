using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Bws.Core;
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
    /// The largest a file may be before this build refuses to treat it as a snapshot.
    ///
    /// Sixty four megabytes, against a measured 1.009 MB for this machine's 799 entries - so about
    /// fifty thousand entries, which no Windows installation has. The number refuses files that are
    /// not snapshots rather than snapshots that are large, and that is the whole of its job.
    /// </summary>
    private const long Biggest = 64L * 1024 * 1024;

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
    ///
    /// <b>The stamp is written with an invariant culture, and until 2026-08-03 it was written
    /// with the machine's.</b> An interpolated hole formats with <c>CurrentCulture</c>, and a
    /// culture carries a calendar - so the same moment produced these names:
    ///
    /// <code>
    ///   pl-PL, en-US, ja-JP   bws-snapshot-MACHINE-20260803-194500.json
    ///   th-TH                 bws-snapshot-MACHINE-25690803-194500.json   Buddhist year
    ///   ar-SA                 bws-snapshot-MACHINE-14480220-194500.json   Hijri
    ///   fa-IR                 bws-snapshot-MACHINE-14050512-194500.json   Persian
    /// </code>
    ///
    /// The timestamp INSIDE the file is unaffected - the serialiser writes ISO 8601 whatever the
    /// machine thinks - so on those three systems <b>the name of the file and the date in it said
    /// different things</b>, and the sentence above about sorting by name was false. The
    /// quarantine name two files away had this right from the day it was written, which is what
    /// makes this an omission rather than a decision.
    /// </summary>
    internal static string Target(string given, SnapshotMetadata metadata) =>
        given.Length > 0
            ? given
            : "bws-snapshot-" + metadata.Machine + "-"
                + metadata.TakenAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json";

    /// <summary>
    /// Whether this file may be written over, and what had to happen first if so.
    ///
    /// <b>Two things, and the second is `ADR-18` finally getting a caller.</b> Nothing may
    /// replace an existing file without being told to - that has held since 2026-08-02 for a
    /// named file and, until 2026-08-03, not at all for the name this tool works out itself.
    ///
    /// And when it IS told to, what it replaces decides how. A readable snapshot is replaced,
    /// because that is what somebody asking for --force means. Anything else is <b>moved aside
    /// first</b>: `ADR-18` asks for quarantine rather than deletion on the grounds that a
    /// corrupt snapshot is sometimes the only remaining trace of what was there, and destroying
    /// evidence is a poor thing for an audit tool to do. <c>AtomicFile.Quarantine</c> has
    /// existed and been tested since S5a and had no caller in the product at all, which made the
    /// promise look kept while nothing kept it.
    ///
    /// <b>Only here, and deliberately not when a file is READ.</b> Moving somebody's file
    /// because they mistyped a path to `snapshot diff` would make a read-only command
    /// destructive, which is a worse fault than the one being fixed. Writing is the moment
    /// `ADR-18` is about - it is the moment the evidence would otherwise be lost.
    /// </summary>
    /// <b>A decision and nothing else - it moves nothing and writes nothing.</b> That split is
    /// what makes this the single place the rule lives, and the mutation runner is what asked
    /// for it: the check was in two places, one early for the courtesy above and one late for
    /// the worked-out name, and taking either one away changed no outcome because the other
    /// still refused. Two gates for one rule means no single mutation can prove the rule, which
    /// the runner reported as MISSED and was right to.
    internal static bool MayWrite(string target, bool force, out string? refusal)
    {
        refusal = null;

        if (!File.Exists(target))
        {
            return true;
        }

        if (!force)
        {
            refusal = Texts.Of("cli.snapshot.fileExists", target);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Moves aside what is about to be written over, when losing it would lose something.
    ///
    /// <b>`ADR-18` finally getting a caller.</b> A readable snapshot is replaced, because that is
    /// what somebody asking for <c>--force</c> means and keeping a copy of every one would fill
    /// their directory with files they never asked for. Anything else is kept: `ADR-18` asks for
    /// quarantine rather than deletion on the grounds that a corrupt snapshot is sometimes the
    /// only remaining trace of what was there, and destroying evidence is a poor thing for an
    /// audit tool to do. <c>AtomicFile.Quarantine</c> had existed and been tested since S5a with
    /// no caller in the product at all, which made the promise look kept while nothing kept it.
    ///
    /// <b>Called just before the write and nowhere near a read.</b> Moving somebody's file
    /// because they mistyped a path to <c>snapshot diff</c> would make a read-only command
    /// destructive, which is a worse fault than the one being fixed. And late rather than early,
    /// so a run that falls over while verifying signatures has not already moved anything.
    /// </summary>
    /// <param name="quarantined">Where the old file went, or null when nothing was moved.</param>
    internal static bool KeepWhatCannotBeRead(string target, out string? quarantined, out string? refusal)
    {
        quarantined = null;
        refusal = null;

        // Read to find out what it is, and nothing is said about the failure - the question here
        // is only "is this a snapshot", and a file that is not one is about to be replaced
        // anyway. What matters is that it is kept.
        if (!File.Exists(target) || Readable(target))
        {
            return true;
        }

        try
        {
            quarantined = AtomicFile.Quarantine(target, new SystemClock());

            return true;
        }
        catch (Exception stuck) when (stuck is IOException or UnauthorizedAccessException)
        {
            // Could not be moved, so it must not be written over either. Anything else would
            // destroy the file this branch exists to preserve.
            refusal = Texts.Of("cli.snapshot.cannotQuarantine", target, stuck.Message);

            return false;
        }
    }

    /// <summary>
    /// Whether what is already at this path is a snapshot this build can read.
    ///
    /// Quiet on purpose, unlike <see cref="Load"/>. Nobody asked to read this file - it is being
    /// asked about only to decide whether replacing it loses anything.
    /// </summary>
    private static bool Readable(string path)
    {
        try
        {
            return SnapshotJson.TryRead(ReadText(path), out _, out _);
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Cannot be read at all - and that now includes "is not UTF-8", because the reader
            // below refuses rather than substitutes. Which is the strongest possible case for
            // keeping the file rather than writing over it.
            return false;
        }
    }

    /// <summary>
    /// The bytes at a path, as text, or nothing at all.
    ///
    /// <b>A decoder that refuses, and until 2026-08-03 this was <c>File.ReadAllText</c>, which
    /// substitutes.</b> That call honours a byte order mark and otherwise assumes UTF-8, and when
    /// the bytes are not UTF-8 it puts a replacement character in and carries on without a word.
    ///
    /// MEASURED on the same snapshot re-encoded four ways and compared against itself:
    ///
    /// <code>
    ///   UTF-8                  No differences.
    ///   UTF-8 with a mark      No differences.
    ///   UTF-16 with a mark     No differences.
    ///   Windows-1250           Changed (297)
    /// </code>
    ///
    /// <b>Two hundred and ninety seven entries reported as changed, and not one of them had.</b>
    /// It takes one person opening a snapshot in an editor set to the machine's code page and
    /// saving it - and with <c>--exit-code</c> that is a pipeline failing over drift that does not
    /// exist. In a tool whose whole purpose is telling real drift from noise, silently guessing at
    /// bytes is the one thing it may not do.
    ///
    /// The mark is still honoured, so a snapshot saved as UTF-16 by something else still reads.
    /// Only bytes that are not any of those come back as a refusal.
    /// </summary>
    /// <exception cref="DecoderFallbackException">The bytes are not text this build can read.</exception>
    private static string ReadText(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

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
            // ASKED HOW BIG IT IS BEFORE READING IT ALL, SINCE 2026-09-03 - backlog 308(c). The
            // reader below builds one string out of every byte, so a path pointing at something
            // enormous ends the process with an out of memory failure and a stack trace, which is
            // the one shape this tool's entry point cannot turn into a sentence worth reading.
            //
            // MEASURED RATHER THAN GUESSED: a snapshot of this machine, 799 entries, is 1.009 MB.
            // Sixty four megabytes is therefore about fifty thousand entries, which no Windows
            // installation has - so this refuses files that are not snapshots rather than
            // snapshots that are large.
            //
            // A file that is not there falls through to the reader below, which produces the
            // sentence about a path somebody got wrong. This clause only ever speaks about a file
            // that exists.
            if (File.Exists(path) && new FileInfo(path).Length > Biggest)
            {
                Console.Error.WriteLine(Texts.Of("cli.diff.tooBig", path, Biggest / (1024 * 1024)));
                code = ExitCode.Usage;

                return false;
            }

            content = ReadText(path);
        }

        // AHEAD OF THE TWO BELOW, and it has to be: a decoder failure IS an ArgumentException, so
        // the clause that calls a mistyped path a mistake would otherwise swallow it and blame
        // the wrong thing. This is not a path somebody got wrong - it is a file that exists,
        // opened cleanly, and holds bytes this build will not guess at.
        catch (DecoderFallbackException)
        {
            Console.Error.WriteLine(Texts.Of("cli.diff.notUtf8", path));
            code = ExitCode.Usage;

            return false;
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

    /// <summary>
    /// Compares two snapshots, or says which side stopped it and why.
    ///
    /// <b>Here rather than beside the verb it serves, and the reason is the size ratchet.</b>
    /// Program.cs stood at 549 lines against a ceiling of 552 that may only ever go down, so the
    /// branch this needs would have reddened the build - which is the ratchet working, not
    /// getting in the way. This file already owns "turn a snapshot the person pointed at into
    /// either a document or a sentence and a code", and a comparison refusing one of its two
    /// sides is the same sentence arriving one step later.
    ///
    /// <b>Unreachable through the command line today, and that is written down rather than left
    /// to be discovered.</b> Both sides of a two-file comparison have been through
    /// <c>SnapshotJson.TryRead</c>, which refuses these documents already, and --live builds its
    /// side from what the manager handed over, which cannot hold one service twice. What this
    /// guards is the next caller - a window showing a comparison - and the cost of guarding it
    /// now is one branch.
    /// </summary>
    /// <param name="difference">
    /// Annotated rather than left for the caller to assert with an exclamation mark. The compiler
    /// then knows what the return value already meant, and the alternative - telling it to be
    /// quiet twice at the call site - says the same thing while switching off the check that
    /// would notice if it stopped being true.
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
}
