// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

// The floors, the lists and the two WCAG numbers live in ContrastFloors.cs since 2026-09-16, and
// this brings them in under the names the tests below always used.
using static Bws.Gui.Tests.ContrastFloors;

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
/// <b>Since 2026-09-16 a floor can name the surface it is measured against</b>, and the table
/// that says which is <see cref="ContrastFloors"/>. The panel is lighter than the window, so for
/// anything drawn on it the window was the check that could not fail - backlog 286, written down
/// on 2026-09-02 and left as an entry until a hover brush for the panel needed a guard that was
/// not passing it for the wrong reason.
///
/// <b>What it cannot see.</b> Whether the colour reaches a pixel at all. A brush can measure
/// 5:1 here and be painted over by a control template, which has happened in this product twice
/// in opposite directions. tools/gui-probe/check.ps1 is the instrument for that and needs a
/// screen.
/// </summary>
public sealed class ContrastGuards
{
    [Fact]
    public void Every_declared_colour_clears_the_ratio_its_role_needs()
    {
        var background = WindowBackground();
        var declared = Declared();
        var byName = declared.ToDictionary(pair => pair.Name, pair => pair.Colour, StringComparer.Ordinal);
        var short_ = new List<string>();

        foreach (var (name, colour) in declared)
        {
            var floor = Required.TryGetValue(name, out var value) ? value : new Floor(ForText);

            // The window unless the table names the surface the brush is drawn on. A surface the
            // theme does not declare is a typo in the table, and that has to be said rather than
            // thrown, or the table could name anything and read as checked.
            Assert.True(
                floor.Against is null || byName.ContainsKey(floor.Against),
                $"{name} is measured against {floor.Against}, which the theme does not declare.");

            var beneath = floor.Against is null ? background : byName[floor.Against];
            var ratio = Contrast(colour, beneath);

            if (ratio < floor.Ratio)
            {
                short_.Add(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"  {name} {Hex(colour)} measures {ratio:F2} against {floor.Against ?? "the window"} {Hex(beneath)} and needs {floor.Ratio:F1}"));
            }
        }

        Assert.True(
            short_.Count == 0,
            "Colours below the ratio their role requires, each against the surface ContrastFloors names for it:"
            + Environment.NewLine + string.Join(Environment.NewLine, short_));
    }

    /// <summary>
    /// Text on a coloured surface is a second pair, and it pulls the other way: lightening a
    /// surface separates it from the background and buries the white text on top of it.
    /// Checking one without the other is how a selected row becomes readable and invisible, or
    /// visible and unreadable.
    ///
    /// <b>IT SWEPT EVERY BRUSH CALLED Surface UNTIL 2026-09-02 AND WAS PASSING ON LUCK.</b> A line
    /// between two rows and the outline of a chip were both being asked whether white text reads on
    /// them, and both cleared it by accident of being very dark or very pale. The scrollbar thumb
    /// added that day is the first piece of furniture dark enough to fail - #7A7A7A measures 4.29 -
    /// and nobody has ever written a word on a scrollbar.
    ///
    /// <b>So the subject is named rather than discovered, which is the repair UnderTheRing below
    /// already made for the same reason</b> - a guard that quietly widens its own subject stops
    /// meaning what its name says. The old name said "every surface a row can take", which was
    /// narrower than what it did and would have thrown away the panel and the button fills.
    ///
    /// <b>And the two lists together have to cover every Surface brush</b>, so a new one cannot be
    /// added without somebody deciding whether anything is written on it. That is the same trick
    /// the Required table plays one test further down, and for the same reason.
    /// </summary>
    [Fact]
    public void White_text_stays_readable_on_every_surface_it_is_drawn_on()
    {
        var declared = Declared();
        var white = Colors.White;
        var short_ = new List<string>();

        var surfaces = declared
            .Where(pair => pair.Name.StartsWith("Surface", StringComparison.Ordinal))
            .Select(pair => pair.Name)
            .ToList();

        var unsorted = surfaces
            .Where(name => !CarriesText.Contains(name) && !CarriesNoText.Contains(name))
            .ToList();

        Assert.True(
            unsorted.Count == 0,
            "These surfaces are in neither list, so nobody has said whether text is drawn on them. "
            + "Put each one in CarriesText or in CarriesNoText in ContrastFloors.cs: " + string.Join(", ", unsorted));

        foreach (var (name, colour) in declared.Where(pair => CarriesText.Contains(pair.Name)))
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

    /// <summary>
    /// The focus ring answers to four backgrounds rather than one, and the worst of them decides
    /// the colour.
    ///
    /// <b>This is the test the first one could not be.</b> Every other brush in this file sits on
    /// the window and is checked against it. A ring is drawn on a row that may already be
    /// selected, hovered or freshly moved, so measuring it against the window alone passes it at
    /// 11.21 while the ratio that decides whether anybody can see it is 3.62.
    ///
    /// It is also the reason the ring is as pale as it is: selected is the lightest surface a row
    /// takes, so it sets the floor for the other three.
    /// </summary>
    [Fact]
    public void The_focus_ring_clears_its_ratio_against_every_surface_a_row_can_take()
    {
        var declared = Declared().ToDictionary(pair => pair.Name, pair => pair.Colour, StringComparer.Ordinal);

        Assert.True(
            declared.ContainsKey("FocusRing"),
            "The theme declares no FocusRing, so the list shows nothing about where the keyboard "
            + "is - WCAG 2.2 SC 1.4.11, and backlog 59.");

        var ring = declared["FocusRing"];
        var short_ = new List<string>();

        foreach (var surface in UnderTheRing.Append(null))
        {
            var beneath = surface is null ? WindowBackground() : declared[surface];
            var ratio = Contrast(ring, beneath);

            if (ratio < ForState)
            {
                short_.Add(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"  on {surface ?? "the window"} {Hex(beneath)} it measures {ratio:F2} and needs {ForState:F1}"));
            }
        }

        Assert.True(
            short_.Count == 0,
            "The focus ring is drawn on a row that may already be in one of these states, so it "
            + "has to be tellable from all of them:"
            + Environment.NewLine + string.Join(Environment.NewLine, short_));
    }

    /// <summary>
    /// The line under a text field with the keyboard in it clears 3:1 against every fill a text box
    /// takes, on every surface a text box stands on - review of PR 21.
    ///
    /// <b>It was the selection blue, 3.09 against the window and 2.51 against the fill it is
    /// actually drawn on.</b> The fills are translucent white, so what the line meets is the fill
    /// composed over what the field stands on: the window for the search field, the plan sheet for
    /// the box of seconds and the box a confirmation is typed into. Resolved out of the running theme rather
    /// than read from the files, because the fills are written as attributes and
    /// <see cref="Declared"/> sees only the element form - backlog 459.
    ///
    /// <b>And the library's key has to carry the same colour</b>, since every text box but the
    /// search field draws its line through it and an alias of FocusLine does not reach that template.
    /// </summary>
    [Fact]
    public void The_focus_line_clears_its_ratio_against_every_fill_a_text_box_takes()
    {
        var line = WpfHost.Declared("FocusLine");

        Assert.Equal(line, WpfHost.Declared("TextControlFocusedBorderBrush"));

        var surfaces = new[] { ("the window", WindowBackground()), ("the plan sheet", WpfHost.Declared("SurfacePanel")) };
        var short_ = new List<string>();

        foreach (var (surface, under) in surfaces)
        {
            foreach (var fill in FillsOfATextBox)
            {
                var beneath = Composed(WpfHost.Declared(fill), under);
                var ratio = Contrast(line, beneath);

                if (ratio < ForState)
                {
                    short_.Add(string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"  {fill} on {surface} reads {Hex(beneath)}, the line measures {ratio:F2} and needs {ForState:F1}"));
                }
            }
        }

        Assert.True(
            short_.Count == 0,
            "The focus line is drawn on the field's own fill, so it has to be tellable from all of them:"
            + Environment.NewLine + string.Join(Environment.NewLine, short_));
    }

    /// <summary>The library's text box fills, all three of which Surfaces.xaml overrides.</summary>
    private static readonly string[] FillsOfATextBox =
        ["TextControlBackground", "TextControlBackgroundFocused", "TextControlBackgroundPointerOver"];

    /// <summary>A translucent colour laid over an opaque one, channel by channel, the way it reaches the pixel.</summary>
    private static Color Composed(Color over, Color under)
    {
        byte Channel(byte top, byte bottom) => (byte)Math.Round(((top * over.A) + (bottom * (255 - over.A))) / 255.0);

        return Color.FromRgb(Channel(over.R, under.R), Channel(over.G, under.G), Channel(over.B, under.B));
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
            + "in the table in ContrastFloors.cs with the reason and the surface it is measured "
            + "against, rather than letting it default:"
            + Environment.NewLine + string.Join(Environment.NewLine, unspoken));
    }

    /// <summary>
    /// Every brush the theme declares, read out of the files rather than held here.
    ///
    /// <b>EVERY THEME FILE IS READ, AND FROM 2026-08-17 THE LIST IS NOT WRITTEN DOWN HERE.</b> It
    /// used to name three, which was already one short: Cells.xaml arrived on 2026-08-12 and
    /// nothing added it, so a brush declared beside a mark's style would have had no ratio and no
    /// test noticing - the exact hole the sentence that stood here promised to close. Naming files
    /// in a guard means the guard stops covering the theme on the day somebody splits it, and this
    /// theme has now been split three times.
    ///
    /// So it takes the directory. A guard that has to be edited to keep covering what it is named
    /// after is a guard that will one day not be - the same sentence AppearanceGuards writes over
    /// its own array.
    /// </summary>
    private static List<(string Name, Color Colour)> Declared()
    {
        var themes = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes");
        var text = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(themes, "*.xaml").Select(File.ReadAllText));

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
            + "itself with. Every ratio in this file is measured against it unless ContrastFloors "
            + "names another surface.");

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

    /// <summary>
    /// Every text style in the theme names its own colour, rather than taking whatever contains it.
    ///
    /// <b>THIS GUARD IS HERE BECAUSE THE FIRST SCREEN ANYBODY SEES SHIPPED WITH BLACK TEXT ON A
    /// BLACK WINDOW.</b> Found by the owner looking at it on 2026-09-01, not by anything in this
    /// repository. <c>HeadingText</c> set a face, a size and a weight and no Foreground - so the
    /// colour of every heading was decided by whatever it happened to sit inside. In the machine
    /// overview each number sits inside a Button whose style carries no <c>BasedOn</c>, so that
    /// button never picked up the theme and fell back to the system default. Black on #202020 is
    /// a ratio of <b>1.29</b>, measured with the rest of this file's arithmetic, against the 3.0
    /// WCAG asks of large text.
    ///
    /// <b>The other tests here ask whether a declared colour is readable. This one asks whether
    /// there is a declared colour at all</b> - and the second question turns out to be the one
    /// that shipped a fault, because a style with no colour has no ratio to check and so passed
    /// every check in this file by not being in it.
    ///
    /// <b>It reads the directory rather than a list of files</b>, for the reason
    /// <see cref="Declared"/> spells out over its own sweep: a guard that has to be edited to keep
    /// covering what it is named after is a guard that one day will not.
    /// </summary>
    [Fact]
    public void Every_text_style_in_the_theme_names_the_colour_it_is_drawn_in()
    {
        var themes = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes");
        var text = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(themes, "*.xaml").Select(File.ReadAllText));

        // BasedOn STYLES INCLUDED SINCE 2026-09-16, and until that day they were invisible here:
        // the pattern asked for `TargetType="TextBlock">` with nothing between, so a style built
        // on another was never read and a colour it failed to name was never missed. A style
        // names its colour if it or something it is based on does, before the triggers - and the
        // self-closing form, an alias with nothing of its own, is a style too.
        var styles = Regex.Matches(
            text,
            @"<Style x:Key=""([^""]+)"" TargetType=""TextBlock""(?: BasedOn=""\{StaticResource (\w+)\}"")?(?:\s*/>|>(.*?)</Style>)",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        // A floor, like every other sweep in this project: a pattern that matched nothing would
        // clear this at once and read exactly like a theme with nothing wrong in it.
        Assert.True(styles.Count >= 8, $"Only {styles.Count} text styles found. That is the sweep being wrong.");

        var byKey = styles.ToDictionary(
            style => style.Groups[1].Value,
            style => (Base: style.Groups[2].Success ? style.Groups[2].Value : null, Body: style.Groups[3].Value),
            StringComparer.Ordinal);

        // A SETTER, NOT A TRIGGER - repaired 2026-09-16 by a mutation entry that came back MISSED.
        // Taking the Foreground setter off the details panel's value style left this guard green,
        // because the style's trigger for a missing value still carried the word "Foreground":
        // a colour that applies in one state is not a colour the text is drawn in. Only what
        // stands before the triggers counts.
        var silent = byKey.Keys
            .Where(key => !NamesItsColour(key, byKey, depth: 0))
            .ToList();

        Assert.True(
            silent.Count == 0,
            "These text styles name no colour, so they are drawn in whatever contains them: "
                + string.Join(", ", silent));
    }

    private static string BeforeTriggers(string body)
    {
        var triggers = body.IndexOf("<Style.Triggers>", StringComparison.Ordinal);

        return triggers < 0 ? body : body[..triggers];
    }

    /// <summary>
    /// The one text style allowed to name no colour, because its colour is the ROW'S. Every cell of
    /// the list is built on it, inside a grid row whose template sets Foreground for the row - and
    /// a cell that named its own would stop taking the row's. Its base is the library's
    /// BodyTextBlockStyle, which sets size and weight and nothing else: measured 2026-09-16 with
    /// `tools/wpfui-probe probe template BodyTextBlockStyle`, two setters, no Foreground. So the
    /// day the guard learned to follow BasedOn it found these four, and this list is the
    /// decision written down rather than the guard passing them by not looking.
    /// </summary>
    private static readonly string[] ColourFromTheRow = ["CellText"];

    private static bool NamesItsColour(string key, Dictionary<string, (string? Base, string Body)> styles, int depth)
    {
        if (ColourFromTheRow.Contains(key))
        {
            return true;
        }

        // A base this sweep did not find is the library's - a StaticResource that resolves to
        // nothing fails the dictionary load, which every window test performs - and the library's
        // text styles do not name a colour either, so it is not one named. The depth is a bound
        // rather than a cycle check: a theme with a cycle in it does not load.
        if (depth > 8 || !styles.TryGetValue(key, out var style))
        {
            return false;
        }

        return BeforeTriggers(style.Body).Contains("Property=\"Foreground\"", StringComparison.Ordinal)
            || (style.Base is { } baseKey && NamesItsColour(baseKey, styles, depth + 1));
    }

    /// <summary>
    /// No view declares a text style of its own on top of the implicit one without naming a
    /// colour - the door the guard above does not watch, and the one the details panel walked
    /// through.
    ///
    /// <b>THE PANEL ABOUT ONE ENTRY SHIPPED WITH BLACK VALUES ON #202020 FOR THIRTY-FOUR DAYS</b>,
    /// 2026-08-13 to 2026-09-16, a ratio of 1.29, and the owner found it by looking - "black
    /// text on grey, cannot be seen". The guard above had stood since 2026-09-01 for exactly this
    /// fault and never saw it, because it reads the theme, and the style that drew the values was
    /// written inside DetailsView.xaml: <c>BasedOn="{StaticResource {x:Type TextBlock}}"</c> plus
    /// one trigger for the face, and no Foreground. The implicit style names no colour either,
    /// so the values took whatever contained them, which outside the list is the system's black.
    /// tools/gui-probe/details-shot.ps1 read it off the pixels: not one pixel lighter than the
    /// ground in 113 thousand.
    ///
    /// <b>A style in a view based on a KEYED theme style is fine and is not policed</b>: every
    /// keyed text style names its colour, the guard above holds that, and the view inherits it.
    /// What is refused is basing on the implicit style - the one style in the theme with no
    /// colour, by design, because it is the density decision and nothing else.
    /// </summary>
    [Fact]
    public void No_view_draws_text_on_the_implicit_style_without_naming_a_colour()
    {
        var views = Path.Combine(SourceTree.Root(), "src", "Bws.Gui");
        var offenders = new List<string>();

        foreach (var view in Directory.EnumerateFiles(views, "*.xaml", SearchOption.TopDirectoryOnly))
        {
            var styles = Regex.Matches(
                File.ReadAllText(view),
                @"<Style TargetType=""TextBlock""\s+BasedOn=""\{StaticResource \{x:Type TextBlock\}\}"">(.*?)</Style>",
                RegexOptions.Singleline,
                TimeSpan.FromSeconds(5));

            foreach (Match style in styles)
            {
                if (!style.Groups[1].Value.Contains("Property=\"Foreground\"", StringComparison.Ordinal))
                {
                    offenders.Add(Path.GetFileName(view));
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These views draw text in a style built on the implicit one and name no colour, so the "
                + "text is whatever contains it - which outside the list is black: "
                + string.Join(", ", offenders));
    }
}
