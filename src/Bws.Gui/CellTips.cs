using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The tooltip a cell shows when its text did not fit, worked out when somebody asks for it.
///
/// <b>WHY THIS EXISTS AT ALL: it replaces a binding per cell with nothing per cell.</b> Until
/// 2026-08-25 `CellText` carried <c>ToolTip="{Binding RelativeSource=Self, Path=Text}"</c>, so every
/// TextBlock in every realised row held a live binding listening to its own Text - counted with
/// tools/gui-probe/row-cost.ps1 at 43 of the 46 bindings a row carries with twenty columns on, about
/// half of them these. They are paid for on every scroll, to serve a tooltip somebody asks for once
/// a minute.
///
/// <b>A CLASS HANDLER, AND THE OBVIOUS VERSION DOES NOT WORK.</b> The obvious version attaches one
/// handler to the grid and lets the event bubble up from the cells. Measured rather than assumed:
/// <c>ToolTipService.ToolTipOpening</c> is a <b>Direct</b> routed event, so a handler on the grid
/// would never fire and the list would quietly show a blank tooltip for ever. Registering a class
/// handler for TextBlock costs one registration for the whole process and still reaches every one of
/// them. `ScrollViewer.ScrollChanged`, used next door in MainWindow for the same kind of job, IS
/// bubbling - which is why the two look different and why both were checked.
///
/// <b>AND IT ONLY SPEAKS WHEN THE TEXT ACTUALLY GAVE WAY.</b> That is not a saving, it is the rule
/// `Cells.xaml` already states: a tooltip is there because text that gives way has to be gettable.
/// A tooltip over a fully visible "Stopped" repeats what is already on screen and covers the row
/// under it.
///
/// <b>THE SINGLE SPACE IN THE CellText STYLE IS WHAT ARMS ALL OF THIS</b>, and it is never seen by
/// anybody. The service ignores an element whose ToolTip is null, so there has to be something
/// there before the opening event can fire and put the real text in - a single space rather than an
/// empty string, because an empty one is treated as absent. The value lives in Cells.xaml, where
/// the style is. Until 2026-09-23 a constant here held a second copy of it that nothing read, which
/// is how this paragraph came to sit on a line of code the window never used.
/// </summary>
internal static class CellTips
{
    /// <summary>
    /// How much wider than its box the text has to want to be before this counts as trimmed.
    ///
    /// Text measured one way and arranged another agree to within a rounding error, and without a
    /// margin the last character of a perfectly fitting string can put a tooltip on screen.
    /// </summary>
    private const double Slack = 0.5;

    private static bool armed;

    /// <summary>
    /// Registers the one class handler that serves every cell, once.
    ///
    /// <b>Called rather than left to a static constructor, because nothing else here would ever
    /// touch this type.</b> No markup names it and no other code calls into it, so a type
    /// initialiser would simply never run and every tooltip in the list would show a blank box.
    /// </summary>
    internal static void Arm()
    {
        if (CellTips.armed)
        {
            return;
        }

        CellTips.armed = true;

        EventManager.RegisterClassHandler(
            typeof(TextBlock),
            ToolTipService.ToolTipOpeningEvent,
            new ToolTipEventHandler(Opening));
    }

    /// <summary>
    /// Fills the tooltip with the text a moment before it is shown, or refuses to show one.
    ///
    /// <b>Assigned on every opening rather than once, and that is what makes it safe under
    /// recycling.</b> The list reuses its TextBlocks for different rows, so a tooltip written once
    /// would go on offering the value of whichever row held that container before. Writing it here
    /// means what is shown is what the cell says at the moment somebody asked.
    /// </summary>
    private static void Opening(object sender, ToolTipEventArgs opening)
    {
        if (sender is not TextBlock cell)
        {
            return;
        }

        // ONLY TEXT THAT CAN GIVE WAY, AND ONLY WHERE NOTHING ELSE ANSWERS FOR THE TOOLTIP. The
        // first half is the rule itself - a tooltip is here because text may be trimmed. The second
        // keeps this off the column headings, which bind theirs to the heading and are built once
        // per column rather than once per row, so they were never what this came to save.
        if (cell.TextTrimming != TextTrimming.CharacterEllipsis
            || BindingOperations.IsDataBound(cell, FrameworkElement.ToolTipProperty))
        {
            return;
        }

        if (CellTips.TipFor(cell) is not { } tip)
        {
            // Handled is how ToolTipService is told not to show one. There is no other way to say
            // no once the service has decided to open.
            opening.Handled = true;

            return;
        }

        cell.ToolTip = tip;
    }

    /// <summary>
    /// What this cell should offer, or nothing if it should offer nothing.
    ///
    /// <b>Separate from the handler so that the DECISION can be tested.</b> The handler itself
    /// cannot be: <c>ToolTipEventArgs</c> has no public constructor, so no test can raise the event
    /// that reaches it. Splitting the judgement out leaves the untestable part down to one
    /// assignment and one Handled.
    ///
    /// <b>A SECOND REASON TO SPEAK, SINCE 2026-09-15: THE CELL TRANSLATED WHAT IT SHOWS.</b> The
    /// account cell says "Local Service" over a value the manager holds as
    /// <c>NT AUTHORITY\LocalService</c>, and that spelling is what <c>account:</c> in the box above
    /// matches - so a person who reads the cell and types it gets an empty list unless something
    /// tells them. The column says what it holds (<see cref="Column.Holds"/>), and it comes first:
    /// a short name never gives way, and a name that did would still owe its spelling more than its
    /// own tail.
    /// </summary>
    internal static string? TipFor(TextBlock cell)
    {
        if (CellTips.HeldBehind(cell) is { } held)
        {
            return Texts.Of("gui.cell.held", held);
        }

        return CellTips.Trimmed(cell) ? cell.Text : null;
    }

    /// <summary>
    /// What the machine holds behind this cell's text, or nothing when the text is that already.
    ///
    /// <b>The column is found by walking up to the cell that owns this text</b>, and the row is the
    /// text's own data context - both are known only at the moment of asking, which is the same
    /// moment recycling makes safe: whatever row this TextBlock served a scroll ago, the one it
    /// serves now is the one under the pointer.
    ///
    /// Visual parents only, and that is safe because everything between a TextBlock and its
    /// DataGridCell is a visual - the walk that throws on a content element is the one
    /// <c>PointAtRowBeforeMenu</c> avoids, going the other way from an arbitrary source.
    /// </summary>
    private static string? HeldBehind(TextBlock cell)
    {
        if (cell.DataContext is not EntryRow row)
        {
            return null;
        }

        DependencyObject? host = cell;

        while (host is not null and not DataGridCell)
        {
            host = VisualTreeHelper.GetParent(host);
        }

        return host is DataGridCell { Column.SortMemberPath: { } id }
            && Columns.Of(id)?.Holds is { } holds
            ? holds(row.Entry)
            : null;
    }

    /// <summary>
    /// Whether the text wants more room than it was given.
    ///
    /// <b>Measured with FormattedText rather than by calling Measure on the element.</b> Measuring a
    /// live element mid-layout is how a probe changes the thing it is asking about - the same
    /// argument tools/gui-probe/columns already makes, and it reads the width the same way.
    ///
    /// This runs when a person hovers, which is once in a while and never in a scroll, so its cost
    /// does not belong to any of the budgets in `docs/04`.
    /// </summary>
    private static bool Trimmed(TextBlock cell)
    {
        if (string.IsNullOrEmpty(cell.Text))
        {
            return false;
        }

        var wanted = new FormattedText(
            cell.Text,
            CultureInfo.CurrentCulture,
            cell.FlowDirection,
            new Typeface(cell.FontFamily, cell.FontStyle, cell.FontWeight, cell.FontStretch),
            cell.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(cell).PixelsPerDip);

        return wanted.Width > cell.ActualWidth + CellTips.Slack;
    }
}
