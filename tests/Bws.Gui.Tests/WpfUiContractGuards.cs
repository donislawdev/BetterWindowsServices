// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it. Third
// time this project has paid for that line, so it is written down beside it.
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace Bws.Gui.Tests;

/// <summary>
/// Every name this product borrows from WPF UI still exists in WPF UI.
///
/// Written 2026-08-02, and one sentence is the whole specification for it: the library will be
/// updated, and an update must not break our interface.
///
/// <b>The failure this exists for is not hypothetical and it is not a build error.</b> Our
/// theme bases four default styles on names that live in their dictionaries -
/// <c>DefaultTextBoxStyle</c> and its siblings - and it paints the window with a brush of
/// theirs. A StaticResource that resolves to nothing is a <b>runtime</b> exception thrown while
/// XAML loads. So a rename on their side compiles perfectly, passes every existing test, and
/// kills the window on an administrator's machine at startup. This turns that into a red test
/// on the build that raises the version.
///
/// <b>The list of names is derived, never held.</b> It is read out of our own XAML by looking
/// for every resource reference the files do not themselves define. That is the same shape as
/// tools/audit/audit.ps1 and for the same reason: a guard carrying its own copy of the thing it
/// guards drifts away from it, and then goes red on a legitimate change while staying green on
/// the failure it was written for.
///
/// <b>What it cannot see, stated so nobody reads a green run as more than it is.</b> It checks
/// that a name resolves, not what the thing behind the name does. WPF UI could keep every key
/// and change a control template underneath it - and that is not a theoretical concern here,
/// because this product already depends on two specific facts about their templates: their row
/// paints <c>{TemplateBinding Background}</c> and their cell hardcodes <c>Transparent</c>. If
/// those ever swap, our three row colours stop reaching a pixel and everything here stays
/// green. <c>tools/gui-probe/check.ps1</c> is the instrument for that half, and it needs a
/// screen.
/// </summary>
public sealed class WpfUiContractGuards
{
    /// <summary>
    /// Where a resource reference is written, in either of the two forms XAML allows. Both are
    /// collected: DynamicResource is how the window takes their background brush, StaticResource
    /// is how the theme takes their styles, and losing either half would leave a real
    /// dependency unguarded.
    /// </summary>
    private static readonly Regex Reference = new(
        @"\{(?:Static|Dynamic)Resource\s+([A-Za-z][A-Za-z0-9_.]*)\s*\}",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    /// <summary>Where a name is declared, so that our own names are not mistaken for theirs.</summary>
    private static readonly Regex Declaration = new(
        @"x:Key\s*=\s*""([^""]+)""",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void Every_name_this_product_borrows_from_the_library_still_exists()
    {
        var borrowed = Borrowed();

        Assert.True(
            borrowed.Count > 0,
            "No borrowed names were found at all, which means this guard is passing by finding " +
            "nothing rather than by checking anything. Either the theme stopped using the " +
            "library or the scan below stopped matching.");

        var theirs = TheirKeys();
        var missing = borrowed.Where(name => !theirs.Contains(name)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            $"WPF UI no longer defines {missing.Count} of the {borrowed.Count} names this product " +
            "borrows from it. Each of these is a window that fails to load at startup rather than " +
            "a build that fails here:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// The library is present and answering. Without this the test above passes on an empty
    /// dictionary, which is exactly the state a broken package reference produces.
    /// </summary>
    [Fact]
    public void The_library_dictionaries_load_and_are_not_empty()
    {
        var theirs = TheirKeys();

        Assert.True(
            theirs.Count > 100,
            $"WPF UI's dictionaries resolved to {theirs.Count} named keys. They carried 844 when " +
            "this was written, so a number this small means they did not really load and every " +
            "other check here is meaningless.");
    }

    /// <summary>
    /// The library's menu item draws the key beside its label.
    ///
    /// <b>The row menu has relied on it since 2026-09-15</b>: "Show details" carries
    /// <c>InputGestureText</c> so that Enter is written where the person can see it - `docs/03`
    /// part 5, a shortcut nobody can find is a hole in discoverability. A template that dropped the
    /// gesture would leave the item looking finished and the key unannounced, with nothing in the
    /// build to say so. Asked of the library's own template, serialised, rather than of a menu on
    /// screen: this is a fact about what their dictionary hands over, not about a layout.
    /// </summary>
    [Fact]
    public void The_library_menu_item_template_draws_the_gesture_text()
    {
        var xaml = WpfHost.On(() =>
        {
            var style = (Style?)WpfHost.Resources[typeof(System.Windows.Controls.MenuItem)];

            Assert.NotNull(style);

            return System.Windows.Markup.XamlWriter.Save(style);
        });

        Assert.Contains("InputGestureText", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Names our XAML refers to and does not define. Whatever is left after subtracting our own
    /// declarations comes from underneath us, which today means WPF UI.
    /// </summary>
    private static SortedSet<string> Borrowed()
    {
        var referenced = new SortedSet<string>(StringComparer.Ordinal);
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in OurXaml())
        {
            var text = File.ReadAllText(file);

            foreach (Match match in Reference.Matches(text))
            {
                referenced.Add(match.Groups[1].Value);
            }

            foreach (Match match in Declaration.Matches(text))
            {
                declared.Add(match.Groups[1].Value);
            }
        }

        // Language keys look like resource references and are not: they are filled in at run
        // time from the embedded language file, by ADR-21, and none of them belongs to WPF UI.
        referenced.RemoveWhere(name => name.StartsWith("gui.", StringComparison.Ordinal));
        referenced.ExceptWith(declared);
        return referenced;
    }

    private static IEnumerable<string> OurXaml()
    {
        var gui = Path.Combine(SourceTree.Root(), "src", "Bws.Gui");
        return Directory
            .EnumerateFiles(gui, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every named key WPF UI defines, assembled the way App.xaml assembles it so that the
    /// answer is about the arrangement this product actually ships.
    /// </summary>
    private static HashSet<string> TheirKeys()
    {
        EnsurePackUrisWork();

        var keys = new HashSet<string>(StringComparer.Ordinal);

        Collect(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark }, keys);
        Collect(new Wpf.Ui.Markup.ControlsDictionary(), keys);
        return keys;
    }

    /// <summary>
    /// Makes <c>pack://</c> resolvable, which it is not in a process that has never had an
    /// Application.
    ///
    /// Two separate things are missing and only fixing both works, which is why this is a
    /// method with a comment rather than a line. The URI SCHEME is registered by touching
    /// PackUriHelper, and that alone gets as far as a Uri that parses and a request that cannot
    /// be made - "The URI prefix is not recognized" out of WebRequest.Create. The PREFIX
    /// HANDLER is registered by Application's static constructor, so the type has to be
    /// initialised even though no Application is ever run here.
    /// </summary>
    private static void EnsurePackUrisWork()
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;

        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(Application).TypeHandle);
    }

    private static void Collect(ResourceDictionary dictionary, HashSet<string> into)
    {
        foreach (var key in dictionary.Keys)
        {
            if (key is string named)
            {
                into.Add(named);
            }
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            Collect(merged, into);
        }
    }
}
