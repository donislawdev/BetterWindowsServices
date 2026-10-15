using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The window's half of snapshots: the dialogs that ask which file, and what the window shows when a
/// reading starts, a comparison arrives or the form before a snapshot opens - `D1` and `D2`, since
/// 2026-10-14, two files, the export and the note since 2026-10-15 (W2).
///
/// <b>A type of its own rather than a partial of the window, and the shape guard is what decided
/// it:</b> MainWindow stood exactly on its ceiling of methods, and another partial file is not a
/// smaller type. The seam is real, too - nothing here needs the grid, the columns or the plan, only
/// the window as the owner of a dialog and one way to rearrange the middle of it.
///
/// <b>The dialogs are the one part nothing here can drive from a test</b> - measured 2026-08-25 for
/// the export: a file dialog opened from a session in the background never appears. So everything on
/// the other side of a dialog lives in <see cref="Snapshotting"/>, which takes a path and can be
/// checked without a window, and what is left here is asking and wiring.
/// </summary>
internal sealed class SnapshotDoors
{
    private readonly Window _owner;
    private readonly MainViewModel _model;
    private readonly Middle _middle;
    private readonly Action _arrange;

    private SnapshotDoors(Window owner, MainViewModel model, Middle middle, Action arrange)
    {
        _owner = owner;
        _model = model;
        _middle = middle;
        _arrange = arrange;
    }

    /// <summary>
    /// Wires the bar's three snapshot items, the form's and the comparison's buttons, and what the
    /// window shows when a reading starts or a view comes and goes.
    /// </summary>
    /// <param name="middle">The two views snapshots put where the list was.</param>
    /// <param name="arrange">
    /// What the window does to give the middle to whatever should have it now - the window owns that
    /// cell, so it is asked rather than reached into.
    /// </param>
    internal static void Open(Window owner, ActionBar actions, Middle middle, MainViewModel model, Action arrange)
    {
        var doors = new SnapshotDoors(owner, model, middle, arrange);
        var comparison = middle.Comparison;

        actions.TakeSnapshotRequest += (_, _) => model.Snapshotting.Prepare();
        actions.CompareRequest += async (_, _) => await doors.CompareWithThisMachine().ConfigureAwait(true);
        actions.CompareTwoRequest += async (_, _) => await doors.CompareTwoSnapshots().ConfigureAwait(true);
        middle.Form.SaveRequest += async (_, _) => await doors.TakeSnapshot().ConfigureAwait(true);
        middle.Form.Finished += (_, _) => model.Snapshotting.PutAway();
        comparison.ExportRequest += async (_, _) => await doors.Export().ConfigureAwait(true);
        comparison.Finished += (_, _) => model.Snapshotting.PutAway();

        model.Snapshotting.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(Snapshotting.Busy))
            {
                actions.ReadingForASnapshot(model.Snapshotting.Busy);
            }
            else if (changed.PropertyName is nameof(Snapshotting.Shown) or nameof(Snapshotting.Form))
            {
                doors.Comparing();
            }
        };
    }

    /// <summary>
    /// A comparison or the form that just arrived takes the middle, and the overview goes if it was
    /// there - the newer thing somebody asked for wins. One put away gives the list back. The form
    /// takes the keyboard into its note, because the person who opened it is about to type.
    /// </summary>
    private void Comparing()
    {
        if (_model.Snapshotting.InTheMiddle)
        {
            _model.ShowingOverview = false;
        }

        _arrange();

        if (_model.Snapshotting.Form is not null && _model.Snapshotting.Shown is null)
        {
            _middle.Form.TakeTheKeyboard();
        }
    }

    /// <summary>
    /// Asks where the snapshot should go and takes it there - from the form's button since W2-d, so
    /// the note somebody typed goes into the file. Backing out of the dialog says nothing and leaves
    /// the form where it was - the export's argument: it is the commonest thing that happens to a
    /// save dialog.
    ///
    /// <b>The dialog asks before replacing a file that is there</b> (its own default), and a yes is
    /// what the window passes on as "a snapshot there may go". What stands there and is not a snapshot
    /// is refused anyway, in the core - S-8.
    /// </summary>
    private Task TakeSnapshot()
    {
        var dialog = FileDialogs.Save(_model.Snapshotting.SuggestedName(), ".json", Texts.Of("gui.snapshot.kind"));

        return dialog.ShowDialog(_owner) == true
            ? _model.Snapshotting.TakeAsync(dialog.FileName)
            : Task.CompletedTask;
    }

    /// <summary>Asks which snapshot to compare with this machine, and compares it.</summary>
    private Task CompareWithThisMachine()
    {
        var dialog = FileDialogs.Open(".json", Texts.Of("gui.snapshot.kind"), Texts.Of("gui.snapshot.chooseForMachine"));

        return dialog.ShowDialog(_owner) == true
            ? _model.Snapshotting.CompareAsync(dialog.FileName)
            : Task.CompletedTask;
    }

    /// <summary>
    /// Asks for two snapshots, one dialog after the other, and compares them - owner's decision W2-a.
    /// The order they are chosen in is not the order they are compared in: Snapshotting puts the one
    /// taken first on the left. Backing out of either dialog compares nothing and says nothing.
    /// </summary>
    private Task CompareTwoSnapshots()
    {
        var first = FileDialogs.Open(".json", Texts.Of("gui.snapshot.kind"), Texts.Of("gui.snapshot.chooseFirst"));

        if (first.ShowDialog(_owner) != true)
        {
            return Task.CompletedTask;
        }

        var second = FileDialogs.Open(".json", Texts.Of("gui.snapshot.kind"), Texts.Of("gui.snapshot.chooseSecond"));

        return second.ShowDialog(_owner) == true
            ? _model.Snapshotting.CompareFilesAsync(first.FileName, second.FileName)
            : Task.CompletedTask;
    }

    /// <summary>
    /// Asks where the comparison on screen should go and writes it there, its sentences first and the
    /// rows as the table shows them - owner's decision W2-c.
    /// </summary>
    private Task Export()
    {
        var dialog = FileDialogs.Save(DiffExporting.FileName(), ".csv", Texts.Of("gui.export.kind"));

        return dialog.ShowDialog(_owner) == true
            ? _model.Snapshotting.ExportAsync(dialog.FileName, _middle.Comparison.InTheOrderOnScreen())
            : Task.CompletedTask;
    }

    /// <summary>The two views snapshots put where the list was, handed over together.</summary>
    internal sealed record Middle(DiffView Comparison, SnapshotFormView Form);
}
