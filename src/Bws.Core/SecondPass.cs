namespace Bws.Core;

/// <summary>
/// The second half of ADR-13: fills in what a listing costs too much to know up front.
///
/// The first pass reads the manager and is cheap - measured at 476-551 ms for 810 entries
/// against a budget of a second. This one opens files and asks the trust providers about
/// them, which measured at 4620-7656 ms over 544 distinct files before it was made to ask
/// about several at once. That gap is the entire reason the two are separate, and it is the
/// first family where the separation earned itself: triggers and launch paths both turned
/// out cheap enough to fold into the first pass, and this one is not.
///
/// Entries come back as new records rather than being modified. A plan and a snapshot are
/// evidence and evidence does not change after it has been shown, and an entry that could
/// quietly gain fields after a caller had it would be the same problem one level down.
/// </summary>
public static class SecondPass
{
    /// <summary>
    /// How many files are asked about at once when nobody says otherwise.
    ///
    /// The number of logical processors, and that came out of a sweep rather than a guess.
    /// Measured on 2026-08-01 over 810 entries and 544 distinct files, five counted runs at
    /// each degree, every degree run once per pass so that all of them met the same
    /// catalogue-store weather:
    ///
    ///     degree    1      2      4      8     16     32
    ///     fastest   4376   2474   1550   1144   1031   1001 ms
    ///     spread    13.5%  35.9%  35.1%  18.9%   7.4%   7.3%
    ///
    /// The machine had 16 logical processors. Sixteen is measurably better than eight - the
    /// ranges [1144, 1360] and [1031, 1107] do not touch. Thirty-two is not measurably
    /// better than sixteen - [1001, 1074] and [1031, 1107] overlap, and the project's own
    /// rule is that a spread wider than the difference means there is no difference. So the
    /// plateau is at the processor count, and going past it buys nothing while asking the
    /// machine for more.
    ///
    /// <b>SETTLED 2026-08-04, on the second machine this has ever run on.</b> Windows Server
    /// 2025, eight logical processors, a different disk, 661 entries over 433 distinct files:
    ///
    ///     degree    1      2      4      8     16     32
    ///     fastest   2506   2117   1497   1296   1306   1305 ms
    ///     spread    5.9%   2.6%   9.0%  13.7%   8.7%  10.3%
    ///
    /// Eight is measurably better than four - [1497, 1632] and [1296, 1474] do not touch.
    /// Sixteen is not measurably better than eight, and neither is thirty-two. <b>So the
    /// plateau moved to eight, which is this machine's processor count.</b> An absolute
    /// number that happened to be sixteen would have shown a gain from eight to sixteen here
    /// as well, and there is none. It follows the processors.
    ///
    /// That is what makes ProcessorCount the right default rather than a number that suited
    /// one machine, and it is the answer a build agent needed: few cores means a lower
    /// plateau, not a wasted one.
    /// </summary>
    public static int DefaultDegreeOfParallelism => Environment.ProcessorCount;

    /// <summary>
    /// Every entry, with the signature, file version and hash filled in.
    ///
    /// One question per distinct file, not per entry. Measured on a real machine on
    /// 2026-08-01: 810 entries point at 544 distinct files, because services sharing a host
    /// process all name the same svchost. Asking per entry would repeat a third of the work
    /// for answers already known, and this is the family where that work is expensive.
    /// </summary>
    public static IReadOnlyList<ScmEntry> Fill(IReadOnlyList<ScmEntry> entries, IBinaryInspector inspector) =>
        Fill(entries, inspector, DefaultDegreeOfParallelism);

    /// <summary>
    /// The same, with the number of files asked about at once given rather than defaulted.
    ///
    /// It is a parameter because the guard needs it. "Threads changed no verdict" can only
    /// be checked by running both ways and comparing, and a guard that cannot run the old
    /// way has nothing to compare against. Verified on 2026-08-01 over 29 376 comparisons
    /// of a real machine's 544 files - a sweep, a run through the thread pool and a soak at
    /// the top degree - with no verdict differing from the single-threaded answer.
    ///
    /// Nothing in the product passes this. Both call sites take the default.
    /// </summary>
    public static IReadOnlyList<ScmEntry> Fill(
        IReadOnlyList<ScmEntry> entries, IBinaryInspector inspector, int degreeOfParallelism)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentOutOfRangeException.ThrowIfLessThan(degreeOfParallelism, 1);

        var files = Distinct(entries);
        var answers = Ask(files, inspector, degreeOfParallelism);
        var filled = new List<ScmEntry>(entries.Count);

        foreach (var entry in entries)
        {
            filled.Add(Fill(entry, answers));
        }

        return filled;
    }

    /// <summary>
    /// The files to ask about, settled before a single question is asked.
    ///
    /// This is why the work is split in three rather than parallelising the loop that used
    /// to do everything at once. The obvious shape - go wide over the entries and let a
    /// concurrent dictionary sort out the repeats - does not hold the one property that
    /// makes this affordable: a concurrent dictionary is documented as free to run the
    /// factory for a key more than once, so 544 files could become rather more than 544
    /// verifications, and each one of those costs about eight milliseconds.
    ///
    /// Fixing the set first makes exactly-once true by construction instead of by luck, and
    /// it costs one cheap walk over the entries.
    /// </summary>
    private static List<string> Distinct(IReadOnlyList<ScmEntry> entries)
    {
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (entry.BinaryFile.Outcome == ReadOutcome.Present && seen.Add(entry.BinaryFile.Value!))
            {
                files.Add(entry.BinaryFile.Value!);
            }
        }

        return files;
    }

    /// <summary>
    /// Every file, several at a time.
    ///
    /// Answers land in a slot of their own, so no two threads write the same place and
    /// nothing has to be locked. The thread pool rather than threads of our own: measured on
    /// 2026-08-01, asking the pool for 32 at once actually got 18, because it adds threads
    /// slowly - and it made no difference to the time, since the gain has already flattened
    /// by sixteen. Something that reaches the plateau on the machinery the platform already
    /// has is worth more over ten years than something that manages its own threads to reach
    /// the same number.
    /// </summary>
    private static Dictionary<string, Answer> Ask(
        List<string> files, IBinaryInspector inspector, int degreeOfParallelism)
    {
        var answers = new Answer[files.Count];

        Parallel.For(
            0,
            files.Count,
            new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism },
            index => answers[index] = new Answer(
                inspector.ReadSignature(files[index]),
                inspector.ReadFileVersion(files[index]),
                inspector.ReadHash(files[index])));

        var byFile = new Dictionary<string, Answer>(files.Count, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < files.Count; index++)
        {
            byFile[files[index]] = answers[index];
        }

        return byFile;
    }

    /// <summary>What was learned about one file, kept together so an entry is filled in one move.</summary>
    private readonly record struct Answer(
        Reading<BinarySignature> Signature, Reading<string> Version, Reading<string> Hash);

    private static ScmEntry Fill(ScmEntry entry, Dictionary<string, Answer> answers)
    {
        switch (entry.BinaryFile.Outcome)
        {
            case ReadOutcome.Absent:
                // The entry names nothing to run and no default applies. There is no file
                // for a signature to be on, which is a fact about the entry rather than
                // something we failed to find out.
                return entry with
                {
                    Signature = Reading<BinarySignature>.Absent(),
                    FileVersion = Reading<string>.Absent(),
                    BinaryHash = Reading<string>.Absent()
                };

            case ReadOutcome.Denied:
                // The configuration was refused, so we never learned which file to look at.
                // Passed on whole, number and sentence together: the reason this is unknown
                // is the reason that was, and inventing a second one would be a sentence
                // nobody could act on.
                return entry with
                {
                    Signature = Reading<BinarySignature>.Denied(entry.BinaryFile.ErrorCode, entry.BinaryFile.Reason!),
                    FileVersion = Reading<string>.Denied(entry.BinaryFile.ErrorCode, entry.BinaryFile.Reason!),
                    BinaryHash = Reading<string>.Denied(entry.BinaryFile.ErrorCode, entry.BinaryFile.Reason!)
                };

            case ReadOutcome.Present:
                var answer = answers[entry.BinaryFile.Value!];

                return entry with
                {
                    Signature = answer.Signature,
                    FileVersion = answer.Version,
                    BinaryHash = answer.Hash
                };

            default:
                // Nobody read the path, so nobody can read what is at the end of it. Left
                // as not read rather than turned into any kind of answer.
                return entry;
        }
    }
}
