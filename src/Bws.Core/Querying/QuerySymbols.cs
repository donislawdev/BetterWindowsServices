namespace Bws.Core.Querying;

/// <summary>
/// How one entry answers one enumeration field, away from the table of which fields exist.
///
/// <b>The second half of backlog 176, split out the same day as <see cref="QueryValueNames"/>.</b>
/// Moving the spellings out took QueryFields from 559 lines to 470 and that was enough until the
/// next field arrived - `mismatch` put it back over the ceiling within the hour. Two seams rather
/// than one because the file was carrying three subjects, not two.
///
/// <b>The subject here is the one that needs an ScmEntry in its hand.</b> Everything in
/// QueryFields is a declaration - a name, a kind, a list of words. Everything here is a reading
/// of an actual entry, with the four states of a <see cref="Reading{T}"/> to answer for and the
/// difference between "no" and "nobody could tell" to keep. They are read at different times and
/// they go wrong in different ways.
///
/// Normalise stays with the table it belongs to and is called across, because it is about how a
/// word is SPELT rather than about what an entry says.
/// </summary>
internal static class QuerySymbols
{
    /// <summary>
    /// Whether both halves of the disagreement could be worked out for this entry.
    ///
    /// <b>ABSENT WHEN THE ENTRY AGREES WITH ITSELF, AND THAT IS WHY `any` AND `none` WORK HERE.</b>
    /// The reserved words every field has are asked of the OUTCOME rather than of the symbols -
    /// `none` is ReadOutcome.Absent and `any` is ReadOutcome.Present. An entry doing exactly what
    /// it is configured to do has genuinely no mismatch, so absent is the honest answer, and
    /// `mismatch:none` then means what somebody typing it expects.
    ///
    /// <b>Present-with-no-symbols was the first version and it was wrong.</b> It made
    /// `mismatch:any` match every entry that could be judged, including the eight hundred that are
    /// fine - a filter that filters nothing while looking exactly like one that worked. The guard
    /// caught it, not the reading: assuming what a reserved word asks about is cheap to do and
    /// invisible afterwards.
    ///
    /// <b>The worse of the two readings, never the better one.</b> Each half comes from a field
    /// that can be refused, and answering absent while one of the two questions was never answered
    /// would report "this one is fine" about an entry nobody managed to judge.
    /// </summary>
    internal static ReadOutcome MismatchOutcome(ScmEntry entry)
    {
        var against = entry.RunsAgainstItsStartType;
        var running = entry.RunsWhileDisabled;

        if (against.Outcome == ReadOutcome.Denied || running.Outcome == ReadOutcome.Denied)
        {
            return ReadOutcome.Denied;
        }

        // ABSENT IS AN ANSWER AND NOT-READ IS NOT, AND UNTIL 2026-08-26 THIS TREATED THEM ALIKE.
        // It did no harm while neither half could ever be absent. Backlog 235 made one of them
        // absent - a per-user template has no answer to whether it runs against its start type -
        // and folding that into not-read would have moved 23 entries of 798 from one wrong
        // sentence to another: from "this disagrees with itself" to "nobody could tell", which is
        // what QueryValues.Unanswerable counts and what the window reports as judged on a field
        // it could not read.
        if (against.Outcome == ReadOutcome.NotRead || running.Outcome == ReadOutcome.NotRead)
        {
            return ReadOutcome.NotRead;
        }

        // Absent reads as "asked, and there is none" - so it contributes no mismatch and does not
        // stop the other half from contributing one. Read through the outcome rather than off the
        // value, because an absent reading carries default(bool) and a bare .Value here would be
        // a false that nobody measured.
        var disagrees =
            (against.Outcome == ReadOutcome.Present && against.Value)
            || (running.Outcome == ReadOutcome.Present && running.Value);

        return disagrees ? ReadOutcome.Present : ReadOutcome.Absent;
    }

    /// <summary>
    /// Which way this entry disagrees with itself, if it does.
    ///
    /// <b>At most one symbol, and never two.</b> The two cannot both be true - one needs an
    /// automatic entry that is stopped, the other a disabled entry that is running - so this reads
    /// as a choice rather than as a set. Nothing here relies on that: it appends what it finds, so
    /// the day a third direction is invented this keeps working instead of quietly picking one.
    ///
    /// An entry that agrees with itself answers with an empty <c>Of()</c> rather than with
    /// <c>Nothing</c>, and the difference is the one this project spends most of its rules on:
    /// empty means "asked, and there is none", Nothing means "nobody could tell".
    /// </summary>
    internal static FieldSymbols MismatchSymbols(ScmEntry entry)
    {
        var outcome = MismatchOutcome(entry);

        if (outcome is ReadOutcome.Denied or ReadOutcome.NotRead)
        {
            return FieldSymbols.Nothing;
        }

        if (outcome == ReadOutcome.Absent)
        {
            return FieldSymbols.Of();
        }

        var found = new List<string>(2);

        // ASKED THROUGH THE OUTCOME RATHER THAN OFF THE VALUE, for the reason spelled out in
        // MismatchOutcome: an absent reading carries default(bool), so a bare .Value here is a
        // false nobody measured. It happens to give the right answer today and it would stop
        // doing so the moment a second field learns to be absent.
        if (entry.RunsAgainstItsStartType is { Outcome: ReadOutcome.Present, Value: true })
        {
            found.Add("stopped");
        }

        if (entry.RunsWhileDisabled is { Outcome: ReadOutcome.Present, Value: true })
        {
            found.Add("running");
        }

        return FieldSymbols.Of([.. found]);
    }

    /// <summary>
    /// Whether the entry has an identity of its own, and which kind.
    ///
    /// An entry with none reports no symbols and is not incomplete: there is genuinely
    /// nothing here, which is what <c>sidtype:none</c> asks about.
    /// </summary>
    internal static FieldSymbols SidTypeSymbols(ScmEntry entry) => entry.SidType.Outcome switch
    {
        ReadOutcome.Present => FieldSymbols.Of(QuerySpelling.Normalise(entry.SidType.Value.ToString())),
        ReadOutcome.Absent => FieldSymbols.Of(),
        _ => FieldSymbols.Nothing
    };

    /// <summary>
    /// What the system concluded about the signature.
    ///
    /// A file with nothing to be signed - an entry naming no binary at all - reports no
    /// symbols and is not incomplete. There is genuinely nothing here, which is what
    /// <c>signed:none</c> asks about.
    /// </summary>
    internal static FieldSymbols SignatureSymbols(ScmEntry entry) => entry.Signature.Outcome switch
    {
        ReadOutcome.Present => FieldSymbols.Of(QuerySpelling.Normalise(entry.Signature.Value!.Status.ToString())),
        ReadOutcome.Absent => FieldSymbols.Of(),
        _ => FieldSymbols.Nothing
    };

    /// <summary>
    /// Whether there is a publisher to ask about.
    ///
    /// Its own function because the answer is not simply the signature's outcome. A file
    /// that was read and turned out to be unsigned has a signature reading that is present
    /// and a publisher that is genuinely absent - and absent is what <c>publisher:none</c>
    /// has to find, rather than nothing at all.
    /// </summary>
    internal static ReadOutcome PublisherOutcome(ScmEntry entry) =>
        entry.Signature.Outcome != ReadOutcome.Present ? entry.Signature.Outcome
            : entry.Signature.Value!.Publisher is null ? ReadOutcome.Absent
            : ReadOutcome.Present;

    /// <summary>
    /// Whether the file the entry runs is on disk.
    ///
    /// An entry naming no file at all reports neither symbol and is not incomplete: there
    /// is genuinely nothing to be present or missing, which is what <c>file:none</c> asks.
    /// </summary>
    internal static FieldSymbols FileSymbols(ScmEntry entry) => entry.BinaryOnDisk.Outcome switch
    {
        ReadOutcome.Present => FieldSymbols.Of(entry.BinaryOnDisk.Value ? "present" : "missing"),
        ReadOutcome.Absent => FieldSymbols.Of(),
        _ => FieldSymbols.Nothing
    };

    /// <summary>
    /// Every kind an entry's triggers carry, plus the actions they take.
    ///
    /// Kinds and actions share one list of symbols on purpose. They are two questions about
    /// the same thing and a person asking "what starts this by itself" should not have to
    /// learn which of the two words they need. The names cannot collide - the kinds are
    /// conditions and the actions are the two verbs this tool already uses everywhere.
    /// </summary>
    internal static FieldSymbols TriggerSymbols(ScmEntry entry)
    {
        if (!entry.Triggers.IsPresent)
        {
            return entry.Triggers.Outcome == ReadOutcome.Absent
                ? FieldSymbols.Of()
                : FieldSymbols.Nothing;
        }

        return FieldSymbols.Of(
        [
            .. entry.Triggers.Value!
                .SelectMany(trigger => new[] { QuerySpelling.Normalise(trigger.Kind.ToString()), QuerySpelling.Normalise(trigger.Action.ToString()) })
                .Distinct(StringComparer.Ordinal)
        ]);
    }

    /// <summary>
    /// The start type, plus "delayed" when the entry carries the delay flag.
    ///
    /// An automatic entry whose delay flag could not be read reports "automatic" and admits
    /// the gap. Reporting only "automatic" would answer <c>start:delayed</c> with a
    /// confident no, and a confident no about something we did not read is the failure
    /// this tool exists to avoid.
    /// </summary>
    internal static FieldSymbols StartSymbols(ScmEntry entry)
    {
        if (!entry.StartType.IsPresent)
        {
            return FieldSymbols.Nothing;
        }

        var startType = QuerySpelling.Normalise(entry.StartType.Value.ToString());

        if (entry.StartType.Value != StartType.Automatic)
        {
            return FieldSymbols.Of(startType);
        }

        return entry.DelayedAuto.Outcome switch
        {
            ReadOutcome.Present when entry.DelayedAuto.Value => FieldSymbols.Of(startType, "delayed"),
            ReadOutcome.Present or ReadOutcome.Absent => FieldSymbols.Of(startType),
            _ => FieldSymbols.Partial(startType)
        };
    }
}
