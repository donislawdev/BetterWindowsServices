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
    /// How a press hands a page to a browser: <see cref="ExternalLinks.OpenAsync"/>, unless somebody
    /// handed this window another way.
    ///
    /// <b>PRODUCTION CODE THAT EXISTS FOR A TEST, the trade <see cref="CarriedOutBy"/> was taken on -
    /// backlog 568, owner's decision of 2026-10-14.</b> A test that pressed Donate, GitHub or the
    /// help menu's item would open a browser on whoever runs the suite, so until that day nothing
    /// checked which page a press reaches, and a wiring pointed at the wrong page or at none stayed
    /// green. This replacement hands back what it was asked for and opens nothing.
    ///
    /// <b>It takes a name, never an address</b>, which is what keeps the safety rule of
    /// ExternalLinks intact: the addresses stay constants in that file, and the most a replacement
    /// can do is fail to open anything. <b>Init rather than settable</b>, as CarriedOutBy is.
    /// </summary>
    internal Func<ExternalLinks.Destination, Task<string?>> PagesOpenedBy { get; init; } = ExternalLinks.OpenAsync;

    /// <summary>
    /// Hands one of the project's pages to a browser through <see cref="ExternalLinks"/>, and says
    /// in the status line why it did not when it did not.
    ///
    /// <b>THE ONE ROAD FOR ALL THREE PRESSES since 2026-10-14</b> - Donate had a method of its own
    /// until then, and two roads to one answer only have to disagree once.
    ///
    /// <b>The window stays as it is either way</b> - nothing about the list, the query or a plan
    /// depends on a browser opening, so a failure is a sentence rather than an interruption. It
    /// keeps answering while the shell is asked, too - ShellHandover says why that is not free.
    /// </summary>
    internal async Task OpenPage(ExternalLinks.Destination destination)
    {
        if (await PagesOpenedBy(destination).ConfigureAwait(true) is { } trouble)
        {
            _model.Says.CouldNotDo(trouble);
        }
    }
}
