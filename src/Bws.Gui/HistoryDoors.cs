using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The window's half of the change history (J2, 2026-10-20): "Change history" under "Audit..." opens it,
/// "Back to the list" puts it away, and the middle of the window is rearranged whenever it comes or goes.
///
/// <b>A type of its own rather than more lines in SnapshotDoors</b>, whose name would stop being true,
/// or in MainWindow, which stands on its ceiling of methods. No dialog here, unlike the snapshots' doors:
/// the log needs no file chosen, so everything past the press lives in <see cref="HistoryReading"/>, where
/// a test reaches it without a window.
/// </summary>
internal static class HistoryDoors
{
    /// <param name="arrange">
    /// What the window does to give the middle to whatever should have it now - the window owns that cell,
    /// so it is asked rather than reached into. The same delegate the snapshots' doors are handed.
    /// </param>
    internal static void Open(ActionBar actions, HistoryView view, MainViewModel model, Action arrange)
    {
        actions.HistoryRequest += async (_, _) => await model.OpenHistory().ConfigureAwait(true);
        view.Finished += (_, _) => model.History.PutAway();

        model.History.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(HistoryReading.Shown))
            {
                arrange();
            }
        };
    }
}
