namespace Bws.Core;

/// <summary>
/// The second half of ADR-13: fills in what a listing costs too much to know up front.
///
/// The first pass reads the manager and is cheap - measured at 322-329 ms for 810 entries
/// against a budget of a second. This one opens files and asks the trust providers about
/// them, which measured at roughly three seconds for the same machine. That gap is the
/// entire reason the two are separate, and it is the first family where the separation
/// earned itself: triggers and launch paths both turned out cheap enough to fold into the
/// first pass, and this one is not.
///
/// Entries come back as new records rather than being modified. A plan and a snapshot are
/// evidence and evidence does not change after it has been shown, and an entry that could
/// quietly gain fields after a caller had it would be the same problem one level down.
/// </summary>
public static class SecondPass
{
    /// <summary>
    /// Every entry, with the signature and file version filled in.
    ///
    /// One question per distinct file, not per entry. Measured on a real machine on
    /// 2026-08-01: 810 entries point at 544 distinct files, because services sharing a host
    /// process all name the same svchost. Asking per entry would repeat a third of the work
    /// for answers already known, and this is the family where that work is expensive.
    /// </summary>
    public static IReadOnlyList<ScmEntry> Fill(IReadOnlyList<ScmEntry> entries, IBinaryInspector inspector)
    {
        var signatures = new Dictionary<string, Reading<BinarySignature>>(StringComparer.OrdinalIgnoreCase);
        var versions = new Dictionary<string, Reading<string>>(StringComparer.OrdinalIgnoreCase);
        var hashes = new Dictionary<string, Reading<string>>(StringComparer.OrdinalIgnoreCase);
        var filled = new List<ScmEntry>(entries.Count);

        foreach (var entry in entries)
        {
            filled.Add(Fill(entry, inspector, signatures, versions, hashes));
        }

        return filled;
    }

    private static ScmEntry Fill(
        ScmEntry entry,
        IBinaryInspector inspector,
        Dictionary<string, Reading<BinarySignature>> signatures,
        Dictionary<string, Reading<string>> versions,
        Dictionary<string, Reading<string>> hashes)
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
                var file = entry.BinaryFile.Value!;

                if (!signatures.TryGetValue(file, out var signature))
                {
                    signature = inspector.ReadSignature(file);
                    signatures[file] = signature;
                }

                if (!versions.TryGetValue(file, out var version))
                {
                    version = inspector.ReadFileVersion(file);
                    versions[file] = version;
                }

                if (!hashes.TryGetValue(file, out var hash))
                {
                    hash = inspector.ReadHash(file);
                    hashes[file] = hash;
                }

                return entry with { Signature = signature, FileVersion = version, BinaryHash = hash };

            default:
                // Nobody read the path, so nobody can read what is at the end of it. Left
                // as not read rather than turned into any kind of answer.
                return entry;
        }
    }
}
