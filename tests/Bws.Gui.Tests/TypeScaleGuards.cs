// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Bws.Gui.Tests;

/// <summary>
/// No text in this product is too small to read.
///
/// The second rule in docs/11-UI-UX.md that turns out to be arithmetic rather than taste, after
/// contrast. Microsoft's typography guidance gives two floors and they are different numbers:
/// <b>12 for plain text and 14 for semibold</b>, because a heavier stroke at a small size closes
/// up its own counters.
///
/// It exists because TextSizeSmall was <b>11</b> from the day this window was built until
/// 2026-08-05, and 11 carried the count line, the notice and the query error - three of the four
/// sentences the window ever says. Nobody noticed for four slices, for the reason this project
/// keeps writing down: <b>XAML is prose</b>, and a number in prose has no guard.
///
/// Same shape as ContrastGuards, and for the same reason: it reads the values out of the theme
/// rather than holding a copy of them, so it cannot drift from what it guards.
/// </summary>
public sealed class TypeScaleGuards
{
    private const double PlainFloor = 12;
    private const double SemiboldFloor = 14;

    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(5);

    [Fact]
    public void No_declared_text_size_is_below_the_floor_for_plain_text()
    {
        var sizes = Sizes();

        Assert.True(sizes.Count > 0, "No text sizes were found in the theme, so this guard checks nothing.");

        var small = sizes
            .Where(pair => pair.Value < PlainFloor)
            .Select(pair => string.Create(CultureInfo.InvariantCulture, $"  {pair.Key} is {pair.Value}"))
            .ToList();

        Assert.True(
            small.Count == 0,
            string.Create(CultureInfo.InvariantCulture, $"Text below {PlainFloor} is below the floor for readability:")
            + Environment.NewLine + string.Join(Environment.NewLine, small));
    }

    /// <summary>
    /// The half that is easy to get wrong, because 13 is fine and 13 semibold is not.
    ///
    /// Written against the WEIGHT rather than against the name of a style, so it holds for the
    /// second thing somebody sets semibold as well as for the column heading it was written for.
    /// </summary>
    [Fact]
    public void Nothing_set_semibold_is_below_the_heavier_floor()
    {
        var sizes = Sizes();
        var offenders = new List<string>();
        var checkedAny = false;

        foreach (var style in Styles())
        {
            if (!style.Contains("{StaticResource TextWeightHeader}", StringComparison.Ordinal))
            {
                continue;
            }

            checkedAny = true;

            var size = Regex.Match(
                style,
                @"Property=""FontSize""\s+Value=""\{StaticResource (\w+)\}""",
                RegexOptions.None,
                Ceiling);

            var named = size.Success ? size.Groups[1].Value : null;

            if (named is null || !sizes.TryGetValue(named, out var value) || value < SemiboldFloor)
            {
                offenders.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  a semibold style asks for {named ?? "no size of its own"}, which is not at least {SemiboldFloor}"));
            }
        }

        Assert.True(
            checkedAny,
            "Nothing in the theme is set semibold, so this guard is passing by finding nothing. If "
            + "the column heading stopped being semibold that is a change somebody made, and it "
            + "should be a decision rather than a quietly green test.");

        Assert.True(
            offenders.Count == 0,
            "Semibold text answers to a higher floor than plain text does:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>Every text size the theme declares, read out of the file.</summary>
    private static Dictionary<string, double> Sizes() =>
        Regex
            .Matches(
                Theme(),
                @"<system:Double\s+x:Key=""(TextSize\w+)""\s*>\s*([0-9.]+)\s*<",
                RegexOptions.None,
                Ceiling)
            .ToDictionary(
                match => match.Groups[1].Value,
                match => double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                StringComparer.Ordinal);

    private static IEnumerable<string> Styles() =>
        Regex
            .Matches(Theme(), @"<Style\b.*?</Style>", RegexOptions.Singleline, Ceiling)
            .Select(match => match.Value);

    /// <summary>
    /// The whole theme as one piece of text, which is what this guard has always read.
    ///
    /// <b>It has to be every part and they have to be joined, because this file asks a question
    /// that spans the seams:</b> the sizes are declared in Values.xaml and the styles that set
    /// them semibold are in the other two, so a guard reading one part alone would find sizes
    /// with nothing using them, or styles naming a size it cannot resolve. Split on 2026-08-11,
    /// backlog 156 and then 163.
    ///
    /// <b>The second split is why this mattered more than it looked.</b> The two semibold
    /// styles ended up on opposite sides of it - ColumnHeading went to List.xaml and CountLine
    /// stayed in Controls.xaml - so a version that had gone on reading two files would have
    /// checked one of them and passed.
    ///
    /// <b>WHICH IS EXACTLY WHY THE LIST IS GONE FROM 2026-08-17.</b> It named three files and the
    /// theme had four - Cells.xaml had been unread here since 2026-08-12 - and the values then
    /// split into three more. A guard that has to be edited on every split is a guard that spends
    /// part of its life not covering the thing it is named after, and the paragraph above proves
    /// that is not hypothetical. It takes the directory now, so the next seam costs it nothing.
    /// </summary>
    private static string Theme()
    {
        var themes = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes");

        return string.Join(Environment.NewLine, Directory.EnumerateFiles(themes, "*.xaml").Select(File.ReadAllText));
    }
}
