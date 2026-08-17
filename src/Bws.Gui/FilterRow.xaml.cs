using System.Windows;
using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// The row of filters, which does as little as a part of a window can.
///
/// Which chips there are and what each one writes into the query is decided in
/// <see cref="ViewModels.FilterBar"/>, so all of that can be checked without opening a window. What
/// is left here is the one thing only a control knows: that somebody pressed the button.
///
/// <b>A UserControl since 2026-08-13, owner's decision, and it removed three mechanisms.</b> While
/// this row lived in the theme its button had to be found by name at construction, refused with an
/// exception when the part was missing, and have its click taken by the window - because a theme
/// file has no code-behind class. None of that is needed here, and the theme got a hundred and
/// fifty lines back.
/// </summary>
public partial class FilterRow : UserControl
{
    public FilterRow() => InitializeComponent();

    /// <summary>
    /// The button the column picker hangs off, so the window can hand it the choices.
    ///
    /// Exposed rather than wired from in here, because WHICH columns exist belongs to the window -
    /// it owns the picker's view model, and the list is the thing the choices change.
    /// </summary>
    internal Button Picker => ColumnsButton;

    private void ChooseColumns(object sender, RoutedEventArgs e) => OpenColumns();

    /// <summary>
    /// Opens the list of columns under the button that asks for it, and says whether there was one.
    ///
    /// <b>Apart from the handler so that it can be checked at all</b> - a handler the framework
    /// calls is reachable only by clicking, and the answer is the part worth asserting: a button
    /// that opens nothing looks exactly like a feature that is not there.
    /// </summary>
    internal bool OpenColumns() => ButtonMenu.OpenUnder(ColumnsButton);
}
