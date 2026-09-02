using System.Globalization;
using System.Windows.Data;

namespace Bws.Gui;

/// <summary>
/// How many cards the first screen puts in a row, given how much room there is.
///
/// <b>THE FIRST CONVERTER IN THIS PRODUCT, AND IT IS HERE BECAUSE XAML CANNOT ASK "IS THIS LESS
/// THAN THAT".</b> A trigger matches a value exactly, so a row that has to change shape below a
/// width has no markup to change it with. The alternatives were a handler on SizeChanged reaching
/// into an ItemsPanelTemplate to find a panel it cannot name, or this.
///
/// <b>IT LIVES IN THE VIEW LAYER AND NOT IN A THEME FILE, WHICH IS `docs/10` TRAP 10 OBEYED.</b> A
/// resource dictionary read from source by <c>XamlReader</c> cannot name a type from this assembly
/// - without <c>;assembly=</c> the probes resolve it against themselves, with it the markup
/// compiler refuses because the assembly does not exist yet. <c>OverviewView.xaml</c> is compiled
/// markup, so it may name this the ordinary way, and nothing loads it from source.
///
/// <b>WHAT IT IS GIVEN AND WHY EACH ONE.</b> The width is the SCROLL VIEWER'S VIEWPORT rather than
/// anything nearer the cards, and that is the difference between working and oscillating: the
/// panel's own width is decided by the column count, so feeding it back in would be a layout pass
/// arguing with itself. A viewport is decided by the window. The count is the number of cards,
/// because <see cref="System.Windows.Controls.Primitives.UniformGrid"/> given more columns than it
/// has children draws the empty ones anyway - so a row of three would go narrower, not wider, the
/// moment somebody widened the window past four cards' worth.
/// </summary>
public sealed class TilesAcross : IMultiValueConverter
{
    /// <summary>
    /// The column count, never below one and never above the number of cards there are.
    ///
    /// <b>Anything it cannot read comes back as one column</b>, which is the answer that loses the
    /// least: a single column is legible at every width, and a converter that threw here would take
    /// the first screen down over a binding that had not arrived yet.
    /// </summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [double room, int cards] || parameter is not double least)
        {
            return 1;
        }

        if (double.IsNaN(room) || double.IsInfinity(room) || least <= 0 || cards <= 0)
        {
            return 1;
        }

        var fit = (int)Math.Floor(room / least);

        return Math.Clamp(fit, 1, cards);
    }

    /// <summary>
    /// Never. A column count says nothing about the width it was worked out from, and a converter
    /// that invented one would be answering a question nobody asked.
    /// </summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("A column count does not convert back into a width.");
}
