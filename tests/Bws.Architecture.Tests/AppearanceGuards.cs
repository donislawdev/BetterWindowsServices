using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Bws.Architecture.Tests;

/// <summary>
/// Keeps every appearance value in one file.
///
/// `ADR-23` in code, and it exists because of a failure that happens to interfaces everywhere:
/// they drift apart. One screen gets a margin of eight, the next
/// one seven, a third invents a slightly different grey - and none of it is noticed, because
/// <b>XAML is prose</b>. A wrong margin does not fail a build and does not redden a test, so
/// it survives, and the next screen copies it.
///
/// That is the same class of problem this project already named for comments and documents.
/// The difference is that here it can be checked, which is what this guard does.
///
/// Written before the first screen exists, on purpose. A guard written afterwards finds the
/// rules already broken and gets weakened to fit what is there - which is how the
/// architecture guards were done and why they hold.
///
/// False alarm estimate, as 04-PLAN-PRAC requires before building a guard: at the moment it
/// was written there were two XAML files carrying nothing but a window size and a title, so
/// zero. Every hit from here on is a real one until somebody shows otherwise.
/// </summary>
public sealed class AppearanceGuards
{
    /// <summary>
    /// The files allowed to hold values. Everything else refers to them by name.
    ///
    /// <b>TWO SINCE 2026-08-11, AND THE SENTENCE THAT USED TO BE HERE WAS AN ARGUMENT FOR ONE.</b>
    /// It said: one file rather than a folder, so that "what does this product look like" is a
    /// question somebody answers by reading one screen of text instead of opening every view and
    /// comparing. That argument was real and it lost to a measurement - the one file reached 849
    /// lines against a markup ceiling of 799, which is well past a screen of text either way, and
    /// backlog 156 carries the owner's decision to split it.
    ///
    /// <b>What is kept is the part that can be enforced, and it is worth saying which part that
    /// is.</b> This guard never checked that the theme was readable in one sitting - no guard can.
    /// It checks that values live in the theme and nowhere else, and that claim survives a theme
    /// of two files unchanged. What does NOT survive is the promise in `ADR-23` about one reading,
    /// and it is restated at the top of both files rather than left to rot.
    ///
    /// <b>A folder is still not what this is.</b> Two named files with a stated seam - values in
    /// one, styles in the other - is a different thing from a directory anybody may add to. The
    /// day this array gains a third entry without an argument beside it, that difference is gone.
    /// </summary>
    private static readonly string[] ThemeFiles = ["Values.xaml", "Controls.xaml"];

    /// <summary>
    /// Attributes that decide whether two screens look like the same product.
    ///
    /// Deliberately not every attribute that takes a number. A window's own Height and Width
    /// are a one-off and say nothing about consistency, and forbidding them would be the kind
    /// of noise that teaches people to work around a guard rather than with it.
    /// </summary>
    private static readonly string[] Policed =
    [
        "Foreground", "Background", "BorderBrush", "Fill", "Stroke",
        "Margin", "Padding", "FontSize", "FontFamily", "FontWeight",
        "BorderThickness", "CornerRadius"
    ];

    /// <summary>
    /// A value pointing at something named. Bindings count: they are indirection too, and
    /// what this guard forbids is a value invented in place.
    /// </summary>
    private static readonly Regex ByName = new(
        @"^\{\s*(StaticResource|DynamicResource|TemplateBinding|Binding|x:Static)\b",
        RegexOptions.Compiled,
        Sources.Ceiling);

    [Fact]
    public void No_view_invents_an_appearance_value_of_its_own()
    {
        var offenders = new List<string>();

        foreach (var file in Views())
        {
            var lines = File.ReadAllLines(file);

            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var attribute in Policed)
                {
                    foreach (Match match in Regex.Matches(
                                 lines[index],
                                 $@"\b{attribute}\s*=\s*""([^""]*)""",
                                 RegexOptions.None,
                                 Sources.Ceiling))
                    {
                        var value = match.Groups[1].Value.Trim();

                        if (value.Length > 0 && !ByName.IsMatch(value))
                        {
                            offenders.Add($"{Path.GetFileName(file)}:{index + 1}  {attribute}=\"{value}\"");
                        }
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Appearance values belong in {string.Join(" or ", ThemeFiles)} and are referred to by " +
            "name. A value written into a view is how two screens start looking like two products:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void A_setter_in_a_style_refers_to_a_name_as_well()
    {
        // The same rule in the shape styles are written in. Without this, a view could be
        // clean while every value it inherits was invented inside a style sitting in a file
        // this guard would otherwise wave through.
        var offenders = new List<string>();

        foreach (var file in Views())
        {
            var lines = File.ReadAllLines(file);

            for (var index = 0; index < lines.Length; index++)
            {
                var setter = Regex.Match(
                    lines[index],
                    @"Property\s*=\s*""(\w+)""\s*Value\s*=\s*""([^""]*)""",
                    RegexOptions.None,
                    Sources.Ceiling);

                if (setter.Success
                    && Policed.Contains(setter.Groups[1].Value, StringComparer.Ordinal)
                    && setter.Groups[2].Value.Trim().Length > 0
                    && !ByName.IsMatch(setter.Groups[2].Value.Trim()))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{index + 1}  {setter.Groups[1].Value}={setter.Groups[2].Value}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A setter outside the theme file invents a value the same way a view would:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_theme_files_exist_and_are_the_only_place_holding_values()
    {
        // Without this the two guards above pass perfectly on a product with no theme file
        // and no views - which is exactly the state they were written in. A guard that is
        // satisfied by absence is satisfied for as long as nobody builds anything.
        //
        // BOTH ARE CHECKED, NOT EITHER. When the theme became two files on 2026-08-11 the
        // cheap version of this was to look for one of them - and that version passes on a
        // product where Values.xaml has been deleted and every name in Controls.xaml resolves
        // to nothing. Absence is the failure this test was written for, so it has to be asked
        // about each half separately.
        foreach (var name in ThemeFiles)
        {
            var theme = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes", name);

            Assert.True(File.Exists(theme), $"A file allowed to hold appearance values is missing: {theme}");

            var text = File.ReadAllText(theme);

            Assert.True(
                Policed.Any(attribute => text.Contains(attribute, StringComparison.Ordinal)),
                $"{name} holds no appearance values at all, so the rule that everything lives " +
                "in the theme is being kept by there being nothing to keep.");
        }
    }

    [Fact]
    public void The_seam_between_the_two_theme_files_is_where_both_of_them_say_it_is()
    {
        // BOTH FILES OPEN BY PROMISING THIS AND UNTIL 2026-08-11 NOTHING CHECKED EITHER SENTENCE.
        // Values.xaml says it holds every appearance value and not one style; Controls.xaml says
        // it holds no value of its own. Prose is the one surface in this project with no guard at
        // all, so a claim written at the head of a file is worth exactly one assertion.
        //
        // <b>It is not tidiness, because the merge order in App.xaml is load bearing.</b> Values
        // are merged before styles, because Controls.xaml resolves their names through
        // StaticResource while it is being read. A style that drifts into Values.xaml is a style
        // resolving names out of a dictionary merged after it, and a value that drifts into
        // Controls.xaml is a value nobody reading the value file will ever find - which is the
        // whole of what `ADR-23` buys.
        //
        // <b>Read as XML rather than by indentation.</b> A resource dictionary is valid XML, so
        // "what are the top level entries" has an exact answer. A version counting leading spaces
        // would stop seeing anything the day somebody reformatted a file, which is the silent way
        // for a guard to die.
        var values = TopLevel("Values.xaml");
        var controls = TopLevel("Controls.xaml");

        // Neither pool may be empty, or the two claims below are kept by there being nothing to
        // keep - the same failure the test above exists to prevent, one level down.
        Assert.NotEmpty(values);
        Assert.NotEmpty(controls);

        var stylesAmongValues = values
            .Where(entry => entry.Name.LocalName == "Style")
            .Select(Describe)
            .ToList();

        Assert.True(
            stylesAmongValues.Count == 0,
            "Values.xaml says at the top that it holds no style, and it does. A style here is "
            + "merged before the file the styles live in, so it resolves names against a "
            + "dictionary that is not there yet:"
            + Environment.NewLine + string.Join(Environment.NewLine, stylesAmongValues));

        var valuesAmongControls = controls
            .Where(entry => entry.Name.LocalName != "Style")
            .Select(Describe)
            .ToList();

        Assert.True(
            valuesAmongControls.Count == 0,
            "Controls.xaml says at the top that it holds no value of its own. A value here is a "
            + "value nobody reading the value file will find:"
            + Environment.NewLine + string.Join(Environment.NewLine, valuesAmongControls));
    }

    /// <summary>The entries a theme file declares, which for a resource dictionary is its root's children.</summary>
    private static List<XElement> TopLevel(string name) =>
        [.. XDocument
            .Load(Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes", name))
            .Root!
            .Elements()];

    /// <summary>
    /// An offending entry named the way somebody would look for it - by its key, or by what it
    /// styles when it has no key, because an implicit style has no other name.
    /// </summary>
    private static string Describe(XElement entry)
    {
        var key = entry
            .Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?
            .Value;

        return $"  <{entry.Name.LocalName}>  {key ?? entry.Attribute("TargetType")?.Value ?? "(no key)"}";
    }

    /// <summary>
    /// Every XAML file that shows something, which is all of them except the two holding the
    /// values and the application entry point that merges them in.
    /// </summary>
    private static IEnumerable<string> Views() =>
        Sources.ShippedMarkup()
            .Where(path => !ThemeFiles.Contains(Path.GetFileName(path), StringComparer.Ordinal))
            .Where(path => Path.GetFileName(path) != "App.xaml");
}
