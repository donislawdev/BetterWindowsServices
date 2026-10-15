using System.ComponentModel;
using System.IO;
using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Taking a snapshot of this machine from the window, and comparing one with it - `D1` and `D2` in
/// the window since 2026-10-14 (`docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md`, W1).
///
/// <b>The work and the state, with no window in it</b> - GUI rule 15 of the project notes. The window
/// asks a person which file, through a dialog only a window can show, and hands the path here. What
/// is read, what is written, what is refused and what is said about it is decided in this class, so a
/// test can drive every one of those without a dialog nobody can answer from a test host.
///
/// <b>THE SAME READING AS `bws snapshot create`, not a cheaper one made from what the list already
/// holds.</b> The list reads signatures and dependents only when asked, and a snapshot without them
/// compares against one that has them as though every entry had changed - <see cref="SnapshotPasses"/>
/// carries that argument, and it is the one place both front ends read a snapshot through. The cost
/// is a second reading of the machine: 1.1-1.2 s over 801 entries on sixteen processors, measured
/// 2026-10-14 through the command line. Off the interface thread, like every reading here.
///
/// <b>NO PLAN, and that is rule 1 of the project notes read correctly rather than skipped:</b> a plan
/// stands in front of a change to the machine. Writing a file the person chose changes no service,
/// which is the argument the command line makes at the same place.
/// </summary>
public sealed partial class Snapshotting : Observable
{
    private readonly IScmCatalog _catalog;
    private readonly IBinaryInspector? _inspector;
    private readonly IClock _clock;
    private readonly Func<Says> _says;

    private bool _busy;
    private DiffSheet? _shown;
    private SnapshotForm? _form;

    internal Snapshotting(IScmCatalog catalog, IBinaryInspector? inspector, IClock clock, Func<Says> says)
    {
        _catalog = catalog;
        _inspector = inspector;
        _clock = clock;
        _says = says;
    }

    /// <summary>
    /// Whether the machine is being read for a snapshot right now. One at a time: a second press while
    /// the first is reading would read the machine twice for one question.
    /// </summary>
    public bool Busy
    {
        get => _busy;
        private set
        {
            // The form's save button goes off with the bar's button, for the same one-at-a-time rule.
            if (Set(ref _busy, value))
            {
                _form?.Reading(value);
            }
        }
    }

    /// <summary>The comparison in the middle of the window, or nothing while the list is there.</summary>
    public DiffSheet? Shown
    {
        get => _shown;
        private set => Set(ref _shown, value);
    }

    /// <summary>
    /// The view before a snapshot is taken, in the middle of the window, or nothing - W2-d, backlog 561.
    /// "Take snapshot..." opens it, and the save dialog opens from its button rather than from the menu.
    /// </summary>
    public SnapshotForm? Form
    {
        get => _form;
        private set => Set(ref _form, value);
    }

    /// <summary>
    /// Whether something of this class stands where the list was - a comparison, or the form before a
    /// snapshot. The list's model asks this rather than either, so a third view here would not leave
    /// the line under the window speaking about a list nobody can see.
    /// </summary>
    internal bool InTheMiddle => Shown is not null || Form is not null;

    /// <summary>
    /// Opens the form before a snapshot, saying what rights the reading will run with - the same fact
    /// the line at the foot of the window says. Nothing when it is open already.
    /// </summary>
    internal void Prepare()
    {
        if (Form is not null)
        {
            return;
        }

        Shown = null;
        Form = new SnapshotForm(_says().Elevated);
    }

    /// <summary>
    /// The name a snapshot gets when nobody chose one - the command line's own, so a file taken in the
    /// window and one taken by <c>bws snapshot create</c> on the same machine sort together.
    /// </summary>
    internal string SuggestedName() =>
        SnapshotOnDisk.Target(string.Empty, SnapshotMetadata.Of(note: null, _clock));

    /// <summary>
    /// Reads the machine and writes a snapshot to the path somebody chose, or says why it did not.
    ///
    /// <b>Told that a snapshot standing there may go</b>, because the save dialog has already asked
    /// "replace it?" and the person said yes - the window's <c>--force</c>. What stands there and is
    /// NOT a snapshot is still refused and left where it is: S-8 is decided in the core, and nothing a
    /// dialog says reaches past it.
    /// </summary>
    internal async Task TakeAsync(string path)
    {
        if (_inspector is not { } inspector || !Starting(Texts.Of("gui.snapshot.taking")))
        {
            return;
        }

        try
        {
            // Read here, on the window's thread, before the reading leaves it - the note is what the
            // form held when somebody pressed save, not whatever is typed while the machine is read.
            var note = Form?.Note;
            var (snapshot, trouble) = await Task.Run(() => Written(path, inspector, note)).ConfigureAwait(true);

            // The same one answer told two ways as in CompareAsync - Written fills one of them, never both.
            if (snapshot is null)
            {
                _says().CouldNotDo(trouble ?? string.Empty);

                return;
            }

            var count = snapshot.Entries.Count;

            _says().Did(count == 1
                ? Texts.Of("gui.snapshot.taken.one", count, Path.GetFileName(path))
                : Texts.Of("gui.snapshot.taken.many", count, Path.GetFileName(path)));

            // Written, so the form has done its work and the list comes back. A refusal keeps it open
            // with the note still in it, so another folder is one more press rather than a retyping.
            Form = null;
        }
        catch (Win32Exception failure)
        {
            _says().CouldNotDo(Texts.Of("gui.snapshot.readFailed", failure.Message));
        }
        catch (IOException failure)
        {
            _says().CouldNotDo(Texts.Of("gui.snapshot.writeFailed", failure.Message));
        }
        catch (UnauthorizedAccessException failure)
        {
            _says().CouldNotDo(Texts.Of("gui.snapshot.writeFailed", failure.Message));
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// Reads a snapshot somebody chose and this machine, compares the two, and puts the comparison in
    /// the middle of the window - or says why not and leaves the window as it was.
    ///
    /// <b>The file first, before the machine</b>: a path that is not a snapshot is refused in a
    /// fraction of a second rather than after the second the machine takes.
    ///
    /// <b>The sheet is built back on the interface thread</b>, because it formats a date in the
    /// person's own culture, and that is the culture of the thread a window runs on.
    /// </summary>
    internal async Task CompareAsync(string path)
    {
        if (_inspector is not { } inspector || !Starting(Texts.Of("gui.snapshot.comparing", Path.GetFileName(path))))
        {
            return;
        }

        try
        {
            var (earlier, diff, trouble) = await Task.Run(() => Compared(path, inspector)).ConfigureAwait(true);

            // Trouble and the two halves are one answer told three ways - Compared fills either the
            // trouble or both halves, never a mix - so asking all three keeps the compiler and the
            // reader on the same page without telling either to be quiet.
            if (earlier is null || diff is null)
            {
                _says().CouldNotDo(trouble ?? string.Empty);

                return;
            }

            Shown = DiffSheet.Of(earlier.Metadata, diff);

            // The line at the foot said what was being read, and the sheet is the answer to it - so
            // it goes, rather than standing under the answer as though the reading were still out.
            _says().Did(string.Empty);
        }
        catch (Win32Exception failure)
        {
            _says().CouldNotDo(Texts.Of("gui.snapshot.readFailed", failure.Message));
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// Reads two snapshots somebody chose, compares them, and puts the comparison in the middle of the
    /// window - or says why not, naming the file that was not a snapshot, and leaves the window as it
    /// was (W2, since 2026-10-15).
    ///
    /// <b>THE ONE TAKEN FIRST IS THE EARLIER SIDE, whichever was chosen first</b> - owner's decision
    /// W2-a, decided in FilesCompared. Chosen the other way round, the same pair would turn every
    /// addition into a removal, and nothing on screen would say so. The command line takes the order it
    /// is given ("the earlier one first"), because a script says what it means.
    ///
    /// <b>No inspector is needed and none is asked for</b> - nothing here reads the machine, so a
    /// window that could not read signatures can still compare two files. <b>Off the window's thread
    /// all the same</b>: two files of up to the size ceiling each, maybe on a share.
    ///
    /// <b>The sheet is built back on the interface thread</b>, for the reason CompareAsync gives: it
    /// formats dates in the culture of the thread a window runs on.
    /// </summary>
    internal async Task CompareFilesAsync(string first, string second)
    {
        if (!Starting(Texts.Of("gui.snapshot.comparingFiles", Path.GetFileName(first), Path.GetFileName(second))))
        {
            return;
        }

        try
        {
            var (earlier, later, diff, trouble) = await Task.Run(() => FilesCompared(first, second)).ConfigureAwait(true);

            // One answer told four ways - FilesCompared fills either the trouble or the other three.
            if (earlier is null || later is null || diff is null)
            {
                _says().CouldNotDo(trouble ?? string.Empty);

                return;
            }

            Shown = DiffSheet.Of(earlier, later, diff);

            _says().Did(string.Empty);
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// Takes the comparison, or the form before a snapshot, out of the middle of the window. True when
    /// there was one to take - the window's Escape asks this in line with everything else Escape backs
    /// out of. The two never stand there together (<see cref="Prepare"/> puts a comparison away), so
    /// one answer covers both.
    ///
    /// <b>The form goes even while the machine is read for it</b> - the reading finishes and says what
    /// it wrote at the foot of the window, which is where that sentence goes whether or not the form
    /// is still up.
    /// </summary>
    internal bool PutAway()
    {
        if (Shown is null && Form is null)
        {
            return false;
        }

        Shown = null;
        Form = null;

        return true;
    }

    /// <summary>
    /// Takes the one reading at a time, and says it is under way.
    ///
    /// <b>No inspector, no snapshot of this machine</b> - asked by the two callers that read it, not
    /// here: a model built for a test without one cannot read signatures, and a snapshot without them
    /// would compare as though every entry had changed. Two files need no inspector, so they start
    /// through here without one. The window always has one - the parameterless MainViewModel hands it
    /// the system's - so a missing one is a test's state.
    /// </summary>
    private bool Starting(string saying)
    {
        if (Busy)
        {
            return false;
        }

        Busy = true;
        _says().Did(saying);

        return true;
    }

    /// <summary>
    /// Reads the machine and writes it where somebody asked, or says why not - all of it off the
    /// interface thread, because asking about a file standing there may read it up to the size
    /// ceiling, and the write is a few megabytes to a disk that may be a share on the network.
    ///
    /// <b>Asked twice, and both are needed.</b> Before the reading, so a target that will be refused
    /// costs no second of reading the machine - the courtesy the command line extends at the same
    /// place. And again right before the write, because a file can appear at the path while the
    /// machine is read, and that second ask is the one S-8 stands on.
    /// </summary>
    private (Snapshot? Taken, string? Trouble) Written(string path, IBinaryInspector inspector, string? note)
    {
        if (Refused(path) is { } early)
        {
            return (null, SnapshotWords.NotWritten(early));
        }

        var read = Read(inspector, note);
        var text = SnapshotJson.Render(read);

        if (Refused(path) is { } late)
        {
            return (null, SnapshotWords.NotWritten(late));
        }

        AtomicFile.Write(path, text);

        return (read, null);
    }

    /// <summary>
    /// A file written off the window's thread - the comparison's export (Snapshotting.Files.cs). Here
    /// rather than there, because this file is where ConcurrencyGuards holds the argument for work
    /// leaving that thread.
    /// </summary>
    private static Task WrittenOffTheWindow(string path, string text) => Task.Run(() => AtomicFile.Write(path, text));

    /// <summary>
    /// Why a snapshot may not go to this path. Told that a snapshot standing there may go, for the
    /// reason <see cref="TakeAsync"/> gives - the dialog has already asked.
    /// </summary>
    private static DiskRefusal? Refused(string path) => SnapshotOnDisk.WhyNotWritten(path, force: true);

    /// <summary>
    /// Everything a snapshot holds, read now - the listing and the two passes on top of it - with the
    /// note somebody typed in the form, or none for a reading that only compares.
    /// </summary>
    private Snapshot Read(IBinaryInspector inspector, string? note) =>
        Snapshot.Of(SnapshotPasses.Fill(_catalog.ReadAll(), _catalog, inspector, out _, out _), note, _clock);

    private (Snapshot? Earlier, SnapshotDiff? Diff, string? Trouble) Compared(string path, IBinaryInspector inspector)
    {
        if (!SnapshotOnDisk.TryLoad(path, out var earlier, out var refusal))
        {
            return (null, null, SnapshotWords.Of(refusal));
        }

        return SnapshotDiff.TryBetween(earlier, Read(inspector, note: null), out var diff, out var failure)
            ? (earlier, diff, null)
            : (null, null, Texts.Of("gui.snapshot.cannotCompare", failure ?? string.Empty));
    }
}
