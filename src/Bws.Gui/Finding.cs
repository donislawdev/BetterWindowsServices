using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// Where Ctrl+F puts the caret - the press is the window's (MainWindow.Act decides it beside every other
/// shortcut), and this is the one thing it then does. Out of Act since 2026-10-20, when the views of the
/// audit got searches of their own (AS-a) and the choice of box would have taken that method over its
/// share of the shape ceiling.
/// </summary>
internal static class Finding
{
    /// <summary>
    /// Puts the caret in the search somebody can see, with what is there already selected.
    ///
    /// <b>Selected, not just focused.</b> Ctrl+F in every other program starts a new search rather than
    /// appending to the last one, and a person who wanted to keep the old text still has it - one key
    /// press away, unselected by typing nothing.
    ///
    /// <b>UNDER A COMPARISON OR THE CHANGE HISTORY, THEIR OWN SEARCH</b> (AS-a). The list's box is
    /// collapsed there, so until then Ctrl+F put the caret nowhere. Asked of the model rather than of the
    /// boxes' visibility, because a window the tests build is never shown and answers false about the
    /// visibility of everything.
    ///
    /// <b>ON THE LIST, A PERSON HAS ARRIVED</b>, which the list under the box wants to know: an empty box
    /// offers the questions that fit the list on screen, a box with text in it - selected, so nothing to
    /// complete - offers nothing. Said here rather than left to the focus event, because a box that
    /// already had the keyboard gets no focus event from this. <b>Not on the overview screen</b>, where
    /// the row holding the box is collapsed: a popup hung off a collapsed target opens somewhere on the
    /// screen with nothing under it, and nothing would close it but Escape.
    /// </summary>
    /// <remarks>
    /// <b>ON THE TOOLS TAB, BACK TO THE LIST FIRST</b>, since 2026-10-20: the page has nothing to search yet,
    /// and Ctrl+F is a person asking for the list's box. Leaving it shows the box, and the window gives it the
    /// keyboard once it is laid out (GiveTheBoxTheKeyboard) - so the list under the box is not opened by this
    /// press, because a popup hung off a row that has not been laid out yet opens somewhere with nothing under it.
    /// </remarks>
    internal static void Begin(MainViewModel model, SearchRow list, ShowRow history, ShowRow comparison)
    {
        var leftTheTools = model.Tabs.LeaveTheTools();

        if (model.MiddleChoices is { Offered: true })
        {
            var box = model.History.InTheMiddle ? history.Box : comparison.Box;

            box.Focus();
            box.SelectAll();

            return;
        }

        list.Box.Focus();
        list.Box.SelectAll();

        if (model.ListOnScreen && !leftTheTools)
        {
            model.Suggesting.Arrived(list.Box.Text);
        }
    }
}
