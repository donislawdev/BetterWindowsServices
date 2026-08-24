using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// The row that says which list the window is showing.
///
/// It holds nothing and decides nothing. Which positions there are is
/// <see cref="ViewModels.Scopes"/>, what each one means to the query language is there too, and
/// what happens when one is pressed is <see cref="ViewModels.MainViewModel.Scope"/> - so all three
/// can be checked without opening a window.
///
/// <b>A UserControl for the same reason FilterRow and SearchRow are</b> - one mechanism for "a part
/// of the window in its own file" rather than two that have to be told apart. The argument in full
/// is at the head of <see cref="FilterRow"/>.
/// </summary>
public partial class ScopeBar : UserControl
{
    public ScopeBar() => InitializeComponent();
}
