using System.Globalization;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The extreme column of the catalogue: what a component is given when it is given too much.
///
/// Its own file rather than a corner of CatalogueGuards, which stands a few lines under the
/// length ceiling. The subject is narrow: the extreme sample is the one column whose CONTENT is
/// chosen per component - a long line for most, a number for the styles that draw numbers - and
/// a wrong choice there is a column that tests nothing.
/// </summary>
public sealed class CatalogueExtremeGuards
{
    /// <summary>
    /// A style that draws a number is stretched with a NUMBER, in the machine's own writing of a
    /// million - because a thousands separator is what a number column has to make room for, and
    /// a long word tests neither the separator nor the alignment. Told apart by the key, which is
    /// the one thing a style says about what its text is for.
    /// </summary>
    [Fact]
    public void A_style_that_draws_a_number_is_stretched_with_a_number_and_the_rest_with_a_line()
    {
        var entries = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .ToDictionary(entry => entry.Key, entry => entry, StringComparer.Ordinal);

        var million = 1_000_000.ToString("N0", CultureInfo.CurrentCulture);

        WpfHost.On(() =>
        {
            foreach (var key in new[] { "CountLine", "CellNumber", "OverviewNumberText", "ScopeCountText" })
            {
                Assert.True(entries.ContainsKey(key), $"{key} is not on the sheet.");
                Assert.Equal(million, ((TextBlock)entries[key].Extreme.Element!).Text);
            }

            // And a style that draws words is stretched with words, longer than any column.
            var prose = (TextBlock)entries["CellProse"].Extreme.Element!;

            Assert.True(prose.Text.Length > 100, "The prose extreme is not long enough to run past a column.");
            Assert.DoesNotContain(million, prose.Text, StringComparison.Ordinal);
        });
    }
}
