using System.Windows;
using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// A template column whose cell holds the template's own content, rather than a presenter showing
/// it - backlog 222.
///
/// <b>WHAT IT REMOVES, MEASURED RATHER THAN REASONED.</b> Broken down element by element with
/// tools/gui-probe/row-cost.ps1, a plain text cell is three elements and one binding -
/// DataGridCell, our ContentPresenter, the TextBlock. A cell from a template column was six and
/// two, and the extra pair was not the mark: it was a SECOND ContentPresenter that
/// <c>DataGridTemplateColumn.GenerateElement</c> builds and binds Content on with an empty Binding,
/// which our own cell template then hosts inside its presenter. One wrapper inside another, three
/// of them on a row with all three mark columns on.
///
/// <b>WHY THIS IS SAFE FOR THE BINDINGS INSIDE THE TEMPLATE.</b> The content becomes the cell's
/// Content, and a FrameworkElement placed there inherits DataContext down the visual tree from the
/// cell, whose DataContext is the row. So <c>{Binding [status]}</c> resolves exactly as it did
/// through their presenter - what their empty Binding did explicitly, inheritance does for nothing.
///
/// <b>AND WHY IT IS SAFE UNDER RECYCLING, WHICH IS THE HALF WORTH CHECKING.</b> The list reuses its
/// containers, so a cell built once is shown against many rows. Nothing here holds a row: the tree
/// is built when the cell is, and every binding in it re-reads when the DataContext changes
/// underneath. That is the same mechanism their presenter relied on, one element higher up.
/// Measured after scrolling rather than assumed - the count per row does not move, which it would
/// if this were rebuilding trees instead of reusing them.
///
/// Editing is left to the base class on purpose. This list does not edit in place, and a column
/// that quietly lost its editing element would be a worse trade than three elements are worth.
/// </summary>
internal sealed class UnwrappedColumn : DataGridTemplateColumn
{
    /// <summary>
    /// The template's content, or the base class's presenter when there is no template.
    ///
    /// The fallback is not defensive noise. <see cref="DataGridTemplateColumn.CellTemplate"/> is
    /// settable and nullable, so a column built without one has to keep behaving the way the
    /// framework says it does rather than throwing from inside a cell being realised - which is a
    /// place an exception is very hard to read.
    /// </summary>
    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem) =>
        CellTemplate?.LoadContent() is FrameworkElement content
            ? content
            : base.GenerateElement(cell, dataItem);

    /// <summary>
    /// What this column puts in a cell, asked directly.
    ///
    /// <b>A seam because <see cref="GenerateElement"/> is protected and the class is sealed</b>, so
    /// nothing could otherwise check the one thing that matters here - that what lands in the cell
    /// is the template's own root and not a presenter wrapped around it. The same split as
    /// CellTips.TipFor and MainWindow.Moved, for the same reason.
    /// </summary>
    internal FrameworkElement Content(DataGridCell cell, object item) => GenerateElement(cell, item);
}
