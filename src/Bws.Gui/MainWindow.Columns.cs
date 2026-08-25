using System.Windows;
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
    /// The layout this window was opened with, kept because one thing outside the arranging needs
    /// it: the item at the foot of the column picker that puts the list back.
    ///
    /// <b>Assigned in <see cref="Arrange"/> and nowhere else</b>, which is why it is allowed to be
    /// null until then - the constructor calls that method before it wires the menu that uses this.
    /// </summary>
    private KeptColumns? _kept;

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
        var kept = _kept = new KeptColumns(preferences);

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

    /// <summary>
    /// Puts the list back the way this window opens - the last item in the column picker.
    ///
    /// <b>One handler for the whole menu, because the items are built from data and a style has
    /// nowhere to put a handler.</b> Every tick in that menu raises this too, so the first line is
    /// not a guard against something unlikely - it is how this handler knows which item was
    /// clicked. A choice is answered by its own binding and has nothing to do here.
    ///
    /// <b>It restores the list on screen and leaves the other two alone</b>, which is the same
    /// promise every other write in <see cref="KeptColumns"/> keeps: somebody who arranged their
    /// drivers list does not lose it by putting the services list back.
    ///
    /// Apart from the click so that it can be checked at all - a handler the framework calls is
    /// reachable only by clicking, and the answer is the part worth asserting.
    /// </summary>
    private void PutColumnsBack(object sender, RoutedEventArgs e)
    {
        if ((e?.OriginalSource as FrameworkElement)?.DataContext is ColumnReset)
        {
            RestoreColumns();
        }
    }

    /// <summary>
    /// Hands the column picker its choices, and takes the one click in it that is not a choice.
    ///
    /// <b>Out of the constructor on 2026-08-25 because an analyser asked</b> - the bar of actions
    /// took that method three lines past the length it allows, and this was the block in it about
    /// a subject this file already owns. The same seam, for the same reason, as the day the scope
    /// switch pushed Arrange out of it.
    /// </summary>
    private void HandTheColumnsOver()
    {
        // SET RATHER THAN BOUND, and that is the same trap the column headers fell into: a menu
        // hangs off a Popup, which is not in the visual tree, so what it inherits is a question
        // with an answer nobody should have to know. Handing it the choices costs one line and
        // has no such question.
        if (ColumnsButton.ContextMenu is { } menu)
        {
            // HEADINGS AS ITEMS, NOT AS GROUPS, AND THAT IS A REPAIR RATHER THAN A PREFERENCE. The
            // first version of this used GroupStyle with a grouped collection view. It looked right
            // and it took the whole menu out of the automation tree: with it open, the window
            // offered twelve togglable elements - all of them filter chips - and none of the
            // seventeen columns. WPF puts a GroupItem between a menu and its items and the menu's
            // peer does not reach through it, so a screen reader sees what the probe saw.
            //
            // A flat list of headings and choices keeps every entry a real MenuItem with a peer of
            // its own. Which style each one wears is decided by the selector below.
            menu.ItemsSource = _columns.Entries;
            menu.ItemContainerStyleSelector = new ColumnEntryStyles();

            // ONE HANDLER ON THE MENU RATHER THAN ONE PER ITEM, because the items are built by
            // WPF from a list of data and a style has nowhere to put a handler. The last entry is
            // the only one that means anything here, and the handler says so by looking at what it
            // was clicked on - see PutColumnsBack.
            menu.AddHandler(MenuItem.ClickEvent, new RoutedEventHandler(PutColumnsBack));
        }
    }

    /// <summary>
    /// Puts the list in the order the file remembers.
    ///
    /// <b>Called once the first reading has arrived, and not while the columns are being built.</b>
    /// A grid with no ItemsSource has no view to hand a comparer to, so an order applied at
    /// construction is an order dropped in silence - ListSorting.By carries the rest of that
    /// argument. Every later move between scopes goes through Reapply, which does it there.
    /// </summary>
    internal void SortAsKept()
    {
        if (_kept is { } kept)
        {
            ListSorting.By(Entries, kept.Plan.Layout.Sort);
        }
    }

    /// <summary>Puts the columns of the list on screen back to what this build opens with.</summary>
    internal void RestoreColumns() => _kept?.Defaults(Entries, _columns, _model.Says);
}
