using System.Windows;
using System.Windows.Media;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The layers of the window - which part of MainWindow.xaml's grid is drawn over which, asked of a
/// click on a shown window. Values.xaml names the layers and says why there are two.
///
/// <b>Its own file because the question is the window's, not a panel's.</b> The plan panel and the
/// search row each have a file of guards, and neither can see the other - the fault this file
/// exists for stood between them.
/// </summary>
public sealed class WindowLayerGuards
{
    /// <summary>
    /// THE PLAN LIES OVER THE WHOLE WINDOW, THE SEARCH FIELD INCLUDED - review of PR 21.
    ///
    /// <b>The search row went up a layer on 2026-09-25</b> so the panel of messages under the field
    /// lies over the filters, and a layer outranks the order siblings are declared in. The plan,
    /// last in MainWindow.xaml so it would cover everything, was drawn under the search field, and
    /// the field took clicks and keys through the dimming while a plan waited to be carried out.
    ///
    /// Asked with the framework's own hit test at the centre of the search field on a shown
    /// window, the way a click asks it. Hit testing walks the layers in the order painting does, so
    /// the one answer covers what is seen as well as what is clicked.
    /// </summary>
    [Fact]
    public async Task The_plan_lies_over_the_whole_window_the_search_field_included()
    {
        var window = await WithOnePicked();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var (hit, onThePlan) = WpfHost.On(() =>
        {
            // SHOWN, off screen, the way PlanViewGuards shows the panel it reads colours from: hit
            // testing needs a laid out window, and a window built here is never laid out otherwise.
            window.Width = 1100;
            window.Height = 700;
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;
            window.Show();
            window.UpdateLayout();

            var field = window.Search.SearchField;
            var centre = field.TranslatePoint(new Point(field.ActualWidth / 2, field.ActualHeight / 2), window);
            var found = window.InputHitTest(centre) as DependencyObject;

            return (found?.GetType().Name ?? "nothing", IsInside(found, window.PlanPanel));
        });

        Assert.True(
            onThePlan,
            $"a click at the centre of the search field lands on {hit} while a plan is open, not on "
            + "the plan's dimming - the search row is drawn over the plan and can be typed into behind it");

        WpfHost.On(window.Close);
    }

    private static bool IsInside(DependencyObject? hit, DependencyObject target)
    {
        for (var at = hit; at is not null; at = at is Visual ? VisualTreeHelper.GetParent(at) : LogicalTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A window with one entry picked, so there is a plan to open. Read before the model reaches
    /// the window, the order PlanViewGuards keeps and for its reason.
    /// </summary>
    private static async Task<MainWindow> WithOnePicked()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler", "Print Spooler")), new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        WpfHost.On(() =>
        {
            window.Entries.ItemsSource = model.Rows;
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Spooler");
        });

        WpfHost.Settled();

        return window;
    }
}
