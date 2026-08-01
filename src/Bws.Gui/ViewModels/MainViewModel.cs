using System.Collections.ObjectModel;
using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the window shows and how it got there.
///
/// The reading runs off the interface thread, because it takes about half a second over 810
/// entries and a window that stops answering for half a second at startup is a window that
/// looks broken. `docs/06`, part 4: nothing from a worker thread touches the interface, so
/// the entries are turned into rows out there and handed over as a finished list.
///
/// Knows nothing about WPF beyond the notification interface. That is what makes it possible
/// to check what the window will show without opening one - and the first thing this slice
/// is judged on is whether the window shows the same entries as the command line, which is
/// a question about this file rather than about the markup.
/// </summary>
public sealed class MainViewModel : Observable
{
    private readonly Func<IReadOnlyList<ScmEntry>> _read;
    private string _status = Texts.Of("gui.status.reading");
    private bool _incomplete;

    public MainViewModel()
        : this(() => new WindowsScmCatalog().ReadAll())
    {
    }

    /// <summary>
    /// The reading is handed in so a test can decide what the manager says, including the
    /// cases a real machine will not produce on demand - a refusal, an empty machine, a
    /// service whose configuration could not be read.
    /// </summary>
    public MainViewModel(Func<IReadOnlyList<ScmEntry>> read) => _read = read;

    public ObservableCollection<EntryRow> Entries { get; } = [];

    /// <summary>One line under the list. Never empty - "reading" is a state worth showing.</summary>
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>
    /// Whether the reading admitted to gaps. Shown, never swallowed.
    ///
    /// Rule 8 in the window: a listing that quietly dropped what it could not read looks
    /// complete, and looking complete is exactly what makes it dangerous.
    /// </summary>
    public bool Incomplete
    {
        get => _incomplete;
        private set => Set(ref _incomplete, value);
    }

    /// <summary>
    /// Reads the machine and fills the list.
    ///
    /// Returns the rows as well as filling the collection, so a test can look at what was
    /// produced without watching a collection it did not create.
    /// </summary>
    public async Task<IReadOnlyList<EntryRow>> LoadAsync()
    {
        Status = Texts.Of("gui.status.reading");

        IReadOnlyList<EntryRow> rows;

        try
        {
            // Off the interface thread. The rows are built out here too - turning 810
            // entries into rows is cheap, and doing it on the way back would put the cost
            // where the window is waiting.
            rows = await Task.Run(() => _read().Select(EntryRow.Of).ToArray()).ConfigureAwait(true);
        }
#pragma warning disable CA1031
        // Broad, and it is the same argument as the entry point of the command line tool:
        // the failure reaches the person, in the line under the list, instead of taking the
        // window down with a dialog nobody can act on. The message is the system's, so it
        // carries its own number and its own language.
        catch (Exception failure)
        {
            Status = Texts.Of("gui.status.failed", failure.Message);
            Incomplete = true;

            return [];
        }
#pragma warning restore CA1031

        Entries.Clear();

        foreach (var row in rows)
        {
            Entries.Add(row);
        }

        Status = Texts.Of("gui.status.read", rows.Count);

        return rows;
    }
}
