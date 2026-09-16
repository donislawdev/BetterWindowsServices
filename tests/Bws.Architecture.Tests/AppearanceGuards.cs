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
    /// <b>A folder is still not what this is.</b> Named files with a stated seam is a different
    /// thing from a directory anybody may add to. The sentence that stood here said the day this
    /// array gained a third entry without an argument beside it, that difference would be gone -
    /// <b>so here is the argument, on 2026-08-11, for the third entry.</b>
    ///
    /// Controls.xaml reached its own 530-line ceiling and the configurable columns of S6d2 need
    /// cell templates a resource dictionary can hand to a column built in code, which is more
    /// lines rather than fewer. Backlog 163 had already measured where the seam was, so the split
    /// was not invented under pressure. <b>List.xaml is the one screen this product has</b> - the
    /// grid, the row template, the cell, the headings and the marks - and Controls.xaml keeps
    /// exactly what its own first line always claimed: the text styles, the field somebody types
    /// into and the box somebody ticks.
    ///
    /// <b>What separates this from a folder is that each name says what is in it and each file
    /// opens by claiming its own half</b>, which the last test in this class then checks. A fourth
    /// entry needs the same two things: a name that answers "what is in there" without opening it,
    /// and a claim at the head of the file that can be asserted.
    ///
    /// <b>And here is the argument for the FOURTH entry, on 2026-08-12, in the same shape.</b>
    /// List.xaml stood exactly on the 507-line markup ceiling, and backlog 166 could not be
    /// repaired without adding lines to it: the start type's mark was BasedOn the status mark, so
    /// it inherited triggers reading the RUN STATE and a running entry wore a green fill in the
    /// column about its NEXT start. Breaking that inheritance needs a shared base with no
    /// triggers, which is more lines rather than fewer. <b>Cells.xaml keeps what a person reads -
    /// the cell, its words and its marks - and List.xaml keeps what the list is BUILT from</b>:
    /// the grid, the row and the template it owns, the headings. Everything about a cell is on
    /// one side of that seam, which is what makes the name answerable without opening the file.
    ///
    /// <b>AND HERE IS THE ARGUMENT FOR THE FIFTH AND SIXTH, ON 2026-08-17, WHICH ARE VALUE FILES
    /// RATHER THAN STYLES ONES - so this stopped being one array with a position that means
    /// something.</b> Values.xaml stood exactly on the markup ceiling of 436 and the ratchet only
    /// goes down, which blocked two of the owner's requests at once: a new column needs a width in
    /// that file and a new mark needs a brush in it. Backlog 189.
    ///
    /// The two blocks taken out were measured before the cut rather than guessed - 162 lines of
    /// brushes and 85 of column widths, out of 436. <b>Colours.xaml answers "what colour" and
    /// Columns.xaml answers "how wide is that column"</b>, which is the same test every entry here
    /// has had to pass: a name that says what is inside without opening it, and a claim at the head
    /// of the file that the last test in this class can assert.
    ///
    /// <b>THE POSITIONAL ARRAY IS GONE AND THAT IS THE REPAIR RATHER THAN A TIDY-UP.</b> What stood
    /// here was one array where index zero meant "the values file" and Skip(1) meant "the rest are
    /// styles". That encoding cannot express a second values file at all - it would have quietly
    /// asserted that Colours.xaml holds no value of its own, which is the opposite of true. Two
    /// named pools say what the one array was always trying to.
    ///
    /// <b>AND HERE IS THE ARGUMENT FOR THE NINTH FILE, ON 2026-09-01, WHICH WAS PREDICTED BEFORE IT
    /// WAS NEEDED.</b> Backlog 271 was written that evening saying Values.xaml stood six lines under
    /// the markup ceiling and naming this exact seam. The next change to that file hit the ceiling
    /// within the hour - the chip needed a corner and a padding of its own - so the entry went from
    /// observation to work without anybody having to find the seam under pressure.
    ///
    /// <b>Type.xaml passes the same test as the other three value files.</b> Colours answers "what
    /// colour", Columns answers "how wide is that column", Values answers "how big is that gap and
    /// what shape is that mark", and type was the one subject left inside the last of those that
    /// nobody would look for under the word "values". It is also the block every appearance argument
    /// in this product ends up citing, because both of Microsoft's readability floors live in it -
    /// 12 plain and 14 semibold, held by TypeScaleGuards.
    ///
    /// <b>AND THE FIFTH, ON 2026-09-15, WHICH IS THE SAME FILE HITTING THE SAME CEILING FOR THE
    /// FOURTH TIME.</b> Values.xaml stood exactly on 376 and the plan sheet needed three widths at
    /// once - the box that takes a number of seconds (backlog 352), the sheet itself, and a ceiling
    /// on a button that now names the entry it acts on. Backlog 173 had the seam counted in
    /// advance: twenty-five Thickness entries, of which twenty-one are margins and paddings.
    /// <b>Spacing.xaml answers "how much air is around it"</b>, and the four Thickness entries that
    /// stayed behind are the ones that fail that question - a border thickness and a focus ring
    /// thickness say how thick a LINE is, which is what Values.xaml still answers.
    /// </summary>
    private static readonly string[] ValueFiles =
        ["Values.xaml", "Spacing.xaml", "Type.xaml", "Colours.xaml", "Surfaces.xaml", "Columns.xaml"];

    /// <summary>
    /// The halves that hold styles and no value of their own.
    ///
    /// <b>AND HERE IS THE ARGUMENT FOR THE SEVENTH, ON 2026-08-25, WHICH BREAKS A SENTENCE THE
    /// FOURTH ONE WROTE.</b> Cells.xaml stood exactly on the markup ceiling of 395, and `A11` needs
    /// a badge beside the name in the list and a template to hold it - so the same thing happened
    /// as on 2026-08-12: a line could not be added until a seam was found. The seam is that a style
    /// saying how a WORD looks and a style saying how a coloured SHAPE looks are two subjects, and
    /// the marks were 207 of the file's 458 lines, measured before the cut rather than guessed.
    ///
    /// <b>What it costs is that the fourth entry's own claim stops being true</b> - "everything
    /// about a cell is on one side of that seam" - and it is rewritten at the head of both files
    /// rather than left to rot, which is the precedent SizeCeilings set for the first split.
    /// Cells.xaml holds the words in a cell and what a cell is made of, Marks.xaml holds the marks.
    /// Both names still answer "what is in there" without opening the file, which is the test every
    /// entry in these two pools has had to pass.
    ///
    /// <b>AND HERE IS THE EIGHTH, ON 2026-09-01, WHICH IS THE FIRST NAMED AFTER A SCREEN.</b> The
    /// owner rejected the machine overview for the third time and the answer was to rebuild it as
    /// cards rather than tune the same column of lines again - and the markup ratchet fired on
    /// OverviewView.xaml three edits running while that work was in flight. Trimming a comment to
    /// fit under a ceiling is the wrong answer here twice over, because the comments are the only
    /// record this project keeps of why anything looks the way it does.
    ///
    /// <b>Overview.xaml passes the same test as the other seven</b>: there is exactly one screen in
    /// this product whose controls look like nothing else in it - no fill, no border, a whole line
    /// taking a click - and the name says where that lives. What could NOT move is the pair of data
    /// templates, because both wire an event handler and a resource dictionary has no code-behind,
    /// which is the seam rather than a compromise.
    /// </summary>
    private static readonly string[] StyleFiles =
    [
            "Text.xaml", "Overview.xaml", "Chips.xaml",

            // Menus.xaml left Controls.xaml on 2026-09-05 when the markup ratchet fired on it -
            // the picker learned to nest and a column heading learned to filter, four styles in
            // one day. ADDED HERE IN THE SAME EDIT AS THE SPLIT, because a styles file this pool
            // does not name is a styles file no rule in this class applies to, and nothing
            // anywhere would say so.
            //
            // Suggestions.xaml, 2026-09-15, took the same seam BEFORE the ratchet fired rather
            // than after: Controls.xaml stood forty-eight lines under its ceiling and the list
            // under the search box needed three templates. It passes the test every entry here
            // has had to pass - the name says what is in it, a floating list of what can be
            // typed, and the file opens by claiming exactly that.
            "Controls.xaml", "Menus.xaml", "Suggestions.xaml",

            // PlanLines.xaml left Plan.xaml on 2026-09-07 when the markup ratchet fired on THAT
            // one - a failure that can be escalated needed a template of its own. Added here in
            // the same edit as the split, for the reason the note above gives: a styles file this
            // pool does not name is one no rule in this class reaches, and nothing would say so.
            "PlanLines.xaml", "Plan.xaml", "List.xaml", "Marks.xaml", "Cells.xaml",

            // Details.xaml, 2026-09-16: the panel about one entry, whose values had been drawn in
            // black for thirty-four days because their only style lived in the view and named no
            // colour - and the guard that catches that reads the theme. Added here in the same
            // edit as the file, for the reason every entry above gives.
            "Details.xaml"
        ];

    /// <summary>Both pools, for the rules that apply to any file allowed to hold appearance.</summary>
    private static IEnumerable<string> ThemeFiles => ValueFiles.Concat(StyleFiles);

    /// <summary>
    /// What a styles file is allowed to declare at the top level.
    ///
    /// <b>A list of what may be there rather than a list of what may not, and that is the whole
    /// strength of it.</b> The rule this enforces is that no VALUE lives beside a style - and a
    /// blacklist of value types passes the first time somebody declares a kind of value nobody
    /// thought of. A whitelist fails instead, which is the direction a guard should fail in.
    ///
    /// <b>DataTemplate joined Style on 2026-08-11, and it is a decision rather than an
    /// accommodation.</b> The columns became a list built in code, so the two cells that carry a
    /// mark as well as a word needed names something could ask for - and a template is a control
    /// composition, the same family as a style, rather than a number or a colour. What the rule
    /// buys is that <c>ADR-23</c> can promise the values are all in one place, and a template
    /// declares none.
    ///
    /// A third entry here means somebody decided a third kind of thing belongs beside the styles.
    /// That is a decision worth making on purpose, which is what a whitelist forces.
    /// </summary>
    private static readonly string[] AllowedBesideStyles = ["Style", "DataTemplate"];

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

            // ASKED OF THE DICTIONARY RATHER THAN OF THE FILE'S TEXT, AND THAT IS STRICTER THAN
            // WHAT STOOD HERE - which matters, because loosening a guard to let a new file through
            // would have been the dishonest way to do backlog 189.
            //
            // The old version looked for the NAME OF A POLICED ATTRIBUTE anywhere in the file:
            // Margin, Foreground, Padding and nine others. That was a proxy for "this file has
            // something in it", and it was wrong in both directions. It PASSED a file with zero
            // resources and the word "Padding" in a comment. And it FAILED a file full of real
            // values that happen not to be attributes - a dictionary of <DataGridLength> entries
            // has keys and no attribute names at all, which is precisely Columns.xaml and is what
            // blocked the split for four days.
            //
            // What this test says it is for, in its own first paragraph, is catching ABSENCE. A
            // resource dictionary is valid XML, so "does this declare anything" has an exact
            // answer and the machinery for it is already in this class.
            Assert.NotEmpty(TopLevel(name));
        }
    }

    [Fact]
    public void The_seams_between_the_theme_files_are_where_all_of_them_say_they_are()
    {
        // EVERY FILE OPENS BY PROMISING THIS AND UNTIL 2026-08-11 NOTHING CHECKED ANY OF THE
        // SENTENCES. Values.xaml says it holds every appearance value and not one style; the
        // styles files say they hold no value of their own. Prose is the one surface in this
        // project with no guard at all, so a claim written at the head of a file is worth exactly
        // one assertion.
        //
        // <b>It is not tidiness, because the merge order in App.xaml is load bearing.</b> Values
        // are merged before styles, because a styles file resolves their names through
        // StaticResource while it is being read. A style that drifts into Values.xaml is a style
        // resolving names out of a dictionary merged after it, and a value that drifts into a
        // styles file is a value nobody reading the value file will ever find - which is the
        // whole of what `ADR-23` buys.
        //
        // <b>WRITTEN OVER THE ARRAY RATHER THAN OVER TWO NAMED FILES, and that is the repair the
        // second split asked for.</b> The first version named Values.xaml and Controls.xaml in
        // its own body, so the day List.xaml arrived it would have gone on passing while saying
        // nothing at all about the file holding most of the styles. A guard that has to be edited
        // to keep covering what it is named after is a guard that will one day not be.
        //
        // <b>Read as XML rather than by indentation.</b> A resource dictionary is valid XML, so
        // "what are the top level entries" has an exact answer. A version counting leading spaces
        // would stop seeing anything the day somebody reformatted a file, which is the silent way
        // for a guard to die.
        // OVER EVERY VALUE FILE SINCE 2026-08-17, WHERE IT USED TO BE ThemeFiles[0]. There are
        // three of them now, and an index into a shared array said "the first one is the values
        // one" - a sentence that was true by accident and stopped being expressible the moment a
        // second values file existed.
        foreach (var name in ValueFiles)
        {
            var values = TopLevel(name);

            // No pool may be empty, or the claims below are kept by there being nothing to keep -
            // the same failure the test above exists to prevent, one level down.
            Assert.NotEmpty(values);

            var stylesAmongValues = values
                .Where(entry => entry.Name.LocalName == "Style")
                .Select(Describe)
                .ToList();

            Assert.True(
                stylesAmongValues.Count == 0,
                $"{name} says at the top that it holds no style, and it does. A style here is "
                + "merged before the files the styles live in, so it resolves names against a "
                + "dictionary that is not there yet:"
                + Environment.NewLine + string.Join(Environment.NewLine, stylesAmongValues));
        }

        foreach (var name in StyleFiles)
        {
            var styles = TopLevel(name);

            Assert.NotEmpty(styles);

            var valuesAmongStyles = styles
                .Where(entry => !AllowedBesideStyles.Contains(entry.Name.LocalName, StringComparer.Ordinal))
                .Select(Describe)
                .ToList();

            Assert.True(
                valuesAmongStyles.Count == 0,
                $"{name} says at the top that it holds no value of its own. A value here is a "
                + "value nobody reading the value file will find. What may sit beside a style is "
                + $"{string.Join(" and ", AllowedBesideStyles)}, and a third kind is a decision "
                + "rather than an oversight:"
                + Environment.NewLine + string.Join(Environment.NewLine, valuesAmongStyles));
        }
    }

    /// <summary>The entries a theme file declares, which for a resource dictionary is its root's children.</summary>
    [Fact]
    public void A_column_ceiling_names_a_share_that_exists_and_sits_above_the_floor()
    {
        // THE MECHANISM IS A NAMING CONVENTION, SO A TYPO IN IT DOES NOTHING AND SAYS NOTHING.
        // ListColumns looks a ceiling up as the width key with "Ceiling" after it, through
        // TryFindResource - so ColumnDisplaynameCeiling, one letter wrong in the middle, is an
        // error nowhere. The column simply goes on growing, and the only way to notice is to
        // maximise the window and measure it. Which is exactly how the fault these ceilings answer
        // stayed unseen for the whole life of this project: every picture ever taken of this window
        // was at the size it opens at or smaller, and the owner keeps it maximised.
        //
        // Three things have to hold and each fails quietly on its own:
        //   - what stands before "Ceiling" is a width key that exists, or nothing is ever read
        //   - that width is a SHARE, because a fixed column is already its own ceiling and capping
        //     one would fight the floor ListColumns puts under it at the same value
        //   - the ceiling is above ColumnFloor, or the column can never reach its own minimum
        var entries = TopLevel("Columns.xaml");

        static string? KeyOf(XElement entry) => entry
            .Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?
            .Value;

        static double Number(XElement entry) =>
            double.Parse(entry.Value.Trim(), System.Globalization.CultureInfo.InvariantCulture);

        var widths = entries
            .Where(entry => entry.Name.LocalName == "DataGridLength")
            .ToDictionary(entry => KeyOf(entry)!, entry => entry.Value.Trim());

        var floor = Number(entries.Single(entry => KeyOf(entry) == "ColumnFloor"));

        var ceilings = entries
            .Where(entry => KeyOf(entry)?.EndsWith("Ceiling", StringComparison.Ordinal) == true)
            .ToList();

        // Not a formality: with none declared this test would pass by having nothing to say, and
        // the day somebody deletes the last ceiling is the day it should be noticed.
        Assert.NotEmpty(ceilings);

        foreach (var ceiling in ceilings)
        {
            var key = KeyOf(ceiling)!;
            var capped = key[..^"Ceiling".Length];

            Assert.True(
                widths.TryGetValue(capped, out var width),
                $"{key} names no column width. ListColumns looks for {capped} and finds nothing, so this caps nothing.");

            Assert.True(
                width!.EndsWith('*'),
                $"{key} caps {capped}, which is {width} and not a share. A fixed width is already its own ceiling.");

            Assert.True(
                Number(ceiling) > floor,
                $"{key} is {Number(ceiling)} against a ColumnFloor of {floor}, so that column can never reach its own minimum.");
        }
    }

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
