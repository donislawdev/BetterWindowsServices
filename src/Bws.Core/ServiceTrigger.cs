namespace Bws.Core;

/// <summary>
/// What sets a trigger off.
///
/// These are the conditions the specification lists in the details panel and calls the
/// strongest thing there: services.msc does not show them at all, and finding out today
/// means running sc qtriggerinfo one service at a time.
///
/// The names are ours, from the glossary, not the manager's - the manager spells them
/// SERVICE_TRIGGER_TYPE_DEVICE_INTERFACE_ARRIVAL and nobody reads that on a screen.
/// </summary>
public enum TriggerKind
{
    /// <summary>A kind the interop metadata does not name. Never silently folded into another.</summary>
    Unknown = 0,

    /// <summary>A device of a particular interface class appeared.</summary>
    DeviceArrival,

    /// <summary>The machine got, or lost, an IP address.</summary>
    IpAddress,

    /// <summary>The machine joined, or left, a domain.</summary>
    DomainJoin,

    /// <summary>A port opened, or closed, in the firewall.</summary>
    FirewallPort,

    /// <summary>A group policy setting changed.</summary>
    GroupPolicy,

    /// <summary>Traffic arrived on an endpoint the service listens to.</summary>
    NetworkEndpoint,

    /// <summary>An event defined by the service itself, identified by a subtype.</summary>
    Custom,

    /// <summary>
    /// A change in some state the system tracks, such as the machine going on battery.
    ///
    /// The interop metadata has no name for this one and it is not rare: measured on a real
    /// machine on 2026-08-01 it was the second most common kind, 89 triggers across 49
    /// entries. Leaving it as Unknown would have made the second largest group on the
    /// machine indistinguishable from anything else we cannot name.
    /// </summary>
    CustomSystemStateChange
}

/// <summary>What the trigger does when it fires.</summary>
public enum TriggerAction
{
    Unknown = 0,
    Start,
    Stop
}

/// <summary>What one data item of a trigger holds, in our words for the manager's five types.</summary>
public enum TriggerDataType
{
    /// <summary>A type the documentation does not name. Its bytes are kept, written as hexadecimal.</summary>
    Unknown = 0,

    /// <summary>Bytes, written as lower case hexadecimal with nothing between them.</summary>
    Binary,

    /// <summary>One string, or several back to back - the port, protocol and program of a firewall trigger.</summary>
    String,

    /// <summary>One byte, an event level, written in decimal.</summary>
    Level,

    /// <summary>A 64 bit keyword mask where any bit is enough, written as 0x and sixteen hexadecimal digits.</summary>
    KeywordAny,

    /// <summary>A 64 bit keyword mask where every bit is needed, written the same way.</summary>
    KeywordAll
}

/// <summary>
/// One data item of a trigger: which device, which port, which RPC interface, which event.
///
/// <b>Always at least one value, and text for every type</b> (owner's decision of 2026-10-27, logic
/// report C01). Several only when the manager holds several strings in one item, which on the machine
/// this was written on is the two firewall port triggers - <c>500</c>, <c>UDP</c>, the program and the
/// service. <c>sc qtriggerinfo</c> joins those with semicolons, and that is a lossy form: a path may hold
/// a semicolon. The bytes, the level and the keyword are one value each, in a form this program writes -
/// hexadecimal rather than Base64, because Base64 tells upper case from lower and hexadecimal written
/// lower case has nothing for a comparison to fold.
///
/// <b>Strings are kept exactly as the manager holds them.</b> The documentation says a network endpoint
/// trigger fires when "the string is an exact match", and of the 96 network endpoint data items on the
/// machine this was written on, 38 are upper case, 41 lower case and some mix the two inside one value -
/// so case is something the manager hands over, and a change of it may change whether the trigger fires.
/// </summary>
public sealed record TriggerData(TriggerDataType Type, IReadOnlyList<string> Values)
{
    /// <summary>
    /// The same type and the same values in the same order, spelled the same - not the same list object,
    /// which is what a record compares a list by unless told otherwise. Review of PR #97.
    /// </summary>
    public bool Equals(TriggerData? other) =>
        other is not null && Type == other.Type && Values.SequenceEqual(other.Values, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Type);

        foreach (var value in Values)
        {
            hash.Add(value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// One condition under which the manager starts or stops an entry by itself.
///
/// <b>Whole since 2026-10-27</b> (logic report C01, snapshot schema seven): until then it held the kind
/// and the action only, and said so. A snapshot of it could not tell one RPC interface from another, or
/// one device class from the next, so two machines whose triggers differed in exactly what they wait for
/// compared as the same.
///
/// <b>The subtype is a GUID the manager points at</b> - the device interface class, the ETW provider,
/// or one of the documented constants that say which half of a kind this is (a port opening rather than
/// closing, the first IP address rather than the last). Null when the manager points at none, which no
/// trigger on the machine this was written on does. The data is empty for the kinds that carry none.
///
/// <b>The two new members have defaults</b> because a trigger with neither is a real thing to build -
/// the specimens and tests that describe what sort of trigger an entry has, and nothing more, say exactly
/// that and no more.
/// </summary>
public sealed record ServiceTrigger(
    TriggerKind Kind, TriggerAction Action, Guid? Subtype = null, IReadOnlyList<TriggerData>? Data = null)
{
    /// <summary>The data items in the order the manager holds them, empty when there are none.</summary>
    public IReadOnlyList<TriggerData> Data { get; init; } = Data ?? [];

    /// <summary>
    /// Equal when everything a trigger says is equal, its data item by item in the manager's order.
    ///
    /// <b>Written out since the review of PR #97</b>: until schema seven this record held two enumeration
    /// values and was equal by them, and the list of data items would have quietly turned that into
    /// "the same list object" - two readings of one unchanged trigger unequal. Nothing compares triggers
    /// this way today, which is when a value type is cheapest to keep honest.
    /// </summary>
    public bool Equals(ServiceTrigger? other) =>
        other is not null
        && Kind == other.Kind
        && Action == other.Action
        && Subtype == other.Subtype
        && Data.SequenceEqual(other.Data);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Kind);
        hash.Add(Action);
        hash.Add(Subtype);

        foreach (var item in Data)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
