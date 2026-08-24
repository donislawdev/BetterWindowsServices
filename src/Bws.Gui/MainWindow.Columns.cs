using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// Which columns the list has - reading the kept layout, building the grid from it, and handing
/// the grid another set when the scope moves.
///
/// <b>Its own file since 2026-08-19, and the size ratchet is what asked.</b> The scope switch
/// brought a second layout path into the constructor, the constructor went past the length an
/// analyser allows it, and then this file went past five hundred lines. Both were answered by the
/// same block coming out, because it was the one part of that file about a single subject.
///
/// <b>A partial rather than a class of its own, which is the shape this window already uses twice
/// - see MainWindow.Menu.cs and MainWindow.Carrying.cs.</b> What is here needs the grid, the
/// column bar and the model at once, and all three are fields of the window. A separate class
/// would take three arguments and be the window wearing a different name.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Everything about which columns the list has - reading the kept layout, building the columns
    /// from it, and moving to another set when the scope moves.
    ///
    /// <b>Out of the constructor on 2026-08-19 because an analyser asked, and the seam is a subject
    /// rather than a line count.</b> The scope switch brought a second layout path in here and took
    /// the constructor past its length, and this was the one block in it that is about one thing.
    ///
    /// It stays in the window rather than moving to <see cref="KeptColumns"/>, and the split is the
    /// one this product makes everywhere: that class decides WHEN a layout is worth writing and
    /// WHAT it should be, and this method is the introduction of the parties - it is the only place
    /// that holds a grid, a bar and a model at once.
    /// </summary>
    private void Arrange(PreferencesFile preferences)
    {
        // A file that is unreadable, stale or from another build is reconciled into something
        // usable first, and whatever could not be honoured is said out loud.
        var kept = new KeptColumns(preferences);

        _columns.Follow(kept.Plan);

        // BEFORE ANY ROW EXISTS, and the grid has no columns at all until this line runs. There
        // are eighteen of them and twelve are off, which is a list somebody chooses from rather
        // than a list written out - see ListColumns.
        if (kept.Trouble(ListColumns.Fill(Entries, _columns, kept.Plan)) is { } trouble)
        {
            _model.Says.AboutTheLayout(trouble);
        }

        kept.Watch(this, Entries, _columns, _model.Says);

        // ANOTHER LIST MEANS ANOTHER SET OF COLUMNS, since 2026-08-19. A driver has no process
        // identifier and almost never an account - measured, 0 and 3 out of 472 - so one layout
        // serving both spends three columns of the longer list on emptiness.
        //
        // Wired to the model rather than to the switch, and that is the same choice every other
        // part of this window makes: the control writes a scope, the model decides what that means,
        // and this is one of the things it means. A handler on the RadioButton would run before the
        // model had moved and would lay out the list somebody was leaving.
        _model.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName != nameof(MainViewModel.Scope))
            {
                return;
            }

            // One write at the end rather than one per column - the argument is at KeptColumns.While.
            kept.While(
                () =>
                {
                    var plan = kept.MoveTo(_model.Scope, Entries);

                    _columns.Follow(plan);

                    if (kept.Trouble(ListColumns.Reapply(Entries, plan)) is { } moving)
                    {
                        _model.Says.AboutTheLayout(moving);
                    }
                },
                Entries,
                _model.Says);
        };
    }
}
