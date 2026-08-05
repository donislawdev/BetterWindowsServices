// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace Bws.Gui.Tests;

/// <summary>
/// Every colour this product declares is readable on the surface it is drawn on.
///
/// Written 2026-08-02, and it makes a liar of a sentence in docs/11-UI-UX.md. That document
/// says none of its rules can be guarded, because taste cannot be tested - which is true of ten
/// of its eleven rules and false of this one. <b>Contrast is arithmetic.</b>
///
/// It exists because nobody had done the arithmetic. When it was finally done, <b>five of ten
/// brushes were short</b>, and the worst was the red carrying the two most important sentences
/// in the tool: an untrusted signature and a query with a mistake in it, at 3.52 against the
/// 4.5 that WCAG 2.2 SC 1.4.3 asks for text.
///
/// <b>The background is not written down here.</b> It is resolved out of WPF UI's own
/// dictionary, the same brush the window paints itself with, so this guard also catches the day
/// an update to that library changes what our colours sit on. A copy of the value would go
/// stale silently, which is the failure this shape - the one audit.ps1 uses - is built to avoid.
///
/// <b>What it cannot see.</b> Whether the colour reaches a pixel at all. A brush can measure
/// 5:1 here and be painted over by a control template, which has happened in this product twice
/// in opposite directions. tools/gui-probe/check.ps1 is the instrument for that and needs a
/// screen.
/// </summary>
public sealed class ContrastGuards
{
    /// <summary>WCAG 2.2 SC 1.4.3, text against its background.</summary>
    private const double ForText = 4.5;

    /// <summary>WCAG 2.2 SC 1.4.11, a component state that has to be tellable from another.</summary>
    private const double ForState = 3.0;

    /// <summary>
    /// What each brush has to clear, and why. <b>A brush missing from this table fails the last
    /// test in this file</b>, so a new colour cannot slip in without somebody deciding what it
    /// answers to - which is the whole reason the table is here rather than a rule about names.
    ///
    /// The two floors that are not WCAG are marked as ours. SC 1.4.11 covers states needed to
    /// IDENTIFY a component: telling the selected row from the others qualifies, a pointer
    /// saying where the pointer already is does not. Calling those 3.0 would be borrowing
    /// authority the standard does not lend.
    /// </summary>
    private static readonly Dictionary<string, double> Required = new(StringComparer.Ordinal)
    {
        ["MeaningUntrusted"] = ForText,
        ["MeaningWarning"] = ForText,
        ["MeaningRunning"] = ForText,
        ["MeaningRejected"] = ForText,
        ["MeaningUnknown"] = ForText,
        ["TextSubdued"] = ForText,
        ["TextOnSurface"] = ForText,
        ["SurfaceSelected"] = ForState,

        // Ours, not WCAG's. Chosen at the point the state stops being visible at all: the
        // values these replaced measured 1.14 and 1.30.
        ["SurfaceHover"] = 1.5,
        ["SurfaceChanged"] = 1.8,

        // Ours too, and the lowest floor in the table on purpose. A line between two rows
        // IDENTIFIES NOTHING - it only has to be seen between them, which is a smaller job than
        // any state above. It also stays UNDER hover, so that pointing at a row remains the
        // stronger of the two signals rather than competing with the furniture.
        ["SurfaceRowLine"] = 1.25
    };

    [Fact]
    public void Every_declared_colour_clears_the_ratio_its_role_needs()
    {
        var background = WindowBackground();
        var short_ = new List<string>();

        foreach (var (name, colour) in Declared())
        {
            var needed = Required.TryGetValue(name, out var value) ? value : ForText;
            var ratio = Contrast(colour, background);

            if (ratio < needed)
            {
                short_.Add(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"  {name} {Hex(colour)} measures {ratio:F2} and needs {needed:F1}"));
            }
        }

        Assert.True(
            short_.Count == 0,
            $"Colours below the ratio their role requires, against the window background {Hex(background)}:"
            + Environment.NewLine + string.Join(Environment.NewLine, short_));
    }

    /// <summary>
    /// Text on a coloured row is a second pair, and it pulls the other way: lightening a
    /// surface separates it from the background and buries the white text on top of it.
    /// Checking one without the other is how a selected row becomes readable and invisible, or
    /// visible and unreadable.
    /// </summary>
    [Fact]
    public void White_text_stays_readable_on_every_surface_a_row_can_take()
    {
        var declared = Declared();
        var white = Colors.White;
        var short_ = new List<string>();

        foreach (var (name, colour) in declared.Where(pair => pair.Name.StartsWith("Surface", StringComparison.Ordinal)))
        {
            var ratio = Contrast(white, colour);

            if (ratio < ForText)
            {
                short_.Add(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"  white on {name} {Hex(colour)} measures {ratio:F2} and needs {ForText:F1}"));
            }
        }

        Assert.True(
            short_.Count == 0,
            "A row in this state carries text and the text has to be readable on it:"
            + Environment.NewLine + string.Join(Environment.NewLine, short_));
    }

    [Fact]
    public void No_colour_is_declared_without_a_ratio_it_has_to_clear()
    {
        var declared = Declared();

        Assert.True(declared.Count > 0, "No brushes were found in the theme, so the guards above check nothing.");

        var unspoken = declared
            .Select(pair => pair.Name)
            .Where(name => !Required.ContainsKey(name))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unspoken.Count == 0,
            "A colour was added to the theme without anybody saying what it has to clear. Put it "
            + "in the table in this file with the reason, rather than letting it default:"
            + Environment.NewLine + string.Join(Environment.NewLine, unspoken));
    }

    /// <summary>Every brush the theme declares, read out of the file rather than held here.</summary>
    private static List<(string Name, Color Colour)> Declared()
    {
        var theme = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes", "Theme.xaml");
        var text = File.ReadAllText(theme);

        return Regex
            .Matches(text, @"<SolidColorBrush\s+x:Key=""([^""]+)""\s*>\s*(#[0-9A-Fa-f]{6})", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(match => (match.Groups[1].Value, (Color)ColorConverter.ConvertFromString(match.Groups[2].Value)))
            .ToList();
    }

    /// <summary>
    /// What the window is actually painted with. MainWindow.xaml takes ApplicationBackgroundBrush
    /// from WPF UI, so this asks WPF UI rather than assuming a value.
    /// </summary>
    private static Color WindowBackground()
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        var theirs = new ResourceDictionary();
        theirs.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });
        theirs.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

        var brush = theirs["ApplicationBackgroundBrush"] as SolidColorBrush;

        Assert.True(
            brush is not null,
            "WPF UI no longer offers ApplicationBackgroundBrush, which is what the window paints "
            + "itself with. Every ratio in this file is measured against it.");

        return brush!.Color;
    }

    /// <summary>WCAG 2.2 relative luminance. Verbatim from the specification rather than approximated.</summary>
    private static double Luminance(Color colour)
    {
        static double Channel(byte value)
        {
            var part = value / 255.0;
            return part <= 0.04045 ? part / 12.92 : Math.Pow((part + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(colour.R)) + (0.7152 * Channel(colour.G)) + (0.0722 * Channel(colour.B));
    }

    private static double Contrast(Color first, Color second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static string Hex(Color colour) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");
}
