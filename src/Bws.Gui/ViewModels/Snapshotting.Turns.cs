namespace Bws.Gui.ViewModels;

/// <summary>
/// One reading at a time, and whose the middle of the window is when it comes back.
///
/// <b>A partial since 2026-10-27</b>, when the second half arrived (logic report S05) and Snapshotting.cs
/// stood over the share of its file ceiling the size guard counts as near. Both halves answer the same
/// question about a reading - may it start, and may it show what it found - so they live together.
/// </summary>
public sealed partial class Snapshotting
{
    /// <summary>
    /// How many times somebody changed what stands in the middle of the window - opened the form, put
    /// something away, went to another tab. A reading takes the count when it starts and gives the middle
    /// its answer only if nobody moved since (logic report S05, 2026-10-27): until then a comparison that
    /// came back after somebody had gone to a list took the window back from it.
    /// </summary>
    private int _turn;

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
    /// Whether somebody changed the middle of the window since a comparison started - and if so, says at
    /// the foot that it finished unseen, so the line about the reading does not stand there with nothing
    /// after it. A trouble is said either way: it is about the files, not about the view.
    /// </summary>
    private bool MovedOn(int turn)
    {
        if (turn == _turn)
        {
            return false;
        }

        _says().Did(Texts.Of("gui.snapshot.movedOn"));

        return true;
    }
}
