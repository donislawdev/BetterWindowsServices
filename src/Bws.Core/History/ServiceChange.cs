namespace Bws.Core.History;

/// <summary>
/// What kind of change one event in the System log records.
///
/// <b>Two, because those are the two the log keeps about configuration by default</b> - measured on
/// the owner's machine and on the throwaway one on 2026-10-15 (docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md
/// section 9). Who stopped a service is not among them, and neither is a changed account, path or
/// recovery list: Windows does not write those to this log at all. Kinds may be added later the way
/// plan warnings are - a script reading the two below keeps working when a third appears.
///
/// <b>Written into JSON exactly as spelled here</b>, rule 5 of docs/03, like every other enumeration
/// this tool prints.
/// </summary>
public enum ChangeKind
{
    /// <summary>
    /// Event 7040. Carries the internal name of the entry, so it can be tied to an entry by identity.
    /// </summary>
    StartTypeChanged,

    /// <summary>
    /// Event 7045. <b>Carries the DISPLAY name and no internal name at all</b> - measured on the
    /// throwaway machine, where services made by tools/hostile/ were logged under names like
    /// "Hostile - switched off". So it is shown, never matched to an entry: rule 3 of CLAUDE.md keeps
    /// display names for people to read.
    /// </summary>
    Installed
}

/// <summary>
/// Who made a change: the account Windows wrote into the event, and its name when this machine can
/// say it without asking anybody else.
/// </summary>
/// <param name="Sid">The identity. Rule 3: an account is its SID, never its name.</param>
/// <param name="Name">
/// For a person to read, and null when the SID is not one this machine can name on its own - an
/// account of a domain would have Windows ask a domain controller, and ADR-19 keeps this program off
/// the network. Owner's decision J1-c, 2026-10-15.
/// </param>
public sealed record Actor(string Sid, string? Name);

/// <summary>
/// The words Windows wrote into the event for the types involved - in the language the system had
/// when it wrote them.
///
/// <b>Kept apart from every value this tool writes itself, and that is the point of the type.</b>
/// "autostart" on a Polish machine and "auto start" on an English one are the same setting, and
/// neither is <c>Automatic</c>. The words are put in by the service manager at the moment it logs
/// (the event template holds four plain strings), so there is no number underneath them to translate
/// back - and a field called startType beside the listing's startType, holding "autostart", would be a
/// value a script compares and gets silently wrong. Owner's decision J1-d, 2026-10-15.
/// </summary>
/// <param name="StartTypeBefore">Event 7040: the start type before the change.</param>
/// <param name="StartTypeAfter">Event 7040: the start type after it.</param>
/// <param name="EntryType">Event 7045: what kind of entry was installed.</param>
/// <param name="StartType">Event 7045: the start type it was installed with.</param>
public sealed record WindowsWords(
    string? StartTypeBefore, string? StartTypeAfter, string? EntryType, string? StartType)
{
    /// <summary>No words, for an event that names none of the four.</summary>
    public static WindowsWords None { get; } = new(null, null, null, null);
}

/// <summary>
/// One change to the configuration of one entry, as the System log recorded it.
///
/// <b>Every field may be null, and none of them is ever invented.</b> An event in a shape this reader
/// did not expect - an older Windows, a field missing - becomes a change with the pieces it had,
/// rather than a change dropped on the floor, which would make the history read as complete when it
/// is not (rule 8).
/// </summary>
public sealed record ServiceChange
{
    /// <summary>When Windows wrote the event. Null only if the event carried no readable time.</summary>
    public required DateTimeOffset? At { get; init; }

    public required ChangeKind Kind { get; init; }

    /// <summary>
    /// The internal name, and therefore the identity. Always null for <see cref="ChangeKind.Installed"/>,
    /// and null for a start type change from a Windows that did not write it.
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>The display name the event carries, for a person to read.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Who did it. Null when the event names nobody.</summary>
    public Actor? By { get; init; }

    /// <summary>
    /// Installations only: the launch command as it was installed, literally - the same thing the
    /// listing calls binaryPath.
    /// </summary>
    public string? BinaryPath { get; init; }

    /// <summary>
    /// Installations only: the account the entry was installed to run as, in the manager's spelling
    /// ("LocalSystem"). Null for a driver, which Windows logs with an empty one.
    /// </summary>
    public string? Account { get; init; }

    public WindowsWords Words { get; init; } = WindowsWords.None;
}
