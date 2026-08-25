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
        // and never running, because it is a template. Whether it should be REPORTED as a
        // failure was left open here until 2026-08-25, and the answer is no - see the test
        // below that takes it back out. This one keeps asking the unfiltered question,
        // because that is what the specification's scenario says and the narrowing is a
        // separate choice made on top of it.
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
    public void The_two_sides_of_the_per_user_family_are_not_swapped()
    {
        // THE ONE MISTAKE IN THIS FIELD THAT WOULD LOOK RIGHT FROM EVERY COUNT.
        //
        // An instance carries the template bit as well as its own - measured 0xe0 on a real
        // machine, which is instance plus template plus share-process. So a reader that asks
        // about the template bit first labels every instance a template, and the totals stay
        // exactly as plausible as before: same number of entries in the family, none missing,
        // every one of them on the wrong side.
        //
        // Nothing about a count can catch that, so this asserts the two sides against the
        // thing that tells them apart from outside: the instance is the one Windows gave a
        // session suffix and a process, and the template is the one that never runs.
        Assert.Equal(["CDPUserSvc"], Names("peruser:template"));
        Assert.Equal(["CDPUserSvc_21aaa4"], Names("peruser:instance"));

        Assert.Equal(EntryStatus.Stopped, Specimens.PerUserTemplate.Status);
        Assert.Equal(EntryStatus.Running, Specimens.PerUserInstance.Status);
    }

    [Fact]
    public void Yes_is_the_two_sides_together_and_no_is_the_rest_of_the_machine()
    {
        // The same shape as type:driver: the grouping word exists because "is this session
        // noise" is the question people have, and neither side alone answers it.
        Assert.Equal(["CDPUserSvc", "CDPUserSvc_21aaa4"], Names("peruser:yes"));

        Assert.DoesNotContain("CDPUserSvc", Names("peruser:no"), StringComparer.Ordinal);
        Assert.DoesNotContain("CDPUserSvc_21aaa4", Names("peruser:no"), StringComparer.Ordinal);
        Assert.Contains("AsusUpdateCheck", Names("peruser:no"), StringComparer.Ordinal);
    }

    [Fact]
    public void The_acceptance_scenario_stops_blaming_a_template_once_it_can_ask_about_one()
    {
        // The whole point of the field, in one line: the specification's scenario one counts
        // CDPUserSvc as an automatic entry that did not come up, and it is not one. It never
        // comes up, by design, while the session instance beside it runs.
        //
        // Measured on a real machine the same day, where this is not one entry but four:
        // the unfiltered question answers 8, adding peruser:no answers 4, and adding
        // trigger:none as well answers 1 - which is the only entry on that machine that
        // genuinely failed to start.
        Assert.Equal(
            ["HalfRead", "AsusUpdateCheck", "sppsvc"],
            Names("start:auto !status:running !type:driver peruser:no"));
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

    /// <summary>
    /// The description is searchable, and its two empty-looking states are asked about separately.
    ///
    /// <b>This is the field where `none` and `?` finally earn their keep.</b> For a name they are
    /// empty questions - every entry has one - but 384 entries of 819 on a real machine have no
    /// description and eight have one nobody could resolve, so both are real populations. A language
    /// that answered the same for both would be reporting a machine as described when it is not.
    /// </summary>
    [Fact]
    public void The_description_is_searchable_and_its_two_kinds_of_emptiness_stay_apart()
    {
        Assert.Contains("Spooler", Names("description:*print*"));

        // Having none, and having one nobody could read. Different questions, different answers,
        // and no entry may be in both.
        var none = Names("description:none");
        var unreadable = Names("description:?");

        Assert.Contains("Tcpip6", unreadable);
        Assert.DoesNotContain("Tcpip6", none);
        Assert.Empty(none.Intersect(unreadable, StringComparer.Ordinal));
    }

    /// <summary>
    /// A wildcard matches across a line break, which it could not until 2026-08-12.
    ///
    /// <b>A latent fault the description exposed rather than caused.</b> A wildcard compiles to a
    /// pattern anchored at both ends, and by default a dot does not cross a newline - so
    /// <c>*step*</c> against a two-line value asked the dot after the word to reach an end of string
    /// it could not get to, and the answer was silently NOTHING. No field could carry a line break
    /// before the description, so nothing had ever reached it.
    ///
    /// <b>Both halves are asserted, because the fault was invisible from one side.</b> The same
    /// pattern shape matched a single-line description perfectly, which is why this needs a value
    /// with a break in it and a word on each side of the break.
    /// </summary>
    [Fact]
    public void A_wildcard_reaches_past_a_line_break_in_a_value()
    {
        // Before the break, and after it. The word after is the half that proves the pattern is not
        // simply stopping at the first line.
        Assert.Contains("ContosoSyncHost", Names("description:*step*"));
        Assert.Contains("ContosoSyncHost", Names("description:*explicitly*"));

        // And the control: a word in no description at all still matches nothing, so the fix is not
        // a pattern that matches everything.
        Assert.DoesNotContain("ContosoSyncHost", Names("description:*chrysanthemum*"));
    }

    /// <summary>
    /// A bare word does NOT reach the description, which is the boundary the owner has not been
    /// asked about yet - backlog 175. Held as a test so that widening it becomes a deliberate act
    /// rather than something that happens because somebody added a field to a list.
    /// </summary>
    [Fact]
    public void A_free_word_does_not_reach_the_description()
    {
        // A word that appears in a specimen's description and nowhere in its name, display name,
        // account or launch path. Asked as a field it matches, asked bare it does not.
        //
        // One word rather than a phrase, and that is the language rather than a shortcut: an
        // unquoted space separates two members, and quoting a value takes the wildcards with it.
        Assert.Contains("ContosoSyncHost", Names("description:*step*"));
        Assert.DoesNotContain("ContosoSyncHost", Names("step"));
    }

    private static QueryResult Run(string text) =>
        QueryParserTests.Valid(text).Filter(Specimens.Catalog().ReadAll());

    private static string[] Names(string text) =>
        [.. Run(text).Entries.Select(entry => entry.ServiceName)];
}
