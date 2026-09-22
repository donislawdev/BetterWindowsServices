using System.Text.Json;
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

            // AND THE ONES NOBODY ASKED FOR, which until 2026-09-22 this test did not demand.
            //
            // It read project files only, so a package arriving as somebody else's dependency
            // and travelling in the build output was never required to have a notice.
            // WPF-UI.Abstractions is exactly that - it comes through WPF-UI, it ships, and it is
            // in the notices because a person put it there rather than because anything asked.
            // The licence does not care which half of the graph a library came from.
            foreach (var package in ShippingAssetsOf(project))
            {
                if (!text.Contains(package, StringComparison.OrdinalIgnoreCase))
                {
                    missing.Add($"  {package}, which {project} does not reference but does carry");
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

    /// <summary>
    /// Every package that puts a real assembly into this project's output, whether it was asked
    /// for or not.
    ///
    /// <b>"A real assembly" is doing all the work in that sentence, and getting it wrong is how
    /// this ends up crying about nothing.</b> Measured 2026-09-22 while writing it: a naive read
    /// of the resolved graph reports four packages with no notice, and ALL FOUR are false.
    /// <c>Bws.Core</c> is ours. <c>Microsoft.Windows.SDK.Win32Docs</c>,
    /// <c>Microsoft.Windows.SDK.Win32Metadata</c> and <c>Microsoft.Windows.WDK.Win32Metadata</c>
    /// are what CsWin32 reads at build time to generate the interop, and they ship nothing at
    /// all - the first of them carries a runtime entry that is the empty placeholder
    /// <c>lib/netstandard2.0/_._</c>, which reads like an assembly and is not one.
    ///
    /// <b>Checked against a real publish rather than trusted.</b> Publishing Bws.Gui
    /// framework-dependent put exactly four assemblies beside ours: <c>Wpf.Ui.dll</c>,
    /// <c>Wpf.Ui.Abstractions.dll</c>, <c>Microsoft.Windows.SDK.NET.dll</c> and
    /// <c>WinRT.Runtime.dll</c>. The first two are the packages this method returns. The last
    /// two come from the Windows Desktop targeting pack rather than from any PackageReference,
    /// so they are in no assets graph and this method cannot see them - they have their own
    /// sections in the notices file, written by a person, and that is the arrangement.
    ///
    /// Absent before a restore, and an empty answer then, which makes this check blinder rather
    /// than louder. These tests run after a build, so it is present.
    /// </summary>
    private static IEnumerable<string> ShippingAssetsOf(string project)
    {
        var assets = Path.Combine(SourceTree.Root(), "src", project, "obj", "project.assets.json");

        // FAILS CLOSED, and the first version of this did not. It returned an empty sequence
        // when the file was missing, with a comment saying that made the check "blinder rather
        // than louder" - which is the polite way of describing a guard that passes because it
        // read nothing. Every other sweep in this repository refuses that, and a second review
        // asked why this one did not. Restore writes this file before any build, so its absence
        // means the tests are being run somewhere nobody intended, and saying so is cheaper than
        // a green run that proves nothing.
        Assert.True(
            File.Exists(assets),
            $"There is no '{assets}'. Restore writes it, so these guards are running against a "
            + "tree that was never restored - and without it this check cannot see which packages "
            + "ship, so it would pass by reading nothing.");

        // Parsed rather than pattern-matched, and that is not a preference. The entry for a
        // package that ships is "Name/1.2.3": { "type": "package", "runtime": { "lib/x/y.dll":
        // {} } }, a placeholder is the same shape with "_._" as its only key, and a project
        // reference says "type": "project". Three distinctions inside nested objects is where a
        // regular expression starts agreeing with itself, and the file is JSON either way.
        using var document = JsonDocument.Parse(File.ReadAllText(assets));

        Assert.True(
            document.RootElement.TryGetProperty("targets", out var targets),
            $"'{assets}' has no 'targets' section, so nothing here can tell which packages ship. "
            + "Either the file is truncated or NuGet changed its shape, and both are reasons to "
            + "stop rather than to pass.");

        var shipping = new List<string>();

        foreach (var framework in targets.EnumerateObject())
        {
            foreach (var entry in framework.Value.EnumerateObject())
            {
                if (!entry.Value.TryGetProperty("type", out var type) || type.GetString() != "package")
                {
                    continue;
                }

                // THREE SECTIONS, NOT ONE, and the first version read only the first of them.
                // `runtime` is where a plain managed assembly lands, `native` is where an
                // unmanaged one does, and `runtimeTargets` is where a package that carries a
                // different binary per architecture puts them. All three reach the published
                // program, so all three create the obligation this test is about. A second
                // review pointed at the omission; checked the same day against this tree, no
                // package here uses the other two today, so adding them changes nothing now and
                // is the difference between a guard that works and one that happens to.
                var carriesAnAssembly = new[] { "runtime", "native", "runtimeTargets" }
                    .Where(section => entry.Value.TryGetProperty(section, out _))
                    .Select(section => entry.Value.GetProperty(section))
                    // ENDS WITH, not equals, and that distinction cost a red run. The
                    // placeholder is written as a PATH - "lib/netstandard2.0/_._" - so comparing
                    // the whole key against "_._" matched nothing and three build-time metadata
                    // packages were reported as shipping. They carry no assembly at all; the
                    // entry exists to say so, which is exactly what "_._" means in this file.
                    .Any(section => section
                        .EnumerateObject()
                        .Any(asset => !asset.Name.EndsWith("_._", StringComparison.Ordinal)));

                if (carriesAnAssembly)
                {
                    shipping.Add(entry.Name.Split('/')[0]);
                }
            }
        }

        return shipping.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_file_here_carries_somebody_elses_licence_header()
    {
        // THE ROUTE NEITHER OTHER LAYER CAN SEE, and it is the one that actually gets GPL
        // projects into trouble. A dependency is declared, resolved, graphed and reviewed - the
        // licence gate on a pull request reads every one a change adds. A file PASTED into src/
        // is none of those things. It is a new source file, it compiles, every test stays green,
        // and the obligation it carries is invisible to everything in this repository.
        //
        // What this looks for is the marks such a file arrives with. Somebody copying a class
        // out of another project almost always brings the header, because deleting it is a
        // deliberate act and keeping it is the default.
        //
        // WHAT IT CANNOT DO, said plainly so a green run is not read as more than it is. A
        // snippet pasted WITHOUT its header is invisible here, and that is the majority of the
        // risk rather than a corner of it - somebody lifting twenty lines off a forum brings no
        // notice with them. This raises the floor from nothing to something; it is not a
        // provenance check, and there is no cheap one.
        //
        // Measured before it was switched on: 468 files, zero hits on all five marks.
        var found = new List<string>();
        var read = 0;

        foreach (var file in OurOwnSource())
        {
            var text = File.ReadAllText(file);
            read++;

            foreach (var (name, pattern) in SomebodyElsesHeader)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.None, Sources.Ceiling))
                {
                    found.Add($"  {name} in {Path.GetRelativePath(SourceTree.Root(), file).Replace('\\', '/')}");
                }
            }
        }

        // A sweep that read nothing finds nothing and reports it in the same green as a sweep
        // that read everything.
        Assert.True(
            read > 100,
            $"This guard read {read} files and this repository has hundreds. It is looking in "
            + "the wrong place, so its green result means nothing.");

        Assert.True(
            found.Count == 0,
            "These files carry a licence header that is not ours. If code was copied in, that "
            + "is a licence question before it is a code question: read the terms, decide "
            + "whether GPL-3.0 can carry them, keep the notice where the licence requires it, "
            + "and write the decision into THIRD-PARTY-NOTICES.md. If the match is a false "
            + "alarm, narrow the pattern rather than deleting the check:"
            + Environment.NewLine + string.Join(Environment.NewLine, found));
    }

    /// <summary>
    /// Every source file this project wrote itself, which is all of them today.
    ///
    /// <b>Except this one, and the exception is the same trap PublicSurfaceGuards names about
    /// itself.</b> A guard that looks for a shape has to contain that shape, so this file holds
    /// the MIT permission sentence and the SPDX marker as literals - and on its first run it
    /// reported ITSELF, twice. One file by name rather than a pattern: anything broader would
    /// be a way to quieten the check by moving code into whatever the exclusion covers.
    /// </summary>
    private static IEnumerable<string> OurOwnSource() =>
        new[] { "src", "site", "tests" }
            .SelectMany(folder => new[] { "*.cs", "*.xaml", "*.css", "*.js", "*.html" }
                .SelectMany(pattern => Directory.EnumerateFiles(
                    Path.Combine(SourceTree.Root(), folder), pattern, SearchOption.AllDirectories)))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !string.Equals(
                Path.GetFileName(path), "LicenceNoticeGuards.cs", StringComparison.Ordinal));

    /// <summary>
    /// Marks that somebody else's code carries when it is copied rather than referenced.
    ///
    /// Narrow on purpose, and every one of these has been checked to find nothing in this tree
    /// today - which is what makes them worth keeping, the same argument PublicSurfaceGuards
    /// makes about its own list of shapes.
    /// </summary>
    private static readonly (string Name, string Pattern)[] SomebodyElsesHeader =
    [
        // The one that travels with almost every copied file, and the one the MIT licence
        // itself requires to be kept.
        //
        // The sign is written \xA9 - a REGEX escape passed through the verbatim string, not a
        // C# one - so this file stays plain ASCII. Writing the character itself here cost a red
        // run while this was being written: PublicSurfaceGuards holds every published file to
        // ASCII, and it caught it. Two guards, working.
        ("a copyright line", @"(?i)copyright\s*(\(c\)|\xA9)"),

        // The machine-readable form, which a modern file carries instead of a paragraph.
        ("an SPDX identifier", "SPDX-License-Identifier"),

        // The two paragraph headers that name a licence outright.
        ("the MIT permission notice", "Permission is hereby granted, free of charge"),
        ("an Apache notice", "Licensed under the Apache License"),

        // Microsoft's own header, which is what a file lifted out of the .NET or WPF
        // repositories looks like - the single most likely source of a paste in this project.
        ("a .NET Foundation header", @"Licensed to the \.NET Foundation")
    ];

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
