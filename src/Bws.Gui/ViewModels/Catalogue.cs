using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Shapes;

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
public static class Catalogue
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
    /// <remarks>
    /// <see cref="Instead"/> is empty rather than null where there is a sample, because the sheet
    /// hides it with a trigger on the empty string - the same shape EmptyState.xaml uses. A null
    /// would not match that trigger and the explanation would sit under every working sample.
    /// </remarks>
    public sealed record Sample(FrameworkElement? Element, string Instead)
    {
        public static Sample Of(FrameworkElement element) => new(element, string.Empty);

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
        Sample Extreme);

    /// <summary>The components declared in one theme file, in the order somebody can search.</summary>
    /// <remarks>
    /// The three column names hang off the group rather than off the window, because the headings
    /// are repeated above every file - a heading printed once at the top of a sheet this long
    /// scrolls away after the first group, and every column under it is then unlabelled.
    /// </remarks>
    public sealed record Group(string Name, IReadOnlyList<Entry> Entries)
    {
        /// <summary>The first column of samples: the component as the window draws it.</summary>
        public string Normally => "as it ships";

        /// <summary>The second: the same thing with IsEnabled off.</summary>
        public string Off => "disabled";

        /// <summary>The third: holding something wrong, for the components that can.</summary>
        public string WhenWrong => "wrong";

        /// <summary>The fourth: more text than it has room for.</summary>
        public string TooMuch => "more text than fits";
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

                entries.Add(Describe(named, style));
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
    private static Entry Describe(string key, Style style)
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
                key, name, Sample.None(why), Sample.None(string.Empty), Sample.None(string.Empty), Sample.None(string.Empty));
        }

        return new Entry(
            key,
            name,
            Make(name, style, Short, enabled: true),
            Make(name, style, Short, enabled: false),
            Wrong(name, style),
            Make(name, style, Long, enabled: true));
    }

    /// <summary>
    /// The component holding something wrong, where its style says it can.
    ///
    /// <b>READ OFF THE STYLE RATHER THAN OFF A LIST OF KEYS, which is the property that keeps
    /// this column true after the interface is rebuilt.</b> A component has a wrong state when
    /// its style triggers on <c>Validation.HasError</c> - the framework's own flag, and the only
    /// one a theme file can trigger on without naming a type of this assembly (`docs/10` trap
    /// 10). So a style that gains such a trigger appears in this column with no edit here, and one
    /// that loses it drops out the same way. A dash for the rest, said once in the note at the top.
    ///
    /// <b>Put into the state the way the product puts it there</b>: the sample's text is bound and
    /// the binding is marked invalid, so <c>Validation.HasError</c> goes true on the control and
    /// the style's own trigger draws whatever it draws. Nothing here paints an edge - a sample the
    /// sheet coloured itself would be a picture of this file rather than of the component.
    /// </summary>
    private static Sample Wrong(string target, Style style)
    {
        if (!Triggers(style).Any(trigger => trigger.Property == Validation.HasErrorProperty))
        {
            return Sample.None(NoSuchState);
        }

        var made = Make(target, style, Short, enabled: true);

        if (made.Element is not TextBox box)
        {
            // The one control this table knows how to hand something wrong to. A style over
            // another type that learns to be wrong is a row here saying so, rather than a sample
            // of a state that was never entered - the same honesty as the disabled column.
            return Sample.None($"has a wrong state, but nothing here knows how to make a {target} wrong");
        }

        box.SetBinding(TextBox.TextProperty, new Binding(nameof(Held.Text)) { Source = new Held(Short) });

        var expression = BindingOperations.GetBindingExpression(box, TextBox.TextProperty)!;

        Validation.MarkInvalid(expression, new ValidationError(new NeverRight(), expression, Short, null));

        return Sample.Of(box);
    }

    /// <summary>Every trigger a style carries, including the ones it inherits through BasedOn.</summary>
    private static IEnumerable<Trigger> Triggers(Style style)
    {
        for (var at = style; at is not null; at = at.BasedOn)
        {
            foreach (var trigger in at.Triggers.OfType<Trigger>())
            {
                yield return trigger;
            }
        }
    }

    /// <summary>Something for a sample's text to be bound to, so that its binding can be marked wrong.</summary>
    private sealed record Held(string Text);

    /// <summary>
    /// A rule that is never satisfied - the shape <c>Validation.MarkInvalid</c> asks for, and
    /// nothing else: the sample is wrong because the sheet says so, not because of what it holds.
    /// </summary>
    private sealed class NeverRight : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo) =>
            new(false, Short);
    }

    /// <summary>
    /// Why a component cannot stand on its own, where that is the case.
    ///
    /// <b>Named one by one rather than caught by a rule</b>, because each of these has a different
    /// reason and a reader deciding whether the omission matters needs the reason rather than the
    /// category. All four are parts of the list, which the window itself shows and
    /// <c>tools/gui-probe/screens.ps1</c> photographs.
    /// </summary>
    private static string? WhyNot(string target) => target switch
    {
        nameof(DataGrid) => "the list itself. It is on every screenshot of this window",
        nameof(DataGridRow) => "only exists inside the list, and a row on its own has no columns",
        nameof(DataGridCell) => "only exists inside a row, which only exists inside the list",
        nameof(ToolTip) => "only appears over something. Rest the pointer on a sample above",

        // A focus ring. It has no control of its own - it is drawn AROUND whichever control has
        // the keyboard, which is why this screen asks the reader to press Tab rather than
        // pretending to show one.
        "IFrameworkInputElement" => "a focus ring. Press Tab on this sheet and watch it appear",

        // A style with no target type at all. Not seen in this product on 2026-09-10, and left
        // here rather than left to throw: a style that targets nothing is a style nothing can
        // wear, and the sheet should say that rather than fall over.
        "-" => "a style with no target type, so nothing can wear it",

        _ => null
    };

    /// <summary>
    /// A control of the given type, wearing the given style.
    ///
    /// <b>A table rather than reflection</b> - see the note at the top of this file about
    /// <c>LayeringGuards</c>. Twelve entries cover fifty two styles, and a target type that is not
    /// here is a red test rather than a missing row.
    /// </summary>
    private static Sample Make(string target, Style style, string text, bool enabled)
    {
        // AN INLINE RATHER THAN AN ELEMENT, since 2026-09-15, when the notice line got a link in
        // it. A Hyperlink cannot stand on a sheet by itself - it lives inside text - so the sample
        // is a TextBlock wearing nothing of its own, holding the one styled link. Disabled goes on
        // the TextBlock, and the link inherits it, which is how the product disables it too.
        if (target == nameof(System.Windows.Documents.Hyperlink))
        {
            var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(text)) { Style = style };

            return Sample.Of(new TextBlock(link) { TextWrapping = TextWrapping.Wrap, IsEnabled = enabled });
        }

        FrameworkElement? element = target switch
        {
            nameof(TextBlock) => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            nameof(Button) => new Button { Content = text },
            nameof(ToggleButton) => new ToggleButton { Content = text },
            nameof(CheckBox) => new CheckBox { Content = text },
            nameof(TextBox) => new TextBox { Text = text },
            nameof(MenuItem) => new MenuItem { Header = text },
            nameof(DataGridColumnHeader) => new DataGridColumnHeader { Content = text },

            // Added 2026-09-10 because the guard asked for it, on the catalogue's own sample box.
            // That is the mechanism working on the day it was written: a style over a control type
            // this table did not know reddened the build rather than printing a row saying nothing
            // could build it.
            nameof(ContentControl) => new ContentControl { Content = text },
            nameof(Border) => new Border { Child = new TextBlock { Text = text } },

            // The list under the search box, 2026-09-15 - the guard asked, as it did for the box
            // above. Two rows of the sample text, so that the list's own scrolling and the item's
            // states are both something to look at rather than one line.
            nameof(ListBox) => new ListBox { ItemsSource = new[] { text, text } },
            nameof(ListBoxItem) => new ListBoxItem { Content = text },
            nameof(Ellipse) => new Ellipse(),
            nameof(Path) => new Path(),
            nameof(Thumb) => new Thumb(),
            nameof(ScrollBar) => new ScrollBar(),
            _ => null
        };

        if (element is null)
        {
            return Sample.None($"nothing here knows how to build a {target} to look at");
        }

        element.Style = style;

        // Asked of the element rather than of the type, because IsEnabled on something that draws
        // no disabled state is a promise this screen would be making on the style's behalf.
        if (!enabled)
        {
            if (element is not Control)
            {
                return Sample.None(NoSuchState);
            }

            element.IsEnabled = false;
        }

        // MEASURED, BECAUSE AN EMPTY CELL IS A CLAIM ABOUT THE PRODUCT. Some styles describe how a
        // shape looks and leave WHAT it is to wherever it is used - FilterDisclosure is a Path
        // whose geometry comes from the control template around it, so on its own it is nothing at
        // all. The first version put those on the sheet as blank cells, which reads as a component
        // that draws nothing rather than as one that cannot stand alone.
        //
        // Asked of WPF rather than of the style: measuring is what the layout pass does anyway,
        // and a setter chain walked by hand would be this file guessing at BasedOn.
        try
        {
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            if (element.DesiredSize.Width <= 0 || element.DesiredSize.Height <= 0)
            {
                return Sample.None("nothing on its own - its shape or size comes from where it is used");
            }
        }
        catch (InvalidOperationException)
        {
            // A control that will not measure outside a tree is still worth showing: the sheet
            // puts it in a real window, where the layout pass will measure it properly. Swallowed
            // deliberately and narrowly - this is a question being asked early, not work.
        }

        return Sample.Of(element);
    }
}
