using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The parts of the recovery form as the view binds them - the five ticks, the three boxes of text and the two
/// options of the flag - and what the form says under them: what a tick means, a part the entries do not agree on,
/// and what was typed that does not read. Apart from how an edit becomes a change (RecoveryForm.cs), which would
/// otherwise stand past the share of the length ceiling the size guard counts as near.
/// </summary>
public sealed partial class RecoveryForm
{
    public bool ChangesList
    {
        get => _changesList;
        set => Tick(ref _changesList, value, RecoveryParts.Actions);
    }

    public bool ChangesPeriod
    {
        get => _changesPeriod;
        set => Tick(ref _changesPeriod, value, RecoveryParts.ResetPeriod);
    }

    public bool ChangesProgram
    {
        get => _changesProgram;
        set => Tick(ref _changesProgram, value, RecoveryParts.Command);
    }

    public bool ChangesMessage
    {
        get => _changesMessage;
        set => Tick(ref _changesMessage, value, RecoveryParts.RebootMessage);
    }

    public bool ChangesFlag
    {
        get => _changesFlag;
        set => Tick(ref _changesFlag, value, RecoveryParts.OnNonCrashFailures);
    }

    /// <summary>The reset period exactly as typed - <c>1d</c>, <c>30m</c>, <c>never</c>.</summary>
    public string PeriodText
    {
        get => _periodText;
        set => Type(ref _periodText, value, RecoveryParts.ResetPeriod);
    }

    /// <summary>The program's whole command line, as Windows keeps it. Empty and ticked removes it.</summary>
    public string ProgramText
    {
        get => _programText;
        set => Type(ref _programText, value, RecoveryParts.Command);
    }

    /// <summary>The message before the computer restarts. Empty and ticked removes it.</summary>
    public string MessageText
    {
        get => _messageText;
        set => Type(ref _messageText, value, RecoveryParts.RebootMessage);
    }

    /// <summary>
    /// Whether the list also answers a stop with an error - the second of two options, the owner's decision
    /// of 2026-11-25 (P4): one tick for "change" and a second tick for the value read as the same thing.
    /// </summary>
    public bool Flag
    {
        get => _flag;
        set
        {
            if (Set(ref _flag, value))
            {
                Raise(nameof(CrashesOnly));
                Touched(RecoveryParts.OnNonCrashFailures);
            }
        }
    }

    /// <summary>The first of the two options, the other side of <see cref="Flag"/> - each option binds its own.</summary>
    public bool CrashesOnly
    {
        get => !_flag;
        set => Flag = !value;
    }

    /// <summary>What a tick means, said once above the parts - on one entry or on each of many.</summary>
    public string Intro => Entries == 1
        ? Texts.Of("gui.recovery.form.kept.one")
        : Texts.Of("gui.recovery.form.kept.many", Entries);

    /// <summary>Under a part the entries do not agree on, or one could not be read, while it has no tick.</summary>
    public string ListNote => Note(ChangesList, _kept.Items is not null);

    /// <inheritdoc cref="ListNote"/>
    public string PeriodNote => Note(ChangesPeriod, _kept.Period is not null);

    /// <inheritdoc cref="ListNote"/>
    public string ProgramNote => Note(ChangesProgram, _kept.Agreed(RecoveryParts.Command));

    /// <inheritdoc cref="ListNote"/>
    public string MessageNote => Note(ChangesMessage, _kept.Agreed(RecoveryParts.RebootMessage));

    /// <inheritdoc cref="ListNote"/>
    public string FlagNote => Note(ChangesFlag, _kept.Flag is not null);

    /// <summary>The first delay of the list that does not read, as a sentence under the list - or nothing.</summary>
    public string ListProblem => Items.FirstOrDefault(item => item.HasErrors)?.DelayProblem ?? string.Empty;

    /// <summary>What is wrong with the period, as a sentence under its box - or nothing.</summary>
    public string PeriodProblem => ChangesPeriod && RecoveryCommandWords.ReadPeriod(_periodText.Trim()) is null
        ? Texts.Of("gui.recovery.form.period.problem")
        : string.Empty;

    /// <summary>Whether anything typed does not read - then there is no plan, and the sheet says so.</summary>
    public bool HasProblem => ListProblem.Length > 0 || PeriodProblem.Length > 0;

    private string Note(bool ticked, bool known) =>
        ticked || known ? string.Empty
        : Entries == 1 ? Texts.Of("gui.recovery.form.unread")
        : _kept.Unread ? Texts.Of("gui.recovery.form.unread.some")
        : Texts.Of("gui.recovery.form.differs");

    public override bool HasErrors => PeriodProblem.Length > 0;

    protected override IEnumerable<string> ErrorsOf(string property) =>
        property == nameof(PeriodText) && HasErrors ? [PeriodProblem] : [];
}
