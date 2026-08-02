using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Keeps text a person reads out of the source files.
///
/// The command line tool exists only in English, so this is not about translation. It is
/// about being able to review the wording at all: part 5 of the glossary sets a standard
/// for it, and a standard needs one place to look. Strings scattered through code cannot
/// be reviewed, or even counted.
///
/// Deliberately narrow. 06-STRUKTURA-I-KONWENCJE says to estimate the false alarm rate
/// before building a guard, because one that shouts at correct code gets switched off
/// within a week and then protects nothing. This one only looks at literals handed
/// straight to a console write, and every legitimate call passes a variable instead, so
/// there is nothing correct for it to shout at.
/// </summary>
public sealed class UserFacingTextGuards
{
    private static readonly Regex LiteralToConsole = new(
        @"Console\.(Out|Error)\.Write\w*\(\s*[$@]*""",
        RegexOptions.Compiled,
        Sources.Ceiling);

    [Fact]
    public void No_shipped_source_file_writes_a_literal_string_to_the_console()
    {
        var offenders = new List<string>();

        foreach (var file in ShippedSourceFiles())
        {
            var lines = File.ReadAllLines(file);

            for (var index = 0; index < lines.Length; index++)
            {
                if (LiteralToConsole.IsMatch(lines[index]))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{index + 1}  {lines[index].Trim()}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "User-facing text belongs in a resource file, not in code:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static IEnumerable<string> ShippedSourceFiles() =>
        Directory
            .EnumerateFiles(Path.Combine(GuardedAssemblies.RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            // Generated interop and build intermediates are not ours to police.
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
