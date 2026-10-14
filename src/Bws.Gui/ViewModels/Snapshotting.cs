using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
public sealed class Snapshotting : Observable
{
    private readonly IScmCatalog _catalog;
    private readonly IBinaryInspector? _inspector;
    private readonly IClock _clock;
    private readonly Func<Says> _says;

    private bool _busy;
    private DiffSheet? _shown;

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
        private set => Set(ref _busy, value);
    }

    /// <summary>The comparison in the middle of the window, or nothing while the list is there.</summary>
    public DiffSheet? Shown
    {
        get => _shown;
        private set => Set(ref _shown, value);
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
        if (!Starting(Texts.Of("gui.snapshot.taking"), out var inspector))
        {
            return;
        }

        try
        {
            var (snapshot, text) = await Task.Run(() =>
            {
                var read = Read(inspector);

                return (read, SnapshotJson.Render(read));
            }).ConfigureAwait(true);

            if (SnapshotOnDisk.WhyNotWritten(path, force: true) is { } refusal)
            {
                _says().CouldNotDo(SnapshotWords.NotWritten(refusal));

                return;
            }

            AtomicFile.Write(path, text);

            var count = snapshot.Entries.Count;

            _says().Did(count == 1
                ? Texts.Of("gui.snapshot.taken.one", count, Path.GetFileName(path))
                : Texts.Of("gui.snapshot.taken.many", count, Path.GetFileName(path)));
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
        if (!Starting(Texts.Of("gui.snapshot.comparing", Path.GetFileName(path)), out var inspector))
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
    /// Takes the comparison out of the middle of the window. True when there was one to take - the
    /// window's Escape asks this in line with everything else Escape backs out of.
    /// </summary>
    internal bool PutAway()
    {
        if (Shown is null)
        {
            return false;
        }

        Shown = null;

        return true;
    }

    /// <summary>
    /// Takes the one reading at a time, and says it is under way.
    ///
    /// <b>No inspector, no snapshot</b>: a model built for a test without one cannot read signatures,
    /// and a snapshot without them would compare as though every entry had changed. The window always
    /// has one - the parameterless MainViewModel hands it the system's - so this is a test's state.
    /// </summary>
    private bool Starting(string saying, [NotNullWhen(true)] out IBinaryInspector? inspector)
    {
        inspector = _inspector;

        if (Busy || inspector is null)
        {
            return false;
        }

        Busy = true;
        _says().Did(saying);

        return true;
    }

    /// <summary>Everything a snapshot holds, read now - the listing and the two passes on top of it.</summary>
    private Snapshot Read(IBinaryInspector inspector) =>
        Snapshot.Of(SnapshotPasses.Fill(_catalog.ReadAll(), _catalog, inspector, out _, out _), note: null, _clock);

    private (Snapshot? Earlier, SnapshotDiff? Diff, string? Trouble) Compared(string path, IBinaryInspector inspector)
    {
        if (!SnapshotOnDisk.TryLoad(path, out var earlier, out var refusal))
        {
            return (null, null, SnapshotWords.Of(refusal));
        }

        return SnapshotDiff.TryBetween(earlier, Read(inspector), out var diff, out var failure)
            ? (earlier, diff, null)
            : (null, null, Texts.Of("gui.snapshot.cannotCompare", failure ?? string.Empty));
    }
}
