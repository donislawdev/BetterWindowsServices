using System.Collections.ObjectModel;
using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The form at the top of a recovery sheet - the Recovery tab of services.msc, for one entry or for many
/// (backlog 555 part a2, the owner's decision of 2026-11-24: a form on the plan sheet, and every part nobody
/// touched left as it is on each entry).
///
/// <b>Five parts, the core's own (<see cref="RecoveryParts"/>), each with a tick saying it will be
/// written.</b> Editing a part ticks it, and taking the tick off puts the part back to what the entries hold.
/// So the plan under the form writes exactly the ticked parts, and the tick is what lets "leave it" and
/// "remove it" be two answers: an empty program box removes the program when ticked and means nothing when
/// not. Over many entries that is the only way to say either - their programs can all differ.
///
/// <b>The form decides no rule of its own.</b> Delays and periods are read by the command line's words
/// (<see cref="RecoveryCommandWords"/>), and whether the result may be written - an unnamed kind, a period
/// with no list, the computer restarting - is decided by the plan the window builds from <see cref="Change"/>,
/// which says it under the form. A form that refused things itself would be a second rule beside the
/// plan's, and the two would part.
/// </summary>
public sealed partial class RecoveryForm : Checked
{
    private readonly RecoveryInCommon _kept;
    private bool _changesList;
    private bool _changesPeriod;
    private bool _changesProgram;
    private bool _changesMessage;
    private bool _changesFlag;
    private bool _flag;
    private string _periodText = string.Empty;
    private string _programText = string.Empty;
    private string _messageText = string.Empty;
    private bool _filling;

    private RecoveryForm(RecoveryInCommon kept, int entries)
    {
        _kept = kept;
        Entries = entries;
        Items = [];
        PutBack(RecoveryParts.Actions | RecoveryParts.ResetPeriod | RecoveryParts.Command | RecoveryParts.RebootMessage | RecoveryParts.OnNonCrashFailures);
        Label();
    }

    /// <summary>
    /// A form over these entries' recoveries as the listing read them: each part shows what every entry
    /// agrees on, and is empty where they differ or one could not be read. Nothing set is an empty list.
    /// </summary>
    internal static RecoveryForm Of(IReadOnlyList<Reading<RecoverySettings>> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var known = readings
            .Select(reading => reading.Outcome switch
            {
                ReadOutcome.Present => reading.Value,
                ReadOutcome.Absent => RecoveryChange.Nothing,
                _ => null
            })
            .ToArray();

        return new RecoveryForm(RecoveryInCommon.Over(known), readings.Count);
    }

    /// <summary>Said whenever a part changes - the window works out the plan again from <see cref="Change"/>.</summary>
    internal event EventHandler? Edited;

    /// <summary>How many entries the form writes to.</summary>
    public int Entries { get; }

    /// <summary>The list, one line per failure.</summary>
    public ObservableCollection<RecoveryFormItem> Items { get; }

    /// <summary>
    /// What the ticked parts write, or nothing while no part is ticked or something typed does not read -
    /// and then there is no plan to show, which the sheet says.
    /// </summary>
    public RecoveryChange? Change
    {
        get
        {
            var parts = Ticked();

            if (parts == RecoveryParts.None || HasProblem)
            {
                return null;
            }

            var list = new RecoveryList(
                ChangesList ? [.. Items.Select(item => item.Item!.Value)] : [],
                ChangesPeriod ? RecoveryCommandWords.ReadPeriod(_periodText.Trim())!.Value : TimeSpan.Zero)
            {
                Command = ChangesProgram && _programText.Length > 0 ? _programText : null,
                RebootMessage = ChangesMessage && _messageText.Length > 0 ? _messageText : null
            };

            return new RecoveryChange(parts, new RecoverySettings(list, ChangesFlag && _flag));
        }
    }

    /// <summary>Adds a failure under the last, a restart after a minute - what services.msc offers first.</summary>
    internal void Add()
    {
        Items.Add(Line(new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1))));
        Touched(RecoveryParts.Actions);
    }

    internal void Remove(RecoveryFormItem item)
    {
        if (Items.Remove(item))
        {
            Touched(RecoveryParts.Actions);
        }
    }

    private RecoveryParts Ticked() =>
        (ChangesList ? RecoveryParts.Actions : RecoveryParts.None)
        | (ChangesPeriod ? RecoveryParts.ResetPeriod : RecoveryParts.None)
        | (ChangesProgram ? RecoveryParts.Command : RecoveryParts.None)
        | (ChangesMessage ? RecoveryParts.RebootMessage : RecoveryParts.None)
        | (ChangesFlag ? RecoveryParts.OnNonCrashFailures : RecoveryParts.None);

    private RecoveryFormItem Line(RecoveryItem item) => new(item, () => Touched(RecoveryParts.Actions));

    /// <summary>A tick set by hand: on means "write this part as it stands", off puts the part back.</summary>
    private void Tick(ref bool field, bool value, RecoveryParts part)
    {
        if (!Set(ref field, value, Name(part)))
        {
            return;
        }

        if (!value)
        {
            PutBack(part);
        }

        Said();
    }

    private void Type(ref string field, string? value, RecoveryParts part, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (Set(ref field, value ?? string.Empty, property))
        {
            Touched(part);
        }
    }

    /// <summary>An edit of a part: it is ticked from now on, and the plan is worked out again.</summary>
    private void Touched(RecoveryParts part)
    {
        if (_filling)
        {
            return;
        }

        _ = part switch
        {
            RecoveryParts.Actions => Set(ref _changesList, true, nameof(ChangesList)),
            RecoveryParts.ResetPeriod => Set(ref _changesPeriod, true, nameof(ChangesPeriod)),
            RecoveryParts.Command => Set(ref _changesProgram, true, nameof(ChangesProgram)),
            RecoveryParts.RebootMessage => Set(ref _changesMessage, true, nameof(ChangesMessage)),
            _ => Set(ref _changesFlag, true, nameof(ChangesFlag))
        };

        Said();
    }

    /// <summary>Puts parts back to what the entries hold - empty where they differ - without ticking them.</summary>
    private void PutBack(RecoveryParts parts)
    {
        _filling = true;

        if (parts.HasFlag(RecoveryParts.Actions))
        {
            Items.Clear();

            foreach (var item in _kept.Items ?? [])
            {
                Items.Add(Line(item));
            }
        }

        if (parts.HasFlag(RecoveryParts.ResetPeriod))
        {
            PeriodText = _kept.Period is { } period ? RecoveryCommandWords.PeriodWord(period) : string.Empty;
        }

        if (parts.HasFlag(RecoveryParts.Command))
        {
            ProgramText = _kept.Command.Value ?? string.Empty;
        }

        if (parts.HasFlag(RecoveryParts.RebootMessage))
        {
            MessageText = _kept.Message.Value ?? string.Empty;
        }

        if (parts.HasFlag(RecoveryParts.OnNonCrashFailures))
        {
            Flag = _kept.Flag ?? false;
        }

        _filling = false;
    }

    /// <summary>
    /// Names each line by where it stands: Windows runs item N on failure N and repeats the last, so the last
    /// line is "and later" and a list of one is every failure.
    /// </summary>
    private void Label()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].Label = Items.Count == 1 ? Texts.Of("gui.recovery.form.item.every")
                : index == Items.Count - 1 ? Texts.Of("gui.recovery.form.item.last", index + 1)
                : Texts.Of("gui.recovery.form.item", index + 1);
        }
    }

    /// <summary>Everything worked out from the parts, and the window told.</summary>
    private void Said()
    {
        Label();
        Raise(nameof(ListProblem));
        Raise(nameof(PeriodProblem));
        Raise(nameof(HasProblem));
        Raise(nameof(ListNote));
        Raise(nameof(PeriodNote));
        Raise(nameof(ProgramNote));
        Raise(nameof(MessageNote));
        Raise(nameof(FlagNote));
        RaiseErrors(nameof(PeriodText));
        Raise(nameof(Change));
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private static string Name(RecoveryParts part) => part switch
    {
        RecoveryParts.Actions => nameof(ChangesList),
        RecoveryParts.ResetPeriod => nameof(ChangesPeriod),
        RecoveryParts.Command => nameof(ChangesProgram),
        RecoveryParts.RebootMessage => nameof(ChangesMessage),
        _ => nameof(ChangesFlag)
    };
}
