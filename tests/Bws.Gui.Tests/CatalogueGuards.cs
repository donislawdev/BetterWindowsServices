// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Text.RegularExpressions;
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

    /// <summary>Every keyed style a theme file declares AT THE TOP LEVEL, which is what a component is.</summary>
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

            keys.AddRange(root.Elements()
                .Where(element => element.Name.LocalName == "Style")
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
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var canBeWrong = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(Themes, "*.xaml"))
        {
            var root = XDocument.Load(file).Root;

            if (root is null)
            {
                continue;
            }

            foreach (var style in root.Elements().Where(element => element.Name.LocalName == "Style"))
            {
                var key = style.Attribute(x + "Key")?.Value;

                if (key is not null && style.Descendants()
                        .Where(element => element.Name.LocalName == "Trigger")
                        .Any(trigger => trigger.Attribute("Property")?.Value == "Validation.HasError"))
                {
                    canBeWrong.Add(key);
                }
            }
        }

        // A guard that is satisfied by nobody being able to be wrong is the absence-shaped pass
        // this file warns about twice already. The box of seconds has been wrong-able since the day
        // this column arrived.
        Assert.Contains("WaitingBox", canBeWrong);

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
}
