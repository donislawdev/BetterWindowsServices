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
    AlreadyThere,

    /// <summary>
    /// The snapshot about to be written is larger than <see cref="SnapshotOnDisk.Biggest"/>, so this build
    /// could not read it back - written nowhere. Since 2026-10-27, logic report S02.
    /// </summary>
    WouldBeTooBig
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
    /// Writes a snapshot's text to the path, or says why not and writes nothing - the one way a snapshot
    /// reaches a disk, for the command line and the window alike.
    ///
    /// <b>Since 2026-10-27, logic report R11 and S02, and both were the same shape:</b> a promise made by a
    /// question asked earlier and not kept by the write. The size this build reads back was checked only
    /// on files already there, so a snapshot over it was written, announced, and then refused by the same
    /// build as "not a snapshot this build wrote". And "nothing replaces a file without being told to" was
    /// true until the move, which always replaced. Both are now asked of the write itself, in this order:
    /// the size of the text (nothing else is worth asking about a file that may not be written at all),
    /// the rule about what stands there, and then a move that may not replace unless told it may.
    /// </summary>
    /// <param name="force">See <see cref="WhyNotWritten"/>.</param>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public static DiskRefusal? Write(string target, string text, bool force) => Write(target, text, force, Biggest);

    /// <param name="ceiling">
    /// <see cref="Biggest"/> everywhere but in a test, which cannot afford sixty four megabytes of text to
    /// show the refusal.
    /// </param>
    /// <param name="meanwhile">
    /// What happens between the question about the target and the move - nothing, everywhere but in a
    /// test, which puts a file there to show the move refusing it. The gap is microseconds wide and R11
    /// was about exactly it, so without this nothing could tell a move that refuses from one that does not.
    /// </param>
    internal static DiskRefusal? Write(string target, string text, bool force, long ceiling, Action? meanwhile = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(text);

        // In the bytes AtomicFile writes - UTF-8 without a mark - because that is the length the reader
        // compares with the same ceiling.
        if (Encoding.UTF8.GetByteCount(text) > ceiling)
        {
            return new DiskRefusal(DiskFault.WouldBeTooBig, target, string.Empty);
        }

        if (WhyNotWritten(target, force) is { } refusal)
        {
            return refusal;
        }

        meanwhile?.Invoke();

        if (force)
        {
            AtomicFile.Write(target, text);

            return null;
        }

        // Something arrived between the question above and the move. Asked again to name it - a folder, a
        // snapshot, another program's file - and called a snapshot standing there when it went again
        // before it could be named, which is still the truth about the moment the move was refused.
        return AtomicFile.WriteNew(target, text)
            ? null
            : WhyNotWritten(target, force: false) ?? new DiskRefusal(DiskFault.AlreadyThere, target, string.Empty);
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
            // reader, which produces the sentence about a path somebody got wrong. Asked first of the
            // path, which needs no handle - so a file too big is refused as too big even while something
            // holds it - and AGAIN of the file once it is open, since 2026-10-27 (logic report S02),
            // because the answer about the path is about a file that can change before it is opened.
            if ((File.Exists(path) && new FileInfo(path).Length > Biggest) || !TryReadText(path, out content))
            {
                refusal = new DiskRefusal(DiskFault.TooBig, path, string.Empty);

                return false;
            }
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
            // Of the open file only, since 2026-10-27 (logic report S02): asked of the path as well, the two
            // would be two gates for one rule here, and no mutation could prove either. TryLoad keeps both,
            // because there the path answers a question the open file cannot - a file held by somebody else.
            if (!TryReadText(path, out var content))
            {
                return new DiskRefusal(DiskFault.TooBig, path, string.Empty);
            }

            return SnapshotJson.TryRead(content, out _, out var failure)
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
    ///
    /// <b>THE MARK IS READ HERE, AND UNTIL 2026-10-27 THE READER READ IT - logic report S01, measured
    /// 2026-10-26 on .NET 10.0.13.</b> A reader told to honour marks swaps the decoder it was handed for
    /// the default one of the encoding it found, and the default ones substitute: a file WITHOUT a mark
    /// and a broken byte was refused, the same file WITH a UTF-8 mark came back with U+FFFD inside a
    /// value, and so did UTF-16 with a mark and a lone surrogate. Now the mark only chooses which strict
    /// decoder reads the rest, so a broken byte is refused whatever stands in front of it.
    ///
    /// <b>The size is asked of the file once it is open as well</b>, since the same day (S02) - the
    /// callers' question about the path is a question about a file that could change before it was
    /// opened. Opened for reading with others allowed only to read, so nothing grows it while it is read.
    /// </summary>
    /// <returns>False, with no text, when the file is larger than <see cref="Biggest"/>.</returns>
    /// <exception cref="DecoderFallbackException">The bytes are not text this build can read.</exception>
    /// <exception cref="IOException">The path is not a file on a disk - a device or a pipe has no length.</exception>
    private static bool TryReadText(string path, out string text)
    {
        text = string.Empty;

        using var stream = File.OpenRead(path);

        if (!stream.CanSeek)
        {
            throw new IOException("It is not a file on a disk, so it was not read.");
        }

        if (stream.Length > Biggest)
        {
            return false;
        }

        using var reader = new StreamReader(stream, Strict(stream), detectEncodingFromByteOrderMarks: false);

        text = reader.ReadToEnd();

        return true;
    }

    /// <summary>
    /// The decoder for what the first bytes say the file is, refusing rather than substituting, with the
    /// stream left just past the mark. The five marks a reader recognises by itself, in its order -
    /// UTF-32 little endian before UTF-16 little endian, because the first begins with the second - and
    /// UTF-8 for a file with none, which is what this build writes.
    /// </summary>
    private static Encoding Strict(Stream stream)
    {
        Span<byte> head = stackalloc byte[4];
        var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);

        var (encoding, mark) = head[..read] switch
        {
            [0xFF, 0xFE, 0x00, 0x00] => ((Encoding)new UTF32Encoding(bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: true), 4),
            [0x00, 0x00, 0xFE, 0xFF] => (new UTF32Encoding(bigEndian: true, byteOrderMark: false, throwOnInvalidCharacters: true), 4),
            [0xEF, 0xBB, 0xBF, ..] => (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), 3),
            [0xFF, 0xFE, ..] => (new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true), 2),
            [0xFE, 0xFF, ..] => (new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true), 2),
            _ => (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), 0)
        };

        stream.Position = mark;

        return encoding;
    }
}
