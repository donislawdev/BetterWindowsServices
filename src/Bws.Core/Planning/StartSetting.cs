namespace Bws.Core.Planning;

/// <summary>
/// What this tool can WRITE as an entry's startup setting - four values, each of them exactly one
/// pair of start type and late start flag.
///
/// <b>A TYPE OF ITS OWN ON THE WRITING SIDE, AND THE READING SIDE KEEPS <see cref="StartType"/>
/// UNTOUCHED.</b> The manager keeps the late start as a flag beside the type rather than as a sixth
/// type (`docs/07`, measured 2026-08-01: BITS and Spooler both answer AUTO_START), so a reading has
/// to be able to say "automatic, and the flag could not be read" - which one enumeration cannot. A
/// write has the opposite need: it has to say exactly one thing, and a start type with a separate
/// boolean beside it is a pair that can say "manual, and late", which means nothing. Four values
/// cannot be inconsistent. Backlog 231, route (c), the owner's decision of 2026-08-26.
///
/// <b>EVERY VALUE WRITES BOTH HALVES, THE WAY sc.exe DOES, and that is the owner's decision of
/// 2026-09-24.</b> Measured on the throwaway machine that day (tools/scm-probe/delayed-write-probe.ps1):
/// sc.exe clears the flag on every start type it writes, manual and disabled included. Writing the
/// whole pair is what makes a way back an exact inverse - "manual, then delayed, then manual again"
/// ends where it began instead of carrying a flag nobody can see in the table and a snapshot can.
///
/// <b>Honest about what it cannot say.</b> Boot and System belong to drivers, which this tool does
/// not operate on, and an entry carrying the flag while not automatic - nine on the owner's machine,
/// WinRM and BITS among them - is a state none of these four produces. <see cref="StartSettings.Of"/>
/// answers nothing for those rather than the nearest value.
/// </summary>
public enum StartSetting
{
    /// <summary>Starts at boot, with everything else. The flag is written false.</summary>
    Automatic,

    /// <summary>Starts at boot, after the other automatic entries and a short delay.</summary>
    AutomaticDelayed,

    /// <summary>Starts when something asks for it. The flag is written false.</summary>
    Manual,

    /// <summary>Does not start at all. The flag is written false.</summary>
    Disabled
}

/// <summary>
/// The one place a startup setting turns into the pair the manager keeps, and back.
///
/// <b>ONE TABLE READ IN BOTH DIRECTIONS, because the way back rests on it.</b> The writer asks
/// <see cref="Written"/> what to put on the machine, and the plan asks <see cref="Of"/> what an
/// entry has now - for the way back, and for "already there". If those two ever disagreed, a way
/// back would name a setting that does not put the entry back, and that is the most dangerous
/// sentence this tool prints. Backlog 232 made that agreement the condition for offering one.
/// </summary>
public static class StartSettings
{
    /// <summary>What a setting puts on the machine: the start type, and whether it starts late.</summary>
    public static (StartType Type, bool Delayed) Written(StartSetting setting) => setting switch
    {
        StartSetting.Automatic => (StartType.Automatic, false),
        StartSetting.AutomaticDelayed => (StartType.Automatic, true),
        StartSetting.Manual => (StartType.Manual, false),
        StartSetting.Disabled => (StartType.Disabled, false),

        // A value nobody taught this table is refused rather than written as something close to it.
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, NoSuchSetting)
    };

    /// <summary>
    /// The setting an entry has now, or nothing where no setting this tool writes would say it.
    ///
    /// <b>Both halves have to be KNOWN.</b> A denied flag beside an automatic type is not
    /// "automatic" - it is automatic and nobody knows whether late, which is the case the way back
    /// refused from its first day. Nothing is not a guess about which of the two it was. An ABSENT
    /// flag is known: it is the answer "there is no such flag on this entry", which is not late.
    /// </summary>
    public static StartSetting? Of(ScmEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!entry.StartType.IsPresent
            || entry.DelayedAuto.Outcome is not (ReadOutcome.Present or ReadOutcome.Absent))
        {
            return null;
        }

        return (entry.StartType.Value, entry.DelayedAuto.IsPresent && entry.DelayedAuto.Value) switch
        {
            (StartType.Automatic, false) => StartSetting.Automatic,
            (StartType.Automatic, true) => StartSetting.AutomaticDelayed,
            (StartType.Manual, false) => StartSetting.Manual,
            (StartType.Disabled, false) => StartSetting.Disabled,
            _ => null
        };
    }

    /// <summary>Whether a setting starts the entry at boot - the two values that do.</summary>
    public static bool StartsAtBoot(StartSetting setting) =>
        setting is StartSetting.Automatic or StartSetting.AutomaticDelayed;

    internal const string NoSuchSetting = "There is no such startup setting to write.";
}
