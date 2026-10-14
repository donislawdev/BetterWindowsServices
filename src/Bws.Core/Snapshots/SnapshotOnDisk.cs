using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Bws.Core.Snapshots;

/// <summary>What is wrong with a path somebody pointed at as a snapshot, or asked one to be written to.</summary>
public enum DiskFault
{
    /// <summary>Not there, or a path that cannot be spelled - what somebody typed.</summary>
    Mistyped,

    /// <summary>There, and the machine would not hand it over - a disk that went away, a lock, a permission.</summary>
    Unreadable,

    /// <summary>Larger than <see cref="SnapshotOnDisk.Biggest"/>, so not something this build wrote.</summary>
    TooBig,

    /// <summary>Bytes that are not UTF-8, which this build refuses rather than guesses at.</summary>
    NotUtf8,

    /// <summary>Read cleanly and not a snapshot this build reads - the reader says why.</summary>
    NotASnapshot,

    /// <summary>A folder stands where a snapshot was to be written.</summary>
    IsFolder,

    /// <summary>A snapshot stands there, and nobody said it may be replaced.</summary>
    AlreadyThere
}

/// <param name="Detail">
/// What was said underneath: Windows' own sentence for a read it refused, the reader's reason for a
/// file that is not a snapshot. Empty where the fault says everything there is to say.
/// </param>
public sealed record DiskRefusal(DiskFault Fault, string Path, string Detail);

/// <summary>
/// The snapshot as a file on somebody's disk: what it is called when nobody named it, whether a path
/// may be written to, and what comes back when somebody points at one.
///
/// <b>Moved here from the command line on 2026-10-14, when the window began taking and comparing
/// snapshots</b> (`docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md`, W1). The window references the core
/// and nothing else, so the alternative was a second copy of every rule below - the size ceiling,
/// the refusal to guess at bytes, the folder, and S-8's "only a snapshot is ever replaced" - and two
/// copies of a rule about somebody's evidence answer the same file two ways the first time one of
/// them is changed. What did not move is the words: this says WHICH fault, and each front end says it
/// in its own sentences.
/// </summary>
public static class SnapshotOnDisk
{
    /// <summary>
    /// The largest a file may be before this build refuses to treat it as a snapshot.
    ///
    /// Sixty four megabytes, against a measured 1.009 MB for this machine's 799 entries - so about
    /// fifty thousand entries, which no Windows installation has. The number refuses files that are
    /// not snapshots rather than snapshots that are large, and that is the whole of its job.
    /// </summary>
    public const long Biggest = 64L * 1024 * 1024;

    /// <summary>
    /// Where the snapshot goes when nobody said: the name given, or the machine and the moment.
    ///
    /// E1's own example writes a snapshot without naming a file, so a name has to be worked out - and
    /// it has to be one somebody can recognise weeks later among a dozen others, which rules out
    /// anything clever. Sorted by name is sorted by time, because the stamp runs from the largest unit
    /// down.
    ///
    /// <b>The stamp is written with an invariant culture, and until 2026-08-03 it was written with the
    /// machine's.</b> An interpolated hole formats with <c>CurrentCulture</c>, and a culture carries a
    /// calendar - so the same moment produced these names:
    ///
    /// <code>
    ///   pl-PL, en-US, ja-JP   bws-snapshot-MACHINE-20260803-194500.json
    ///   th-TH                 bws-snapshot-MACHINE-25690803-194500.json   Buddhist year
    ///   ar-SA                 bws-snapshot-MACHINE-14480220-194500.json   Hijri
    ///   fa-IR                 bws-snapshot-MACHINE-14050512-194500.json   Persian
    /// </code>
    ///
    /// The timestamp INSIDE the file is unaffected - the serialiser writes ISO 8601 whatever the
    /// machine thinks - so on those three systems the name of the file and the date in it said
    /// different things.
    /// </summary>
    public static string Target(string given, SnapshotMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(given);
        ArgumentNullException.ThrowIfNull(metadata);

        return given.Length > 0
            ? given
            : "bws-snapshot-" + metadata.Machine + "-"
                + metadata.TakenAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json";
    }

    /// <summary>
    /// Why this path may not be written to, or nothing when it may.
    ///
    /// <b>Nothing may replace an existing file without being told to</b> - that has held since
    /// 2026-08-02 for a named file and, since 2026-08-03, for the name this tool works out itself.
    ///
    /// <b>AND ONLY A SNAPSHOT IS EVER REPLACED, TOLD OR NOT, SINCE 2026-10-06 - security report S-8,
    /// owner's decision.</b> Everything that is not a snapshot this build reads - another program's
    /// file, a snapshot from a schema this build does not know, one that is too large or will not
    /// open - is refused and left exactly where it was, not even renamed.
    ///
    /// <b>A decision and nothing else - it moves nothing and writes nothing.</b> That split is what
    /// makes this the single place the rule lives, and the mutation runner asked for it: the check was
    /// once in two places, and taking either one away changed no outcome because the other still
    /// refused. Two gates for one rule means no single mutation can prove the rule.
    /// </summary>
    /// <param name="force">
    /// Whether somebody said a snapshot standing there may go - <c>--force</c> on the command line,
    /// the answer to "replace it?" in the window's save dialog.
    /// </param>
    public static DiskRefusal? WhyNotWritten(string target, bool force)
    {
        ArgumentNullException.ThrowIfNull(target);

        // A FOLDER IS NEVER WRITTEN OVER, WITH OR WITHOUT --force, since 2026-09-30 - stability report
        // D-6, measured: `bws snapshot create <an existing folder>` spent 1545 ms reading signatures and
        // then ended with "Access to the path is denied." and code 2, because File.Exists says no for a
        // folder and the refusal came only from the write.
        if (Directory.Exists(target))
        {
            return new DiskRefusal(DiskFault.IsFolder, target, string.Empty);
        }

        if (!File.Exists(target))
        {
            return null;
        }

        if (WhyNotASnapshot(target) is { } why)
        {
            return why;
        }

        if (!force)
        {
            return new DiskRefusal(DiskFault.AlreadyThere, target, string.Empty);
        }

        return null;
    }

    /// <summary>
    /// Reads one snapshot from disk, or says what is wrong with what was pointed at.
    ///
    /// Deliberately narrow catches rather than a broad one. The ways a path can be wrong are a list
    /// somebody can finish - it is not there, it is a directory, it cannot be opened, it is not
    /// spellable - which is exactly the argument the broad catches in this project are allowed by,
    /// run backwards.
    ///
    /// <b>Whose fault it was is kept apart</b> - <see cref="DiskFault.Mistyped"/> against
    /// <see cref="DiskFault.Unreadable"/> - because the command line ends the two with different codes:
    /// a disk that went away or a file another process holds is the machine failing, and calling it a
    /// typo teaches a monitor to ignore the code that says so (docs/12, step 5).
    /// </summary>
    public static bool TryLoad(
        string path, [NotNullWhen(true)] out Snapshot? snapshot, [NotNullWhen(false)] out DiskRefusal? refusal)
    {
        ArgumentNullException.ThrowIfNull(path);

        snapshot = null;
        refusal = null;

        string content;

        try
        {
            // ASKED HOW BIG IT IS BEFORE READING IT ALL, SINCE 2026-09-03 - backlog 308(c). The reader
            // below builds one string out of every byte, so a path pointing at something enormous ends
            // the process with an out of memory failure. A file that is not there falls through to the
            // reader, which produces the sentence about a path somebody got wrong.
            if (File.Exists(path) && new FileInfo(path).Length > Biggest)
            {
                refusal = new DiskRefusal(DiskFault.TooBig, path, string.Empty);

                return false;
            }

            content = ReadText(path);
        }

        // AHEAD OF THE TWO BELOW, and it has to be: a decoder failure IS an ArgumentException, so the
        // clause that calls a mistyped path a mistake would otherwise swallow it and blame the wrong
        // thing.
        catch (DecoderFallbackException)
        {
            refusal = new DiskRefusal(DiskFault.NotUtf8, path, string.Empty);

            return false;
        }
        catch (Exception mistyped) when (mistyped is FileNotFoundException or DirectoryNotFoundException or ArgumentException)
        {
            refusal = new DiskRefusal(DiskFault.Mistyped, path, mistyped.Message);

            return false;
        }
        catch (Exception broke) when (broke is IOException or UnauthorizedAccessException)
        {
            refusal = new DiskRefusal(DiskFault.Unreadable, path, broke.Message);

            return false;
        }

        // The reader's own out value carries no promise about null, so the promise this method makes
        // is kept here rather than asserted with an exclamation mark - a document it calls read and
        // hands back as nothing would otherwise reach a caller the annotation told not to check.
        if (SnapshotJson.TryRead(content, out var read, out var failure) && read is not null)
        {
            snapshot = read;

            return true;
        }

        // The reason is never null when the read failed, and saying so out loud is cheaper than a
        // nullable sentence in a message.
        refusal = new DiskRefusal(DiskFault.NotASnapshot, path, failure ?? string.Empty);

        return false;
    }

    /// <summary>
    /// Why what is already at this path is not a snapshot this build reads, or nothing when it is one.
    ///
    /// <b>The same faults <see cref="TryLoad"/> names</b>, because it is the same question about the
    /// same kind of file - asked here of something standing where a snapshot is about to go.
    ///
    /// <b>THE SIZE IS ASKED BEFORE A BYTE IS READ, and until 2026-10-06 it was not.</b> Measured that
    /// day with the shipped 0.3.0: <c>snapshot create</c> over a 1.5 GB file with --force ended after
    /// 4045 ms with code 1 and "Insufficient memory to continue the execution of the program."
    /// </summary>
    private static DiskRefusal? WhyNotASnapshot(string path)
    {
        try
        {
            if (new FileInfo(path).Length > Biggest)
            {
                return new DiskRefusal(DiskFault.TooBig, path, string.Empty);
            }

            return SnapshotJson.TryRead(ReadText(path), out _, out var failure)
                ? null
                : new DiskRefusal(DiskFault.NotASnapshot, path, failure ?? string.Empty);
        }

        // Ahead of the clause below for the reason TryLoad gives: a decoder failure IS an ArgumentException.
        catch (DecoderFallbackException)
        {
            return new DiskRefusal(DiskFault.NotUtf8, path, string.Empty);
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new DiskRefusal(DiskFault.Unreadable, path, unreadable.Message);
        }
    }

    /// <summary>
    /// The bytes at a path, as text, or nothing at all.
    ///
    /// <b>A decoder that refuses, and until 2026-08-03 this was <c>File.ReadAllText</c>, which
    /// substitutes.</b> That call honours a byte order mark and otherwise assumes UTF-8, and when the
    /// bytes are not UTF-8 it puts a replacement character in and carries on without a word.
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
    /// <b>Two hundred and ninety seven entries reported as changed, and not one of them had.</b> The
    /// mark is still honoured, so a snapshot saved as UTF-16 by something else still reads. Only bytes
    /// that are not any of those come back as a refusal.
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
}
