using Bws.Core.Querying;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Asking what an entry is allowed to do, and who is allowed to do things to it.
///
/// Three questions read together and independent of each other, which is the thing these
/// tests are mostly about: the privileges an entry declares, whether it has an identity of
/// its own, and its permissions. A machine has services that declare much and have no
/// identity, and services that declare nothing and have the tighter kind, so any code that
/// treats one as a proxy for another is wrong on real entries rather than in theory.
/// </summary>
public sealed class PermissionQueryTests
{
    [Fact]
    public void A_privilege_can_be_asked_for_by_name_and_by_fragment()
    {
        // The audit question this field exists for, in the two spellings somebody will
        // actually type. Nobody types a privilege in full the first time.
        Assert.Contains("DcomLaunch", Match("privilege:SeDebugPrivilege"));
        Assert.Contains("DcomLaunch", Match("privilege:debug"));

        Assert.DoesNotContain("BFE", Match("privilege:debug"));
    }

    [Fact]
    public void Case_does_not_decide_whether_a_privilege_matches()
    {
        // Not a nicety. The manager returns these names in whatever casing the service was
        // registered with, and the casing differs between services on one machine: Schedule
        // declares SeSystemTimePrivilege where Sense declares SeSystemtimePrivilege, and
        // they are the same privilege. A comparison that honoured case would answer a
        // correct query with half the machine.
        Assert.Contains("DcomLaunch", Match("privilege:sedebugprivilege"));
        Assert.Contains("DcomLaunch", Match("privilege:SEDEBUGPRIVILEGE"));
    }

    [Fact]
    public void An_exact_match_is_against_one_privilege_and_not_the_whole_list()
    {
        // The reason this field holds a list rather than a joined string. Joined, an exact
        // match would have to equal ten privileges written end to end and would match
        // nothing at all - and it would fail silently, as an empty result.
        Assert.Contains("DcomLaunch", Match("privilege:=SeTcbPrivilege"));
        Assert.Contains("BFE", Match("privilege:=SeAuditPrivilege"));

        // BFE declares exactly one, so this is the same assertion from the other side: a
        // list of one must not become a substring match against a longer name.
        Assert.DoesNotContain("BFE", Match("privilege:=SeAudit"));
    }

    [Fact]
    public void A_wildcard_does_not_reach_across_two_privileges()
    {
        // What joining the list would have cost. DcomLaunch declares SeAuditPrivilege
        // followed by SeChangeNotifyPrivilege, so a wildcard spanning the gap between two
        // values would match a privilege nobody declared.
        Assert.Empty(Match("privilege:SeAudit*Change*"));
        Assert.Contains("DcomLaunch", Match("privilege:SeChange*"));
    }

    [Fact]
    public void Declaring_nothing_and_nobody_having_read_it_are_different_answers()
    {
        // Rule 8 of CLAUDE.md on this field. Declaring no privileges is the permissive case,
        // not the careful one, so answering "declares none" about an entry nobody could read
        // would turn a gap in our access into a reassuring finding.
        Assert.Contains("AsusUpdateCheck", Match("privilege:none"));
        Assert.DoesNotContain("Locked", Match("privilege:none"));
        Assert.Contains("Locked", Match("privilege:?"));
    }

    [Fact]
    public void Having_no_identity_of_its_own_is_asked_about_with_the_word_every_field_has()
    {
        // Windows calls this NONE and it is tempting to make it a third value of the
        // enumeration. It is not a kind of identity, it is the absence of one, so it is the
        // reserved word - and the payoff is right here: an entry nobody could read does not
        // answer it.
        Assert.Contains("AsusUpdateCheck", Match("sidtype:none"));
        Assert.Contains("Beep", Match("sidtype:none"));
        Assert.DoesNotContain("Locked", Match("sidtype:none"));
        Assert.Contains("Locked", Match("sidtype:?"));
    }

    [Fact]
    public void The_two_kinds_of_identity_are_told_apart()
    {
        Assert.Contains("BFE", Match("sidtype:restricted"));
        Assert.Contains("DcomLaunch", Match("sidtype:unrestricted"));

        Assert.DoesNotContain("BFE", Match("sidtype:unrestricted"));
        Assert.DoesNotContain("DcomLaunch", Match("sidtype:restricted"));
    }

    [Fact]
    public void What_an_entry_declares_and_what_identity_it_has_are_separate_facts()
    {
        // The counter-examples, both real. BFE carries the stronger identity and declares
        // one privilege. McmSvc declares none and has an identity anyway. Anything reading
        // one of these as a proxy for the other is wrong on this machine, today.
        Assert.Contains("BFE", Match("sidtype:restricted privilege:any"));
        Assert.Contains("McmSvc", Match("sidtype:unrestricted privilege:none"));
    }

    [Fact]
    public void A_refused_descriptor_costs_that_field_and_no_other()
    {
        // The measurement that shaped how this family is read, as an assertion. Under a
        // restricted token five entries of 810 open for configuration and refuse
        // READ_CONTROL, so the descriptor must not travel on the configuration handle.
        //
        // LSM is one of the five, and here it answers every question except the one it
        // refuses.
        Assert.Contains("LSM", Match("sddl:?"));
        Assert.DoesNotContain("LSM", Match("sddl:any"));

        Assert.Contains("LSM", Match("start:auto"));
        Assert.Contains("LSM", Match("sidtype:unrestricted"));
        Assert.Contains("LSM", Match("privilege:none"));
    }

    [Fact]
    public void The_permissions_can_be_searched_as_the_text_they_are()
    {
        // Blunt, and the honest interim until the permission list is decoded for a panel to
        // show it. "Which entries let ordinary users do anything" is askable today only like
        // this, and being able to ask badly beats not being able to ask.
        Assert.Contains("DcomLaunch", Match("sddl:BU"));
        Assert.DoesNotContain("Beep", Match("sddl:BU"));
    }

    [Fact]
    public void Every_entry_reports_the_same_three_facts_or_says_why_not()
    {
        // A guard on the catalogue rather than on the code. Every specimen has to have an
        // answer for all three fields, because a fixture left at "not read" would quietly
        // exempt itself from every test above and pass while checking nothing.
        Assert.All(Specimens.All, entry =>
        {
            Assert.NotEqual(ReadOutcome.NotRead, entry.RequiredPrivileges.Outcome);
            Assert.NotEqual(ReadOutcome.NotRead, entry.SidType.Outcome);
            Assert.NotEqual(ReadOutcome.NotRead, entry.SecurityDescriptor.Outcome);
        });
    }

    private static List<string> Match(string query)
    {
        var parsed = QueryParser.Parse(query);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));

        return [.. parsed.Query!.Filter(Specimens.All).Entries.Select(entry => entry.ServiceName)];
    }
}
