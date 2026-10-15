namespace Bws.Gui.ViewModels;

/// <summary>
/// The view in place of the list before a snapshot is taken - owner's decision W2-d of 2026-10-15,
/// backlog 561: what rights the window reads the machine with, and a note to keep inside the file.
///
/// <b>WHY A VIEW OF ITS OWN RATHER THAN A FIELD IN THE SAVE DIALOG.</b> The dialog Windows draws has
/// no room for one - WPF's SaveFileDialog offers none, and the COM interface that does is new interop
/// for one text box in a dialog no test can open (FileDialogs). The price is one click more for every
/// snapshot, said when the owner chose it.
///
/// <b>THE RIGHTS SENTENCE IS THE OTHER HALF OF THE ARGUMENT</b> - `D1` says a snapshot taken with and
/// without administrator rights are two different documents, and until this view nothing said which
/// one was about to be written until a comparison found out.
/// </summary>
public sealed class SnapshotForm : Observable
{
    private string _note = string.Empty;
    private bool _busy;

    internal SnapshotForm(bool elevated)
    {
        Rights = elevated ? Texts.Of("gui.snapshot.form.elevated") : Texts.Of("gui.snapshot.form.unelevated");
    }

    /// <summary>What rights the reading will run with, and what that means for the file.</summary>
    public string Rights { get; }

    /// <summary>
    /// What somebody typed to keep inside the snapshot - empty when they typed nothing. Handed to the
    /// core as typed: the spaces around it, and a note of nothing but spaces, are settled where the
    /// command line's note is settled (SnapshotMetadata.Of), so both front ends write the same file.
    /// </summary>
    public string Note
    {
        get => _note;
        set => Set(ref _note, value ?? string.Empty);
    }

    /// <summary>
    /// Whether the save button may be pressed - not while the machine is being read for the snapshot
    /// this view already asked for, the same one-at-a-time rule as the button on the bar.
    /// </summary>
    public bool Ready => !_busy;

    /// <summary>Told when the machine starts and stops being read for this snapshot.</summary>
    internal void Reading(bool busy)
    {
        if (_busy != busy)
        {
            _busy = busy;
            Raise(nameof(Ready));
        }
    }
}
