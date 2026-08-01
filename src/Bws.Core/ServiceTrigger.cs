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

/// <summary>
/// One condition under which the manager starts or stops an entry by itself.
///
/// Kind and action only, for now. A trigger also carries a subtype and its own data items -
/// which device class, which port, which event - and those belong to the details panel
/// rather than to a listing. Said here rather than left to be noticed: this type answers
/// "is it trigger started and by what sort of thing", not "by exactly what".
/// </summary>
public sealed record ServiceTrigger(TriggerKind Kind, TriggerAction Action);
