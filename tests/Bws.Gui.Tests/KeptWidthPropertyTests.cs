using System.Globalization;
using System.Windows.Controls;
using Bws.Gui.ViewModels;
using CsCheck;

namespace Bws.Gui.Tests;

/// <summary>
/// A width kept in the layout file, put through the grid - step 3b of the fuzzing project, point C5,
/// the half past the reader.
///
/// <b>The reader keeps a width as text</b> (KeptColumn says why), so whether that text is a width is
/// decided only when ListColumns builds the column. KeptWidthGuards holds six texts by hand, and
/// backlog 308(a) was the seventh nobody had thought of: 1e300, which the converter reads happily and no
/// screen can draw. This holds the rule over any text: the column gets a width it can draw, or the
/// theme's own and the column named - and never an exception out of the converter.
///
/// <b>One thread, few draws.</b> Every draw builds a grid on the window's thread, which is the cost
/// docs/PROJEKT-FUZZING-20261016.md weighed for anything WPF.
/// </summary>
public sealed class KeptWidthPropertyTests
{
    /// <summary>The widest a kept width may be - ListColumns.Widest, written again on purpose.</summary>
    private const double Widest = 10_000;

    [Fact]
    [Trait("search", "weekly")]
    public void A_kept_width_of_any_text_is_drawable_or_left_to_the_theme_and_named()
    {
        _ = WpfHost.Resources;

        Gen.Select(Gen.OneOfConst([.. Columns.All]), LayoutTexts.Width)
            .Searched(
                drawn =>
                {
                    var (column, width) = drawn;
                    var refused = new List<string>();

                    var (used, floor, theme, least) = WpfHost.On(() =>
                    {
                        var grid = new DataGrid();
                        var plan = ColumnPlan.Of(new ColumnLayout([new KeptColumn(column.Id, Shown: true, Width: width)]), EntryScope.Services);

                        refused.AddRange(ListColumns.Fill(grid, new ColumnBar(), plan));

                        var built = grid.Columns.First(each => each.SortMemberPath == column.Id);

                        return (built.Width, built.MinWidth, (DataGridLength)WpfHost.Resources[column.WidthKey], (double)WpfHost.Resources["ColumnFloor"]);
                    });

                    var expected = Drawable(width);
                    var wrong = Named(refused, expected is null ? [column.Id] : [])
                        ?? Width(used, expected ?? theme)
                        ?? Floor(used, floor, least);

                    return wrong is null ? true : throw new InvalidOperationException(wrong);
                },
                new(300) { Threads = 1, Print = drawn => $"{drawn.Item1.Id} kept as <{drawn.Item2}>" });
    }

    /// <summary>
    /// What a column given this text should get, asked of WPF's own converter and the bound a screen
    /// can show - NOT of ListColumns, so a grid that quietly took the theme's width for a good one, or
    /// fell back for a bad one without naming the column, is caught rather than agreed with. Review of
    /// #76: the first version took the list of refused columns from the product as its oracle.
    /// </summary>
    private static DataGridLength? Drawable(string width)
    {
        try
        {
            return Converter.ConvertFrom(null, CultureInfo.InvariantCulture, width) is DataGridLength length
                && double.IsFinite(length.Value) && length.Value is >= 0 and <= Widest
                ? length
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static readonly DataGridLengthConverter Converter = new();

    /// <summary>The column named exactly when its width is not drawable, and nothing else named.</summary>
    private static string? Named(List<string> refused, string[] expected) =>
        refused.SequenceEqual(expected, StringComparer.Ordinal)
            ? null
            : $"Named [{string.Join(", ", refused)}] where [{string.Join(", ", expected)}] was expected.";

    /// <summary>
    /// The width the column got is exactly the one expected - compared by value and unit, because the
    /// grid keeps its own desired and displayed values beside them.
    /// </summary>
    private static string? Width(DataGridLength used, DataGridLength expected) =>
        used.Value.Equals(expected.Value) && used.UnitType == expected.UnitType
            ? null
            : $"The column is {used.Value} {used.UnitType}, expected {expected.Value} {expected.UnitType}.";

    /// <summary>The floor under a column follows the width that was really used - ListColumns.Build says why.</summary>
    private static string? Floor(DataGridLength used, double floor, double least) =>
        floor == (used.IsAbsolute ? used.Value : least)
            ? null
            : $"The floor is {floor} under a width of {used.Value} {used.UnitType}.";
}
