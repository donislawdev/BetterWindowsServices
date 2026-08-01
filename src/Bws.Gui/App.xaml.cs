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
    }
}
