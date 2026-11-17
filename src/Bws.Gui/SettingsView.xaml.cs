using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Bws.Gui;

/// <summary>
/// The settings page. Its buttons are reached by SettingsDoor through these properties rather than wired
/// here, the arrangement ScopeBar uses - what pressing them does needs the preferences file and the
/// window's own restart, which this control does not hold.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    internal Button Restart => RestartButton;

    internal Button ForgetRecent => ForgetButton;

    internal Button OpenFolder => OpenFolderButton;

    /// <summary>
    /// Puts the keyboard on the marked language, or on the first one when none is marked - the first thing
    /// on the page somebody can change. Called after layout, when the page has just come up.
    /// </summary>
    internal void TakeTheKeyboard()
    {
        RadioButton? first = null;

        foreach (var option in RadioButtonsIn(LanguageList))
        {
            first ??= option;

            if (option.IsChecked == true)
            {
                Keyboard.Focus(option);
                return;
            }
        }

        if (first is not null)
        {
            Keyboard.Focus(first);
        }
    }

    private static IEnumerable<RadioButton> RadioButtonsIn(System.Windows.DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is RadioButton option)
            {
                yield return option;
            }

            foreach (var inner in RadioButtonsIn(child))
            {
                yield return inner;
            }
        }
    }
}
