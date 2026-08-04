using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Keeps every appearance value in one file.
///
/// `ADR-23` in code, and it exists because of a failure the owner has watched happen in
/// other projects: the interface drifts apart. One screen gets a margin of eight, the next
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
    /// The one file allowed to hold values. Everything else refers to them by name.
    ///
    /// One file rather than a folder, and that is the point rather than tidiness: it makes
    /// "what does this product look like" a question somebody answers by reading one screen
    /// of text, instead of by opening every view and comparing.
    /// </summary>
    private const string ThemeFile = "Theme.xaml";

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
            $"Appearance values belong in {ThemeFile} and are referred to by name. A value written " +
            "into a view is how two screens start looking like two products:" +
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
    public void The_theme_file_exists_and_is_the_only_place_holding_values()
    {
        // Without this the two guards above pass perfectly on a product with no theme file
        // and no views - which is exactly the state they were written in. A guard that is
        // satisfied by absence is satisfied for as long as nobody builds anything.
        var theme = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes", ThemeFile);

        Assert.True(File.Exists(theme), $"The one file allowed to hold appearance values is missing: {theme}");

        var text = File.ReadAllText(theme);

        Assert.True(
            Policed.Any(attribute => text.Contains(attribute, StringComparison.Ordinal)),
            $"{ThemeFile} holds no appearance values at all, so the rule that everything lives " +
            "there is being kept by there being nothing to keep.");
    }

    /// <summary>
    /// Every XAML file that shows something, which is all of them except the one holding the
    /// values and the application entry point that merges it in.
    /// </summary>
    private static IEnumerable<string> Views() =>
        Directory
            .EnumerateFiles(Path.Combine(SourceTree.Root(), "src"), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => Path.GetFileName(path) != ThemeFile)
            .Where(path => Path.GetFileName(path) != "App.xaml");
}
