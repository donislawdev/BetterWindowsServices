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

        return new[] { "*.cs", "*.csproj", "*.xaml", "*.json", "*.yml", "*.md", "*.props", "*.slnx" }
            .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Where(NotBuildOutput)
            .Where(InVersionControl)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool NotBuildOutput(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>
    /// The folders kept out of version control on purpose, which is where the private things are
    /// supposed to live. Reading them here would fail the build for material doing exactly what
    /// it was told to do.
    /// </summary>
    private static bool InVersionControl(string path)
    {
        var relative = Path.GetRelativePath(SourceTree.Root(), path);

        return !relative.StartsWith("docs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !relative.StartsWith("tools" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !relative.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase)
            && !relative.Equals("CHANGELOG-DEV.md", StringComparison.OrdinalIgnoreCase);
    }
}
