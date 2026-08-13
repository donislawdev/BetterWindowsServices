using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The layout the window was opened with, and the layout it leaves behind.
///
/// <b>Between the file and the grid rather than in either.</b> <see cref="PreferencesFile"/> knows
/// where a file lives and how to write one without losing what was there. <see cref="ColumnLayout"/>
/// knows what a layout is and how a stale one is reconciled with the columns this build has.
/// <c>ListColumns</c> knows what a width means. What is left - reading once at startup, saying in
/// one sentence what could not be honoured, and writing the same thing back - is this class, and it
/// is the only part that has to know all three exist.
///
/// <b>WHEN IT WRITES, AND WHY IT IS NOT ONLY AT CLOSING TIME.</b> Every failure this can meet is
/// ordinary - a roaming profile on a share that went away, a folder an administrator locked down,
/// a full disk - and a window being closed has nowhere to show a sentence. So the same write also
/// happens the moment somebody turns a column on or off, which is the one layout change that
/// happens while there is still a window to say it in. The cost of that choice is stated rather
/// than hidden: a width dragged and then a process killed is a width that was never written, and
/// only a tick or an orderly close puts it on disk.
/// </summary>
internal sealed class KeptColumns
{
    private readonly PreferencesFile _file;
    private readonly LayoutReading _reading;

    internal KeptColumns(PreferencesFile file)
    {
        _file = file;
        _reading = file.Read();

        Plan = ColumnPlan.Of(_reading.Layout);
    }

    /// <summary>What the window should show, whatever the file turned out to be.</summary>
    internal ColumnPlan Plan { get; }

    /// <summary>
    /// Everything the file could not give the window, in one sentence, or nothing at all.
    ///
    /// <b>Rule 8 arriving somewhere it is easy to think it does not apply.</b> A layout quietly
    /// half applied is a window that is not the one somebody left, with no way to tell that from
    /// having imagined arranging it. Every branch here is a thing that was silently dropped in the
    /// first version of this class.
    ///
    /// The widths come in from outside because only the grid can say whether a saved width means
    /// anything - a width is text until something that knows what a width is has looked at it.
    /// </summary>
    internal string? Trouble(IReadOnlyList<string> widthsRefused)
    {
        ArgumentNullException.ThrowIfNull(widthsRefused);

        var said = new List<string>();

        if (_reading.OtherSchemaVersion is { } version)
        {
            said.Add(Texts.Of(
                "gui.layout.otherSchema", version, ColumnLayout.CurrentSchemaVersion));
        }

        if (_reading.Unreadable is { } why)
        {
            said.Add(_reading.MovedAside is { } aside
                ? Texts.Of("gui.layout.unreadable", why, aside)
                : Texts.Of("gui.layout.unreadableStays", why));
        }

        if (Plan.Ignored.Count > 0)
        {
            said.Add(Texts.Of("gui.layout.ignored", Listed(Plan.Ignored)));
        }

        if (Plan.NoneWasShown)
        {
            said.Add(Texts.Of("gui.layout.noneShown"));
        }

        if (widthsRefused.Count > 0)
        {
            said.Add(Texts.Of("gui.layout.widths", Listed(widthsRefused)));
        }

        return said.Count == 0 ? null : string.Join(" ", said);
    }

    /// <summary>
    /// Ties the layout to the window, so that it follows what somebody does to it.
    ///
    /// <b>Here rather than in the window, and that is the same split every other decision in this
    /// product makes.</b> The window's job is introducing the parties. WHEN a layout is worth
    /// writing down is a decision, it has an argument behind it - the one this class opens with -
    /// and it belongs beside the writing rather than beside the event handlers.
    /// </summary>
    internal void Watch(Window window, DataGrid grid, ColumnBar bar, Says says)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(bar);

        bar.Changed += (_, _) => Keep(grid, says);

        // Closing rather than Closed: the columns still belong to a live window here, and their
        // widths and their order are read off them. This is also the write that has nowhere to
        // report a failure, which is the whole reason the line above exists.
        window.Closing += (_, _) => Keep(grid, says);
    }

    /// <summary>
    /// Writes down what the grid looks like now, and says in the window what stopped it.
    ///
    /// <b>The whole layout every time, rather than the part that moved.</b> Working out what
    /// changed would need a second copy of the state to compare against, and a file of eighteen
    /// short lines costs less to write than that copy costs to keep correct.
    /// </summary>
    private void Keep(DataGrid grid, Says says)
    {
        if (_file.Write(ListColumns.Harvest(grid)) is { } trouble)
        {
            says.AboutTheLayout(Texts.Of("gui.layout.notKept", trouble));
        }
    }

    /// <summary>
    /// Identifiers as a person sees them in the file, which is the point of naming them at all.
    ///
    /// Not translated, and that is deliberate: the sentence is about a line in a file they can
    /// open, so it has to use the words that are in it. A heading would send somebody looking for
    /// "Display name" in a file that says <c>displayName</c>.
    /// </summary>
    private static string Listed(IReadOnlyList<string> identifiers) =>
        string.Join(", ", identifiers);
}
