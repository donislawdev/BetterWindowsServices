namespace Bws.Core.Tests;

/// <summary>
/// A trigger is a value: two readings of one unchanged trigger are equal.
///
/// <b>Written for the review of PR #97.</b> Until schema seven the record held two enumeration values and
/// was equal by them. Its list of data items would have made it equal only to itself - a record compares
/// a list by the object, not by what is in it.
/// </summary>
public sealed class ServiceTriggerTests
{
    [Fact]
    public void Two_triggers_built_apart_from_the_same_reading_are_equal_and_hash_alike()
    {
        var first = Read("0497B57D-2E66-424F-A0C6-157CD5D41700");
        var second = Read("0497B57D-2E66-424F-A0C6-157CD5D41700");

        Assert.NotSame(first.Data, second.Data);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    /// Case is part of the value, for the reason the comparison of two snapshots gives: a network endpoint
    /// trigger fires on an exact match of its data.
    /// </summary>
    [Fact]
    public void A_trigger_whose_data_differs_only_in_case_is_a_different_trigger()
    {
        Assert.NotEqual(Read("0497B57D-2E66-424F-A0C6-157CD5D41700"), Read("0497b57d-2e66-424f-a0c6-157cd5d41700"));
    }

    private static ServiceTrigger Read(string rpcInterface) =>
        new(
            TriggerKind.NetworkEndpoint,
            TriggerAction.Start,
            Fakes.Specimens.RpcInterfaceEvent,
            [new TriggerData(TriggerDataType.String, [rpcInterface]), new TriggerData(TriggerDataType.Level, ["4"])]);
}
