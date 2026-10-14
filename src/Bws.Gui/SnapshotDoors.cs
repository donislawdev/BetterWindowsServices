using System.Windows;
using Bws.Gui.ViewModels;
using Microsoft.Win32;

namespace Bws.Gui;

/// <summary>
/// The window's half of snapshots: the two dialogs that ask which file, and what the window shows
/// when a reading starts or a comparison arrives - `D1` and `D2`, since 2026-10-14.
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
    private readonly Action _arrange;

    private SnapshotDoors(Window owner, MainViewModel model, Action arrange)
    {
        _owner = owner;
        _model = model;
        _arrange = arrange;
    }

    /// <summary>
    /// Wires the bar's two snapshot items, the comparison's way back, and what the window shows when
    /// a reading starts or a comparison comes and goes.
    /// </summary>
    /// <param name="arrange">
    /// What the window does to give the middle to whatever should have it now - the window owns that
    /// cell, so it is asked rather than reached into.
    /// </param>
    internal static void Open(Window owner, ActionBar actions, DiffView comparison, MainViewModel model, Action arrange)
    {
        var doors = new SnapshotDoors(owner, model, arrange);

        actions.TakeSnapshotRequest += async (_, _) => await doors.TakeSnapshot().ConfigureAwait(true);
        actions.CompareRequest += async (_, _) => await doors.CompareWithThisMachine().ConfigureAwait(true);
        comparison.Finished += (_, _) => model.Snapshotting.PutAway();

        model.Snapshotting.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(Snapshotting.Busy))
            {
                actions.ReadingForASnapshot(model.Snapshotting.Busy);
            }
            else if (changed.PropertyName == nameof(Snapshotting.Shown))
            {
                doors.Comparing();
            }
        };
    }

    /// <summary>
    /// A comparison that just arrived takes the middle, and the overview goes if it was there - the
    /// newer thing somebody asked for wins. One put away gives the list back.
    /// </summary>
    private void Comparing()
    {
        if (_model.Snapshotting.Shown is not null)
        {
            _model.ShowingOverview = false;
        }

        _arrange();
    }

    /// <summary>
    /// Asks where the snapshot should go and takes it there. Backing out of the dialog says nothing -
    /// the export's argument: it is the commonest thing that happens to a save dialog.
    ///
    /// <b>The dialog asks before replacing a file that is there</b> (its own default), and a yes is
    /// what the window passes on as "a snapshot there may go". What stands there and is not a snapshot
    /// is refused anyway, in the core - S-8.
    /// </summary>
    private Task TakeSnapshot()
    {
        var dialog = new SaveFileDialog
        {
            FileName = _model.Snapshotting.SuggestedName(),
            DefaultExt = ".json",
            Filter = Texts.Of("gui.snapshot.kind") + " (*.json)|*.json",
            AddExtension = true
        };

        return dialog.ShowDialog(_owner) == true
            ? _model.Snapshotting.TakeAsync(dialog.FileName)
            : Task.CompletedTask;
    }

    /// <summary>Asks which snapshot to compare with this machine, and compares it.</summary>
    private Task CompareWithThisMachine()
    {
        var dialog = new OpenFileDialog
        {
            DefaultExt = ".json",
            Filter = Texts.Of("gui.snapshot.kind") + " (*.json)|*.json",
            CheckFileExists = true
        };

        return dialog.ShowDialog(_owner) == true
            ? _model.Snapshotting.CompareAsync(dialog.FileName)
            : Task.CompletedTask;
    }
}
