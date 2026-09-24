namespace Bws.Architecture.Tests;

/// <summary>
/// Nothing in the product is left over from a change that moved on without it.
///
/// Nobody reads this code line by line - the owner steers by behaviour, and the assistant that
/// writes it forgets between sessions. A helper written in one session and superseded in the next
/// keeps compiling, keeps passing, and keeps being read as something that matters. On the day this
/// was written it found eight such things, and three of them had a comment describing a caller,
/// a purpose or a placement that was no longer true - the prose outlived the code it was about.
///
/// <b>Two halves, and the other half is not here.</b> Private members are held by IDE0051 and
/// IDE0052 in the build. This holds what no analyser can see: a public or internal member reached
/// from nowhere across the three assemblies and their markup. <see cref="DeadCode"/> is the scan,
/// and says what it cannot see.
///
/// <b>A test is not a consumer.</b> A definition only its tests name is dead code with a test suite
/// attached - it passes, it reads as maintained, and nothing in the program would notice it gone.
/// When it exists FOR the tests, that is a reason, and the reason goes on the list below.
/// </summary>
public sealed class DeadCodeGuards
{
    /// <summary>
    /// The floor under the definitions read, well below the 2074 counted on the day on purpose. It
    /// catches a scan that read nothing, not a tree that shrank.
    /// </summary>
    private const int FewestDefinitionsRead = 1000;

    private const string ScreenSeam =
        "Exists for the tests, and says so: it reads what reached the screen rather than what the model " +
        "holds, so a binding that resolved to nothing comes back empty instead of right. The window has no " +
        "use for it and no test could replace it with a model property without losing exactly that.";

    private const string TimeSeam =
        "Exists for the tests, and says so: the working answer is the one a person gets, and a test cannot be " +
        "asked to wait for real time or for a real run to be in the state it needs.";

    /// <summary>
    /// Reached from nowhere on purpose, each with the reason it stays. The same rule as every list in
    /// this project that excuses something: it may only get shorter, and the test below it refuses an
    /// entry the day that entry gains a caller or disappears.
    /// </summary>
    private static readonly Dictionary<string, string> KeptWithoutACaller = new(StringComparer.Ordinal)
    {
        ["Bws.Gui.ActionBar.Offering"] = ScreenSeam,
        ["Bws.Gui.ActionBar.OverviewBack"] = ScreenSeam,
        ["Bws.Gui.FilterRow.Switch"] = ScreenSeam,
        ["Bws.Gui.StatusRow.NoticeLinkWords"] = ScreenSeam,
        ["Bws.Gui.MainWindow.HeadingMenu"] = ScreenSeam,
        ["Bws.Gui.MainWindow.HoldingForScroll"] = ScreenSeam,
        ["Bws.Gui.PlanView.TheSheet"] = ScreenSeam,
        ["Bws.Gui.PlanView.StepLines"] = ScreenSeam,
        ["Bws.Gui.PlanView.ProblemLines"] = ScreenSeam,
        ["Bws.Gui.PlanView.CommandLines"] = ScreenSeam,
        ["Bws.Gui.PlanView.FailureLines"] = ScreenSeam,
        ["Bws.Gui.PlanView.OfferLines"] = ScreenSeam,
        ["Bws.Gui.PlanView.WarningLines"] = ScreenSeam,
        ["Bws.Gui.PlanView.AlsoStopLines"] = ScreenSeam,
        ["Bws.Gui.PlanView.WayBackLines"] = ScreenSeam,
        ["Bws.Gui.PlanView.ProblemsShown"] = ScreenSeam,
        ["Bws.Gui.PlanView.WayBackShown"] = ScreenSeam,
        ["Bws.Gui.PlanView.StepsShown"] = ScreenSeam,
        ["Bws.Gui.PlanView.CommandsShown"] = ScreenSeam,
        ["Bws.Gui.MainWindow.TakeThisAsARun"] = TimeSeam,
        ["Bws.Gui.ViewModels.Planned.Clock"] = TimeSeam,
        ["Bws.Core.Querying.QueryParser.SyntaxVersion"] =
            "The version of the query syntax, raised from 1 to 2 when a bare word in the window changed meaning. " +
            "The query language document records it as a frozen contract with no reader today, which is a debt " +
            "written down there - deleting the number would not pay it, only hide it.",
    };

    private static readonly Lazy<DeadCodeScan> Product = new(() => DeadCodeScan.Of(
        Sources.Shipped().Select(path => (CodeShape.NameOf(path), CodeShape.TreeOf(path).GetRoot())),
        Sources.Testing().Select(path => CodeShape.TreeOf(path).GetRoot()),
        Sources.ShippedMarkup().Select(File.ReadAllText)));

    [Fact]
    public void Nothing_in_the_product_is_reached_only_by_its_tests_or_by_nothing()
    {
        var scan = Product.Value;

        // The canary every scan here carries: one that read nothing finds nothing, in the same green.
        Assert.True(
            scan.FilesRead >= ShapeCeilings.FewestFilesRead && scan.Definitions.Count >= FewestDefinitionsRead,
            $"The dead-code scan read {scan.FilesRead} files and {scan.Definitions.Count} definitions - it is reading the wrong place.");

        var found = scan.Unreached()
            .Where(definition => !KeptWithoutACaller.ContainsKey(definition.Key))
            .Select(definition => $"  {definition.Where}{(scan.NamedByTests(definition) ? " - named only by tests" : string.Empty)}")
            .ToArray();

        Assert.True(
            found.Length == 0,
            "Nothing alive reaches these. Delete one - or, if it exists for the tests or is read by the " +
            "serialiser, put it on KeptWithoutACaller with the reason. Careful with the second: a property only " +
            "the serialiser reads is a field of a snapshot or of the JSON output, and those are frozen contracts " +
            "(docs/02), so deleting one changes what the product writes." +
            Environment.NewLine + string.Join(Environment.NewLine, found));
    }

    [Fact]
    public void The_list_of_code_kept_without_a_caller_names_only_code_that_still_has_none()
    {
        // The other direction, and the one that lets a list rot into a wish: an entry that gained a
        // caller, or whose code is gone, reads as a reason still being given.
        var unreached = Product.Value.Unreached().Select(definition => definition.Key).ToHashSet(StringComparer.Ordinal);
        var stale = KeptWithoutACaller.Keys.Where(key => !unreached.Contains(key)).ToArray();

        Assert.True(
            stale.Length == 0,
            "These are on the list of code kept without a caller, and either have a caller now or are gone. " +
            "Take them off, so the list keeps meaning what it says:" +
            Environment.NewLine + string.Join(Environment.NewLine, stale));
    }
}
