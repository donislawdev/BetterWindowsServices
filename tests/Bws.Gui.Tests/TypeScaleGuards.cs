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

    /// <summary>
    /// Two styles that mean different things do not render the same.
    ///
    /// <b>WRITTEN 2026-08-19 BECAUSE THEY DID, FOR A WHOLE DAY, AND NOTHING NOTICED.</b>
    /// <c>PlanNoticeText</c> - where the plan panel is in its sequence, the line telling somebody
    /// whether anything has happened yet - and <c>SectionHeadingText</c> - the label over a list -
    /// had IDENTICAL setters: same face, same 14, same SemiBold, same colour. So "Nothing has been
    /// done" was drawn exactly like "In this order:", and the panel had four headings and no body.
    ///
    /// <b>It arrived through a correction rather than through carelessness, which is the part worth
    /// guarding.</b> The notice wore the warning colour, that was a real fault, and moving it onto
    /// weight fixed it - straight onto the other style's exact values. A fix is a claim and needs
    /// checking as one.
    ///
    /// <b>Deliberately not a rule about which of them should be heavier.</b> That is a judgement
    /// this file has no business holding. What it holds is that they are distinguishable at all,
    /// which is checkable without taste.
    /// </summary>
    [Fact]
    public void A_state_sentence_is_not_dressed_as_a_section_label()
    {
        var notice = Setters("PlanNoticeText");
        var heading = Setters("SectionHeadingText");

        Assert.True(notice.Count > 0, "PlanNoticeText was not found in the theme, so this guard checks nothing.");
        Assert.True(heading.Count > 0, "SectionHeadingText was not found in the theme, so this guard checks nothing.");

        // SORTED, so that reordering two identical setter lists cannot be mistaken for a
        // difference. What is being asked is whether the two styles SAY the same thing, and the
        // order somebody wrote them in is not part of that.
        Assert.False(
            notice.Order(StringComparer.Ordinal).SequenceEqual(heading.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "PlanNoticeText and SectionHeadingText set exactly the same things, so a sentence about "
            + "what has happened is drawn as a label for a list. One of them has to differ in size, "
            + "weight or colour - which one is a judgement, that they differ is not:"
            + Environment.NewLine + string.Join(Environment.NewLine, notice));
    }

    /// <summary>
    /// The substance of a plan is not smaller than the label above it.
    ///
    /// <b>It was, and that is why the panel read as a wall of bold labels with nothing under
    /// them.</b> A step was drawn by a style built on <c>SubduedText</c>, which is
    /// <c>TextSizeSmall</c>, under a section heading at <c>TextSizeHeader</c> SemiBold - so what
    /// will happen to the machine was the smallest text on the panel and the words "In this order"
    /// were among the largest.
    ///
    /// Asked as a comparison rather than against a number, so rescaling the whole theme cannot
    /// quietly reintroduce it.
    ///
    /// <b>THE ONE POINT OF SLACK CAME OUT ON 2026-09-02, AND IT CAME OUT BECAUSE THE MUTATION
    /// REGISTRY REPORTED THIS GUARD MISSED.</b> The assertion read <c>step &gt;= heading - 1</c>,
    /// which permitted the label to be a point BIGGER than the thing it labels - the exact state
    /// the name of this guard forbids. It was survivable while the two were 13 against 14, because
    /// the slack was the whole difference. When the section label came down to 13 the real gap
    /// became zero, and one point of slack over a zero-point gap swallows any mutation that can be
    /// written: dropping the step to <c>TextSizeSmall</c> left green.
    ///
    /// <b>So the guard was weaker than its own sentence, and had been since the day it was
    /// written.</b> Equal is not smaller, which is what the name says and what this now asks. This
    /// is the second time in one day that this guard was found green over the fault it is named
    /// after - the first was on weight and colour, axes it does not look at at all.
    /// </summary>
    [Fact]
    public void The_substance_of_a_plan_is_not_smaller_than_the_label_above_it()
    {
        var sizes = Sizes();
        var step = Named(Setters("PlanStepText"), "FontSize");
        var heading = Named(Setters("SectionHeadingText"), "FontSize");

        Assert.NotNull(step);
        Assert.NotNull(heading);
        Assert.True(sizes.ContainsKey(step) && sizes.ContainsKey(heading), $"{step} or {heading} is not a declared size.");

        Assert.True(
            sizes[step] >= sizes[heading],
            string.Create(CultureInfo.InvariantCulture, $"A step is {sizes[step]} and the heading over it is {sizes[heading]}.")
            + " The label is bigger than the thing it labels, so a reader's eye stops on the word"
            + " \"steps\" rather than on what would happen to their machine.");
    }

    /// <summary>Every setter of one named style, as text, in the order the theme declares them.</summary>
    private static List<string> Setters(string key)
    {
        // RESOLVED THROUGH BasedOn SINCE 2026-09-16, the day six styles that spelled out "Segoe,
        // 13" in full became BasedOn two body styles that say it once. Before that a style built
        // on another had no setters here at all, and worse: the alias form is a self-closing tag,
        // and the old pattern ran from its opening tag to the NEXT style's closing one, counting
        // a neighbour's setters as its own. A style's own setters win over its base's, and only
        // what stands before <Style.Triggers> counts - a size that applies in one state is not
        // the style's size. Depth is bounded by a number rather than a cycle check, because a
        // theme with a cycle in it does not load.
        var setters = new Dictionary<string, string>(StringComparer.Ordinal);
        Gather(key, setters, depth: 0);

        return setters.Select(pair => $"{pair.Key}={pair.Value}").ToList();
    }

    private static void Gather(string key, Dictionary<string, string> setters, int depth)
    {
        // NO \b AFTER THE CLOSING QUOTE, and the first version had one. A word boundary
        // between a quote and a space is not a boundary at all, so the pattern matched
        // nothing and the guard failed saying the style was absent - which is the safe
        // direction, and only because it was written to say so rather than to pass on
        // finding none. The quotes already make the key exact.
        var style = Regex.Match(
            Theme(),
            $@"<Style\s+x:Key=""{Regex.Escape(key)}""(?<head>[^>]*?)(?:/>|>(?<body>.*?)</Style>)",
            RegexOptions.Singleline,
            Ceiling);

        if (!style.Success || depth > 8)
        {
            return;
        }

        var basedOn = Regex.Match(style.Groups["head"].Value, @"BasedOn=""\{StaticResource\s+(\w+)\}""", RegexOptions.None, Ceiling);

        if (basedOn.Success)
        {
            Gather(basedOn.Groups[1].Value, setters, depth + 1);
        }

        var body = style.Groups["body"].Value;
        var triggers = body.IndexOf("<Style.Triggers>", StringComparison.Ordinal);
        var own = triggers < 0 ? body : body[..triggers];

        foreach (Match setter in Regex.Matches(own, @"<Setter\s+Property=""(\w+)""\s+Value=""([^""]*)""", RegexOptions.None, Ceiling))
        {
            setters[setter.Groups[1].Value] = setter.Groups[2].Value;
        }
    }

    /// <summary>The key a named setter points at, with the StaticResource wrapper taken off.</summary>
    private static string? Named(List<string> setters, string property) =>
        setters
            .Where(setter => setter.StartsWith($"{property}=", StringComparison.Ordinal))
            .Select(setter => Regex.Match(setter, @"\{StaticResource\s+(\w+)\}", RegexOptions.None, Ceiling).Groups[1].Value)
            .FirstOrDefault(name => name.Length > 0);

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
            // The self-closing form is a style too - an alias with nothing of its own - and
            // without the alternative here it would swallow the next style whole.
            .Matches(Theme(), @"<Style\b[^>]*?(?:/>|>.*?</Style>)", RegexOptions.Singleline, Ceiling)
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

    /// <summary>
    /// The ceiling under the details panel's title is two lines of the heading size and not three
    /// - so the display name wraps to a second line and never a third, and the number cannot drift
    /// from the size it was made for when either of them moves.
    ///
    /// <b>A number standing in for a computation, and this is the guard that makes it one.</b>
    /// GUI rule 14 says layout is computed, never measured, and a ceiling of exactly two lines
    /// cannot be written in markup without a converter - so Values.xaml holds a number beside a
    /// comment saying what it is a function of, and this holds the number to the function. The
    /// bounds are the face's own leading: Segoe UI sets a line at about 1.33 of its size, so two
    /// lines lie between 2.0 and 3.0 sizes with room for the slack the first photograph asked for
    /// (48 trimmed the title on its first line - 56 does not).
    /// </summary>
    [Fact]
    public void The_details_title_ceiling_is_two_lines_of_the_heading_size_and_not_three()
    {
        var sizes = Sizes();
        var ceiling = Regex.Match(
            Theme(),
            @"<system:Double\s+x:Key=""HeightDetailTitle""\s*>\s*([0-9.]+)\s*<",
            RegexOptions.None,
            Ceiling);

        Assert.True(ceiling.Success, "HeightDetailTitle is not declared in the theme, so the details title has no ceiling.");
        Assert.True(sizes.ContainsKey("TextSizeHeading"), "TextSizeHeading is not declared, so there is nothing to hold the ceiling to.");

        var height = double.Parse(ceiling.Groups[1].Value, CultureInfo.InvariantCulture);
        var heading = sizes["TextSizeHeading"];

        Assert.True(
            height >= 2 * 1.33 * heading && height < 3 * 1.2 * heading,
            string.Create(
                CultureInfo.InvariantCulture,
                $"HeightDetailTitle is {height} against a heading size of {heading}: two lines need at least "
                + $"{2 * 1.33 * heading:0.#} and three would fit from {3 * 1.2 * heading:0.#}."));
    }
}
