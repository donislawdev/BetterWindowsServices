using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One trigger in the words a person reads in this window: the kind and the action, the subtype, and the
/// data (backlog 625, 2026-10-27). The command line has the same in its own <c>TriggerWords</c>, in its own
/// language file, out of the same <see cref="TriggerParts"/> - so the two can differ in words and never in
/// which subtype a GUID is or how its bytes read.
///
/// <b>Every piece from outside goes through <see cref="Printable"/> here</b>, because this window's texts do
/// not escape what they are given and the command line's do: a kind, a subtype or a value out of a snapshot
/// file is whatever the file says, and the comparison table shows two files side by side.
/// </summary>
internal static class TriggerLines
{
    /// <summary>
    /// One trigger on one line, for a row of the comparison table - the kind, the action, the subtype, and the
    /// data items joined by a bar, the one character no value on the machine this was measured on holds.
    /// </summary>
    internal static string Line(TriggerDocument trigger)
    {
        var items = Items(trigger).ToList();

        return items.Count == 0 ? Head(trigger) : Texts.Of("gui.trigger.withData", Head(trigger), string.Join(" | ", items));
    }

    /// <summary>
    /// The kind and the action, and the subtype after them - in words for the ten the documentation names, as
    /// the GUID otherwise, and none at all for a trigger out of a file older than schema seven.
    /// </summary>
    internal static string Head(TriggerDocument trigger) =>
        trigger.Subtype is null
            ? KindAndAction(trigger)
            : Texts.Of("gui.trigger.withSubtype", KindAndAction(trigger), Subtype(trigger));

    /// <summary>The kind and the action - the label of a trigger's line in the details panel.</summary>
    internal static string KindAndAction(TriggerDocument trigger) =>
        Texts.Of("gui.trigger.head", Kind(trigger.Kind), Printable.Of(trigger.Action ?? string.Empty));

    /// <summary>
    /// What the details panel says under a trigger's kind and action: the subtype on the first line and each
    /// data item on a line of its own - the command line's layout (owner's decision "B"), as one value of
    /// several lines. Empty for a trigger with neither, which the panel then says in its word for nothing.
    /// </summary>
    internal static string Detail(TriggerDocument trigger) => string.Join(
        '\n',
        trigger.Subtype is null ? Items(trigger) : Items(trigger).Prepend(Subtype(trigger)));

    /// <summary>The subtype in words for the ten the documentation names, as the GUID otherwise.</summary>
    private static string Subtype(TriggerDocument trigger) =>
        TriggerParts.Subtype(trigger) is { } name ? Subtype(name) : Printable.Of(trigger.Subtype ?? string.Empty);

    /// <summary>Each data item as one piece of text, the type's word around its values.</summary>
    internal static IEnumerable<string> Items(TriggerDocument trigger) =>
        (trigger.Data ?? []).Select(item => TriggerParts.TypeOf(item) switch
        {
            TriggerDataType.Level => Texts.Of("gui.trigger.level", Values(item)),
            TriggerDataType.KeywordAny => Texts.Of("gui.trigger.keywordAny", Values(item)),
            TriggerDataType.KeywordAll => Texts.Of("gui.trigger.keywordAll", Values(item)),
            TriggerDataType.Unknown => Texts.Of("gui.trigger.unknownType", Values(item)),
            _ => Values(item)
        });

    private static string Values(TriggerDataDocument item) => Printable.Of(TriggerParts.Values(item));

    /// <summary>
    /// A kind as a document names it, in the list's own word - and a name this build does not know left as
    /// it is, since a file written by a later build may hold one.
    /// </summary>
    private static string Kind(string? kind) =>
        kind is not null && Enum.GetNames<TriggerKind>().Contains(kind, StringComparer.Ordinal)
            ? CellFaces.TriggerKindLabel(Enum.Parse<TriggerKind>(kind))
            : kind is null ? Texts.Of("gui.cell.unknown") : Printable.Of(kind);

    private static string Subtype(TriggerSubtypeName name) => name switch
    {
        TriggerSubtypeName.RpcInterface => Texts.Of("gui.trigger.subtype.rpcInterface"),
        TriggerSubtypeName.NamedPipe => Texts.Of("gui.trigger.subtype.namedPipe"),
        TriggerSubtypeName.JoinedDomain => Texts.Of("gui.trigger.subtype.joinedDomain"),
        TriggerSubtypeName.LeftDomain => Texts.Of("gui.trigger.subtype.leftDomain"),
        TriggerSubtypeName.PortOpened => Texts.Of("gui.trigger.subtype.portOpened"),
        TriggerSubtypeName.PortClosed => Texts.Of("gui.trigger.subtype.portClosed"),
        TriggerSubtypeName.MachinePolicy => Texts.Of("gui.trigger.subtype.machinePolicy"),
        TriggerSubtypeName.UserPolicy => Texts.Of("gui.trigger.subtype.userPolicy"),
        TriggerSubtypeName.FirstAddressArrived => Texts.Of("gui.trigger.subtype.firstAddressArrived"),
        TriggerSubtypeName.LastAddressRemoved => Texts.Of("gui.trigger.subtype.lastAddressRemoved"),
        _ => name.ToString()
    };
}
