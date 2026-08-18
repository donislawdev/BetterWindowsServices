using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// What the middle of the window says when the list has no rows.
///
/// <b>The emptiest part of the window has the emptiest file, and that is the point rather than an
/// accident.</b> Which sentence appears, and whether there is a way out to offer beside it, is
/// worked out in <see cref="ViewModels.ListState"/> - five states, one of which is a machine that
/// handed over nothing, which is not the same as a query that matched nothing. None of that needs
/// a window to check, and none of it belongs here.
///
/// <b>A UserControl rather than a template in the theme.</b> A theme file describes the look of one
/// control - a chip, a cell, a heading - and this is a part of the window, like the list, the row
/// of filters and the details panel. The criterion is backlog 183 and this is the second case it
/// has been applied to.
///
/// <b>No behaviour at all, so the two members below are the whole class and neither of them does
/// anything.</b> This panel takes no clicks and no keys - the markup turns off the three defaults a
/// ContentControl arrives with - so there is no handler here and nothing to decide.
/// </summary>
public partial class EmptyState : UserControl
{
    public EmptyState() => InitializeComponent();

    /// <summary>
    /// The sentence itself, so a test can read what a person would read.
    ///
    /// <b>Exposed for the same reason the row of filters exposes its button: a binding that resolves
    /// to nothing is silent.</b> Nothing in a build, and nothing on a screenshot either - a dead
    /// binding here paints an empty rectangle in the middle of the window, which is exactly the
    /// complaint this panel was built to answer. The view model side is already covered without a
    /// window, and this is the one question that cover cannot ask.
    /// </summary>
    internal TextBlock Sentence => MessageText;

    /// <summary>
    /// The line under it that names the two keys, which needs its own way in.
    ///
    /// <b>Its visibility is a trigger of its own, so the panel being on the window says nothing
    /// about this line.</b> It is empty while the list is loading and on a machine with nothing on
    /// it, and offering a way out of neither of those would be the window being confidently wrong.
    /// Two separate answers, so two members.
    /// </summary>
    internal TextBlock WayOut => WayOutText;
}
