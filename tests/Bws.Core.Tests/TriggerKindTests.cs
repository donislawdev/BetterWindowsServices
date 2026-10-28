using Windows.Win32.System.Services;

namespace Bws.Core.Tests;

/// <summary>
/// The manager's number for a trigger kind, in our words - above all the two numbers the interop
/// metadata does not name.
///
/// <b>Written 2026-10-28 for backlog 627</b>, when kind thirty got the name winsvc.h of the Windows SDK
/// gives it, SERVICE_TRIGGER_TYPE_AGGREGATE. Until then nothing held the mapping of an unnamed number:
/// seven was read right on every real machine, and a change sending it back to Unknown would have
/// passed every test.
/// </summary>
public sealed class TriggerKindTests
{
    [Theory]
    [InlineData(7u, TriggerKind.CustomSystemStateChange)]
    [InlineData(30u, TriggerKind.Aggregate)]
    public void A_kind_the_metadata_does_not_name_is_read_by_its_number(uint number, TriggerKind kind)
    {
        Assert.Equal(kind, ManagerTerms.Trigger((SERVICE_TRIGGER_TYPE)number));
    }

    /// <summary>
    /// A number no header names stays Unknown rather than being folded into a neighbour - the numbers
    /// right beside the named ones as winsvc.h lists them: before one, after seven, after twenty and
    /// after thirty.
    /// </summary>
    [Theory]
    [InlineData(0u)]
    [InlineData(8u)]
    [InlineData(21u)]
    [InlineData(31u)]
    public void A_number_no_header_names_stays_unknown(uint number)
    {
        Assert.Equal(TriggerKind.Unknown, ManagerTerms.Trigger((SERVICE_TRIGGER_TYPE)number));
    }
}
