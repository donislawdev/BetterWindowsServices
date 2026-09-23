using System.Windows;
using System.Windows.Controls;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Every component this window is built out of, read out of the theme files themselves.
///
/// <b>Rule 4 of the owner's GUI rules asks for this screen and names the property that makes it
/// worth having:</b> the catalogue may not hold components the application does not have, and may
/// not leave out ones it does. A hand written gallery satisfies that on the day it is written and
/// on no day afterwards - so nothing here is hand written. This walks the merged dictionaries the
/// window really loaded and reports what it finds.
///
/// <b>THE SAMPLES ARE BUILT BY TARGET TYPE RATHER THAN BY KEY, AND THAT IS WHAT SURVIVES A
/// REBUILD.</b> There are fifty two keyed styles today and twelve target types among them. A
/// factory per key would be fifty two things to maintain, and the fifty third would be the one
/// nobody added. A factory per TYPE means a new style over an existing control appears here on
/// its own, with no edit at all - and a style over a control type nothing has used yet reddens
/// <c>CatalogueGuards</c> until this file learns about it. The owner has said the interface will
/// be rebuilt, so the maintenance cost of this file is the whole question.
///
/// <b>Reflection is not available here and the constraint improved the design.</b>
/// <c>LayeringGuards</c> forbids <c>System.Activator</c> in this assembly - "it does not build
/// types a file asked for" - so a sample cannot be conjured from a type name. The table below is
/// the honest version of the same thing.
///
/// <b>What it cannot show, said here rather than left to be noticed:</b> hover and keyboard focus.
/// Both need a person: a pointer over a control and a Tab press into it. That is not a gap in this
/// screen, it is what the screen is FOR - every sample below is a real control on a real surface,
/// so moving the mouse over it and walking it with Tab shows those two states as the product draws
/// them. <c>tools/gui-probe/reach.ps1</c> is the instrument that measures the same two without a
/// person.
/// </summary>
public static partial class Catalogue
{
    /// <summary>
    /// The words the samples carry.
    ///
    /// <b>Constants here rather than keys in the language files, and it is a decision rather than
    /// an oversight.</b> Rule 13 keeps text a person reads out of the source, and it is right
    /// about the product: every word the window says is a translation key. These are not words
    /// the window says. They are DATA handed to a component so that it has something to draw, on
    /// a screen reached only by a launch argument - and putting them in the language files would
    /// mean asking a translator for a sentence whose whole job is to be too long.
    /// </summary>
    private const string Short = "Sample";

    /// <summary>Long enough to run past any column, so wrapping, clipping and the ellipsis all show.</summary>
    private const string Long =
        "A sample line long enough to run well past the width of any column in this window, so that "
        + "wrapping, clipping and the ellipsis can all be looked at in one place";

    /// <summary>
    /// What a style that draws a NUMBER is given as its extreme - a million, written the way the
    /// machine writes a million, because a thousands separator is the one thing a number column
    /// has to make room for and a right aligned digit is the one thing a long word cannot test.
    /// </summary>
    private static readonly string BigNumber = 1_000_000.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// The extreme text for a style: a number for the styles that draw one, the long line for
    /// the rest. Told apart by the KEY, which is the one place this file reads a key of a style -
    /// a number style is one that says so in its name (CellNumber, CountLine, OverviewNumberText),
    /// because a style has no other way of saying what kind of text it is for.
    /// </summary>
    private static string Extreme(string key) =>
        key.Contains("Number", StringComparison.Ordinal) || key.Contains("Count", StringComparison.Ordinal) ? BigNumber : Long;

    /// <summary>
    /// The theme files, by the name they are merged under. Anything else in the merged set belongs
    /// to WPF UI, and a catalogue of somebody else's library is not what rule 4 asks for.
    /// </summary>
    private const string Ours = "Themes/";

    /// <summary>
    /// One sample of one component, or the reason there is none.
    ///
    /// Never both, and never neither: a cell on the sheet either shows a control or says why it
    /// does not, because an empty cell reads as a component that draws nothing.
    /// </summary>
    /// <param name="Element">The control, styled, ready to be put on the sheet.</param>
    /// <param name="Instead">What to say where a sample cannot be built.</param>
    /// <param name="Tall">
    /// Whether the sample measured past the row ceiling and needs the taller one. Decided here,
    /// by measuring, so that the sheet's style can raise its ceiling without a list of tall
    /// components anywhere - since 2026-09-16, when the plan sheet was found drawn as two edges.
    /// </param>
    /// <remarks>
    /// <see cref="Instead"/> is empty rather than null where there is a sample, because the sheet
    /// hides it with a trigger on the empty string - the same shape EmptyState.xaml uses. A null
    /// would not match that trigger and the explanation would sit under every working sample.
    /// </remarks>
    public sealed record Sample(FrameworkElement? Element, string Instead, bool Tall = false)
    {
        public static Sample Of(FrameworkElement element, bool tall = false) => new(element, string.Empty, tall);

        public static Sample None(string why) => new(null, why);
    }

    /// <summary>
    /// One component: what it is called, what it is for, and how it looks in each state.
    ///
    /// <b>Four samples since 2026-09-15, when the first component with a WRONG state arrived</b> -
    /// the box of seconds on the plan sheet, point 5 of `docs/11` 2.14. GUI rule 3 names four
    /// states for a control and rule 4 says this sheet shows every one of them, so the day a
    /// component could be wrong the sheet owed a column for it.
    /// </summary>
    public sealed record Entry(
        string Key,
        string Target,
        Sample Normal,
        Sample Disabled,
        Sample Wrong,
        Sample Chosen,
        Sample Extreme)
    {
        /// <summary>
        /// The same samples as a list with a heading each, for a group drawn one state UNDER the
        /// next rather than beside it - the views, which are as wide as a window and would not
        /// stand five abreast on any screen. Only the states the component has: a dash is left out
        /// here, where it would be a labelled row saying nothing.
        /// </summary>
        public IReadOnlyList<Labelled> Stacked { get; init; } = [];
    }

    /// <summary>One state of a component with the name of the state, for the stacked layout.</summary>
    public sealed record Labelled(string Heading, Sample Sample);

    /// <summary>The components declared in one theme file, in the order somebody can search.</summary>
    /// <remarks>
    /// The column names hang off the group rather than off the window, because the headings are
    /// repeated above every file - a heading printed once at the top of a sheet this long scrolls
    /// away after the first group, and every column under it is then unlabelled.
    ///
    /// <b>Five columns since 2026-09-16, and the fifth is the state a chip spends its working life
    /// in.</b> A filter chip that is lit, a row in the list under the search box that Down has
    /// reached, a column in the picker that is on - every one of them has a style with a trigger
    /// for it, and until that day the sheet drew each of them only in the state nobody looks at.
    /// </remarks>
    public sealed record Group(string Name, IReadOnlyList<Entry> Entries)
    {
        /// <summary>The first column of samples: the component as the window draws it.</summary>
        public string Normally { get; init; } = "as it ships";

        /// <summary>The second: the same thing with IsEnabled off.</summary>
        public string Off { get; init; } = "disabled";

        /// <summary>The third: holding something wrong, for the components that can.</summary>
        public string WhenWrong { get; init; } = "wrong";

        /// <summary>The fourth: checked, selected or switched on, for the components that can be.</summary>
        public string WhenChosen { get; init; } = "chosen";

        /// <summary>The fifth: more text than it has room for.</summary>
        public string TooMuch { get; init; } = "more text than fits";

        /// <summary>
        /// Whether the group is drawn one state under the next rather than five abreast - the
        /// views, which are as wide as a window. The sheet's markup picks the row template by
        /// this, and hides the column headings, which a stacked row carries on each state instead.
        /// </summary>
        public bool IsStacked { get; init; }

        /// <summary>
        /// The headings a group of VIEWS wears - the four states of a view with data, GUI rule
        /// 3, and the extreme one. Set on the group rather than on the window, because a column
        /// headed "disabled" with an empty list under it would teach the sheet to be read wrong.
        /// Each entry gets its states stacked under these headings, dashes left out.
        /// </summary>
        public static Group OfViews(IReadOnlyList<Entry> entries)
        {
            const string WithData = "with data";
            const string Empty = "empty";
            const string Wrong = "wrong";
            const string Loading = "loading";
            const string Extreme = "extreme";

            static IEnumerable<Labelled> States(Entry entry) =>
                new[] { (WithData, entry.Normal), (Empty, entry.Disabled), (Wrong, entry.Wrong), (Loading, entry.Chosen), (Extreme, entry.Extreme) }
                    .Where(state => state.Item2.Element is not null || state.Item2.Instead != NoSuchState)
                    .Select(state => new Labelled(state.Item1, state.Item2));

            return new Group("views", [.. entries.Select(entry => entry with { Stacked = [.. States(entry)] })])
            {
                IsStacked = true,
                Normally = WithData,
                Off = Empty,
                WhenWrong = Wrong,
                WhenChosen = Loading,
                TooMuch = Extreme
            };
        }
    }

    /// <summary>
    /// What the disabled column says for something that has no disabled state.
    ///
    /// <b>A dash rather than a sentence, and the first version was the sentence.</b> Twenty four
    /// of the fifty five components are TextBlock styles, so "not a control, so there is no
    /// disabled state to draw" was printed twenty four times down one column - three hundred
    /// words of the same six. The sentence is now said once, in the note at the top of the
    /// screen, which is where a thing that is true of the whole sheet belongs.
    /// </summary>
    private const string NoSuchState = "-";

    /// <summary>
    /// What the screen says about itself: how many components it knows about and how many it can
    /// draw.
    ///
    /// <b>Printed on the screen rather than kept for the guard</b>, because the guard runs in a
    /// test and the person looking at this screen is the one who needs to know that four of the
    /// fifty two are not on it.
    /// </summary>
    public sealed record Counts(int Known, int Shown, int Explained);

    /// <summary>
    /// Every component in the theme files, grouped by the file that declares it.
    /// </summary>
    /// <param name="resources">
    /// The application's own resources, merged. Handed in rather than reached for, so a test can
    /// ask the same question of the same dictionaries without an application running.
    /// </param>
    public static IReadOnlyList<Group> Read(ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var groups = new List<Group>();

        var limits = Limits.Of(resources);

        foreach (var dictionary in resources.MergedDictionaries)
        {
            var source = dictionary.Source?.OriginalString;

            // A DICTIONARY WITH NO SOURCE IS ONE OF OURS, AND THAT IS MEASURED RATHER THAN
            // ASSUMED. The first version of this line required the source to name Themes/, which
            // is true of every dictionary App.xaml merges and false of every dictionary the test
            // host merges: WpfHost reads the real files off disk with XamlReader.Load, and a
            // dictionary built from a STREAM has no Source at all. So the sheet was complete in
            // the product and empty in the tests, and CatalogueGuards said so on its first run -
            // all fifty five components reported missing.
            //
            // WPF UI'S OWN DICTIONARIES ARE THE THING BEING KEPT OUT, and they arrive with a pack
            // URI that names their assembly. If one of them ever arrives without one, the second
            // guard in CatalogueGuards goes red immediately: a catalogue showing a key no theme
            // file declares is the other half of rule 4.
            if (source is not null && !source.Contains(Ours, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entries = new List<Entry>();

            foreach (var key in dictionary.Keys)
            {
                // A dictionary holds brushes, thicknesses and numbers as well as styles, and the
                // three value files hold nothing else. Only a Style is a component.
                if (key is not string named || dictionary[key] is not Style style)
                {
                    continue;
                }

                entries.Add(Describe(named, style, limits));
            }

            if (entries.Count == 0)
            {
                continue;
            }

            // Sorted rather than left in dictionary order. A ResourceDictionary is a hash table
            // underneath, so its enumeration order is not the order the file was written in and
            // is not promised to be stable between runs - and a sheet that reorders itself
            // between two screenshots cannot be compared with the one before it.
            groups.Add(new Group(
                // The file name where there is one. Where there is not, the dictionary was read
                // from a stream - which happens in the test host and never in the product, so the
                // name is only ever seen by something that is counting keys rather than reading.
                source is null ? "read from disk" : System.IO.Path.GetFileName(source),
                entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray()));
        }

        // The keyed templates, after the styles - a second kind of component, since 2026-09-16.
        groups.AddRange(Templates(resources, limits));

        return groups;
    }

    /// <summary>How much of what is declared this screen can actually draw.</summary>
    public static Counts Count(IReadOnlyList<Group> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var all = groups.SelectMany(group => group.Entries).ToArray();

        return new Counts(
            all.Length,
            all.Count(entry => entry.Normal.Element is not null),
            all.Count(entry => entry.Normal.Element is null));
    }

    /// <summary>
    /// One component in its three arrangements.
    ///
    /// Disabled is asked of a <see cref="Control"/> only. Setting IsEnabled on a TextBlock is
    /// allowed and changes nothing anybody can see, so a column of identical text would be three
    /// hundred pixels of noise on a screen whose whole job is to make a difference visible.
    /// </summary>
    private static Entry Describe(string key, Style style, Limits limits)
    {
        var target = style.TargetType;

        // MEASURED RATHER THAN ASSUMED, 2026-09-10. This file was written expecting a focus ring
        // to have no target type at all, and the sheet then reported PrimaryActionFocusRing as an
        // IFrameworkInputElement "nothing knows how to build". A FocusVisualStyle does carry a
        // target type - that interface - so the honest place for it is beside the other four
        // things that cannot stand on their own, with the reason spelled out.
        var name = target is null ? "-" : target.Name;
        var why = WhyNot(name);

        if (why is not null)
        {
            return new Entry(
                key, name, Sample.None(why), Sample.None(string.Empty), Sample.None(string.Empty), Sample.None(string.Empty), Sample.None(string.Empty));
        }

        return new Entry(
            key,
            name,
            Make(name, style, Short, enabled: true, limits),
            Make(name, style, Short, enabled: false, limits),
            Wrong(name, style, limits),
            Chosen(name, style, limits),
            Make(name, style, Extreme(key), enabled: true, limits));
    }
}
