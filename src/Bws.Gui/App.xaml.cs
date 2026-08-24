using System.Windows;

namespace Bws.Gui;

/// <summary>
/// The application, and the one place the chosen language reaches the markup.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Texts.PutInto(Resources);

        // One class handler for every cell tooltip in the list, registered once. CellTips carries
        // the argument, including why no markup names it and why it therefore has to be told to.
        CellTips.Arm();
    }
}
