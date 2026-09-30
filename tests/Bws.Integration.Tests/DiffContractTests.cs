using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// The comparison command as somebody types it.
///
/// The engine has its own tests against fabricated snapshots, which is where the rules live.
/// This class is about the surface: what the exit codes mean, what happens when the arguments
/// do not add up, and the one claim only a real machine can settle - that a snapshot taken a
/// moment ago compares against the machine it came from with nothing to report.
///
/// Read-only. Taking a snapshot and comparing it changes nothing.
/// </summary>
public sealed class DiffContractTests
{
    // The three below never open the service control manager - comparing two files is offline by
    // design, and these three never get as far as reading either of them. So they run on a build
    // agent like any other test, which is what the trait says. The fourth one does not: it takes
    // a snapshot of the machine it is on, which is the one claim here only a real machine settles.
    [Fact]
    [Trait("runs", "anywhere")]
    public void One_file_on_its_own_is_refused_rather_than_guessed_at()
    {
        // The alternative would be to treat it as --live, which reads the whole machine and
        // needs rights over its services. A command does not start doing that because an
        // argument was left out.
        var run = CommandLineTool.Run("snapshot", "diff", Somewhere("only.json"));

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("--live", run.StandardError, StringComparison.Ordinal);
        Assert.Empty(run.StandardOutput);
    }

    [Fact]
    [Trait("runs", "anywhere")]
    public void Two_files_and_live_together_are_refused()
    {
        // Three sides to a comparison that takes two. Picking one silently would ignore
        // something the person typed.
        var run = CommandLineTool.Run("snapshot", "diff", Somewhere("a.json"), Somewhere("b.json"), "--live");

        Assert.Equal(2, run.ExitCode);
        Assert.Empty(run.StandardOutput);
    }

    [Fact]
    [Trait("runs", "anywhere")]
    public void A_file_that_is_not_there_is_answered_rather_than_thrown()
    {
        // Somebody pointed at the wrong file. That is an ordinary thing to do, and a stack
        // trace would say less while looking like the tool falling over.
        var run = CommandLineTool.Run("snapshot", "diff", Somewhere("a.json"), "--live");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("a.json", run.StandardError, StringComparison.Ordinal);
        Assert.Empty(run.StandardOutput);
    }

    [Fact]
    public void A_snapshot_taken_now_has_no_configuration_differences_against_the_machine_it_came_from()
    {
        // The claim the whole slice rests on, and the only one a fabricated snapshot cannot
        // make. Configuration only: running state moves on a live machine without anybody
        // touching it - measured on 2026-08-01, two entries started by themselves between a
        // snapshot and a comparison a minute later. Asserting on those would be a test that
        // fails for reasons that are not about this code.
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bws-difftest-{Guid.NewGuid():N}.json");

        try
        {
            Assert.Equal(0, CommandLineTool.Run("snapshot", "create", file).ExitCode);

            var run = CommandLineTool.Run("snapshot", "diff", file, "--live", "--json");

            Assert.Equal(0, run.ExitCode);

            var document = JsonDocument.Parse(run.StandardOutput).RootElement;

            Assert.Empty(document.GetProperty("added").EnumerateArray());
            Assert.Empty(document.GetProperty("removed").EnumerateArray());

            var configuration = document.GetProperty("changed").EnumerateArray()
                .SelectMany(entry => entry.GetProperty("differences").EnumerateArray())
                .Where(difference => difference.GetProperty("group").GetString() == "Configuration")
                .Select(difference => difference.GetProperty("field").GetString())
                .ToArray();

            Assert.True(
                configuration.Length == 0,
                "A snapshot compared against the machine it was just taken from reported " +
                $"configuration drift: {string.Join(", ", configuration)}");

            // And the one boolean a pipeline reads says the same, whatever started or stopped in the
            // minute between - since 2026-09-30 it answers about configuration only (D-2). Until
            // then this test could not ask it, for the reason the comment at the top gives.
            Assert.False(document.GetProperty("differs").GetBoolean());

            // Both sides were written by this build on this machine, so both know the Windows update
            // and the language the manager names things in (D-5). A build that stopped reading
            // either would land here as "not known" rather than as silence.
            Assert.Empty(document.GetProperty("caveats").GetProperty("notKnown").EnumerateArray());

            // And the comparison really had something to compare. Every assertion above is
            // satisfied by two empty snapshots agreeing perfectly, so the count comes from
            // the file itself rather than from the comparison's own account of its work.
            var compared = JsonDocument.Parse(File.ReadAllText(file))
                .RootElement.GetProperty("entries").GetArrayLength();

            Assert.True(compared > 100, $"The snapshot held {compared} entries, so agreeing about them proves little.");

            // No field went uncompared on both sides. A build that stopped reading something
            // would show up here rather than as silence.
            Assert.Empty(document.GetProperty("neitherRead").EnumerateArray());

            // And nothing went uncompared for a reason other than the machine refusing it, which
            // is the assertion that actually holds the live reading to its word. If the live side
            // skipped signatures and hashes, every entry would land here with three fields nobody
            // could compare - and every other assertion in this test would still pass, since an
            // uncompared field is not a difference. Found by asking what this test would miss
            // rather than by watching it fail.
            //
            // IT ASKED FOR ZERO UNTIL 2026-08-12, AND ZERO STOPPED BEING THE HONEST NUMBER when
            // the description arrived - it is the first field a normal elevated session is
            // genuinely REFUSED for. Eight entries of 809 on this machine carry a description the
            // manager will not resolve into words, Tcpip6 and UCPD among them, and Tcpip6's own
            // display name comes back as "@todo.dll,-100;Microsoft IPv6 Protocol Driver", which
            // says what kind of entry these are. Both sides refuse it, both sides say so, and a
            // comparison that reported "no change" about a value neither side could read would be
            // the confident wrong answer rule 8 exists to prevent.
            //
            // So the claim is now the one that was always meant: every field nobody could compare
            // is a field the machine refused, and the refusal is visible. A field going
            // uncomparable for any OTHER reason - a live reading quietly skipping what the file
            // holds - still fails here, because such a field would not be in this list.
            var refusable = new[] { "description" };

            var unexplained = document.GetProperty("notFullyCompared").EnumerateArray()
                .SelectMany(entry => entry.GetProperty("incomparable").EnumerateArray()
                    .Select(field => (Entry: entry.GetProperty("serviceName").GetString(), Field: field.GetString())))
                .Where(pair => !refusable.Contains(pair.Field, StringComparer.Ordinal))
                .Select(pair => $"{pair.Entry}.{pair.Field}")
                .ToArray();

            Assert.True(
                unexplained.Length == 0,
                "A field went uncompared for a reason other than the machine refusing it, which " +
                "means the live reading is skipping something the file holds: " +
                string.Join(", ", unexplained));
        }
        finally
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>
    /// Code 5 is drift, and running state is not drift - stability report D-2, owner's decision of
    /// 2026-09-30. Until then a nightly <c>--exit-code</c> ended with 5 over an entry that had only
    /// stopped by itself.
    ///
    /// <b>Nothing in the suite ran the exit code at all before this</b>, so the change of its meaning
    /// had nothing to redden. One real snapshot and two copies edited in one field each, so the only
    /// thing that differs between the two runs is which group the field belongs to.
    /// </summary>
    [Fact]
    public void The_exit_code_answers_about_configuration_and_never_about_running_state_alone()
    {
        var original = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bws-exitcode-{Guid.NewGuid():N}.json");
        var stateOnly = original.Replace(".json", "-state.json", StringComparison.Ordinal);
        var setUp = original.Replace(".json", "-setup.json", StringComparison.Ordinal);

        try
        {
            Assert.Equal(0, CommandLineTool.Run("snapshot", "create", original).ExitCode);

            File.WriteAllText(stateOnly, Edited(original, ("status", "Running", "Stopped")));
            File.WriteAllText(setUp, Edited(original, ("startType", "Manual", "Disabled")));

            var state = CommandLineTool.Run("snapshot", "diff", original, stateOnly, "--exit-code");

            Assert.Equal(0, state.ExitCode);
            Assert.Contains("Stopped", state.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(5, CommandLineTool.Run("snapshot", "diff", original, setUp, "--exit-code").ExitCode);
        }
        finally
        {
            foreach (var file in new[] { original, stateOnly, setUp }.Where(File.Exists))
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>The snapshot with one field of the first entry holding the given value changed.</summary>
    private static string Edited(string path, (string Field, string From, string To) change)
    {
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        var entry = document["entries"]!.AsArray()
            .First(candidate => candidate![change.Field]?.GetValue<string>() == change.From)!;

        entry[change.Field] = change.To;

        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>A path inside the temporary directory, which is where a stray file would do least harm.</summary>
    private static string Somewhere(string name) =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bws-missing-{Guid.NewGuid():N}-{name}");
}
