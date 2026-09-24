using System.Windows;
using System.Windows.Controls;
using Bws.Core.Planning;

namespace Bws.Gui;

/// <summary>
/// One startup setting as a menu offers it: what it writes, what it is called on screen, and what
/// it means.
///
/// <b>Label and Gesture are the two names RowMenuItem binds</b>, so this wears the row menu's style
/// rather than a copy of it - GUI rule 2, the variant made by the data rather than by a second file.
/// A setting has no key of its own, so Gesture is empty, which the template draws as nothing.
///
/// <b>Its own file since 2026-09-24, when a second menu started offering the same four</b> -
/// UX-GUI-018. The menu on a row now has the startup type the action bar has, and both are filled
/// by <see cref="Offer"/>, so the two cannot offer different settings or say different things
/// about one.
/// </summary>
public sealed class StartSettingChoice
{
    internal StartSettingChoice(StartSetting setting) => Setting = setting;

    internal StartSetting Setting { get; }

    /// <summary>What the item says - the same letters the cell of such an entry says.</summary>
    public string Label => ViewModels.CellFaces.SettingLabel(Setting);

    /// <summary>No key does this, so nothing is written beside the label.</summary>
    public string Gesture => string.Empty;

    /// <summary>
    /// What the setting means, on the item - UX-GUI-018 found these the only items in the window's
    /// menus that change a machine and said nothing about what they change it to.
    ///
    /// <b>Three of the four are the sentences the Startup type column already puts beside its
    /// marks</b>, so a setting is explained in one place whichever way somebody meets it. The
    /// delayed one had no sentence, because its cell wears the automatic mark - its own is worded
    /// from Microsoft's description of the flag, "started after other auto-start services are
    /// started plus a short delay" (SERVICE_DELAYED_AUTO_START_INFO on learn.microsoft.com).
    ///
    /// Four calls with the key written in each, for the reason <see cref="ViewModels.Exporting.FileName"/>
    /// gives: TextKeyGuards sees a key only where it is written.
    /// </summary>
    public string Hint => Setting switch
    {
        StartSetting.Automatic => Texts.Of("gui.start.mark.automatic"),
        StartSetting.AutomaticDelayed => Texts.Of("gui.start.mark.delayed"),
        StartSetting.Manual => Texts.Of("gui.start.mark.manual"),
        _ => Texts.Of("gui.start.mark.disabled")
    };

    /// <summary>
    /// Puts the four settings under a menu, in the order the command line offers the words.
    ///
    /// <b>Items added one by one rather than handed over as ItemsSource</b>, which is RowMenu's
    /// arrangement and the one the guards can see: <c>Items.OfType&lt;MenuItem&gt;()</c> answers
    /// with the real items whether or not the menu has ever opened (`docs/10` trap 19), and each
    /// item is a real MenuItem with its own automation peer (trap 7).
    ///
    /// <b>The tooltip is set here rather than bound in the shared style</b>, because the style is
    /// worn by every item of three menus and these four are the only ones with something to say
    /// on hover - a binding on the style would look for a property the others do not have.
    /// </summary>
    internal static void Offer(ItemsControl menu, Style style, Func<StartSetting, Task> chosen)
    {
        ArgumentNullException.ThrowIfNull(menu);

        foreach (var setting in Enum.GetValues<StartSetting>())
        {
            var choice = new StartSettingChoice(setting);
            var item = new MenuItem { DataContext = choice, Style = style, ToolTip = choice.Hint };

            // What choosing did is on the screen as a plan, or said on the status line by the
            // window - a menu item has nowhere to say it, which RowMenu.Build says about its own.
            item.Click += async (_, _) => await chosen(setting).ConfigureAwait(true);

            menu.Items.Add(item);
        }
    }
}
