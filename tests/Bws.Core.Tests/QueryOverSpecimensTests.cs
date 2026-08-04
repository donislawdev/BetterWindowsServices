using Bws.Core.Querying;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The query language against the catalogue of real specimens rather than entries built
/// for one question at a time.
///
/// The difference matters. A fixture written next to the test it serves tends to contain
/// exactly what makes that test pass, so a query that also picks up something unexpected
/// has nothing unexpected to pick up. Here everything is present at once - drivers,
/// per-user instances, a virtual account, a refused entry, an entry read only in part -
/// so an answer that is too wide has somewhere to go wrong.
///
/// 05-PRZYPADKI-BRZEGOWE calls itself a list of things the interface and the command line
/// can trip over. This is the tripping.
/// </summary>
public sealed class QueryOverSpecimensTests
{
    [Fact]
    public void The_acceptance_scenario_finds_the_automatic_entries_that_are_not_running()
    {
        // Scenario one from the specification, run against every awkward case at once.
        //
        // CDPUserSvc belongs here and is easy to forget: a per-user template is automatic
        // and never running, because it is a template. Whether that should be reported as
        // a failure at all is a question for the slice that collapses per-user services.
        //
        // HalfRead belongs too, and for the opposite reason: its start type was read and
        // says automatic, so nothing about the delay flag being refused changes the answer.
        Assert.Equal(
            ["HalfRead", "AsusUpdateCheck", "sppsvc", "CDPUserSvc"],
            Names("start:auto !status:running !type:driver"));
    }

    [Fact]
    public void An_entry_read_only_in_part_is_still_judged_and_the_result_says_so()
    {
        // Two entries cannot answer this question and both have to be counted, for two
        // different reasons. HalfRead had its start type read and its delay flag refused.
        // Locked had the start type itself refused, so it cannot even say it is automatic.
        // A count of one would mean the second kind was being missed.
        var result = Run("start:delayed");

        Assert.Equal(["BITS", "sppsvc"], result.Entries.Select(entry => entry.ServiceName));
        Assert.Equal(2, result.Unreadable);
    }

    [Fact]
    public void Asking_what_could_not_be_read_finds_exactly_the_refused_entry()
    {
        // The question an audit tool has to be able to ask. Without it somebody can see
        // that a result is partial and has no way to find out what is missing.
        Assert.Equal(["Locked"], Names("account:?"));
    }

    [Fact]
    public void Asking_for_a_missing_value_never_returns_the_one_that_was_refused()
    {
        // The distinction the whole four-state model exists for. Getting this wrong makes
        // a snapshot taken without elevation look like a snapshot of a different machine.
        var absent = Names("account:none");

        Assert.Contains("AppvStrm", absent);
        Assert.Contains("CDPUserSvc_21aaa4", absent);
        Assert.DoesNotContain("Locked", absent);
    }

    [Fact]
    public void Driver_covers_both_driver_kinds_and_nothing_else()
    {
        Assert.Equal(["AppvStrm", "amduw23g-202073-df09ebb6", "Beep"], Names("type:driver"));
    }

    [Fact]
    public void A_name_with_a_space_needs_quoting_and_then_works()
    {
        Assert.Equal(["ContosoVPN Tunnel"], Names("""name:"ContosoVPN Tunnel" """));

        // Unquoted it is two members rather than one, and here that still finds the entry,
        // because the default operator is "contains" and the free word matches the same
        // display name. Which is worth knowing: the quoting rule is not what makes the
        // ordinary case work, it is what makes the value mean one thing.
        Assert.Equal(["ContosoVPN Tunnel"], Names("name:ContosoVPN Tunnel"));

        // Ask for the whole value and the difference stops being subtle. Quoted it matches,
        // unquoted the exact comparison is against "ContosoVPN" alone and nothing has that name.
        Assert.Equal(["ContosoVPN Tunnel"], Names("""name:="ContosoVPN Tunnel" """));
        Assert.Empty(Names("name:=ContosoVPN Tunnel"));
    }

    [Fact]
    public void A_virtual_account_is_found_by_the_prefix_that_makes_it_virtual()
    {
        // The third category of account, and the one that looks like a system account at a
        // glance. Two specimens, so a test cannot pass on a single lucky example.
        Assert.Equal(["McmSvc", "OpenVPNService"], Names(@"account:NT\ SERVICE"));
    }

    [Fact]
    public void A_display_name_in_another_language_is_searchable_in_any_case()
    {
        // 05-PRZYPADKI-BRZEGOWE lists a non-English system as a case with no evidence
        // behind it. These display names came off a Polish install, so this is the
        // evidence, and it covers the part people get wrong: folding case over letters
        // outside the English alphabet.
        var lower = Names("display:usługa");
        var upper = Names("display:USŁUGA");

        Assert.NotEmpty(lower);
        Assert.Equal(lower, upper);

        // And no accent folding, which is a decision rather than an oversight.
        Assert.Empty(Names("display:usluga"));
    }

    [Fact]
    public void Names_differing_only_in_case_are_the_same_name()
    {
        // Windows treats service names case-insensitively, so a query cannot pretend it
        // can tell these apart. Both come back.
        Assert.Equal(["Twin", "TWIN"], Names("name:=twin"));
    }

    [Fact]
    public void A_per_user_template_and_its_instance_are_both_found_by_the_shared_prefix()
    {
        // The suffix is random per session, so the prefix is the only stable handle a
        // person has until the interface learns to collapse them.
        Assert.Equal(["CDPUserSvc", "CDPUserSvc_21aaa4"], Names("name:CDPUserSvc"));
    }

    [Fact]
    public void Services_sharing_one_process_are_found_by_that_process()
    {
        // "Which of the services in this svchost is the one eating the machine" is a real
        // question, and the process id is how somebody arrives from Task Manager.
        Assert.Equal(["DcomLaunch", "PlugPlay"], Names("pid:1900"));
    }

    [Fact]
    public void A_stopped_entry_has_no_process_and_asking_about_one_is_false_not_an_error()
    {
        var result = Run("pid:>0");

        Assert.DoesNotContain("sppsvc", result.Entries.Select(entry => entry.ServiceName));

        // And no part of that answer rested on something unreadable, so nothing is claimed
        // about completeness that is not true.
        Assert.Equal(0, result.Unreadable);
    }

    [Fact]
    public void A_free_word_reaches_the_display_name_and_the_account_as_well_as_the_name()
    {
        Assert.Contains("BITS", Names("transferu"));
        Assert.Contains("sppsvc", Names("networkservice"));
    }

    [Fact]
    public void The_whole_catalogue_comes_back_when_nothing_is_asked()
    {
        Assert.Equal(Specimens.All.Count, Names("").Length);
    }

    private static QueryResult Run(string text) =>
        QueryParserTests.Valid(text).Filter(Specimens.Catalog().ReadAll());

    private static string[] Names(string text) =>
        [.. Run(text).Entries.Select(entry => entry.ServiceName)];
}
