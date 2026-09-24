using System.IO;
using System.Windows;
using System.Windows.Data;
using Bws.Core.Snapshots;
using Bws.Gui.ViewModels;
using Microsoft.Win32;

namespace Bws.Gui;

/// <summary>
/// Writing what the list shows to a file - the half of it that only a window can answer.
///
/// <b>Which rows and which columns are questions about a grid</b>, and both have an answer that is
/// easy to get almost right: the rows in the order somebody sorted them into rather than the order
/// the model holds, and the columns in the order they dragged them into rather than the order the
/// catalogue lists. What the file SAYS is decided in <see cref="Exporting"/>, where it can be
/// checked without opening a window.
///
/// <b>Its own file, the shape this window uses five times now</b> - the constructor is at the length
/// an analyser allows and this is one subject.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Asks where the file should go and writes it, or says why it could not.
    ///
    /// <b>Backing out of the dialog is not a failure and says nothing</b> - it is the commonest
    /// thing that happens to a save dialog, and a sentence about it would be a window complaining
    /// that somebody changed their mind.
    ///
    /// <b>WHEN IT WORKS IT SAYS SO, SINCE 2026-09-24 - UX-GUI-016, owner's decision, and it
    /// reverses one.</b> Until that day nothing was said: the file is where the person put it and
    /// they chose the name, so the confirmation was the thing itself. The audit's answer was that
    /// nothing on screen then said how many rows went in, so checking an export against the window
    /// meant opening the file. The sentence is in <see cref="WriteShownTo"/>, beside the failure.
    ///
    /// <b>The name offered is the list's</b> - drivers.csv on the Drivers tab rather than
    /// services.csv on every tab. <see cref="Exporting.FileName"/> says why.
    /// </summary>
    private const string Mark = "\uFEFF";

    private void ExportWhatIsShown()
    {
        var dialog = new SaveFileDialog
        {
            FileName = Exporting.FileName(_model.Scope),
            DefaultExt = ".csv",
            Filter = Texts.Of("gui.export.kind") + " (*.csv)|*.csv",
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _ = WriteShownTo(dialog.FileName);
    }

    /// <summary>
    /// Writes what the list shows to a named file and says what happened - how many rows went where,
    /// or why none did. Hands back the sentence about the failure, for a guard to read.
    ///
    /// <b>Apart from the dialog because the dialog is the one part nothing here can drive.</b>
    /// Measured 2026-08-25: a save dialog opened from a session started in the background never
    /// appears - twenty seconds of waiting and no window anywhere on the desktop - which is the same
    /// limit tools/gui-probe/interact.ps1 refuses on rather than pretending about. Everything on
    /// this side of the dialog is a file on disk, which a guard can read.
    ///
    /// <b>A byte order mark, written as a character rather than by the encoder.</b> AtomicFile
    /// deliberately writes none - a snapshot is read back by our own reader, which needs no help -
    /// but a CSV is opened by a spreadsheet that guesses the encoding without one, and the display
    /// names on this machine are full of characters it guesses wrongly.
    /// </summary>
    internal string? WriteShownTo(string path)
    {
        var rows = InTheOrderOnScreen();
        var text = Exporting.AsCsv(ShownColumns(), rows);

        string? trouble;

        try
        {
            AtomicFile.Write(path, Mark + text);

            trouble = null;
        }
        catch (IOException failure)
        {
            trouble = Texts.Of("gui.export.failed", failure.Message);
        }
        catch (UnauthorizedAccessException failure)
        {
            trouble = Texts.Of("gui.export.failed", failure.Message);
        }

        if (trouble is null)
        {
            _model.Says.Did(Exporting.Wrote(rows.Count, Path.GetFileName(path)));
        }
        else
        {
            _model.Says.CouldNotDo(trouble);
        }

        return trouble;
    }

    /// <summary>
    /// The columns that are on, in the order they are on screen.
    ///
    /// <b>Through <c>ColumnFromDisplayIndex</c> rather than through the collection</b>, which is the
    /// same distinction the kept layout is measured on: the collection is the order columns were
    /// added in and never moves, and the display order is what a person sees.
    /// </summary>
    internal IReadOnlyList<string> ShownColumns() =>
    [
        .. Enumerable
            .Range(0, Entries.Columns.Count)
            .Select(Entries.ColumnFromDisplayIndex)
            .Where(column => column.Visibility == Visibility.Visible)
            .Select(column => column.SortMemberPath ?? string.Empty)
            .Where(id => Columns.Of(id) is not null)
    ];

    /// <summary>
    /// The rows as the list has them, sorted the way somebody sorted them.
    ///
    /// <b>The view rather than the model, and that is the whole point of asking a window.</b> The
    /// model holds the entries the query left - the order they are read in is a comparer on the view
    /// over them, so a file built from the model would be the right rows in the wrong order.
    /// </summary>
    internal IReadOnlyList<EntryRow> InTheOrderOnScreen() =>
        CollectionViewSource.GetDefaultView(Entries.ItemsSource) is { } view
            ? [.. view.Cast<EntryRow>()]
            : [.. _model.Rows];
}
