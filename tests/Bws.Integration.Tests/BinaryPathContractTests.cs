using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// What the manager says a service runs, checked against sc qc.
///
/// The comparison matters more here than on most fields. The value arrives in one string
/// holding the executable and its arguments together, written in any of six shapes, and
/// every step of taking it apart is a chance to be confidently wrong. Checking the raw
/// string for a file on disk - the obvious first attempt - called 785 of 825 entries
/// broken on the machine this was written against.
///
/// Read-only throughout. Asking what a service runs, and whether that file is there,
/// changes nothing.
/// </summary>
public sealed class BinaryPathContractTests
{
    [Fact]
    public void The_launch_command_we_report_is_the_one_sc_reports()
    {
        // Taken from the machine rather than named here, because a service name written into
        // a test is a name that does not exist on somebody else's install. Eight is enough
        // to cover several of the shapes at once on any real machine.
        var sampled = 0;

        foreach (var entry in CommandLineTool.Listing("--query", "path:any !type:driver").Take(8))
        {
            var name = CommandLineTool.Text(entry, "serviceName");

            Assert.Equal(
                BinaryPathAccordingToServiceControl(name),
                CommandLineTool.Text(entry, "binaryPath"));

            sampled++;
        }

        // A loop that sampled nothing passes while checking nothing, which is the one way a
        // green run means least.
        Assert.Equal(8, sampled);
    }

    [Fact]
    public void Drivers_are_compared_too_because_they_are_where_the_odd_shapes_live()
    {
        // \SystemRoot\ and the bare relative form are almost entirely a driver habit: 237
        // and 216 entries of 825. Sampling only services would leave the two shapes most
        // likely to be resolved wrongly untested against anything.
        var sampled = 0;

        foreach (var entry in CommandLineTool.Listing("--query", "path:any type:driver").Take(8))
        {
            var name = CommandLineTool.Text(entry, "serviceName");

            Assert.Equal(
                BinaryPathAccordingToServiceControl(name),
                CommandLineTool.Text(entry, "binaryPath"));

            sampled++;
        }

        Assert.Equal(8, sampled);
    }

    [Fact]
    public void Every_file_we_call_present_is_a_file_that_is_there()
    {
        // The resolution checked from the outside. Our answer about the path is one thing
        // and the disk is another, so this asks the disk directly rather than trusting the
        // reading that produced the answer.
        var sampled = 0;

        foreach (var entry in CommandLineTool.Listing("--query", "file:present").Take(40))
        {
            var file = CommandLineTool.Text(entry, "binaryFile");

            Assert.True(
                File.Exists(file),
                $"{CommandLineTool.Text(entry, "serviceName")} was reported as having its file, " +
                $"and '{file}' is not there.");

            sampled++;
        }

        Assert.Equal(40, sampled);
    }

    [Fact]
    public void Every_file_we_call_missing_is_a_file_that_is_not_there()
    {
        // The direction that would hide a resolver quietly failing on one shape and calling
        // a whole family of entries broken. There are five such entries on the machine this
        // was written against, so this deliberately does not demand a sample size - on a
        // healthy machine the right answer is none at all.
        foreach (var entry in CommandLineTool.Listing("--query", "file:missing"))
        {
            var file = CommandLineTool.Text(entry, "binaryFile");

            Assert.False(
                File.Exists(file),
                $"{CommandLineTool.Text(entry, "serviceName")} was reported as having lost its file, " +
                $"and '{file}' is there.");
        }
    }

    [Fact]
    public void Nothing_is_called_missing_while_a_longer_reading_of_the_command_is_on_disk()
    {
        // This one exists because the tests above did not catch the mistake it is named for.
        // Cutting an unquoted command at the first space was put back into the resolver on
        // purpose to see what would notice, and the answer was three unit tests and none of
        // these - because that mistake is wrong about two entries out of 810, which no
        // sample and no proportion is going to see.
        //
        // So this asks the question the count cannot: when we say a file is not there, is
        // there a longer reading of the same command that is? An answer of yes means the
        // resolver stopped too early and called something broken that runs perfectly well.
        foreach (var entry in CommandLineTool.Listing("--query", "file:missing"))
        {
            var command = CommandLineTool.Text(entry, "binaryPath");
            var reported = CommandLineTool.Text(entry, "binaryFile");

            foreach (var longer in LongerReadingsOf(command, reported))
            {
                Assert.False(
                    File.Exists(longer),
                    $"{CommandLineTool.Text(entry, "serviceName")} was reported as having lost " +
                    $"'{reported}', while '{longer}' is on disk and is the same command read further.");
            }
        }
    }

    /// <summary>
    /// The readings of an unquoted command that go past the one we settled on.
    ///
    /// Only for the unquoted case: quotes end the file name and there is nothing further to
    /// read. The reported path is resolved and the command may not be, so the comparison is
    /// on how many words in the answer stops rather than on the text.
    /// </summary>
    private static IEnumerable<string> LongerReadingsOf(string command, string reported)
    {
        var trimmed = command.Trim();

        if (trimmed.Length == 0 || trimmed[0] == '"' || !trimmed.Contains(' '))
        {
            yield break;
        }

        var settled = reported.Count(character => character == ' ');

        for (var space = trimmed.IndexOf(' '); space >= 0; space = trimmed.IndexOf(' ', space + 1))
        {
            var candidate = trimmed[..space];

            if (candidate.Count(character => character == ' ') > settled)
            {
                yield return Resolved(candidate);
            }
        }

        if (trimmed.Count(character => character == ' ') > settled)
        {
            yield return Resolved(trimmed);
        }
    }

    /// <summary>
    /// The two prefixes that need expanding before a candidate can be looked for. Kept
    /// small on purpose: this is a second opinion on the resolver, so borrowing its code
    /// would make it agree with itself.
    /// </summary>
    private static string Resolved(string candidate)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        if (candidate.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            return candidate[4..];
        }

        if (candidate.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(windows, candidate[@"\SystemRoot\".Length..]);
        }

        return candidate.Length > 1 && candidate[1] == ':'
            ? candidate
            : Path.Combine(windows, candidate);
    }

    [Fact]
    public void Almost_everything_on_a_working_machine_has_its_file()
    {
        // Not a number pinned to one install, but the shape of the claim. A resolver that
        // stopped handling one of the six shapes would take a large family down with it, and
        // the failure would look exactly like a machine full of orphans.
        var missing = CommandLineTool.Listing("--query", "file:missing").Length;
        var present = CommandLineTool.Listing("--query", "file:present").Length;

        Assert.True(
            missing < present / 20,
            $"{missing} entries report a missing file against {present} that do not, which is the " +
            "shape of a resolver that gave up on one of the ways a path can be written.");
    }

    /// <summary>
    /// What sc qc says the service runs.
    ///
    /// The field is printed as a padded label and a value on one line, and the value can
    /// contain colons, spaces and quotes of its own - so it is split once, at the first
    /// colon after the label, and everything past that belongs to the value.
    /// </summary>
    private static string BinaryPathAccordingToServiceControl(string serviceName)
    {
        var line = CommandLineTool.ServiceControl("qc", serviceName).StandardOutput
            .Split('\n')
            .FirstOrDefault(text => text.Contains("BINARY_PATH_NAME", StringComparison.Ordinal))
            ?? string.Empty;

        var colon = line.IndexOf(':', line.IndexOf("BINARY_PATH_NAME", StringComparison.Ordinal) + 1);

        return colon < 0 ? string.Empty : line[(colon + 1)..].Trim();
    }
}
