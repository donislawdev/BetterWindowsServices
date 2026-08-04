using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Everything this program carries out into the world is named where the law expects it.
///
/// <b>This is an obligation, not tidiness.</b> The MIT licence - which is what most of the
/// borrowed code here is under - grants everything on one condition: "The above copyright notice
/// and this permission notice shall be included in all copies or substantial portions of the
/// Software." Ship a release with somebody's compiled library inside it and no notice, and the
/// grant that made it legal was not met.
///
/// <b>Why it can go wrong quietly.</b> A package added to a shipped project arrives with no
/// prompt and no warning. The build stays green, the tests stay green, and the omission only
/// becomes visible to somebody reading the release afterwards. That is precisely the shape this
/// project builds guards for.
///
/// <b>What this cannot do, said so a green run is not read as more than it is.</b> It compares
/// NAMES against THIRD-PARTY-NOTICES.md. It does not read anybody's licence, does not know
/// whether the terms changed between versions, and cannot tell whether a package started
/// incorporating something new. Those are questions for a person, and the notices file says when
/// a person last answered them.
/// </summary>
public sealed class LicenceNoticeGuards
{
    private const string Notices = "THIRD-PARTY-NOTICES.md";

    /// <summary>
    /// Projects whose output reaches a user. A package referenced here travels with the program.
    /// Test projects are deliberately out of scope - nothing they pull in is ever shipped, and
    /// the notices file lists them separately as a courtesy to whoever audits the dependencies.
    /// </summary>
    private static readonly string[] Shipped = ["Bws.Core", "Bws.Cli", "Bws.Gui"];

    [Fact]
    public void The_licence_of_this_program_is_in_the_repository_and_is_the_one_that_was_chosen()
    {
        var licence = Path.Combine(SourceTree.Root(), "LICENSE");

        Assert.True(File.Exists(licence), $"There is no LICENSE at '{licence}'.");

        var text = File.ReadAllText(licence);

        // Both halves. The heading alone appears in plenty of files that only mention the
        // licence, and the version line is what separates GPL 3 from every other GNU licence.
        Assert.Contains("GNU GENERAL PUBLIC LICENSE", text, StringComparison.Ordinal);
        Assert.Contains("Version 3, 29 June 2007", text, StringComparison.Ordinal);

        // A truncated licence file is a licence file that grants nothing, and it looks fine
        // from the top. The real text runs past six hundred lines.
        Assert.True(
            text.Split('\n').Length > 600,
            "The LICENSE file is too short to be the whole GPL 3 text. A truncated licence "
            + "reads as complete and is not.");
    }

    [Fact]
    public void Every_package_that_ships_is_named_in_the_notices()
    {
        var notices = Path.Combine(SourceTree.Root(), Notices);

        Assert.True(
            File.Exists(notices),
            $"There is no {Notices}. This program redistributes other people's compiled code, "
            + "and the licences on it require their notices to travel with it.");

        var text = File.ReadAllText(notices);
        var missing = new List<string>();

        foreach (var project in Shipped)
        {
            foreach (var package in PackagesOf(project))
            {
                if (!text.Contains(package, StringComparison.OrdinalIgnoreCase))
                {
                    missing.Add($"  {package}, referenced by {project}");
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            $"These are referenced by a project that ships and are not named in {Notices}. "
            + "Read the licence in the package on disk - not the label on its listing - and "
            + "write down what it requires:"
            + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void The_notices_do_not_name_packages_that_no_longer_exist()
    {
        // The other direction, and the one that rots silently. A notices file listing a package
        // that was removed a year ago is a file nobody has checked, and its confident tone is
        // the problem: the next reader has no way to tell which half of it is still true.
        var text = File.ReadAllText(Path.Combine(SourceTree.Root(), Notices));

        // Everything resolved, not only what a project file names. WPF-UI.Abstractions arrives
        // through WPF-UI rather than being asked for, and it ships - so a notices file that
        // lists it is right and a check that called it stale was wrong. That was this guard's
        // first finding and it was about the guard.
        var referenced = Shipped
            .SelectMany(PackagesOf)
            .Concat(TestPackages())
            .Concat(Resolved())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Only lines that look like a package reference are examined - a table row or a heading
        // naming a package and a version. Prose about a component inside somebody else's
        // library is not a package we reference and must not be dragged in here.
        // Hyphens belong in the name part. Without them this cut "UI.Abstractions" out of
        // "WPF-UI.Abstractions" and reported a package nobody had ever referenced - the guard
        // inventing a finding on its first run, which is the failure mode that makes a guard
        // worth less than nothing.
        var named = Regex
            .Matches(text, @"([A-Za-z][A-Za-z0-9-]*(?:\.[A-Za-z][A-Za-z0-9-]*)+)\s+\d+\.\d+\.\d+", RegexOptions.None, Sources.Ceiling)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => !referenced.Contains(name))
            .ToList();

        Assert.True(
            named.Count == 0,
            $"{Notices} names packages with versions that no project references any more. "
            + "Either they came back out and the entry should go, or the name drifted:"
            + Environment.NewLine + string.Join(Environment.NewLine, named.Select(name => "  " + name)));
    }

    private static IEnumerable<string> PackagesOf(string project) =>
        PackagesIn(Path.Combine(SourceTree.Root(), "src", project, project + ".csproj"))
            .Concat(PackagesIn(Path.Combine(SourceTree.Root(), "Directory.Build.props")));

    /// <summary>
    /// Every package the restore actually resolved, transitive ones included.
    ///
    /// Read out of the assets file the build writes, because that is the only place the full
    /// graph is written down. Absent before a restore has run, and empty is a safe answer here -
    /// it makes the check stricter rather than blinder, and these tests run after a build.
    /// </summary>
    private static IEnumerable<string> Resolved() =>
        Directory
            .EnumerateFiles(SourceTree.Root(), "project.assets.json", SearchOption.AllDirectories)
            .SelectMany(file => Regex
                .Matches(
                    File.ReadAllText(file),
                    @"""([A-Za-z][A-Za-z0-9._-]*)/\d+\.\d+\.\d+[^""]*""\s*:\s*\{",
                    RegexOptions.None,
                    Sources.Ceiling)
                .Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> TestPackages() =>
        Directory
            .EnumerateFiles(Path.Combine(SourceTree.Root(), "tests"), "*.csproj", SearchOption.AllDirectories)
            .SelectMany(PackagesIn);

    private static IEnumerable<string> PackagesIn(string projectFile)
    {
        if (!File.Exists(projectFile))
        {
            return [];
        }

        return Regex
            .Matches(
                File.ReadAllText(projectFile),
                @"<PackageReference\s+Include=""([^""]+)""",
                RegexOptions.None,
                Sources.Ceiling)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
