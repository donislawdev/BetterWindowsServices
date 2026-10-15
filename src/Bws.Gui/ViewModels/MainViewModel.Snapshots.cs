using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Snapshots from the window - taking one, and comparing one with this machine (`D1`, `D2`, since
/// 2026-10-14).
///
/// <b>A partial holding what the list's model needs to know about them, which is little.</b> The work
/// and its state are <see cref="ViewModels.Snapshotting"/>, which knows nothing about the list, the
/// query or the reading - and this class, which is about the list, stood on the size ratchet's
/// allowance for a long file. Made in the constructor from the same manager, clock and inspector the
/// list reads through, so a test hands one fake machine to both.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Taking and comparing snapshots, and the comparison when one is on screen.</summary>
    public Snapshotting Snapshotting { get; }

    /// <summary>
    /// Whether the list is what the middle of the window shows - neither the overview nor a comparison
    /// is standing in front of it.
    ///
    /// <b>One question where there used to be "is the overview up"</b>, asked in three places that were
    /// all really asking this: whether the line under the window may speak about the list, whether
    /// Ctrl+F may open the suggestions under a box nobody can see, and whether the box may take the
    /// keyboard. A comparison hides the list exactly as the overview does, so each of the three would
    /// otherwise have answered wrongly while one is up. The form before a snapshot (W2-d) hides it too,
    /// and <see cref="Snapshotting.InTheMiddle"/> answers for both.
    /// </summary>
    public bool ListOnScreen => !_showingOverview && !Snapshotting.InTheMiddle;

    /// <summary>
    /// Escape's middle step: a comparison standing where the list was goes first, then the panel about
    /// one entry - true when either went.
    ///
    /// <b>Here rather than as two more links in the window's chain</b>, because the method holding that
    /// chain counts every <c>||</c> against the shape guard's ceiling and stood one under the line it
    /// calls near. The order is the same argument the chain makes: the more recent thing somebody
    /// opened goes first, and a comparison replaces the list the panel sits beside.
    /// </summary>
    internal bool PutAwayAView() => Snapshotting.PutAway() || Chosen.Hide();

    /// <summary>
    /// The comparison's state, wired to the line under the window: when a comparison comes or goes, the
    /// sentence about the list is said again, because whether it may speak has just changed.
    /// </summary>
    private Snapshotting Comparing(IScmCatalog catalog, IBinaryInspector? inspector, IClock clock)
    {
        var snapshotting = new Snapshotting(catalog, inspector, clock, () => Says);

        snapshotting.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName is nameof(ViewModels.Snapshotting.Shown) or nameof(ViewModels.Snapshotting.Form))
            {
                SayTheAnswer();
            }
        };

        return snapshotting;
    }
}
