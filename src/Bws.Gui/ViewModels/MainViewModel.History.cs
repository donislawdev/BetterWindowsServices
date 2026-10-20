using Bws.Core.History;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The change history from the window - J2, since 2026-10-20 (`docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md`
/// section 10). The work and its state are <see cref="HistoryReading"/>, which knows nothing about the
/// list, and this partial holds the one thing the list's model decides about it - what the middle of the
/// window gives way to it.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Reading the System log, the view of the whole machine, and each entry's section of the panel.</summary>
    public HistoryReading History { get; }

    /// <summary>
    /// Puts the change history of the whole machine where the list was, and reads the log again.
    ///
    /// <b>The newer thing somebody asked for wins the middle</b> - the argument the comparison makes when
    /// it arrives (SnapshotDoors). So a comparison, the form before a snapshot and the overview all go, and
    /// only one view ever stands where the list was.
    /// </summary>
    internal Task OpenHistory()
    {
        Snapshotting.PutAway();
        ShowingOverview = false;

        return History.Open();
    }

    /// <summary>
    /// The history's state, wired to the line under the window: when the view comes or goes, the sentence
    /// about the list is said again, because whether it may speak has just changed - the arrangement
    /// <see cref="Comparing"/> makes for a comparison.
    /// </summary>
    private HistoryReading HistoryOf(IChangeHistoryReader? reader)
    {
        var history = new HistoryReading(reader);

        history.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(HistoryReading.Shown))
            {
                SayTheAnswer();
            }
        };

        return history;
    }
}
