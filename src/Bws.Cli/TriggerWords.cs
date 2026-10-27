using Bws.Core;
using Bws.Core.Snapshots;

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
    /// <summary>
    /// One trigger as the lines <c>bws show</c> prints under its label: the kind, the action and the
    /// subtype on the first, and each data item on a line of its own under it, indented (backlog 625,
    /// owner's decision "B - data under the trigger", 2026-10-27).
    ///
    /// <b>One item, one line</b>, because the items are what the documentation counts: a device trigger
    /// fires on ANY of its hardware IDs, each an item, while a firewall trigger's one item holds four
    /// values that must ALL match. Lines keep the first apart and the comma keeps the second together.
    /// </summary>
    internal static IEnumerable<string> Lines(TriggerDocument trigger) =>
        Items(trigger).Select(item => Texts.Of("cli.show.trigger.data", item)).Prepend(Head(trigger));

    /// <summary>
    /// One trigger on one line, for a comparison, which says "before -> after" per line - the items joined
    /// by a bar, the one character no value on the machine this was measured on holds.
    /// </summary>
    internal static string Line(TriggerDocument trigger)
    {
        var items = Items(trigger).ToList();

        return items.Count == 0 ? Head(trigger) : Texts.Of("cli.trigger.withData", Head(trigger), string.Join(" | ", items));
    }

    /// <summary>
    /// The kind and the action, and the subtype after them - in words for the ten the documentation names,
    /// as the GUID otherwise. None at all for a trigger out of a file older than schema seven.
    /// </summary>
    private static string Head(TriggerDocument trigger)
    {
        var head = Texts.Of("cli.show.trigger", Kind(trigger.Kind), trigger.Action);

        return trigger.Subtype is { } subtype
            ? Texts.Of("cli.trigger.withSubtype", head, TriggerParts.Subtype(trigger) is { } name ? Subtype(name) : subtype)
            : head;
    }

    /// <summary>
    /// A kind as a document names it, in words - and a name this build does not know left as it is, since
    /// a file written by a later build may hold one, and "unknown" would claim less than the file says.
    /// </summary>
    private static string Kind(string? kind) =>
        kind is not null && Enum.GetNames<TriggerKind>().Contains(kind, StringComparer.Ordinal)
            ? Of(Enum.Parse<TriggerKind>(kind))
            : kind ?? Texts.Of("cli.cell.unknown");

    /// <summary>Each data item as one piece of text, the type's word around its values.</summary>
    private static IEnumerable<string> Items(TriggerDocument trigger) =>
        (trigger.Data ?? []).Select(item => TriggerParts.TypeOf(item) switch
        {
            TriggerDataType.Level => Texts.Of("cli.trigger.level", TriggerParts.Values(item)),
            TriggerDataType.KeywordAny => Texts.Of("cli.trigger.keywordAny", TriggerParts.Values(item)),
            TriggerDataType.KeywordAll => Texts.Of("cli.trigger.keywordAll", TriggerParts.Values(item)),
            TriggerDataType.Unknown => Texts.Of("cli.trigger.unknownType", TriggerParts.Values(item)),
            _ => TriggerParts.Values(item)
        });

    private static string Subtype(TriggerSubtypeName name) => name switch
    {
        TriggerSubtypeName.RpcInterface => Texts.Of("cli.trigger.subtype.rpcInterface"),
        TriggerSubtypeName.NamedPipe => Texts.Of("cli.trigger.subtype.namedPipe"),
        TriggerSubtypeName.JoinedDomain => Texts.Of("cli.trigger.subtype.joinedDomain"),
        TriggerSubtypeName.LeftDomain => Texts.Of("cli.trigger.subtype.leftDomain"),
        TriggerSubtypeName.PortOpened => Texts.Of("cli.trigger.subtype.portOpened"),
        TriggerSubtypeName.PortClosed => Texts.Of("cli.trigger.subtype.portClosed"),
        TriggerSubtypeName.MachinePolicy => Texts.Of("cli.trigger.subtype.machinePolicy"),
        TriggerSubtypeName.UserPolicy => Texts.Of("cli.trigger.subtype.userPolicy"),
        TriggerSubtypeName.FirstAddressArrived => Texts.Of("cli.trigger.subtype.firstAddressArrived"),
        TriggerSubtypeName.LastAddressRemoved => Texts.Of("cli.trigger.subtype.lastAddressRemoved"),
        _ => name.ToString()
    };

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
