using Bws.Core.Querying;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Both directions of one relation in the query language - `dependson` and `requiredby`, added
/// together on 2026-09-06.
///
/// <b>TOGETHER BECAUSE ONE ALONE IS THE WORSE SURFACE.</b> The window has carried a "Depends on"
/// column since backlog 165 and the language could not ask about it, so adding only the new
/// direction would have left <c>requiredby:rpcss</c> working while <c>dependson:rpcss</c> did not.
/// One half of a pair working is harder to explain than neither half - owner's decision.
///
/// <b>They are nothing alike in what they cost, and that IS the difference worth testing.</b> The
/// declaration arrives in the configuration structure the start type comes from and is free. The
/// other direction is a call per entry and declares a family, so a listing nobody asked answers
/// about it with "nobody looked" rather than with an empty list.
/// </summary>
public sealed class DependencyQueryTests
{
    /// <summary>
    /// What an entry declares it needs, asked of the free direction.
    ///
    /// <b>A fragment matches, because that is what a text field does</b> - the same reading
    /// <c>privilege:debug</c> gets, and for the same reason: nobody types a service name in full
    /// when they are looking for what touches RPC.
    /// </summary>
    [Fact]
    public void The_declared_direction_is_answered_without_anybody_asking_for_a_reading()
    {
        // Not one entry has been through the new pass, and this direction does not care: the
        // names come off the listing itself.
        Assert.Contains("RemoteAccess", Match("dependson:rpcss"));
        Assert.Contains("RemoteAccess", Match("dependson:rpc"));
    }

    /// <summary>
    /// A load order group is askable, marker and all.
    ///
    /// <b>The manager returns "+NetBIOSGroup" and nothing strips the plus</b>, which is written on
    /// <c>ScmEntry.DependsOn</c>: taking the marker off would turn a group into a service that
    /// does not exist. So the marker is part of what somebody can ask about, and a fragment finds
    /// the group without it.
    /// </summary>
    [Fact]
    public void A_load_order_group_is_asked_about_the_way_the_manager_spells_it()
    {
        Assert.Contains("RemoteAccess", Match("dependson:+netbiosgroup"));
        Assert.Contains("RemoteAccess", Match("dependson:netbios"));
    }

    /// <summary>
    /// The other direction answers once the pass has run, and admits it has not before that.
    ///
    /// <b>Both halves, because the first alone is the failure this whole language is arranged to
    /// avoid.</b> A field nobody read answering "no matches" is a correct query returning what
    /// reads exactly like "there are none" - so before the pass the honest answer is that the
    /// question is unanswered, which is what the reserved word asks about.
    /// </summary>
    [Fact]
    public void The_other_direction_is_answered_only_once_somebody_has_paid_for_it()
    {
        // Taught against a name this fixture really has - RemoteAccess is the specimen that
        // declares four services and a load order group, so it is the one somebody would ask
        // about from either direction.
        var catalog = new FakeScmCatalog(Specimens.All)
            .DependedOnBy("RemoteAccess", "Spooler", "Dhcp");

        // NOBODY HAS LOOKED YET, AND THE ANSWER SAYS SO RATHER THAN SAYING "NONE". The list is
        // empty and every entry is counted among those the answer is unsure about - which is the
        // whole of rule 8 in this language, and the difference between a short list and a short
        // list that admits it.
        var unpaid = Valid("requiredby:spooler").Filter(Specimens.All);

        Assert.Empty(unpaid.Entries);
        Assert.Equal(Specimens.All.Count, unpaid.Unreadable);

        // AND NOT THROUGH THE QUESTION MARK, which is worth pinning because it is the obvious
        // guess and it is wrong: `?` asks what could not be READ - a refusal - and "nobody looked"
        // is a different state. QueryValueReader maps the word to ReadOutcome.Denied and only
        // that, so this stays empty until something is actually refused.
        Assert.Empty(MatchOver("requiredby:?", Specimens.All));

        var filled = RequiredByPass.Fill(Specimens.All, catalog);

        Assert.Contains("RemoteAccess", MatchOver("requiredby:spooler", filled));

        // And the entry that is depended ON does not match a question about who depends on
        // IT, which is the direction being the whole point of the pair.
        Assert.DoesNotContain("Spooler", MatchOver("requiredby:spooler", filled));
    }

    /// <summary>
    /// The new direction says which family it needs and the free one says none.
    ///
    /// <b>This is what makes a window or a command line able to decide without a list of field
    /// names of its own</b> - the argument written on <c>QueryField.Needs</c>. A list kept
    /// anywhere else goes stale the first time a family is added, and goes stale silently.
    /// </summary>
    [Fact]
    public void Only_the_direction_that_costs_a_call_declares_a_family()
    {
        Assert.Equal(ExtraRead.RequiredBy, Valid("requiredby:spooler").Needs);
        Assert.Equal(ExtraRead.None, Valid("dependson:rpcss").Needs);

        // And both aliases reach the same field, which is what keeps a member somebody wrote last
        // year working after the name was chosen from the glossary rather than from the code.
        Assert.Equal(ExtraRead.RequiredBy, Valid("dependents:spooler").Needs);
        Assert.Equal(ExtraRead.RequiredBy, Valid("neededBy:spooler").Needs);
        Assert.Equal(ExtraRead.None, Valid("requires:rpcss").Needs);
    }

    private static Query Valid(string text) => QueryParserTests.Valid(text);

    private static List<string> Match(string query) => MatchOver(query, Specimens.All);

    private static List<string> MatchOver(string query, IReadOnlyList<ScmEntry> entries) =>
        [.. Valid(query).Filter(entries).Entries.Select(entry => entry.ServiceName)];
}
