using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One line of the list on the recovery form: what one failure sets off, and after how long.
///
/// <b>The delay is text, read by the command line's own rule</b> (<see cref="RecoveryCommandWords.ReadDelay"/>),
/// for the reason the box of seconds on the plan sheet holds text: a box bound to a number lets WPF read it
/// in silence, and a word typed there would be a list written with the old delay under a box saying
/// something else. So a delay <c>bws recovery</c> takes is one this takes, in the same words.
///
/// <b>An item that does nothing keeps the delay it had, unseen.</b> Windows keeps a delay beside "take no
/// action" too, and a list read back has to be written back to the millisecond for the way back to be one -
/// but a box beside "take no action" would ask for a number that decides nothing.
/// </summary>
public sealed class RecoveryFormItem : Checked
{
    private readonly System.Action _edited;
    private readonly TimeSpan _quiet;
    private RecoveryAction _kind;
    private string _delayText;
    private string _label = string.Empty;

    internal RecoveryFormItem(RecoveryItem item, System.Action edited)
    {
        _edited = edited;
        _kind = item.Action;
        _quiet = item.Delay;
        _delayText = RecoveryCommandWords.DelayWord(item.Delay);
    }

    /// <summary>What this failure sets off. Choosing another kind is an edit of the list.</summary>
    public RecoveryAction Kind
    {
        get => _kind;
        set
        {
            if (Set(ref _kind, value))
            {
                Raise(nameof(KindText));
                Raise(nameof(HasDelay));
                RaiseErrors(nameof(DelayText));
                _edited();
            }
        }
    }

    /// <summary>The kind in words, on the button that opens the menu of kinds - every key inside its own call.</summary>
    public string KindText => Kind switch
    {
        RecoveryAction.Nothing => Texts.Of("gui.recovery.form.kind.nothing"),
        RecoveryAction.RestartService => Texts.Of("gui.recovery.form.kind.restart"),
        RecoveryAction.RunProgram => Texts.Of("gui.recovery.form.kind.program"),
        RecoveryAction.RestartComputer => Texts.Of("gui.recovery.form.kind.reboot"),
        _ => Texts.Of("gui.recovery.form.kind.unnamed")
    };

    /// <summary>Whether the delay box is shown - every kind but "take no action".</summary>
    public bool HasDelay => Kind != RecoveryAction.Nothing;

    /// <summary>The delay exactly as typed.</summary>
    public string DelayText
    {
        get => _delayText;
        set
        {
            if (Set(ref _delayText, value ?? string.Empty))
            {
                RaiseErrors(nameof(DelayText));
                _edited();
            }
        }
    }

    /// <summary>"Failure 1", "Failure 3 and later" - set by the form, because it depends on where the line stands.</summary>
    public string Label
    {
        get => _label;
        internal set => Set(ref _label, value);
    }

    /// <summary>What is wrong with the delay, or nothing.</summary>
    public string DelayProblem => HasDelay && RecoveryCommandWords.ReadDelay(_delayText.Trim()) is null
        ? Texts.Of("gui.recovery.form.delay.problem")
        : string.Empty;

    public override bool HasErrors => DelayProblem.Length > 0;

    protected override IEnumerable<string> ErrorsOf(string property) =>
        property == nameof(DelayText) && HasErrors ? [DelayProblem] : [];

    /// <summary>The item as Windows keeps it, or nothing while the delay does not read.</summary>
    internal RecoveryItem? Item => HasErrors
        ? null
        : new RecoveryItem(Kind, HasDelay ? RecoveryCommandWords.ReadDelay(_delayText.Trim())!.Value : _quiet);
}
