using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Keeps the two output channels apart.
///
/// 02-DECYZJE-TECHNICZNE lists this as a public contract rather than a style: standard
/// output carries data and nothing else, everything else goes to the error channel, and a
/// failed run must not write a single character to the data channel. Break it and --json
/// stops being parsable the moment the first warning appears, which the admin who piped
/// our tool into something discovers in the middle of the night.
///
/// That document also names the way it breaks: one stray write in an error branch. The
/// integration tests cover the paths somebody thought to run, and a stray write in the
/// path nobody covered is exactly the one that slips through. These two catch it in the
/// source instead.
///
/// False alarm estimate, as 04-PLAN-PRAC requires before building a guard: the tool
/// renders one listing from one place, so the count below is one and any second one is a
/// genuine question, not noise. The bare Console.Write forms have no legitimate use here
/// at all, because writing data says Console.Out and writing anything else says
/// Console.Error, so neither guard has correct code to shout at.
/// </summary>
public sealed class OutputChannelGuards
{
    /// <summary>
    /// Matches Console.Write and Console.WriteLine, and deliberately not the same calls on
    /// Console.Out or Console.Error, which name the channel they mean.
    /// </summary>
    private static readonly Regex UnnamedChannel = new(
        @"\bConsole\.Write\w*\(", RegexOptions.Compiled, Sources.Ceiling);

    private static readonly Regex DataChannel = new(
        @"\bConsole\.Out\.", RegexOptions.Compiled, Sources.Ceiling);

    [Fact]
    public void No_shipped_source_file_writes_without_naming_the_channel()
    {
        // Console.WriteLine goes to standard output. It reads like a neutral "print", which
        // is exactly why it is the way a diagnostic ends up in the data channel by accident.
        var offenders = Offenders(UnnamedChannel);

        Assert.True(
            offenders.Count == 0,
            "Writing without naming the channel puts text on standard output, which carries " +
            "data only. Say Console.Error for anything a person reads, Console.Out for data:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Exactly_one_place_writes_to_the_data_channel()
    {
        var writers = Offenders(DataChannel);

        // Not a limit for its own sake. One writer is what makes "did anything else reach
        // standard output" answerable by looking, and a second one is the shape the contract
        // breaks in: a listing printed here, a summary printed there.
        Assert.True(
            writers.Count == 1,
            $"Expected exactly one place writing to the data channel, found {writers.Count}. " +
            "A second writer means the answer to what lands on standard output now depends " +
            "on which branch ran:" +
            Environment.NewLine + string.Join(Environment.NewLine, writers));
    }

    private static List<string> Offenders(Regex pattern)
    {
        var found = new List<string>();

        foreach (var file in ShippedSourceFiles())
        {
            var lines = File.ReadAllLines(file);

            for (var index = 0; index < lines.Length; index++)
            {
                // Comments talk about these calls, including in this file's own neighbours,
                // and a guard that counts prose would be noise from the first day.
                var code = lines[index].Trim();

                if (code.StartsWith("//", StringComparison.Ordinal) || code.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                if (pattern.IsMatch(code))
                {
                    found.Add($"{Path.GetFileName(file)}:{index + 1}  {code}");
                }
            }
        }

        return found;
    }

    private static IEnumerable<string> ShippedSourceFiles() =>
        Directory
            .EnumerateFiles(Path.Combine(SourceTree.Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
