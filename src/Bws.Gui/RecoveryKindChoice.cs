using System.Windows;
using System.Windows.Controls;
using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// One kind of failure action as the recovery form's menu offers it - the shape of <see cref="StartSettingChoice"/>,
/// so the menu wears the row menu's style (Label and Gesture are what it binds) rather than a copy of it.
///
/// <b>Four kinds and not five.</b> The fifth, a kind Windows does not document, can be read but never
/// chosen: nothing could be said about what it does.
/// </summary>
public sealed class RecoveryKindChoice
{
    private static readonly RecoveryAction[] Offered =
        [RecoveryAction.Nothing, RecoveryAction.RestartService, RecoveryAction.RunProgram, RecoveryAction.RestartComputer];

    internal RecoveryKindChoice(RecoveryAction kind) => Kind = kind;

    internal RecoveryAction Kind { get; }

    /// <summary>What the item says - the words the form's button says once it is chosen.</summary>
    public string Label => Kind switch
    {
        RecoveryAction.Nothing => Texts.Of("gui.recovery.form.kind.nothing"),
        RecoveryAction.RestartService => Texts.Of("gui.recovery.form.kind.restart"),
        RecoveryAction.RunProgram => Texts.Of("gui.recovery.form.kind.program"),
        _ => Texts.Of("gui.recovery.form.kind.reboot")
    };

    /// <summary>No key does this, so nothing is written beside the label.</summary>
    public string Gesture => string.Empty;

    /// <summary>
    /// Fills the menu under one line's button and opens it - built on each press, because the line it acts
    /// on is the one pressed, and a menu kept between presses would remember the last line.
    /// </summary>
    internal static void Offer(Button button, Style style, RecoveryFormItem line)
    {
        if (button.ContextMenu is not { } menu)
        {
            return;
        }

        menu.Items.Clear();

        foreach (var kind in Offered)
        {
            var item = new MenuItem { DataContext = new RecoveryKindChoice(kind), Style = style };

            item.Click += (_, _) => line.Kind = kind;
            menu.Items.Add(item);
        }

        ButtonMenu.OpenUnder(button);
    }
}
