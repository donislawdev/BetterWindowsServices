using System.Windows;

namespace Bws.Gui;

/// <summary>
/// The window's half of the Help button: the menu under it, F1, and opening the page the menu
/// offers - UX-GUI-014, owner's decision 2026-09-24.
///
/// <b>A partial of its own because the constructor is at the analyser's length</b>, the reason
/// MainWindow.Overview.cs gives for the same shape: one line there, and the wiring here. What the
/// menu HOLDS is <see cref="HelpMenu"/>, which knows nothing about where the menu hangs.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Hangs the help menu under its button. Called once, from the constructor.
    ///
    /// <b>Built with the row menu's builder and the row menu's item style</b>, and HelpMenu says why
    /// the entry is the row's.
    /// </summary>
    private void IntroduceTheHelp()
    {
        Scope.Help.ContextMenu = RowMenu.Build(HelpMenu.GroupsFor(this), (Style)FindResource("RowMenuItem"));
        Scope.Help.Click += (_, _) => OpenHelp();
    }

    /// <summary>
    /// Opens the help menu under its button - the button's press, and F1. Answers whether it opened,
    /// the way <see cref="OpenColumns"/> does, so a key press that found no menu is not swallowed.
    /// </summary>
    internal bool OpenHelp() => ButtonMenu.OpenUnder(Scope.Help);

    /// <summary>
    /// Hands one of the project's pages to a browser through <see cref="ExternalLinks"/>, and says
    /// in the status line why it did not when it did not - the same shape as the Donate button.
    /// </summary>
    internal async Task OpenPage(Func<Task<string?>> open)
    {
        ArgumentNullException.ThrowIfNull(open);

        if (await open().ConfigureAwait(true) is { } trouble)
        {
            _model.Says.CouldNotDo(trouble);
        }
    }
}
