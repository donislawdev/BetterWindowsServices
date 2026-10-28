using System.Buffers;

namespace Bws.Core.Snapshots;

/// <summary>
/// The trigger subtypes the documentation of <c>SERVICE_TRIGGER</c> gives a name - ten of them, read off
/// learn.microsoft.com on 2026-10-27 (backlog 625). Every other subtype is a GUID nobody names: a device
/// interface class, an ETW provider, or the one every system state change trigger on this machine carries.
/// </summary>
public enum TriggerSubtypeName
{
    /// <summary>RPC_INTERFACE_EVENT_GUID, under a network endpoint trigger.</summary>
    RpcInterface,

    /// <summary>NAMED_PIPE_EVENT_GUID, under a network endpoint trigger.</summary>
    NamedPipe,

    /// <summary>DOMAIN_JOIN_GUID, under a domain trigger.</summary>
    JoinedDomain,

    /// <summary>DOMAIN_LEAVE_GUID, under a domain trigger.</summary>
    LeftDomain,

    /// <summary>FIREWALL_PORT_OPEN_GUID, under a firewall port trigger.</summary>
    PortOpened,

    /// <summary>FIREWALL_PORT_CLOSE_GUID, under a firewall port trigger.</summary>
    PortClosed,

    /// <summary>MACHINE_POLICY_PRESENT_GUID, under a group policy trigger.</summary>
    MachinePolicy,

    /// <summary>USER_POLICY_PRESENT_GUID, under a group policy trigger.</summary>
    UserPolicy,

    /// <summary>NETWORK_MANAGER_FIRST_IP_ADDRESS_ARRIVAL_GUID, under an IP address trigger.</summary>
    FirstAddressArrived,

    /// <summary>NETWORK_MANAGER_LAST_IP_ADDRESS_REMOVAL_GUID, under an IP address trigger.</summary>
    LastAddressRemoved
}

/// <summary>
/// The parts of a trigger a person reads that are the same in every language - which documented subtype
/// it is, and its data as text - so the two interfaces put their own words around one answer.
///
/// <b>Read from <see cref="TriggerDocument"/>, the form a snapshot holds</b>, because a comparison has
/// nothing else: its two sides are documents, and a side older than schema seven holds the kind and the
/// action alone. A live entry goes through <see cref="TriggerDocument.Of"/> first, so a trigger reads the
/// same in <c>bws show</c> and in a comparison.
/// </summary>
public static class TriggerParts
{
    /// <summary>
    /// The documented subtypes by the kind they belong to. <b>The kind is part of the key</b>: the
    /// documentation names each GUID for one kind only, so a port GUID under a group policy trigger is a GUID
    /// nobody named, and calling it "port opened" there would be a claim the documentation does not make.
    /// </summary>
    private static readonly Dictionary<(string Kind, Guid Subtype), TriggerSubtypeName> Documented = new()
    {
        [(nameof(TriggerKind.NetworkEndpoint), new Guid("bc90d167-9470-4139-a9ba-be0bbbf5b74d"))] = TriggerSubtypeName.RpcInterface,
        [(nameof(TriggerKind.NetworkEndpoint), new Guid("1f81d131-3fac-4537-9e0c-7e7b0c2f4b55"))] = TriggerSubtypeName.NamedPipe,
        [(nameof(TriggerKind.DomainJoin), new Guid("1ce20aba-9851-4421-9430-1ddeb766e809"))] = TriggerSubtypeName.JoinedDomain,
        [(nameof(TriggerKind.DomainJoin), new Guid("ddaf516e-58c2-4866-9574-c3b615d42ea1"))] = TriggerSubtypeName.LeftDomain,
        [(nameof(TriggerKind.FirewallPort), new Guid("b7569e07-8421-4ee0-ad10-86915afdad09"))] = TriggerSubtypeName.PortOpened,
        [(nameof(TriggerKind.FirewallPort), new Guid("a144ed38-8e12-4de4-9d96-e64740b1a524"))] = TriggerSubtypeName.PortClosed,
        [(nameof(TriggerKind.GroupPolicy), new Guid("659fcae6-5bdb-4da9-b1ff-ca2a178d46e0"))] = TriggerSubtypeName.MachinePolicy,
        [(nameof(TriggerKind.GroupPolicy), new Guid("54fb46c8-f089-464c-b1fd-59d1b62c3b50"))] = TriggerSubtypeName.UserPolicy,
        [(nameof(TriggerKind.IpAddress), new Guid("4f27f2de-14e2-430b-a549-7cd48cbc8245"))] = TriggerSubtypeName.FirstAddressArrived,
        [(nameof(TriggerKind.IpAddress), new Guid("cc4ba62a-162e-4648-847a-b6bdf993e335"))] = TriggerSubtypeName.LastAddressRemoved
    };

    /// <summary>Between the values of one data item - see <see cref="Values"/>.</summary>
    private const string ValueSeparator = ", ";

    /// <summary>Between the data items of one trigger on one line - see <see cref="OneLine"/>.</summary>
    private const string ItemSeparator = " | ";

    /// <summary>
    /// What puts a value in quotation marks: the two separators above, and the quotation mark itself.
    /// <b>Written out rather than built from the two constants</b>, so a separator changed without this
    /// changing with it stops the tests that pin a value holding each of them.
    /// </summary>
    private static readonly SearchValues<char> Mistakable = SearchValues.Create([',', '|', '"']);

    /// <summary>
    /// Which documented subtype the trigger points at, or null - for a GUID the documentation does not
    /// name, for a named one under another kind, and for a trigger with no subtype at all, which every
    /// trigger read from a file older than schema seven is.
    /// </summary>
    public static TriggerSubtypeName? Subtype(TriggerDocument trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        return trigger.Subtype is { } text
            && Guid.TryParse(text, out var subtype)
            && Documented.TryGetValue((trigger.Kind, subtype), out var name)
                ? name
                : null;
    }

    /// <summary>
    /// One data item's values as a person reads them, without the word for its type - the interfaces add
    /// "level" or "any of" around it.
    ///
    /// <b>Bytes as pairs with a space between them</b>, the way <c>sc qtriggerinfo</c> prints them, for
    /// the binary type and for a type nobody documented - both are written as unbroken hex in the
    /// document, where nobody counts them. A value of either that is not an even run of hex digits, which
    /// only a file written by hand holds, is shown as text instead, so no pair is cut out of something that
    /// is not a byte. <b>The values of one item joined by a comma</b>, where <c>sc</c> uses a semicolon: the
    /// house rule keeps semicolons out of what this program writes for a person.
    ///
    /// <b>A value that could be read as something else is in quotation marks, its own doubled</b> - empty,
    /// holding a comma, the bar <see cref="OneLine"/> joins items with or a quotation mark, or starting or
    /// ending in white space. Since the review of PR #98: until then only the empty one was, on the argument
    /// that no value on the machine this was measured on holds a comma. A firewall rule's program path may,
    /// and <c>C:\a, b.exe</c> read exactly like the two values <c>C:\a</c> and <c>b.exe</c> - so a comparison
    /// printed two identical lines for a change that was real (<c>tools/snapshot-probe/trigger-shapes.ps1</c>,
    /// case <c>comma-in-a-value</c>).
    /// </summary>
    public static string Values(TriggerDataDocument item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var bytes = item.Type is nameof(TriggerDataType.Binary) or nameof(TriggerDataType.Unknown);

        return string.Join(ValueSeparator, item.Values.Select(value => bytes && IsHex(value) ? Pairs(value) : Shown(value)));
    }

    /// <summary>
    /// The data items of one trigger on one line, for a comparison, which says "before -> after" per line.
    ///
    /// <b>Here, beside the rule that quotes, rather than in the two language files</b> - the review of PR
    /// #98 asked for those, and until it the bar was written in both interfaces. The bar and the comma are
    /// safe only because <see cref="Values"/> quotes every value holding one, and a translation choosing
    /// another separator would bring back, silently, two lines that read the same for two triggers.
    /// </summary>
    public static string OneLine(IEnumerable<string> items) => string.Join(ItemSeparator, items);

    private static string Shown(string value) => NeedsQuotes(value)
        ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
        : value;

    private static bool NeedsQuotes(string value) =>
        value.Length == 0
        || value.AsSpan().ContainsAny(Mistakable)
        || char.IsWhiteSpace(value[0])
        || char.IsWhiteSpace(value[^1]);

    private static bool IsHex(string value) => value.Length > 0 && value.Length % 2 == 0 && value.All(char.IsAsciiHexDigit);

    private static string Pairs(string hex) => string.Join(' ', hex.Chunk(2).Select(pair => new string(pair)));

    /// <summary>
    /// A data item's type as the member it names, or <see cref="TriggerDataType.Unknown"/> for a word this
    /// build does not know - which a file written by a later one could hold.
    /// </summary>
    public static TriggerDataType TypeOf(TriggerDataDocument item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // By the names rather than Enum.TryParse, which would take "3" or "binary" as well.
        return Enum.GetNames<TriggerDataType>().Contains(item.Type, StringComparer.Ordinal)
            ? Enum.Parse<TriggerDataType>(item.Type)
            : TriggerDataType.Unknown;
    }
}
