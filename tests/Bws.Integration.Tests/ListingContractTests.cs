using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// Runs the real command line tool against the real service control manager and checks
/// the things the owner can check by hand: does it agree with the system, does it keep
/// its output channels apart, does it end with the right code.
///
/// Read-only. Nothing here changes any service, so it is safe on the machine we work on.
/// Anything that writes belongs on a throwaway virtual machine.
/// </summary>
public sealed class ListingContractTests
{
    [Fact]
    public void The_entry_count_agrees_with_the_service_control_manager()
    {
        var ours = CommandLineTool.Listing().Length;
        var theirs = CommandLineTool.ServiceControlCount("query", "type=", "all", "state=", "all");

        // sc.exe is the authority here. A difference is a bug on our side unless it is
        // deliberate and explained, and for the total there is nothing to explain away.
        Assert.Equal(theirs, ours);
    }

    [Fact]
    public void The_driver_count_agrees_with_the_service_control_manager()
    {
        var ours = CommandLineTool.Listing().Count(entry =>
            CommandLineTool.Text(entry, "entryType") is "KernelDriver" or "FileSystemDriver");

        Assert.Equal(CommandLineTool.ServiceControlCount("query", "type=", "driver", "state=", "all"), ours);
    }

    [Fact]
    public void We_show_more_services_than_sc_because_we_include_per_user_ones()
    {
        var ours = CommandLineTool.Listing().Count(entry =>
            CommandLineTool.Text(entry, "entryType") is not ("KernelDriver" or "FileSystemDriver"));

        var theirs = CommandLineTool.ServiceControlCount("query", "type=", "service", "state=", "all");

        // This is the one intended difference. sc.exe asks for plain Win32 services only,
        // so it leaves out per-user services. We show them, collapsed later in the GUI.
        // Asserting "greater" rather than an exact gap keeps this from breaking every time
        // somebody logs in or out.
        Assert.True(
            ours > theirs,
            $"Expected more entries than sc.exe reports for services. Ours: {ours}, sc.exe: {theirs}.");
    }

    [Fact]
    public void The_two_sides_of_the_per_user_family_are_not_swapped()
    {
        // THE MISTAKE THIS FIELD INVITES, AND THE ONE NO COUNT CAN SEE.
        //
        // An instance carries the template bit as well as its own - 0xe0 on this machine,
        // which is instance plus template plus share-process - so a reader that asks about
        // the template bit first calls every instance a template. Both counts stay exactly
        // as plausible as they were, and every entry is on the wrong side.
        //
        // The oracle is the shape Windows gives the names: an instance is its template's
        // name plus an underscore and the session. That relationship does not survive the
        // sides being exchanged, because a bare name never begins with a suffixed one.
        var listing = CommandLineTool.Listing();

        var templates = Named(listing, "Template");
        var instances = Named(listing, "Instance");

        Assert.NotEmpty(templates);
        Assert.NotEmpty(instances);

        Assert.All(instances, instance => Assert.Contains(
            templates,
            template => instance.StartsWith(template + "_", StringComparison.OrdinalIgnoreCase)));
    }

    private static string[] Named(JsonElement[] listing, string role) =>
        [.. listing
            .Where(entry => string.Equals(
                CommandLineTool.Text(entry, "perUserRole"), role, StringComparison.Ordinal))
            .Select(entry => CommandLineTool.Text(entry, "serviceName"))];

    [Fact]
    public void Data_goes_to_the_output_channel_and_nothing_else_does()
    {
        var run = CommandLineTool.Run("list", "--json");

        Assert.Equal(0, run.ExitCode);

        // Valid JSON with nothing else mixed in. The moment a warning slips into this
        // channel, every pipe reading our output breaks.
        var parsed = JsonDocument.Parse(run.StandardOutput);
        Assert.True(parsed.RootElement.GetArrayLength() > 0);
    }

    [Fact]
    public void A_failed_run_writes_nothing_to_the_data_channel()
    {
        var run = CommandLineTool.Run("list", "--no-such-option");

        Assert.Equal(2, run.ExitCode);
        Assert.Empty(run.StandardOutput.Trim());
        Assert.Contains("Unknown option", run.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void An_option_that_needs_a_value_and_has_none_is_a_mistake_rather_than_an_empty_query()
    {
        // Reading it as "no query" would quietly list every entry on the machine, which is
        // the opposite of what somebody who typed --query wanted.
        var run = CommandLineTool.Run("list", "--query");

        Assert.Equal(2, run.ExitCode);
        Assert.Empty(run.StandardOutput.Trim());
    }

    [Theory]
    [InlineData(0, "list", "--json")]
    [InlineData(0, "list", "--query", "status:running", "--json")]
    [InlineData(0, "list", "--query", "name:nothing-is-called-this", "--json")]
    [InlineData(2, "list", "--no-such-option")]
    [InlineData(2, "list", "--query")]
    [InlineData(2, "list", "--query", "stat:running")]
    [InlineData(2, "list", "--query", "status:runing")]
    [InlineData(2, "list", "--query", "pid:abc")]
    [InlineData(2, "list", "--query", "name:/[unclosed/")]
    [InlineData(2, "list", "stray-word")]
    public void Every_way_this_can_end_keeps_the_channels_apart(int expected, params string[] arguments)
    {
        // One case per way the tool can finish, because a stray write in the branch nobody
        // ran is the one that reaches somebody's pipe. The exit codes are a public contract
        // of their own: monitoring and CI branch on them, so they are pinned here rather
        // than left to whatever the code happens to return.
        var run = CommandLineTool.Run(arguments);

        Assert.Equal(expected, run.ExitCode);

        if (expected == 0)
        {
            // Valid JSON and nothing else mixed in, even when the query matched nothing.
            _ = JsonDocument.Parse(run.StandardOutput);
        }
        else
        {
            Assert.Empty(run.StandardOutput);
            Assert.NotEmpty(run.StandardError.Trim());
        }
    }

    [Fact]
    public void A_partial_answer_is_not_a_failure()
    {
        // Reporting an ordinary lack of permissions as a failing run would make scripts stop
        // on something normal. The warning goes to the error channel and the code stays zero.
        var run = CommandLineTool.Run("list", "--query", "account:?", "--json");

        Assert.Equal(0, run.ExitCode);
        _ = JsonDocument.Parse(run.StandardOutput);
    }

    /// <summary>
    /// The other direction of that relation is read when it is asked for, and not before - and
    /// what comes back agrees with sc.exe.
    ///
    /// <b>Both halves, because either alone passes over a build that does nothing.</b> A test that
    /// only asked WITH the switch would pass on one that always read them, which is the cost this
    /// family exists to avoid - a call per entry, measured at 236-259 ms over 313 services. A test
    /// that only checked the field was null without it would pass on one that never reads them at
    /// all.
    ///
    /// <b>sc.exe is the authority, as it is for the declared direction above.</b> Its own note
    /// records that `sc enumdepend` truncates its output at three entries and exits with
    /// ERROR_MORE_DATA - so the comparison runs over services whose answer is short enough for it
    /// to be believed, and says how many it checked.
    /// </summary>
    [Fact]
    public void Who_depends_on_a_service_is_read_when_asked_for_and_left_alone_when_not()
    {
        // WITHOUT THE SWITCH: nobody looked, so the field is null and the entry says so in
        // "notRead" rather than reporting an empty list of dependents.
        foreach (var entry in CommandLineTool.Listing("--query", "name:spooler"))
        {
            Assert.Equal(JsonValueKind.Null, entry.GetProperty("requiredBy").ValueKind);

            Assert.True(
                entry.TryGetProperty("notRead", out var notRead)
                && notRead.EnumerateArray().Any(field => field.GetString() == "requiredBy"),
                "The listing left requiredBy unread and did not say so, which is a short answer "
                + "that looks complete.");
        }

        // WITH IT: the field is filled, and by something the system agrees with.
        var read = 0;

        foreach (var entry in CommandLineTool.Listing("--required-by", "--query", "!type:driver"))
        {
            if (entry.GetProperty("requiredBy").ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var name = CommandLineTool.Text(entry, "serviceName");

            // SKIPPED RATHER THAN COMPARED WHEN THE AUTHORITY CUT ITS OWN ANSWER SHORT. sc.exe
            // truncates enumdepend at three entries and exits with ERROR_MORE_DATA without
            // retrying - measured 2026-08-01 - so a longer list is one it cannot speak about, and
            // comparing ours against what it managed would fail the build over sc's own limit.
            if (DependentsBySc(name) is not { } theirs)
            {
                continue;
            }

            var ours = entry.GetProperty("requiredBy").EnumerateArray().Select(value => value.GetString()!);

            Assert.Equal(
                string.Join('|', theirs).ToLowerInvariant(),
                string.Join('|', ours.Order(StringComparer.OrdinalIgnoreCase)).ToLowerInvariant());

            read++;
        }

        Assert.True(read > 0, "Not one service reported anybody depending on it, so this proves nothing.");
    }

    /// <summary>
    /// What sc.exe says depends on a service, sorted so the comparison is about the names.
    ///
    /// <b>Only the ones it can answer about.</b> Its output truncates at three and exits with
    /// ERROR_MORE_DATA without retrying - measured 2026-08-01 and recorded at
    /// <c>IScmCatalog.ReadDependents</c> - so anything longer than that is skipped rather than
    /// compared against a list the authority itself cut short.
    /// </summary>
    private static IReadOnlyList<string>? DependentsBySc(string name)
    {
        var run = CommandLineTool.ServiceControl("enumdepend", name);

        var names = run.StandardOutput
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.TrimStart().StartsWith("SERVICE_NAME:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Split(':', 2)[1].Trim())
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ASKED OF THE EXIT CODE RATHER THAN COUNTED, AND THE COUNT IS WHAT BROKE THIS TEST.
        //
        // It said "three is where sc stops, so three or more may have been cut short" - measured
        // on 2026-08-01 and true that day. On 2026-09-10 this test went red on a green tree with
        // sc answering TWO names for RpcSs and DcomLaunch, so the skip never fired and our two
        // hundred names were compared against sc's two.
        //
        // WHY TWO AND NOT THREE: sc.exe fills a fixed buffer and stops when the next name will
        // not fit, so how many it manages depends on how LONG the names are. One of them that
        // day was printworkflowusersvc_10d26ae - a per user service whose suffix is the logon
        // session, so it is a different length on a different day. The number three was never a
        // property of sc, it was a property of that afternoon's service names.
        //
        // ERROR_MORE_DATA is 234 and sc returns it exactly when it had more to say. Checked
        // against the product the same day: our answer and sc's agree for all 46 services that
        // have dependents at all, so the fault here was never in what the product reads.
        const int ErrorMoreData = 234;

        return run.ExitCode == ErrorMoreData ? null : names;
    }

    [Fact]
    public void Declared_dependencies_agree_with_sc_for_every_service_that_has_any()
    {
        // sc.exe is the authority. The multi-string the manager returns has to be walked to
        // its second null, and reading only as far as the first would report one dependency
        // for a service that declares five - quiet, plausible, and wrong.
        var checkedEntries = 0;

        foreach (var entry in CommandLineTool.Listing("--query", "!type:driver"))
        {
            if (entry.GetProperty("dependsOn").ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var name = CommandLineTool.Text(entry, "serviceName");
            var ours = entry.GetProperty("dependsOn").EnumerateArray().Select(value => value.GetString()!);

            var theirs = DeclaredBySc(name);

            Assert.Equal(
                string.Join('|', theirs).ToLowerInvariant(),
                string.Join('|', ours).ToLowerInvariant());

            checkedEntries++;
        }

        // Measured on 2026-08-01: 210 of 339 services declare at least one. A run that
        // checked a handful would pass on a build that dropped all but the first name.
        Assert.True(checkedEntries > 100, $"Only {checkedEntries} services had dependencies to check.");
    }

    /// <summary>
    /// Pulls the dependency block out of sc qc, continuation lines included. They arrive
    /// under a bare colon with no label, so reading only the labelled line loses everything
    /// after the first.
    /// </summary>
    private static List<string> DeclaredBySc(string serviceName)
    {
        var declared = new List<string>();
        var inside = false;

        foreach (var line in CommandLineTool.ServiceControl("qc", serviceName).StandardOutput.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.Contains("DEPENDENCIES", StringComparison.Ordinal))
            {
                inside = true;
                declared.AddRange(Values(trimmed));
                continue;
            }

            if (!inside)
            {
                continue;
            }

            if (trimmed.TrimStart().StartsWith(':'))
            {
                declared.AddRange(Values(trimmed));
                continue;
            }

            break;
        }

        return declared;
    }

    private static IEnumerable<string> Values(string line) =>
        line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [Fact]
    public void Every_entry_carries_a_name_a_display_name_and_a_type()
    {
        foreach (var entry in CommandLineTool.Listing())
        {
            Assert.False(string.IsNullOrWhiteSpace(CommandLineTool.Text(entry, "serviceName")));
            Assert.False(string.IsNullOrWhiteSpace(CommandLineTool.Text(entry, "displayName")));
            Assert.NotEqual("Unknown", CommandLineTool.Text(entry, "entryType"));
        }
    }
}
