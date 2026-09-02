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
        ["SurfaceRowLine"] = 1.25,

        // THE SCROLLBAR THUMB ASLEEP AND AWAKE, 2026-09-02, AND THE PAIR IS THE POINT. The bar was
        // one colour at 2.84 and the owner said it was too big and ugly - the width was half of
        // that and the brightness was the other half. At rest the thumb answers "where am I in the
        // list" for somebody who is not reaching for it: it identifies no component and competes
        // with nothing, so SC 1.4.11 does not reach it and 1.4 is ours. It has to beat the line
        // between two rows, which carries less and sits at 1.25.
        //
        // The awake colour is the one somebody can act on, so it answers to the standard rather
        // than to us: 3.79 measured, against the 3.0 of 1.4.11. The step between the two is the
        // largest of any state pair in this theme, which is what makes the wake-up readable.
        ["SurfaceScrollThumb"] = 1.4,
        ["SurfaceScrollThumbAwake"] = ForState,

        // THE ONE ENTRY IN THIS TABLE ASKING FOR NOTHING, 2026-09-02, AND THE REASON IS THAT A
        // RATIO IS THE WRONG QUESTION FOR IT. The scrim exists to REMOVE contrast - it dims the
        // window while a plan sheet is open. Asking whether it is tellable from the window would be
        // asking it to fail at the only job it has.
        //
        // What it must not do is make anything else worse, and it does the opposite: the sheet's own
        // #343434 measures 1.25 against the bare window and better than that against a window under
        // this scrim, which reads near #101010. So the check that matters is SurfacePanel's, which
        // is already in this table and already passes without any help from here.
        ["SurfaceScrim"] = 1.0,

        // THE COMMAND INSET, 2026-09-02, AND ITS NUMBER HERE IS MEASURED AGAINST THE WRONG THING -
        // which is backlog 286 showing up as an entry rather than as a sentence. This brush is only
        // ever drawn inside the plan sheet, where it reads 1.22 against #343434. Against the window
        // it reads 1.08, and that is the figure the test below takes. Floored at 1.0 rather than
        // given a number nobody measured, so this table does not pretend to have checked it.
        ["SurfaceCommandBox"] = 1.0,

        // THE DESTRUCTIVE FILL AND ITS TWO DARKER STATES, 2026-09-02, and they answer to exactly the
        // same numbers as the primary action they sit beside - the fill to SC 1.4.11 because it is
        // what identifies the control, the other two to 1.5 of ours because they only have to be
        // TELLABLE from that fill. Measured: 3.00 at rest against the window, and white on it 5.44,
        // 6.21 hovering, 7.03 pressed. Darker at every step, so the words get easier to read.
        ["SurfaceDestructiveAction"] = ForState,
        ["SurfaceDestructiveActionHover"] = 1.5,
        ["SurfaceDestructiveActionPressed"] = 1.5,

        // THE PANEL AND ITS EDGE, 2026-08-19. Ours as well, and the floors say what each one is
        // for. The fill only has to make a whole column of the window tellable as its own
        // surface - a large region, which reads at a lower ratio than any thin thing in this
        // table - so it sits beside the row line rather than beside hover. The edge is the one
        // line the panel has and separates two regions doing different jobs, so it clears more
        // than the line between two rows of the same kind.
        ["SurfacePanel"] = 1.25,
        ["SurfacePanelEdge"] = 1.5,

        // THE TWO THE PALETTE STUDY ADDED, 2026-09-01, and both answer to WCAG rather than to a
        // floor of ours - which is the point of them. The primary action's fill is a component
        // state, and a chip that is off has nothing but its outline to say it is a control at all,
        // so the outline IS the component. Both measure over 3.2 and white on either clears the
        // 4.5 text asks: 5.07 on the action, 5.10 on the chip edge.
        ["SurfacePrimaryAction"] = ForState,
        ["SurfaceChipEdge"] = ForState,

        // THE SAME BUTTON UNDER THE POINTER AND UNDER THE FINGER, 2026-09-02. Ours at 1.5, the same
        // floor and the same argument as SurfaceHover above: SC 1.4.11 covers what IDENTIFIES a
        // component, and the button was identified by the fill it has at rest - which answers to
        // 3.0 on the line above and clears it. These two only have to be TELLABLE from that rest
        // fill, which is a smaller job, and they are darker rather than lighter so white on them
        // gets easier at every step: 5.07 at rest, 6.06 hovering, 7.17 pressed.
        ["SurfacePrimaryActionHover"] = 1.5,
        ["SurfacePrimaryActionPressed"] = 1.5,

        // WCAG 2.2 SC 1.4.11 again, and THE NUMBER HERE IS THE WEAKER OF THE TWO CHECKS IT GETS.
        // The test below this table measures it against the window, where it comes out at 11.21
        // and clears anything - which would be a guard passing for the wrong reason, because a
        // ring is drawn on top of whatever state the row already has. Its real floor is the
        // worst of four surfaces and lives in its own test further down.
        ["FocusRing"] = ForState,

        // THE FOUR START TYPES, 2026-08-17. SC 1.4.11 rather than the text floor, and the
        // distinction is not a rounding: nobody reads these, they are rings beside a word that
        // carries the same answer in text. What they have to do is be TELLABLE from the window and
        // from each other, which is exactly what 1.4.11 covers.
        //
        // Being tellable from each other is a second check this table cannot make, and it is made
        // by MarkDistinctionGuards instead - a ratio against the background says nothing about two
        // marks that never appear side by side. Both are required and neither implies the other.
        ["StartBoot"] = ForState,
        ["StartSystem"] = ForState,
        ["StartAutomatic"] = ForState,
        ["StartManual"] = ForState
    };

    /// <summary>
    /// Every surface a row can be, which is what a ring drawn on that row has to be seen against.
    ///
    /// Named rather than discovered, because "every brush starting with Surface" would also
    /// sweep in the line between rows - furniture the ring is never drawn over - and a guard that
    /// quietly widens its own subject stops meaning what its name says.
    /// </summary>
    private static readonly string[] UnderTheRing =
    [
        "SurfaceSelected", "SurfaceHover", "SurfaceChanged"
    ];

    /// <summary>
    /// The surfaces somebody's words are actually drawn on - three states a row takes, the panel a
    /// plan is written in, and the three fills of the one button this product draws itself.
    /// </summary>
    private static readonly string[] CarriesText =
    [
        "SurfaceSelected", "SurfaceHover", "SurfaceChanged", "SurfacePanel", "SurfaceCommandBox",
        "SurfacePrimaryAction", "SurfacePrimaryActionHover", "SurfacePrimaryActionPressed",
        "SurfaceDestructiveAction", "SurfaceDestructiveActionHover", "SurfaceDestructiveActionPressed"
    ];

    /// <summary>
    /// Furniture. Two lines, an outline, and the two states of a scrollbar thumb - nothing is ever
    /// written on any of them, and asking whether white would read on a hairline is a question with
    /// no reader behind it.
    ///
    /// <b>It exists so that the pair is exhaustive rather than so that anything is skipped.</b>
    /// Every Surface brush has to be in one list or the other, which is what stops a text-bearing
    /// surface being added one day and quietly checked by nothing.
    /// </summary>
    private static readonly string[] CarriesNoText =
    [
        "SurfaceRowLine", "SurfacePanelEdge", "SurfaceChipEdge",
        "SurfaceScrollThumb", "SurfaceScrollThumbAwake", "SurfaceScrim"
    ];

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
            + "Put each one in CarriesText or in CarriesNoText: " + string.Join(", ", unsorted));

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

        var styles = Regex.Matches(
            text,
            @"<Style x:Key=""([^""]+)"" TargetType=""TextBlock"">(.*?)</Style>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        // A floor, like every other sweep in this project: a pattern that matched nothing would
        // clear this at once and read exactly like a theme with nothing wrong in it.
        Assert.True(styles.Count >= 8, $"Only {styles.Count} text styles found. That is the sweep being wrong.");

        var silent = styles
            .Where(style => !style.Groups[2].Value.Contains("Property=\"Foreground\"", StringComparison.Ordinal))
            .Select(style => style.Groups[1].Value)
            .ToList();

        Assert.True(
            silent.Count == 0,
            "These text styles name no colour, so they are drawn in whatever contains them: "
                + string.Join(", ", silent));
    }
}
