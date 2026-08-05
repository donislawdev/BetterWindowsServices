namespace Bws.Core;

/// <summary>
/// The things that change what an entry's start type means, decided once for every surface
/// that shows a start type.
///
/// <b>It is here rather than in either interface because it is a rule, not a wording.</b> The
/// command line worked this out for itself from 2026-08-01, and the window showing the same
/// column would have been a second copy - which is the failure this project keeps paying for,
/// most recently when the manager's exit code table lived in two places. The rule is one thing.
/// The words are two, because they come out of two language files, and that split is exactly
/// where the line belongs: this type says <i>what</i> qualifies, and each surface says it in
/// its own words.
///
/// None of it costs a reading. Every field it looks at arrives in the ordinary listing pass.
/// </summary>
public readonly record struct StartQualifiers
{
    /// <summary>The entry starts automatically, but late on purpose.</summary>
    public bool Delayed { get; private init; }

    /// <summary>
    /// The entry starts automatically and nobody could find out whether it is delayed.
    ///
    /// Reported rather than swallowed, and only here: printing a plain "Automatic" would be a
    /// claim that it starts at boot, which is precisely what was not established.
    /// </summary>
    public bool DelayUnknown { get; private init; }

    /// <summary>
    /// Something can ask for this entry by event.
    ///
    /// <b>The single most load-bearing one of the four.</b> Measured on a real machine on
    /// 2026-08-01: four of the ten entries that read as "automatic and not running" were
    /// waiting to be asked for rather than broken. Without this, a person scanning for a
    /// failure finds four healthy entries first and stops trusting the column.
    /// </summary>
    public bool OnTrigger { get; private init; }

    /// <summary>
    /// The file the manager would run is not on disk. Whatever it was going to do with this
    /// entry, it cannot.
    /// </summary>
    public bool FileMissing { get; private init; }

    /// <summary>Whether anything at all qualifies the start type.</summary>
    public bool Any => Delayed || DelayUnknown || OnTrigger || FileMissing;

    public static StartQualifiers Of(ScmEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Only where the flag does something, which is not the same as everywhere it is read.
        // The manager is asked about the delay for every non-driver entry because it is stored
        // configuration a snapshot should carry - but Windows ignores it unless the entry
        // starts automatically. "Manual (delayed)" would be a sentence about a setting with no
        // effect, on eight entries of the machine this was measured on.
        var delayMeansSomething = entry.StartType.IsPresent && entry.StartType.Value == StartType.Automatic;

        return new StartQualifiers
        {
            Delayed = delayMeansSomething
                && entry.DelayedAuto is { Outcome: ReadOutcome.Present, Value: true },

            DelayUnknown = delayMeansSomething && entry.DelayedAuto.Outcome == ReadOutcome.Denied,

            // Only a trigger that STARTS it. One that stops the entry says nothing about
            // whether it will come up, and marking it here would answer a question nobody
            // asked with a fact about something else.
            OnTrigger = entry.Triggers.IsPresent
                && entry.Triggers.Value!.Any(trigger => trigger.Action == TriggerAction.Start),

            FileMissing = entry.BinaryOnDisk is { Outcome: ReadOutcome.Present, Value: false }
        };
    }
}
