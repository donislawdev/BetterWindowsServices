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
    /// Puts the keyboard on <see cref="Landing"/> - the first thing on the page somebody can change. Called
    /// after layout, when the page has just come up.
    /// </summary>
    internal void TakeTheKeyboard()
    {
        if (Landing() is { } option)
        {
            _ = Keyboard.Focus(option);
        }
    }

    /// <summary>
    /// The marked language, or the first one when none is marked, or nothing before the list is drawn. Apart
    /// from the move itself so that a test can ask it: whether keyboard focus lands depends on which window
    /// Windows lets have it, and a suite cannot count on being that window.
    /// </summary>
    internal RadioButton? Landing()
    {
        var options = RadioButtonsIn(LanguageList).ToList();

        return options.Find(option => option.IsChecked == true) ?? options.FirstOrDefault();
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
