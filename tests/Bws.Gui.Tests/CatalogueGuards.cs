// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml.Linq;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The catalogue holds nothing the window does not have, and leaves out nothing it does.
///
/// <b>This is rule 4 of the owner's GUI rules, and it is the only reason the catalogue is worth
/// building.</b> A gallery of components is easy. A gallery that is still true a year later, after
/// an interface has been rebuilt around it, needs something that goes red the day it stops
/// matching - the rule says so in as many words: the catalogue may not contain components the
/// application does not have, nor leave out ones it does.
///
/// <b>Read off disk rather than out of the merged dictionary, on the left hand side.</b> Comparing
/// what <see cref="Catalogue"/> found against what the same dictionary holds would be the tool
/// agreeing with itself. The theme FILES are the declaration, so they are the side this reads,
/// with an XML parser rather than a regular expression: a style nested inside a control template
/// carries a key too, and is not a component anybody can put on a sheet.
/// </summary>
public sealed class CatalogueGuards
{
    /// <summary>Where the theme files live.</summary>
    private static string Themes => Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes");

    /// <summary>
    /// Every keyed style AND every keyed data template a theme file declares AT THE TOP LEVEL,
    /// which is what a component is.
    ///
    /// <b>Templates since 2026-09-16.</b> The row under the search box IS SuggestionRow and a
    /// filter group IS FilterGroupTemplate - a sheet that counted styles alone was complete about
    /// half of what the window is made of. Catalogue.xaml's own templates are left out on both
    /// sides: they are the sheet, and a sheet drawing its own row template inside its own row
    /// template is a mirror.
    /// </summary>
    private static IReadOnlyList<string> Declared()
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var keys = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Themes, "*.xaml"))
        {
            var root = XDocument.Load(file).Root;

            if (root is null)
            {
                continue;
            }

            var sheet = string.Equals(Path.GetFileName(file), "Catalogue.xaml", StringComparison.OrdinalIgnoreCase);

            keys.AddRange(root.Elements()
                .Where(element => element.Name.LocalName == "Style" || (element.Name.LocalName == "DataTemplate" && !sheet))
                .Select(element => element.Attribute(x + "Key")?.Value)
                .Where(key => key is not null)
                .Select(key => key!));
        }

        return keys;
    }

    [Fact]
    public void The_catalogue_leaves_out_no_component_the_window_declares()
    {
        var declared = Declared();
        var shown = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        var missing = declared.Where(key => !shown.Contains(key)).OrderBy(key => key, StringComparer.Ordinal).ToArray();

        Assert.True(
            missing.Length == 0,
            $"{missing.Length} component(s) are declared in a theme file and are not on the catalogue "
            + $"sheet: [{string.Join(", ", missing)}]. Rule 4 of the GUI rules says the catalogue may "
            + "not leave out a component the application has. Nothing about the catalogue is written "
            + "by hand, so a component missing from it means Catalogue.Read stopped seeing a file - "
            + "check that the dictionary is merged in App.xaml and named in WpfHost.ThemeFiles.");
    }

    [Fact]
    public void The_catalogue_shows_nothing_the_window_does_not_declare()
    {
        var declared = Declared().ToHashSet(StringComparer.Ordinal);
        var shown = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .Select(entry => entry.Key)
            .ToArray();

        var invented = shown.Where(key => !declared.Contains(key)).OrderBy(key => key, StringComparer.Ordinal).ToArray();

        Assert.True(
            invented.Length == 0,
            $"The catalogue shows {invented.Length} component(s) no theme file declares: "
            + $"[{string.Join(", ", invented)}]. The other half of rule 4 - a catalogue with an extra "
            + "component in it sends somebody looking for a style that is not there.");
    }

    [Fact]
    public void Every_component_either_draws_a_sample_or_says_why_not()
    {
        var silent = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .Where(entry => entry.Normal.Element is null && string.IsNullOrWhiteSpace(entry.Normal.Instead))
            .Select(entry => entry.Key)
            .ToArray();

        Assert.True(
            silent.Length == 0,
            $"{silent.Length} component(s) draw nothing and say nothing: [{string.Join(", ", silent)}]. "
            + "An empty cell on that sheet reads as a component that draws nothing, which is a claim "
            + "about the product rather than about this screen.");
    }

    /// <summary>
    /// <b>The half of the design that has to keep working after the interface is rebuilt.</b>
    ///
    /// Samples are built from a table of TARGET TYPES rather than from a list of keys, so a new
    /// style over a control this table already knows appears on the sheet with no edit at all.
    /// A style over a control type nothing has used yet is the case that needs a hand, and this is
    /// what makes it a red test rather than a row nobody notices reading "nothing here knows how
    /// to build a Slider to look at".
    /// </summary>
    [Fact]
    public void No_component_is_on_the_sheet_only_because_nothing_can_build_it()
    {
        var unbuildable = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .Where(entry => entry.Normal.Instead.Contains("nothing here knows how to build", StringComparison.Ordinal))
            .Select(entry => $"{entry.Key} ({entry.Target})")
            .ToArray();

        Assert.True(
            unbuildable.Length == 0,
            $"{unbuildable.Length} component(s) target a control the catalogue cannot make: "
            + $"[{string.Join(", ", unbuildable)}]. Add the type to the table in Catalogue.Make, or "
            + "to WhyNot with the reason it cannot stand on its own. Both are one line, and either "
            + "is better than a sheet that quietly stops covering a part of the window.");
    }

    /// <summary>
    /// App.xaml and the test host merge the same theme files, in the same order.
    ///
    /// <b>Nothing compared these until 2026-09-10 and the two halves fail differently, which is
    /// what makes the gap worth a test.</b> A file named here and not in App.xaml means the whole
    /// suite builds a window subtly unlike the one that ships - and it does not throw, because a
    /// missing style is a control drawn with the library's default. A file named in App.xaml and
    /// not here throws at parse time, loudly, in whichever test touched it first.
    ///
    /// Order as well as membership: every styles file resolves names declared in the file before
    /// it, which App.xaml says about itself three times in comments.
    /// </summary>
    [Fact]
    public void The_test_host_merges_the_same_theme_files_as_the_application()
    {
        var app = File.ReadAllText(Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "App.xaml"));

        // A timeout, because the analyser asks for one on every expression and is right to: a
        // pattern with no deadline on input nobody controls is a run that never ends. This one
        // reads a file in this repository and could not take a second, so the number is a ceiling
        // rather than a budget.
        var inApp = Regex.Matches(app, @"Source=""Themes/([^""]+)""", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(hit => hit.Groups[1].Value)
            .ToArray();

        Assert.True(
            inApp.Length > 10,
            $"Only {inApp.Length} theme dictionaries were found in App.xaml. That is this test being "
            + "wrong about how they are written, not the window losing its look.");

        Assert.Equal(inApp, WpfHost.ThemeFiles);
    }

    /// <summary>
    /// The fourth column, since 2026-09-15: a component whose style triggers on
    /// <c>Validation.HasError</c> is drawn wrong on the sheet, and one whose style does not gets
    /// a dash rather than a sample of a state it was never put into.
    ///
    /// <b>The declaration side is read out of the theme files with an XML parser, like
    /// <see cref="Declared"/>, and the sheet side out of the catalogue's own model</b> - so the two
    /// can disagree, which is what a guard is for. Both directions are held: a wrong state the sheet
    /// misses is rule 4 broken, and a wrong sample of something that cannot be wrong is a claim
    /// about the product the product does not make.
    /// </summary>
    [Fact]
    public void Every_component_that_can_be_wrong_is_shown_wrong_on_the_sheet()
    {
        var canBeWrong = StylesThatCanBeWrong();

        // A guard that is satisfied by nobody being able to be wrong is the absence-shaped pass
        // this file warns about twice already. The box of seconds has been wrong-able since the day
        // this column arrived, and the search box since 2026-09-23 through the style they share.
        Assert.Contains("WaitingBox", canBeWrong);
        Assert.Contains("ReportingBox", canBeWrong);

        var entries = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .ToArray();

        var missing = entries
            .Where(entry => canBeWrong.Contains(entry.Key) && entry.Wrong.Element is null)
            .Select(entry => $"{entry.Key}: {entry.Wrong.Instead}")
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"{missing.Length} component(s) can be wrong and the sheet does not show them wrong: "
            + $"[{string.Join(", ", missing)}]. GUI rule 3 names the error state and rule 4 says "
            + "the catalogue shows every state a component has.");

        var invented = entries
            .Where(entry => !canBeWrong.Contains(entry.Key) && entry.Wrong.Element is not null)
            .Select(entry => entry.Key)
            .ToArray();

        Assert.True(
            invented.Length == 0,
            $"{invented.Length} component(s) are drawn wrong on the sheet and have no wrong state "
            + $"in their style: [{string.Join(", ", invented)}].");
    }

    /// <summary>
    /// Every keyed style whose triggers - its own, or inherited through BasedOn - include one on
    /// <c>Validation.HasError</c>, read out of the theme files with an XML parser.
    ///
    /// <b>UP THE BasedOn CHAIN, SINCE 2026-09-23</b> - the catalogue walks it (Catalogue.States), so
    /// the guard has to as well, or the two disagree about a style that inherits its wrong state.
    /// That day the box of seconds became WaitingBox BasedOn ReportingBox, and read only by its own
    /// triggers it stopped being able to be wrong while the window still drew it red.
    /// </summary>
    private static HashSet<string> StylesThatCanBeWrong()
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var canBeWrong = new HashSet<string>(StringComparer.Ordinal);
        var basedOn = new Dictionary<string, string>(StringComparer.Ordinal);

        var styles = Directory.EnumerateFiles(Themes, "*.xaml")
            .Select(file => XDocument.Load(file).Root)
            .Where(root => root is not null)
            .SelectMany(root => root!.Elements().Where(element => element.Name.LocalName == "Style"));

        foreach (var style in styles)
        {
            if (style.Attribute(x + "Key")?.Value is not { } key)
            {
                continue;
            }

            if (style.Descendants()
                    .Where(element => element.Name.LocalName == "Trigger")
                    .Any(trigger => trigger.Attribute("Property")?.Value == "Validation.HasError"))
            {
                canBeWrong.Add(key);
            }

            var parent = Regex.Match(style.Attribute("BasedOn")?.Value ?? string.Empty, @"^\{StaticResource (\w+)\}$", RegexOptions.None, TimeSpan.FromSeconds(5));

            if (parent.Success)
            {
                basedOn[key] = parent.Groups[1].Value;
            }
        }

        ThroughBasedOn(canBeWrong, basedOn);

        return canBeWrong;
    }

    /// <summary>
    /// A style is wrong-able when its parent is. Repeated until a pass adds nothing, so a chain of
    /// any length reaches its root whatever order the files were read in.
    /// </summary>
    private static void ThroughBasedOn(HashSet<string> canBeWrong, Dictionary<string, string> basedOn)
    {
        int added;

        do
        {
            added = 0;

            foreach (var (child, parent) in basedOn)
            {
                if (canBeWrong.Contains(parent) && canBeWrong.Add(child))
                {
                    added++;
                }
            }
        }
        while (added > 0);
    }

    /// <summary>
    /// <b>A sample on the sheet puts at least one pixel on a bitmap - GUI rule 10, and the sheet
    /// shipped three blank cells for six days on the strength of a measurement.</b>
    ///
    /// AgainstMark, StartMark and FilterDisclosure all measure to their token size and draw
    /// nothing - two are hidden until a row's state shows them, one takes its colour from the
    /// button around it - so "has a size" passed them and the sheet showed three empty cells,
    /// which read as three components that draw nothing. Since 2026-09-16 the model renders each
    /// sample on the way in and turns a blank one into a sentence. This renders them AGAIN, with
    /// its own code, so that a model that lets a blank through - a render that threw and was read
    /// as "could not ask", say - is caught by something that did not share its mistake.
    ///
    /// Alpha rather than a colour count: a path with no geometry is zero pixels of any opacity,
    /// while a panel-coloured border on a panel with text in it is two colours and paints.
    /// </summary>
    [Fact]
    public void Every_drawn_sample_puts_a_pixel_on_the_sheet()
    {
        var blank = WpfHost.On(() =>
        {
            var found = new List<string>();

            // The same two bounds the sheet draws to, read from the theme rather than written here.
            var widest = (double)WpfHost.Resources["WidthCatalogueExtreme"];
            var tallest = (double)WpfHost.Resources["HeightCatalogueTall"];

            foreach (var group in Catalogue.Read(WpfHost.Resources))
            {
                foreach (var entry in group.Entries)
                {
                    if (entry.Normal.Element is not { } element)
                    {
                        continue;
                    }

                    element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                    var width = (int)Math.Ceiling(Math.Min(element.DesiredSize.Width, widest));
                    var height = (int)Math.Ceiling(Math.Min(element.DesiredSize.Height, tallest));

                    if (width <= 0 || height <= 0)
                    {
                        found.Add(entry.Key + " (no size)");

                        continue;
                    }

                    element.Arrange(new Rect(0, 0, width, height));

                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                        width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);

                    bitmap.Render(element);

                    var pixels = new byte[width * 4 * height];
                    bitmap.CopyPixels(pixels, width * 4, 0);

                    var painted = false;

                    for (var at = 3; at < pixels.Length && !painted; at += 4)
                    {
                        painted = pixels[at] != 0;
                    }

                    if (!painted)
                    {
                        found.Add(entry.Key);
                    }
                }
            }

            return found;
        });

        Assert.True(
            blank.Count == 0,
            $"{blank.Count} sample(s) on the sheet draw no pixel at all: [{string.Join(", ", blank)}]. "
            + "A cell that shows nothing is a claim that the component draws nothing - say why instead.");
    }

    /// <summary>
    /// The three components that draw nothing alone are on the sheet as a SENTENCE, not as a
    /// blank - and the sentence is the one the pixel check writes, so that the guard above is
    /// known to be guarding something that really happens. A sheet on which nothing ever needed
    /// the sentence would be the absence-shaped pass this file warns about.
    /// </summary>
    [Fact]
    public void A_component_that_draws_nothing_alone_says_so_rather_than_standing_blank()
    {
        var entries = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .ToDictionary(entry => entry.Key, entry => entry.Normal, StringComparer.Ordinal);

        foreach (var key in new[] { "AgainstMark", "StartMark", "FilterDisclosure" })
        {
            Assert.True(entries.ContainsKey(key), $"{key} is not on the sheet at all.");
            Assert.Null(entries[key].Element);
            Assert.Contains("draws nothing on its own", entries[key].Instead, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>Every keyed template is drawn over a specimen, and the extreme one differs from the
    /// ordinary one.</b> A template whose data shape this sheet does not know is a red build, not
    /// a row saying "nothing here knows what data X draws" - the same rule the factory table
    /// holds for styles, and the reason a new template is a new row in Catalogue.Specimens.cs.
    /// </summary>
    [Fact]
    public void Every_template_is_drawn_over_a_specimen_of_its_own_data()
    {
        var templates = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .Where(entry => entry.Target == "DataTemplate")
            .ToArray();

        // Eleven on the day this was written. Fewer means a file stopped being read - the
        // absence-shaped pass this file warns about - so the count is held, not just the shape.
        Assert.True(templates.Length >= 11, $"Only {templates.Length} template(s) reached the sheet.");

        var unknown = templates.Where(entry => entry.Normal.Element is null).Select(entry => $"{entry.Key}: {entry.Normal.Instead}").ToArray();

        Assert.True(
            unknown.Length == 0,
            $"{unknown.Length} template(s) have no specimen to be drawn over: [{string.Join(", ", unknown)}]. "
            + "Add a row to Catalogue.TemplateSpecimens - a template drawn over nothing is a blank cell.");

        var same = templates.Where(entry => entry.Extreme.Element is null).Select(entry => entry.Key).ToArray();

        Assert.True(same.Length == 0, $"{same.Length} template(s) have no extreme specimen: [{string.Join(", ", same)}].");
    }

    /// <summary>
    /// The chosen column holds the states a chip and a suggestion row spend their working lives
    /// in - lit, and reached by Down - and the sheet drew neither until 2026-09-16. Read off the
    /// style's template triggers, so the two are checked by effect: the chip is checked, the row
    /// is selected, and a plain button, which no trigger ever chooses, has a dash.
    /// </summary>
    [Fact]
    public void A_chip_is_shown_lit_and_a_suggestion_row_selected_in_the_chosen_column()
    {
        var entries = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .ToDictionary(entry => entry.Key, entry => entry, StringComparer.Ordinal);

        WpfHost.On(() =>
        {
            Assert.True(entries["FilterChip"].Chosen.Element is System.Windows.Controls.Primitives.ToggleButton { IsChecked: true });
            Assert.True(entries["SuggestionItem"].Chosen.Element is System.Windows.Controls.ListBoxItem { IsSelected: true });
        });

        Assert.Null(entries["PrimaryAction"].Chosen.Element);
        Assert.Equal("-", entries["PrimaryAction"].Chosen.Instead);

        // A menu item's check mark is drawn only in the SubmenuItem role, which a sample with no
        // menu around it never has - so the cell says so instead of showing an empty box and
        // calling it chosen.
        Assert.Null(entries["ColumnChoiceItem"].Chosen.Element);
        Assert.Contains("inside an open menu", entries["ColumnChoiceItem"].Chosen.Instead, StringComparison.Ordinal);
    }

    /// <summary>
    /// The one thing the extreme column exists to show, checked as an EFFECT rather than as the
    /// presence of TextTrimming in the markup - GUI rule 10. The plan button's label trims with an
    /// ellipsis, and a label long enough to need one gets one when laid out at the column's width.
    /// </summary>
    [Fact]
    public void The_trimmed_button_label_ends_in_an_ellipsis_when_its_text_does_not_fit()
    {
        var trimmed = WpfHost.On(() =>
        {
            var entry = Catalogue.Read(WpfHost.Resources)
                .SelectMany(group => group.Entries)
                .Single(entry => entry.Key == "TrimmedButtonLabel");

            var element = entry.Extreme.Element!;
            var width = (double)WpfHost.Resources["WidthCatalogueExtreme"];

            // Unconstrained first: how wide the text WANTS to be. Then at the column's width.
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var wanted = element.DesiredSize.Width;

            element.Measure(new Size(width, double.PositiveInfinity));
            element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height));
            element.UpdateLayout();

            var block = Descendants(element).OfType<System.Windows.Controls.TextBlock>().First();

            return (block.TextTrimming, Wanted: wanted, Given: width, Laid: block.ActualWidth);
        });

        // Needs trimming: the text wants more than the column has. Gets it: the mechanism WPF
        // draws the ellipsis with is on the block, and the block was laid out inside the column.
        Assert.True(trimmed.Wanted > trimmed.Given, $"The extreme specimen wants {trimmed.Wanted} and the column gives {trimmed.Given} - nothing to trim.");
        Assert.Equal(System.Windows.TextTrimming.CharacterEllipsis, trimmed.TextTrimming);
        Assert.True(trimmed.Laid <= trimmed.Given, "The label was laid out wider than the column it was given.");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);

            yield return child;

            foreach (var below in Descendants(child))
            {
                yield return below;
            }
        }
    }

    /// <summary>
    /// A sample taller than the row is marked tall, and the sheet's style raises its ceiling for
    /// it - the plan sheet was drawn as two faint edges until 2026-09-16 because its own margin
    /// pushed its content past a 96 pixel clip. Both halves: the model marks it, and the theme
    /// declares the taller ceiling the style reaches for.
    /// </summary>
    [Fact]
    public void A_sample_taller_than_the_row_is_given_the_taller_ceiling()
    {
        var sheet = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .Single(entry => entry.Key == "PlanSheet");

        Assert.NotNull(sheet.Normal.Element);
        Assert.True(sheet.Normal.Tall, "PlanSheet measures past the row ceiling and is not marked tall.");

        var row = WpfHost.On(() => (double)WpfHost.Resources["HeightCatalogueSample"]);
        var tall = WpfHost.On(() => (double)WpfHost.Resources["HeightCatalogueTall"]);

        Assert.True(tall > row, $"The taller ceiling ({tall}) is not taller than the row ceiling ({row}).");

        // And a plain button is not tall - the flag is measured, not handed out.
        var button = WpfHost.On(() => Catalogue.Read(WpfHost.Resources))
            .SelectMany(group => group.Entries)
            .Single(entry => entry.Key == "PrimaryAction");

        Assert.False(button.Normal.Tall);
    }
}
