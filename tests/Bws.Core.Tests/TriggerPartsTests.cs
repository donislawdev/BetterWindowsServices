using Bws.Core.Snapshots;

namespace Bws.Core.Tests;

/// <summary>
/// The parts of a trigger a person reads, before either interface puts its words around them (backlog
/// 625, 2026-10-27): which documented subtype a GUID is, and a data item's values as text.
/// </summary>
public sealed class TriggerPartsTests
{
    /// <summary>
    /// Every GUID the documentation of <c>SERVICE_TRIGGER</c> names, under the kind it names it for. Ten,
    /// read off learn.microsoft.com on 2026-10-27 - backlog 625 said twelve, which was a miscount.
    /// </summary>
    [Theory]
    [InlineData("NetworkEndpoint", "bc90d167-9470-4139-a9ba-be0bbbf5b74d", TriggerSubtypeName.RpcInterface)]
    [InlineData("NetworkEndpoint", "1f81d131-3fac-4537-9e0c-7e7b0c2f4b55", TriggerSubtypeName.NamedPipe)]
    [InlineData("DomainJoin", "1ce20aba-9851-4421-9430-1ddeb766e809", TriggerSubtypeName.JoinedDomain)]
    [InlineData("DomainJoin", "ddaf516e-58c2-4866-9574-c3b615d42ea1", TriggerSubtypeName.LeftDomain)]
    [InlineData("FirewallPort", "b7569e07-8421-4ee0-ad10-86915afdad09", TriggerSubtypeName.PortOpened)]
    [InlineData("FirewallPort", "a144ed38-8e12-4de4-9d96-e64740b1a524", TriggerSubtypeName.PortClosed)]
    [InlineData("GroupPolicy", "659fcae6-5bdb-4da9-b1ff-ca2a178d46e0", TriggerSubtypeName.MachinePolicy)]
    [InlineData("GroupPolicy", "54fb46c8-f089-464c-b1fd-59d1b62c3b50", TriggerSubtypeName.UserPolicy)]
    [InlineData("IpAddress", "4f27f2de-14e2-430b-a549-7cd48cbc8245", TriggerSubtypeName.FirstAddressArrived)]
    [InlineData("IpAddress", "cc4ba62a-162e-4648-847a-b6bdf993e335", TriggerSubtypeName.LastAddressRemoved)]
    public void A_documented_subtype_under_its_own_kind_has_its_name(string kind, string subtype, TriggerSubtypeName name)
    {
        Assert.Equal(name, TriggerParts.Subtype(new TriggerDocument(kind, "Start", subtype, [])));

        // Read as a GUID rather than as text, so the capitals the documentation prints them in name it too.
        Assert.Equal(name, TriggerParts.Subtype(new TriggerDocument(kind, "Start", subtype.ToUpperInvariant(), [])));
    }

    /// <summary>
    /// A named GUID under another kind is a GUID nobody named there, and a subtype the documentation does
    /// not list, or no subtype at all - every trigger of a file older than seven - has no name either.
    /// </summary>
    [Fact]
    public void A_subtype_is_named_only_where_the_documentation_names_it()
    {
        Assert.Null(TriggerParts.Subtype(new TriggerDocument("GroupPolicy", "Start", "b7569e07-8421-4ee0-ad10-86915afdad09", [])));
        Assert.Null(TriggerParts.Subtype(new TriggerDocument("CustomSystemStateChange", "Start", "2d7a2816-0c5e-45fc-9ce7-570e5ecde9c9", [])));
        Assert.Null(TriggerParts.Subtype(new TriggerDocument("FirewallPort", "Start", null, null)));
        Assert.Null(TriggerParts.Subtype(new TriggerDocument("FirewallPort", "Start", "not a guid", [])));
    }

    /// <summary>Bytes in pairs like <c>sc qtriggerinfo</c> prints them, for the binary type and for a type nobody documented.</summary>
    [Fact]
    public void Bytes_are_read_in_pairs()
    {
        Assert.Equal("75 18 bc a3 28 00 92 13", TriggerParts.Values(new TriggerDataDocument("Binary", ["7518bca328009213"])));
        Assert.Equal("0a ff", TriggerParts.Values(new TriggerDataDocument("Unknown", ["0aff"])));
    }

    /// <summary>
    /// The values of one item joined by a comma - the four that a firewall port trigger must all match -
    /// with an empty one shown as two quotation marks rather than as nothing.
    /// </summary>
    [Fact]
    public void The_values_of_one_item_are_joined_and_an_empty_one_is_shown()
    {
        Assert.Equal(
            @"500, UDP, %windir%\system32\svchost.exe, IKEEXT",
            TriggerParts.Values(new TriggerDataDocument("String", ["500", "UDP", @"%windir%\system32\svchost.exe", "IKEEXT"])));
        Assert.Equal("first, \"\", third", TriggerParts.Values(new TriggerDataDocument("String", ["first", "", "third"])));
        Assert.Equal("\"\"", TriggerParts.Values(new TriggerDataDocument("String", [""])));
        Assert.Equal("4", TriggerParts.Values(new TriggerDataDocument("Level", ["4"])));
        Assert.Equal("0x0000000000000001", TriggerParts.Values(new TriggerDataDocument("KeywordAny", ["0x0000000000000001"])));
    }

    /// <summary>
    /// <b>A value that could be read as something else is in quotation marks</b> (the review of PR #98): a
    /// comma, the bar items are joined with, a quotation mark - doubled inside - or white space at either end.
    /// One firewall path holding a comma no longer reads like two values, and a value holding the bar no
    /// longer reads like two items.
    /// </summary>
    [Fact]
    public void A_value_that_could_be_read_as_something_else_is_quoted()
    {
        var one = TriggerParts.Values(new TriggerDataDocument("String", [@"C:\a, b.exe"]));
        var two = TriggerParts.Values(new TriggerDataDocument("String", [@"C:\a", "b.exe"]));

        Assert.Equal(@"""C:\a, b.exe""", one);
        Assert.Equal(@"C:\a, b.exe", two);
        Assert.Equal("\"x | y\"", TriggerParts.Values(new TriggerDataDocument("String", ["x | y"])));
        Assert.NotEqual(
            TriggerParts.OneLine([TriggerParts.Values(new TriggerDataDocument("String", ["x | y"]))]),
            TriggerParts.OneLine(["x", "y"]));
        Assert.Equal("\"say \"\"hi\"\"\"", TriggerParts.Values(new TriggerDataDocument("String", ["say \"hi\""])));
        Assert.Equal("\" lead\", \"trail \"", TriggerParts.Values(new TriggerDataDocument("String", [" lead", "trail "])));
        Assert.Equal("in the middle", TriggerParts.Values(new TriggerDataDocument("String", ["in the middle"])));
    }

    /// <summary>
    /// Bytes are pairs only where they are bytes - an even run of hex digits. Anything else a hand-written file
    /// puts under the binary type is text, quoted by the same rule, so no pair is cut out of a comma.
    /// </summary>
    [Fact]
    public void A_binary_value_that_is_not_bytes_is_shown_as_text()
    {
        Assert.Equal("abc", TriggerParts.Values(new TriggerDataDocument("Binary", ["abc"])));
        Assert.Equal("not hex", TriggerParts.Values(new TriggerDataDocument("Binary", ["not hex"])));
        Assert.Equal("\"ab,cd\"", TriggerParts.Values(new TriggerDataDocument("Binary", ["ab,cd"])));
        Assert.Equal("\"\"", TriggerParts.Values(new TriggerDataDocument("Binary", [""])));
    }

    /// <summary>The data items of one trigger on one line, joined by the bar, for a comparison.</summary>
    [Fact]
    public void The_items_of_one_trigger_are_joined_by_a_bar() =>
        Assert.Equal("level 4 | any of 0x1", TriggerParts.OneLine(["level 4", "any of 0x1"]));

    /// <summary>A type word this build does not know - a later build could write one - is the unknown type, with its values left as they are.</summary>
    [Fact]
    public void A_type_this_build_does_not_know_is_the_unknown_type()
    {
        Assert.Equal(TriggerDataType.KeywordAll, TriggerParts.TypeOf(new TriggerDataDocument("KeywordAll", ["0x1"])));
        Assert.Equal(TriggerDataType.Unknown, TriggerParts.TypeOf(new TriggerDataDocument("Filter", ["x"])));
        Assert.Equal(TriggerDataType.Unknown, TriggerParts.TypeOf(new TriggerDataDocument("binary", ["00"])));
        Assert.Equal(TriggerDataType.Unknown, TriggerParts.TypeOf(new TriggerDataDocument("3", ["00"])));
        Assert.Equal("x", TriggerParts.Values(new TriggerDataDocument("Filter", ["x"])));
    }
}
