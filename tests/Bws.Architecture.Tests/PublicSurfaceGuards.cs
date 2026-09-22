using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Nothing private reaches the files that go out to the world.
///
/// <b>This repository is meant to be public, and public means all of it.</b> Not only the code -
/// every comment, every test fixture, every commit message and every pull request. A machine
/// name, an account name or a home directory written into a comment is published the moment the
/// repository is, and it stays published: a later commit removing it does not remove it from the
/// history.
///
/// <b>Why this is a test and not a paragraph in a document.</b> The rule already existed in
/// prose. Prose is guarded by nothing, and the one thing that has ever caught this class of
/// mistake in this project is a run that goes red.
///
/// <b>The trap this guard had to avoid being.</b> A guard that looks for one particular machine
/// name has to contain that machine name, and this file is public - so it would publish the very
/// thing it exists to keep back. Hence two halves:
///
///   The half here looks for SHAPES rather than values. A home directory path, an e-mail
///   address, a private key header - naming those gives nothing away, and they are what actual
///   leaks look like.
///
///   The half that knows particular names lives outside the repository, in tools/privacy, which
///   is not in version control. When it is absent this says so out loud instead of passing
///   quietly, because somebody cloning this project has no such file and must not be failed for
///   it - and a check that reports nothing is a check that proves nothing.
/// </summary>
public sealed class PublicSurfaceGuards
{
    /// <summary>
    /// Shapes that are private wherever they appear, so they can be named in a public file.
    ///
    /// Kept narrow on purpose. A guard that cries about legitimate content is a guard people
    /// learn to skip, and the ones below have all been checked to find nothing today, which is
    /// what makes them worth keeping.
    /// </summary>
    private static readonly (string Name, string Pattern)[] Shapes =
    [
        // A home directory carries the account name of whoever wrote the line.
        ("a home directory path", @"[A-Za-z]:\\Users\\[A-Za-z0-9._-]+"),

        // Same on the other side of the family, for when this project grows a tool that runs there.
        ("a unix home directory path", @"/(?:home|Users)/[A-Za-z0-9._-]+"),

        // An address belongs to a person, and a repository is not where somebody consents to
        // publishing theirs. The one legitimate use - a contact address in a licence or a
        // security policy - lives in files this guard does not read.
        ("an e-mail address", @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"),

        // The worst case, and cheap to look for.
        ("a private key", @"BEGIN (?:OPENSSH|RSA|DSA|EC|PGP) PRIVATE KEY"),
        ("an ssh private key file", @"id_(?:rsa|ed25519|ecdsa)\b"),

        // Addresses on somebody's own network. The 10.x range is deliberately NOT here: .NET
        // version strings like 10.0.17763.57 have the same shape, and a guard with a false
        // alarm in it is a guard that gets muted rather than fixed.
        ("a private network address", @"\b(?:192\.168|169\.254|172\.(?:1[6-9]|2[0-9]|3[01]))\.\d{1,3}\.\d{1,3}\b")
    ];

    [Fact]
    public void No_shape_that_belongs_to_a_person_is_written_into_a_published_file()
    {
        var found = new List<string>();
        var read = 0;

        foreach (var file in Published())
        {
            var text = File.ReadAllText(file);
            read++;

            foreach (var (name, pattern) in Shapes)
            {
                var match = Regex.Match(text, pattern, RegexOptions.None, Sources.Ceiling);

                if (match.Success)
                {
                    found.Add($"  {name} in {Path.GetFileName(file)}: {match.Value}");
                }
            }
        }

        // A scan that read nothing finds nothing, and reports it in the same green as a scan
        // that read everything. The number is a floor rather than a count, so it survives files
        // being added and split - what it refuses is the day this stops seeing the repository
        // at all, which is a filter mistake away.
        Assert.True(
            read > 50,
            $"Only {read} published files were read, so this passed by looking at almost "
            + "nothing. The filter is wrong, not the repository.");

        Assert.True(
            found.Count == 0,
            "These go out to the world the moment this repository does, and a later commit "
            + "removing them does not remove them from the history:"
            + Environment.NewLine + string.Join(Environment.NewLine, found));
    }

    /// <summary>
    /// Files allowed to hold text outside plain ASCII, each with the reason it is allowed.
    ///
    /// <b>This exists because of what got through without it.</b> The continuous integration
    /// workflow carried a sentence of Polish for two days - a remark quoted from a conversation,
    /// in a file that goes out with the repository. Two rules said it should not be there: this
    /// project writes everything in its files in English, and nothing said in a conversation
    /// belongs in a published file. Neither rule was checked by anything.
    ///
    /// <b>A blanket ban would be wrong and would be turned off within a week</b>, because
    /// several files carry non-English text for good reasons - a copyright holder's name is not
    /// negotiable, and a service display name captured from a localised Windows is evidence.
    /// So this is a list with a reason beside each entry, and a file that is not on it fails.
    /// Adding a file here is the moment somebody has to say why, which is the whole mechanism.
    /// </summary>
    private static readonly Dictionary<string, string> MayHoldOtherAlphabets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["THIRD-PARTY-NOTICES.md"] =
            "copyright holders' names, which a licence requires to be reproduced as written",

        ["tests/Bws.Core.Tests/Fakes/Specimens.cs"] =
            "service display names captured from a localised Windows - the evidence that a "
            + "service name and a display name are two different things",

        ["tests/Bws.Core.Tests/Fakes/Specimens.Processes.cs"] =
            "the same captured display names, in the half of that catalogue the size ratchet "
            + "moved out on 2026-08-25 - a per-user pair whose two display names differ only "
            + "by the session suffix",

        ["tests/Bws.Core.Tests/QueryOverSpecimensTests.cs"] =
            "queries asked against those captured display names, including one that proves "
            + "matching ignores case outside ASCII too",

        ["src/Bws.Core/Reading.cs"] =
            "one sentence of a localised refusal, quoted to show why a message is carried "
            + "beside its number instead of being compared as words",

        ["src/Bws.Cli/ListingJson.cs"] =
            "names a character that a wrong console encoding turns every localised name into",

        ["src/Bws.Core/Snapshots/SnapshotJson.cs"] =
            "the same character, for the same reason, on the writing side",

        ["tests/Bws.Core.Tests/SnapshotDiffTests.cs"] =
            "a localised refusal quoted to show two machines answering the same question in "
            + "two languages",

        ["tests/Bws.Integration.Tests/SnapshotContractTests.cs"] =
            "an accented word written to a file on purpose, to prove the encoding survives",

        ["README.md"] =
            "one star character on the line asking for a star, the same line the owner's other "
            + "public repositories carry - the rest of the file is plain ASCII on purpose",

        ["site/i18n/pl.json"] =
            "the website's Polish chrome and the sentences beside its generated tables. The "
            + "site speaks two languages by the owner's decision, and this is the ONE JSON file "
            + "that holds words a visitor reads - everything else a visitor reads is in a .html "
            + "fragment, which this sweep does not cover. Putting the Polish in page.json files "
            + "instead would be a dozen more permissions on this list"
    };

    [Fact]
    public void Nothing_outside_plain_ascii_appears_without_a_reason_on_the_list()
    {
        var root = SourceTree.Root();
        var unexplained = new List<string>();

        foreach (var file in Published())
        {
            // The website's own pages are the exception, and by extension rather than by name.
            // The site is published in Polish as well as English on the owner's decision, so
            // every one of its fragments holds another alphabet on purpose - a permission per
            // file would be two dozen entries saying the same sentence, and a list that long
            // stops being read. The private-name sweep below still covers them, and that is
            // the check these files actually need.
            if (file.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Letters, not bytes. This reads the file as text, so what is being asked is
            // whether anybody wrote something in another alphabet - not how it is encoded.
            if (!File.ReadAllText(file).Any(character => character > 127))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (!MayHoldOtherAlphabets.ContainsKey(relative))
            {
                unexplained.Add("  " + relative);
            }
        }

        Assert.True(
            unexplained.Count == 0,
            "Everything in this repository's files is written in English, and these hold "
            + "something that is not. If there is a reason - captured data, a name that has to "
            + "be reproduced as written - put the file on the list in this class with that "
            + "reason. If there is not, it is prose that wandered in from somewhere else:"
            + Environment.NewLine + string.Join(Environment.NewLine, unexplained));
    }

    /// <summary>
    /// The list of surfaces kept out of the sweep is the one thing here that cannot prove
    /// itself, and this is the guard for it.
    ///
    /// <b>Why the other three tests cannot cover this.</b> They all assert that a set is EMPTY -
    /// no private shape, no unexplained alphabet, no stale permission. Widen the exclusion list
    /// until it excludes everything and all three stay green, because a sweep over nothing finds
    /// nothing. That is also why this class has no mutation entry of the usual kind: breaking
    /// the sweep does not redden anything. It reddens this.
    ///
    /// <b>Written on 2026-09-05, when the list gained two entries.</b> The owner's decision, and
    /// the reasoning is in docs/06. The list had named four surfaces while .gitignore names six,
    /// and nothing could see the difference.
    ///
    /// <b>It asks both directions.</b> That the six really are excluded, and that the places
    /// where published files live really are not - because a guard that excludes everything and
    /// a guard that works look identical from outside.
    /// </summary>
    [Fact]
    public void The_surfaces_left_out_of_the_sweep_are_the_ones_that_are_outside_git()
    {
        var root = SourceTree.Root();

        (string Path, bool Swept)[] cases =
        [
            // Outside version control by decision, so private material is supposed to live there.
            (Path.Combine("docs", "01-SPEC-PRODUKTOWY.md"), false),
            (Path.Combine("tools", "check.ps1"), false),
            (Path.Combine("artifacts", "anything.md"), false),
            (Path.Combine(".claude", "settings.local.json"), false),
            ("CLAUDE.md", false),
            ("CHANGELOG-DEV.md", false),

            // Published, and every one of these has to stay swept. A change that quietly stops
            // reading src/ would make all three of the sweeps above pass over an empty set.
            (Path.Combine("src", "Bws.Core", "ScmEntry.cs"), true),
            (Path.Combine("tests", "Bws.Architecture.Tests", "PublicSurfaceGuards.cs"), true),
            (Path.Combine(".github", "workflows", "anything.yml"), true),
            ("README.md", true),
            ("CHANGELOG.md", true),
        ];

        var wrong = new List<string>();

        foreach (var (relative, swept) in cases)
        {
            if (InVersionControl(Path.Combine(root, relative)) != swept)
            {
                wrong.Add($"  {relative} is {(swept ? "not swept but should be" : "swept but should not be")}");
            }
        }

        Assert.True(
            wrong.Count == 0,
            "The list of surfaces this guard leaves alone no longer matches what is outside "
            + "version control. Either .gitignore moved or InVersionControl did, and the two "
            + "have to say the same thing - docs/06 carries why:"
            + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    public void The_list_does_not_keep_permissions_nobody_uses_any_more()
    {
        // The other direction. An entry left behind after the text it excused was rewritten is
        // a hole standing open, and it looks exactly like a considered decision.
        var root = SourceTree.Root();

        var stale = MayHoldOtherAlphabets.Keys
            .Where(relative =>
            {
                var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                return !File.Exists(file) || !File.ReadAllText(file).Any(character => character > 127);
            })
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            stale.Count == 0,
            "These are allowed to hold text outside ASCII and no longer do. Take them off the "
            + "list rather than leaving a permission nobody is using:"
            + Environment.NewLine + string.Join(Environment.NewLine, stale.Select(name => "  " + name)));
    }

    [Fact]
    public void The_list_of_particular_names_is_either_applied_or_reported_as_absent()
    {
        var list = Path.Combine(SourceTree.Root(), "tools", "privacy", "patterns.txt");

        if (!File.Exists(list))
        {
            // Not a failure. A clone has no tools directory - it is outside version control -
            // and failing a stranger's build over a file they were never given would teach
            // them to delete this test. Saying it out loud is the whole of what is owed here.
            Assert.True(true, "no local pattern list, so only the shapes above were checked");
            return;
        }

        var patterns = File
            .ReadAllLines(list)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        Assert.True(
            patterns.Count > 0,
            $"'{list}' is there and holds no patterns, which reads like a check that ran and "
            + "found nothing. Either put the names in it or delete the file.");

        var found = new List<string>();

        foreach (var file in Published())
        {
            var text = File.ReadAllText(file);

            // Ordinal and case-insensitive: a machine name typed in a different case is the
            // same machine name, and none of these are words in any language.
            foreach (var pattern in patterns.Where(pattern =>
                text.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                // The pattern itself is NOT put in the message. This output ends up in build
                // logs, and a build log naming what it was told to keep back has published it.
                found.Add($"  {Path.GetFileName(file)} holds one of the names from {list}");
            }
        }

        Assert.True(
            found.Count == 0,
            "A name from the local list is in a file that goes out to the world:"
            + Environment.NewLine + string.Join(Environment.NewLine, found.Distinct()));
    }

    /// <summary>
    /// Everything that will be readable by anybody once this repository is public.
    ///
    /// Source and project files, the workflow, and the markdown at the root. Deliberately not
    /// only <c>src</c>: a test fixture is as public as a shipped file, and the fixtures in this
    /// project are built from readings taken off real machines, which is exactly where a private
    /// path would arrive without anybody deciding to put one there.
    /// </summary>
    private static IEnumerable<string> Published()
    {
        var root = SourceTree.Root();

        // *.html and *.css joined the list on 2026-09-22, when the website arrived in this
        // repository. They are the most public files here - a visitor reads them without
        // cloning anything - and until they were added, the ONE sweep that matters for them
        // was not running: the private-name check. They are deliberately NOT held to ASCII,
        // because the site speaks Polish as well as English and every Polish word a visitor
        // reads lives in a fragment; the test above therefore skips this pair by extension
        // rather than by a permission per file.
        //
        // *.py and *.txt joined it later the same day, with the supply chain gates, and for the
        // same reason as .html did: two Python scripts went into .github/scripts and would have
        // been the only published files in this repository that no sweep read. They hold paths,
        // measurements and the names of other repositories, which is exactly the material a
        // home directory or an address wanders into. .txt brings in the two NativeMethods.txt
        // lists that CsWin32 reads and the pinned scanner version - all three already ASCII,
        // checked when they were added here.
        return new[] { "*.cs", "*.csproj", "*.xaml", "*.json", "*.yml", "*.md", "*.props", "*.slnx", "*.html", "*.css", "*.py", "*.txt" }
            .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Where(NotBuildOutput)
            .Where(InVersionControl)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <remarks>
    /// <b>dist/ joined obj/ and bin/ on 2026-09-23, and it arrived by turning this guard red.</b>
    /// packaging/build-dist.ps1 stages LICENSE and THIRD-PARTY-NOTICES.md beside each executable,
    /// because the licences on the borrowed code require their notices to travel with it - and
    /// the notices file quotes a copyright sign and a name with an umlaut in it, both of which
    /// are on the permission list under the file's real path at the repository root. The copy in
    /// the staging folder is a different path, so it had no permission and was reported as prose
    /// that wandered in.
    ///
    /// The finding was real and was about the guard: a sweep that reads build output reports the
    /// same file twice and one of the two can never be fixed, because it is written by a script
    /// every time it runs.
    /// </remarks>
    private static bool NotBuildOutput(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>
    /// The folders kept out of version control on purpose, which is where the private things are
    /// supposed to live. Reading them here would fail the build for material doing exactly what
    /// it was told to do.
    ///
    /// <b>Two of these six arrived on 2026-09-05, and the list had been wrong since the day
    /// <c>artifacts/</c> was first ignored.</b> This method named four surfaces. <c>.gitignore</c>
    /// ignores six - it also ignores <c>artifacts/</c> at line 24 and <c>.claude/</c> at line 54.
    /// So two directories that are exactly as private as <c>docs/</c> were being read as if they
    /// were published.
    ///
    /// <b>Nothing ever noticed, and the reason is worth keeping.</b> The sweep above looks at
    /// <c>*.cs</c>, <c>*.md</c>, <c>*.json</c> and their kin. Until that day <c>artifacts/</c>
    /// held only screenshots and a built executable, and <c>.claude/</c> held one settings file
    /// with nothing private in it - so no file this guard reads had ever lived there. The first
    /// Polish <c>.md</c> written into <c>artifacts/</c> reddened three tests at once, which is
    /// the guard being right about the file and wrong about the folder.
    ///
    /// <b>What this costs, said rather than left to be found.</b> Anything genuinely private
    /// that lands in those two directories is now unguarded there, exactly as it already is in
    /// <c>docs/</c> and <c>tools/</c>. That is the trade this whole method makes: the four
    /// surfaces outside git are where private material is SUPPOSED to live, and a guard that
    /// fails the build for material doing what it was told is a guard people switch off. The
    /// owner decided this on 2026-09-05 and the reasoning is in docs/06.
    /// </summary>
    private static bool InVersionControl(string path)
    {
        var relative = Path.GetRelativePath(SourceTree.Root(), path);

        string[] outside = ["docs", "tools", "artifacts", ".claude"];

        foreach (var directory in outside)
        {
            if (relative.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return !relative.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase)
            && !relative.Equals("CHANGELOG-DEV.md", StringComparison.OrdinalIgnoreCase);
    }
}
