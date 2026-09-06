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

    /// <summary>The heading menu on screen, if one is. See OfferTheColumnMenu for why it is kept.</summary>
    private ContextMenu? _headingMenu;

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

        // THE PICKER GETS A VOICE IN WHAT THE WINDOW GOES AND READS, 2026-09-05. Five columns are
        // fed by the second phase of `ADR-13` and every one of them said "unknown" on every row
        // until the box above the list happened to mention the same family - because the query was
        // the only thing being asked. The full account is at Column.Needs.
        //
        // Wired HERE because this method is the only place that holds a grid, a bar and a model at
        // once, which is the sentence above this one and the reason it says so.
        //
        // Before Follow rather than after, and it makes no difference: what is handed over is a
        // question, so it is answered whenever the reading gets round to asking. It stands first
        // because a kept layout with the memory column already on has to be heard on the FIRST
        // reading, and that reading is started by the caller of this method.
        _model.ColumnsNeed = () => _columns.Needs;

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
    /// A grid with no ItemsSource has no view to hand a COMPARER to, so an order applied at
    /// construction sorts nothing - ListSorting.By carries the rest of that argument. Every later
    /// move between scopes goes through Reapply, which does it there.
    ///
    /// <b>What By no longer drops, since 2026-09-03, is the MARK on the heading</b> - backlog 315.
    /// That half never needed a view and losing it silently was the whole of that defect. This
    /// call is still the one that gives the view its comparer, so it is still the moment the rows
    /// actually move.
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

    /// <summary>
    /// The menu under a right click on a column heading, built for the column that was clicked.
    ///
    /// <b>THE OWNER'S ASK OF 2026-09-05: click Status, choose Running, without learning the query
    /// language first.</b> What it offers is decided by <see cref="ColumnMenu"/> and the language
    /// itself - this method is the introduction of the parties, which is the same division of work
    /// the picker's own menu already uses.
    ///
    /// <b>Built each time rather than kept.</b> Every column offers a different list, and the
    /// ticks in it are read out of the query text at the moment of asking - so a menu held between
    /// openings would be a second copy of an answer that changes under it. Twenty-seven menus
    /// standing ready would also be twenty-seven sets of chips subscribed to nothing.
    ///
    /// <b>The items are handed their own DataContext, one at a time.</b> A ContextMenu hangs off a
    /// Popup, which is not in the visual tree, so what it inherits is a question with an answer
    /// nobody should have to know - the picker carries the same note for the same reason.
    ///
    /// It answers whether it opened, so the caller can tell a heading from the empty space under
    /// the last row without asking the visual tree a second time.
    /// </summary>
    internal bool OfferTheColumnMenu(object source)
    {
        if (HeadingUnder(source) is not { } heading
            || heading.Column?.SortMemberPath is not { Length: > 0 } id
            || ViewModels.Columns.Of(id) is null)
        {
            return false;
        }

        var model = new ColumnMenu(
            id,
            _columns.Choices.FirstOrDefault(choice => choice.Column.Id == id),
            () => _model.QueryText,

            // The PROPERTY rather than the field, which is the same door a chip and a keystroke go
            // through: the setter is what parses the query, applies it and tells the list.
            text => _model.QueryText = text);

        var menu = new ContextMenu { PlacementTarget = heading };

        foreach (var value in model.Values)
        {
            menu.Items.Add(new MenuItem
            {
                DataContext = value,
                Style = (Style)FindResource("ColumnValueItem")
            });
        }

        // Only when there is something above it to be separated FROM. A rule at the top of a menu
        // is a line that means nothing, and on five columns of the catalogue this item is alone.
        if (model.Values.Count > 0)
        {
            menu.Items.Add(new Separator());
        }

        var away = new MenuItem { DataContext = model, Style = (Style)FindResource("ColumnHideItem") };

        // ONE AT A TIME. A ContextMenu opened from code is not owned by anything in the visual
        // tree, so nothing closes it on our behalf - and a second right click on a second heading
        // would put a second popup on screen over the first, both live, both writing into the same
        // query box. Closing the standing one first is one line and the alternative is a window
        // that accumulates menus.
        if (_headingMenu is { } standing)
        {
            standing.IsOpen = false;
        }

        // In code rather than in the style, because a ResourceDictionary with no code behind has
        // nowhere to put an EventSetter's handler. The values above need none: a tick is bound
        // two ways to the chip, so clicking one writes the member itself.
        away.Click += (_, _) => model.Hide();

        menu.Items.Add(away);

        _headingMenu = menu;
        menu.IsOpen = true;

        return true;
    }

    /// <summary>
    /// The menu a heading opened, so that the next one can close it - and so that a test which
    /// opened one is not left with a popup standing over whatever it builds next.
    /// </summary>
    internal ContextMenu? HeadingMenu => _headingMenu;

    /// <summary>
    /// The column heading a right click landed on, or nothing.
    ///
    /// <b>It walks whichever tree the node is actually in</b>, and that is not defensive: the
    /// thing under a pointer can be a content element rather than a visual one, and
    /// <c>VisualTreeHelper.GetParent</c> throws on those. MainWindow.Menu.cs carries the same note
    /// beside the row lookup, which solves the same problem with ContainerFromElement - there is
    /// no such helper for a heading.
    /// </summary>
    private static System.Windows.Controls.Primitives.DataGridColumnHeader? HeadingUnder(object source)
    {
        var node = source as DependencyObject;

        while (node is not null and not System.Windows.Controls.Primitives.DataGridColumnHeader)
        {
            node = node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }

        return node as System.Windows.Controls.Primitives.DataGridColumnHeader;
    }
}
