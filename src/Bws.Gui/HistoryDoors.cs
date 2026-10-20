using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The window's half of the change history (J2, 2026-10-20): "Change history" under "Audit..." opens it,
/// "Back to the list" puts it away, "Export..." writes it to a file (AS-c, the same day), and the middle of
/// the window is rearranged whenever it comes or goes.
///
/// <b>A type of its own rather than more lines in SnapshotDoors</b>, whose name would stop being true,
/// or in MainWindow, which stands on its ceiling of methods. One dialog here, the export's save dialog,
/// and everything past it lives in <see cref="HistoryReading"/>, where a test reaches it without a window.
/// </summary>
internal static class HistoryDoors
{
    /// <param name="owner">The window the save dialog belongs to.</param>
    /// <param name="arrange">
    /// What the window does to give the middle to whatever should have it now - the window owns that cell,
    /// so it is asked rather than reached into. The same delegate the snapshots' doors are handed.
    /// </param>
    internal static void Open(Window owner, ActionBar actions, HistoryView view, MainViewModel model, Action arrange)
    {
        actions.HistoryRequest += async (_, _) => await model.OpenHistory().ConfigureAwait(true);
        view.Finished += (_, _) => model.History.PutAway();
        view.ExportRequest += async (_, _) => await Export(owner, view, model).ConfigureAwait(true);

        model.History.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(HistoryReading.Shown))
            {
                arrange();
            }
        };
    }

    /// <summary>
    /// Asks where the history on screen should go and writes it there, its sentences first and the lines
    /// as the table shows them - the comparison's door (SnapshotDoors.Export), for the same reason.
    /// </summary>
    private static Task Export(Window owner, HistoryView view, MainViewModel model)
    {
        var dialog = FileDialogs.Save(HistoryExporting.FileName(), ".csv", Texts.Of("gui.export.kind"));

        return dialog.ShowDialog(owner) == true
            ? model.History.ExportAsync(dialog.FileName, view.InTheOrderOnScreen())
            : Task.CompletedTask;
    }
}
