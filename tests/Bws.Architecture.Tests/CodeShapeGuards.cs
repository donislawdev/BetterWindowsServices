namespace Bws.Architecture.Tests;

/// <summary>
/// Ceilings on the shape of every method, signature and type, set at what the largest one is today
/// and allowed only to go down. Owner's decision 2026-09-23 - the design, the baseline and what was
/// weighed are in docs/PROJEKT-KSZTALT-KODU-20260923.md.
///
/// <b>Nobody reads this code line by line.</b> The owner steers by behaviour, so there is no reviewer
/// to notice a method reaching two hundred lines over six sessions, each of which added thirty and
/// each of which was reasonable on its own. A ceiling is the only thing that notices, and it costs
/// nothing while nothing grows. The file ratchet in <see cref="SizeRatchetGuards"/> did this for
/// files since 2026-08-02, and the same idea now covers the five axes a file ceiling cannot see:
///
///   - LENGTH of a method, in lines of code - comments are free, see <see cref="CodeShape"/>,
///   - COMPLEXITY, the forks in its path - a short method can be a knot,
///   - DEPTH, how far in its deepest block is indented on screen,
///   - WIDTH, how many arguments a caller has to line up,
///   - the TYPE, methods and state summed over every partial file - splitting a file does not
///     shrink the class a reader has to hold, and MainWindow in eleven files is the measured case.
///
/// <b>Lowering a number is ordinary work. Raising one is the owner's decision</b>, never a way to
/// get unblocked - a threshold bent to fit the code has stopped being a threshold. When a method
/// crosses one, the answer is to split the method.
///
/// <b>What this does not check</b>, the same list the Python suite it follows gives about itself:
/// whether a method does one thing, whether its name is honest, and whether splitting it scattered
/// the logic across ten places - that last one has its own cost and no metric.
/// </summary>
public sealed class CodeShapeGuards
{
    /// <summary>Every axis, src/ first. Growing this list is free, and each entry names its own numbers.</summary>
    internal static readonly ShapeAxis[] Axes =
    [
        new(ShapeCeilings.ShippedLength, "lines of code",
            "ShapeCeilings." + nameof(ShapeCeilings.LongestMethod), ShapeCeilings.LongestMethod,
            "ShapeCeilings." + nameof(ShapeCeilings.MethodsNearLongest), ShapeCeilings.MethodsNearLongest,
            ShapeAxis.Share(ShapeCeilings.LongestMethod, ShapeCeilings.NearShare),
            "Split the method along a seam it already has", () => Lengths(CodeShape.Shipped)),
        new(ShapeCeilings.ShippedBranching, "forks",
            "ShapeCeilings." + nameof(ShapeCeilings.MostComplexMethod), ShapeCeilings.MostComplexMethod,
            "ShapeCeilings." + nameof(ShapeCeilings.MethodsNearMostComplex), ShapeCeilings.MethodsNearMostComplex,
            ShapeAxis.Share(ShapeCeilings.MostComplexMethod, ShapeCeilings.NearShare),
            "Give a decision a name and a method of its own", () => Branching(CodeShape.Shipped)),
        new(ShapeCeilings.ShippedDepth, "levels",
            "ShapeCeilings." + nameof(ShapeCeilings.DeepestMethod), ShapeCeilings.DeepestMethod,
            "ShapeCeilings." + nameof(ShapeCeilings.MethodsNearDeepest), ShapeCeilings.MethodsNearDeepest,
            ShapeAxis.AtLeast(ShapeCeilings.DepthNear),
            "Pull the inner block out into a method of its own, or return early", () => Depths(CodeShape.Shipped)),
        new(ShapeCeilings.ShippedWidth, "parameters",
            "ShapeCeilings." + nameof(ShapeCeilings.WidestSignature), ShapeCeilings.WidestSignature,
            "ShapeCeilings." + nameof(ShapeCeilings.SignaturesNearWidest), ShapeCeilings.SignaturesNearWidest,
            ShapeAxis.AtLeast(ShapeCeilings.WidthNear),
            "Group the arguments that travel together into one type", () => Widths(CodeShape.Shipped)),
        new(ShapeCeilings.ShippedTypeMethods, "methods",
            "ShapeCeilings." + nameof(ShapeCeilings.MostMethodsInType), ShapeCeilings.MostMethodsInType,
            "ShapeCeilings." + nameof(ShapeCeilings.TypesNearMostMethods), ShapeCeilings.TypesNearMostMethods,
            ShapeAxis.Share(ShapeCeilings.MostMethodsInType, ShapeCeilings.NearShare),
            "Move a responsibility out into a type of its own - another partial file is not a smaller type",
            () => CodeShape.Shipped.Types.Select(type => new ShapeItem(type.Key, type.Where, type.Methods))),
        new(ShapeCeilings.ShippedTypeState, "fields",
            "ShapeCeilings." + nameof(ShapeCeilings.MostStateInType), ShapeCeilings.MostStateInType,
            "ShapeCeilings." + nameof(ShapeCeilings.TypesNearMostState), ShapeCeilings.TypesNearMostState,
            ShapeAxis.Share(ShapeCeilings.MostStateInType, ShapeCeilings.NearShare),
            "Move the state that changes together into a type of its own",
            () => CodeShape.Shipped.Types.Select(type => new ShapeItem(type.Key, type.Where, type.State))),
        new(ShapeCeilings.TestLength, "lines of code",
            "ShapeCeilings." + nameof(ShapeCeilings.LongestTestMethod), ShapeCeilings.LongestTestMethod,
            "ShapeCeilings." + nameof(ShapeCeilings.TestMethodsNearLongest), ShapeCeilings.TestMethodsNearLongest,
            ShapeAxis.Share(ShapeCeilings.LongestTestMethod, ShapeCeilings.NearShare),
            "Split the test, or give its arrangement a helper", () => Lengths(CodeShape.Testing)),
        new(ShapeCeilings.TestBranching, "forks",
            "ShapeCeilings." + nameof(ShapeCeilings.MostComplexTestMethod), ShapeCeilings.MostComplexTestMethod,
            "ShapeCeilings." + nameof(ShapeCeilings.TestMethodsNearMostComplex), ShapeCeilings.TestMethodsNearMostComplex,
            ShapeAxis.Share(ShapeCeilings.MostComplexTestMethod, ShapeCeilings.NearShare),
            "A test that decides this much is a second program - give the deciding a method", () => Branching(CodeShape.Testing)),
        new(ShapeCeilings.TestDepth, "levels",
            "ShapeCeilings." + nameof(ShapeCeilings.DeepestTestMethod), ShapeCeilings.DeepestTestMethod,
            "ShapeCeilings." + nameof(ShapeCeilings.TestMethodsNearDeepest), ShapeCeilings.TestMethodsNearDeepest,
            ShapeAxis.AtLeast(ShapeCeilings.TestDepthNear),
            "Pull the inner block out into a helper", () => Depths(CodeShape.Testing)),
        new(ShapeCeilings.TestWidth, "parameters",
            "ShapeCeilings." + nameof(ShapeCeilings.WidestTestSignature), ShapeCeilings.WidestTestSignature,
            "ShapeCeilings." + nameof(ShapeCeilings.TestSignaturesNearWidest), ShapeCeilings.TestSignaturesNearWidest,
            ShapeAxis.AtLeast(ShapeCeilings.TestWidthNear),
            "Group the arguments into one type", () => Widths(CodeShape.Testing)),
    ];

    public static TheoryData<string> AxisNames()
    {
        var names = new TheoryData<string>();
        foreach (var axis in Axes)
        {
            names.Add(axis.Name);
        }

        return names;
    }

    [Theory]
    [MemberData(nameof(AxisNames))]
    public void Every_ceiling_is_exactly_the_largest_thing_under_it(string axis)
    {
        var verdict = Axes.Single(candidate => candidate.Name == axis).CeilingVerdict();

        Assert.True(verdict is null, verdict);
    }

    [Theory]
    [MemberData(nameof(AxisNames))]
    public void Every_crowd_count_is_exactly_what_stands_near_its_ceiling(string axis)
    {
        var verdict = Axes.Single(candidate => candidate.Name == axis).CrowdVerdict();

        Assert.True(verdict is null, verdict);
    }

    [Fact]
    public void The_scan_read_the_whole_tree()
    {
        // The canary both projects this follows carry, for the same reason: a scan that finds
        // nothing satisfies every ceiling ever set, and looks exactly like a guard that works.
        foreach (var (pool, report) in new[] { ("src", CodeShape.Shipped), ("tests", CodeShape.Testing) })
        {
            Assert.True(
                report.Files.Count >= ShapeCeilings.FewestFilesRead && report.Units.Count >= ShapeCeilings.FewestUnitsRead,
                $"The shape scan of {pool}/ read {report.Files.Count} files and {report.Units.Count} units, below the floor " +
                $"of {ShapeCeilings.FewestFilesRead} and {ShapeCeilings.FewestUnitsRead}. An empty scan passes every ceiling, " +
                "so this is the scan being wrong, not the code being small.");
        }
    }

    [Fact]
    public void Every_file_parses_without_a_syntax_error()
    {
        // The canary for the parser falling behind the language. The build compiles LangVersion
        // latest, and a parser that does not know a new piece of syntax reads it as errors and then
        // measures the recovery. The package version is in Bws.Architecture.Tests.csproj.
        var broken = CodeShape.Shipped.Files.Concat(CodeShape.Testing.Files)
            .Where(file => file.SyntaxErrors.Count > 0)
            .Select(file => $"  {file.Name}: {file.SyntaxErrors[0]}")
            .ToArray();

        Assert.True(
            broken.Length == 0,
            "The parser behind the shape guards cannot read these files, so every number measured from them is " +
            "wrong. If they build, Microsoft.CodeAnalysis.CSharp is older than the language the build compiles - " +
            "move it to the Roslyn the SDK carries:" + Environment.NewLine + string.Join(Environment.NewLine, broken));
    }

    [Fact]
    public void Every_exemption_names_one_unit_that_still_stands_over_its_ceiling()
    {
        var problems = new List<string>();
        foreach (var exemption in ShapeCeilings.Exemptions)
        {
            var axis = Axes.SingleOrDefault(candidate => candidate.Name == exemption.Axis);
            var matches = axis?.Measure().Where(item => item.Name == exemption.Unit).ToArray() ?? [];

            if (axis is null || matches.Length != 1)
            {
                problems.Add($"  {exemption.Unit} on {exemption.Axis}: names {matches.Length} unit(s) on an axis that " +
                    (axis is null ? "does not exist" : "exists") + ", and it must name exactly one");
            }
            else if (matches[0].Value <= axis.Ceiling)
            {
                problems.Add($"  {exemption.Unit} on {exemption.Axis}: measures {matches[0].Value}, under the ceiling " +
                    $"of {axis.Ceiling}, so it no longer needs an exemption - remove it, the list only gets shorter");
            }
        }

        Assert.True(problems.Count == 0, "Stale exemptions from the shape ceilings:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static IEnumerable<ShapeItem> Lengths(ShapeReport report) =>
        report.Units.Select(unit => new ShapeItem(unit.Name, unit.Where, unit.CodeLines));

    private static IEnumerable<ShapeItem> Branching(ShapeReport report) =>
        report.Units.Select(unit => new ShapeItem(unit.Name, unit.Where, unit.Complexity));

    private static IEnumerable<ShapeItem> Depths(ShapeReport report) =>
        report.Units.Select(unit => new ShapeItem(unit.Name, $"{unit.File}:{unit.DeepestLine} {unit.Name}", unit.Depth));

    private static IEnumerable<ShapeItem> Widths(ShapeReport report) =>
        report.Signatures.Select(signature => new ShapeItem(signature.Name, signature.Where, signature.Parameters));
}
