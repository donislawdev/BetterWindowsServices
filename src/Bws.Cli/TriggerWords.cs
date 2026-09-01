using Bws.Core;

namespace Bws.Cli;

/// <summary>
/// The word for what starts a service on demand, in sentence case rather than the name of an
/// enumeration value.
///
/// <b>WRITTEN 2026-09-01 FOR BACKLOG 260</b>, the third and last family with this fault - the
/// argument for all three is in <see cref="StatusWords"/> and is not repeated here. Seven of the
/// nine members read as two or three words jammed together: <c>DeviceArrival</c>,
/// <c>IpAddress</c>, <c>DomainJoin</c>, <c>FirewallPort</c>, <c>GroupPolicy</c>,
/// <c>NetworkEndpoint</c> and <c>CustomSystemStateChange</c>.
///
/// <b>THE KEY FOR THE LAST ONE IS NOT NAMED AFTER ITS MEMBER, and that is the window's choice
/// followed rather than a slip.</b> <c>CustomSystemStateChange</c> is answered by
/// <c>trigger.systemState</c> in both interfaces, because "custom system state change" is the
/// name of a Win32 constant and "System state" is what it means to somebody reading a list.
/// Backlog 260 was written against those key names and called the member <c>SystemState</c>,
/// which does not exist - the two are worth keeping straight when this is next read.
///
/// <b>WHAT IS DELIBERATELY LEFT ALONE: the trigger's ACTION.</b> It is printed in the same line by
/// <c>EntryReport</c> and it does NOT have this fault - measured 2026-09-01, its three members are
/// <c>Unknown</c>, <c>Start</c> and <c>Stop</c>, so the value name and the word for a person are
/// the same string and nothing disagrees with anything. It is named here because it is the
/// neighbour somebody will wonder about, and the answer is cheaper written down than measured
/// twice. Backlog 260 does not mention it at all.
/// </summary>
internal static class TriggerWords
{
    internal static string Of(TriggerKind kind) => kind switch
    {
        TriggerKind.DeviceArrival => Texts.Of("cli.cell.trigger.deviceArrival"),
        TriggerKind.IpAddress => Texts.Of("cli.cell.trigger.ipAddress"),
        TriggerKind.DomainJoin => Texts.Of("cli.cell.trigger.domainJoin"),
        TriggerKind.FirewallPort => Texts.Of("cli.cell.trigger.firewallPort"),
        TriggerKind.GroupPolicy => Texts.Of("cli.cell.trigger.groupPolicy"),
        TriggerKind.NetworkEndpoint => Texts.Of("cli.cell.trigger.networkEndpoint"),
        TriggerKind.Custom => Texts.Of("cli.cell.trigger.custom"),
        TriggerKind.CustomSystemStateChange => Texts.Of("cli.cell.trigger.systemState"),
        _ => Texts.Of("cli.cell.unknown")
    };
}
