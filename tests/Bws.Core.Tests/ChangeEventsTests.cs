using System.Xml;
using Bws.Core.History;

namespace Bws.Core.Tests;

/// <summary>
/// The events of the change history, read from the XML Windows renders for them - every specimen
/// below has the shape of a real one read on 2026-10-15 (docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md
/// section 9), with the machine, the account and the paths replaced.
///
/// <b>Letters outside ASCII are written as XML character references</b>, which the parser turns into
/// the letters themselves - so the specimens hold what a Polish machine writes while this file stays
/// ASCII, which is what PublicSurfaceGuards asks of a file in git.
/// </summary>
public sealed class ChangeEventsTests
{
    private const string Space = "http://schemas.microsoft.com/win/2004/08/events/event";
    private const string Person = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    /// <summary>The fields of event 7045, in the order of its template.</summary>
    private static readonly string[] InstallationFields = ["ServiceName", "ImagePath", "ServiceType", "StartType", "AccountName"];

    /// <summary>
    /// What a Polish Windows calls a manual start - "uruchamianie na" and a word starting with z-dot
    /// and a-ogonek - built from char codes so this file stays ASCII.
    /// </summary>
    private static readonly string OnDemand = "uruchamianie na " + (char)0x17C + (char)0x105 + "danie";

    [Fact]
    public void A_start_type_change_names_the_entry_by_its_internal_name_and_keeps_the_words()
    {
        var change = ChangeEvents.Change(StartType(
            "S-1-5-18",
            "Instalator modu&#x142;&#xF3;w systemu Windows", "uruchamianie na &#x17C;&#x105;danie", "autostart", "TrustedInstaller"));

        Assert.NotNull(change);
        Assert.Equal(ChangeKind.StartTypeChanged, change.Kind);
        Assert.Equal("TrustedInstaller", change.ServiceName);
        Assert.Equal(OnDemand, change.Words.StartTypeBefore);
        Assert.Equal("autostart", change.Words.StartTypeAfter);
        Assert.Null(change.Words.EntryType);
        Assert.Null(change.Words.StartType);
        Assert.Equal("S-1-5-18", change.By!.Sid);

        // Named later, and only where the machine can do it without the network.
        Assert.Null(change.By.Name);
    }

    [Fact]
    public void A_change_a_person_made_carries_that_persons_sid()
    {
        // F22: a start type flipped with sc.exe from the owner's session was written under the
        // session's own SID, not under SYSTEM.
        var change = ChangeEvents.Change(StartType(Person, "Print Spooler", "auto start", "demand start", "Spooler"));

        Assert.Equal(Person, change!.By!.Sid);
        Assert.Equal("Print Spooler", change.DisplayName);
    }

    [Fact]
    public void An_installation_carries_the_display_name_and_never_a_service_name()
    {
        // F23: the field called ServiceName holds the display name. Measured on the throwaway
        // machine, where tools/hostile/ installs entries with names like this one.
        var change = ChangeEvents.Change(Installation(
            "S-1-5-21-1111111111-2222222222-3333333333-500",
            "Hostile - switched off", @"C:\Windows\system32\svchost.exe -k netsvcs", "user mode service", "disabled", "LocalSystem"));

        Assert.NotNull(change);
        Assert.Equal(ChangeKind.Installed, change.Kind);
        Assert.Null(change.ServiceName);
        Assert.Equal("Hostile - switched off", change.DisplayName);
        Assert.Equal(@"C:\Windows\system32\svchost.exe -k netsvcs", change.BinaryPath);
        Assert.Equal("LocalSystem", change.Account);
        Assert.Equal("user mode service", change.Words.EntryType);
        Assert.Equal("disabled", change.Words.StartType);
        Assert.Null(change.Words.StartTypeBefore);
    }

    [Fact]
    public void A_driver_installed_with_an_empty_account_has_no_account_rather_than_an_empty_one()
    {
        var change = ChangeEvents.Change(Installation(
            "S-1-5-18",
            "KslD", @"system32\drivers\wd\KslD.sys", "sterownik trybu j&#x105;dra", "uruchamianie na &#x17C;&#x105;danie", ""));

        Assert.Null(change!.Account);
        Assert.Equal(OnDemand, change.Words.StartType);
    }

    [Fact]
    public void A_start_type_change_without_the_internal_name_is_kept_with_what_it_has()
    {
        // An older Windows, or a shape nobody has seen - the change stays, without a name, and a view of
        // one entry counts it instead of losing it.
        var change = ChangeEvents.Change(StartType("S-1-5-18", "Print Spooler", "auto start", "demand start"));

        Assert.NotNull(change);
        Assert.Null(change.ServiceName);
        Assert.Equal("Print Spooler", change.DisplayName);
    }

    [Fact]
    public void An_event_this_history_does_not_read_is_no_change()
    {
        // 7036 is a state change, not configuration, and 7040 from another provider is not ours.
        Assert.Null(ChangeEvents.Change(Event(new Header("Service Control Manager", "7036", "S-1-5-18"), "<EventData/>")));
        Assert.Null(ChangeEvents.Change(Event(new Header("Some-Other-Provider", "7040", "S-1-5-18"), "<EventData/>")));
    }

    [Fact]
    public void The_time_is_the_instant_windows_wrote_in_utc_to_the_full_precision()
    {
        var change = ChangeEvents.Change(StartType("S-1-5-18", "Print Spooler", "a", "b", "Spooler"));

        // Two changes 11 ms apart were measured on the owner's machine, and seconds would make them one.
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 8, 26, 57, TimeSpan.Zero).AddTicks(738968), change!.At);
        Assert.Equal(TimeSpan.Zero, change.At!.Value.Offset);
    }

    [Fact]
    public void Clearing_the_log_being_read_is_a_clearing_and_clearing_another_one_is_not()
    {
        // F25: Windows writes event 104 into System for clearing ANY channel - five of them on the
        // owner's machine on one afternoon - and only the one naming System says System was emptied.
        Assert.NotNull(ChangeEvents.ClearingOf(Cleared("System"), "System"));
        Assert.Null(ChangeEvents.ClearingOf(Cleared("Windows PowerShell"), "System"));
        Assert.Equal(Person, ChangeEvents.ClearingOf(Cleared("System"), "System")!.By!.Sid);
    }

    [Fact]
    public void A_start_type_change_is_not_mistaken_for_a_clearing()
    {
        Assert.Null(ChangeEvents.ClearingOf(StartType("S-1-5-18", "x", "a", "b", "x"), "System"));
    }

    [Fact]
    public void A_document_type_definition_is_refused_rather_than_read()
    {
        // Windows never renders one. The parser is still the one place that should not have to trust
        // the text it is handed.
        var hostile = "<!DOCTYPE Event [<!ENTITY a 'b'>]>" + StartType("S-1-5-18", "&a;", "a", "b", "x");

        Assert.Throws<XmlException>(() => ChangeEvents.Change(hostile));
    }

    [Fact]
    public void An_event_bigger_than_one_event_can_be_is_refused_rather_than_read()
    {
        // Review of #70: the size of what is parsed is a number from the log. One record fits in a 64 KB
        // chunk, so anything past the bound is refused - and something just under it still reads.
        var huge = new string('a', ChangeEvents.LargestRendering / sizeof(char));
        var large = new string('a', 100_000);

        Assert.Throws<XmlException>(() => ChangeEvents.Change(StartType("S-1-5-18", huge, "a", "b", "x")));
        Assert.Equal(large, ChangeEvents.Change(StartType("S-1-5-18", large, "a", "b", "x"))!.DisplayName);
    }

    /// <summary>Event 7040, with param1 to param4 in the order given - fewer when a test leaves some out.</summary>
    private static string StartType(string user, params string[] values) =>
        Event(new Header("Service Control Manager", "7040", user), Data(values.Select((value, index) => ($"param{index + 1}", value))));

    /// <summary>Event 7045, with its five fields in the order of the template.</summary>
    private static string Installation(string user, params string[] values) =>
        Event(new Header("Service Control Manager", "7045", user), Data(InstallationFields.Zip(values)));

    private static string Data(IEnumerable<(string Name, string Value)> fields) =>
        "<EventData>" + string.Concat(fields.Select(field => $"<Data Name='{field.Name}'>{field.Value}</Data>")) + "</EventData>";

    private static string Cleared(string channel) =>
        Event(new Header("Microsoft-Windows-Eventlog", "104", Person),
            "<UserData><LogFileCleared xmlns='http://manifests.microsoft.com/win/2004/08/windows/eventlog'>" +
            $"<SubjectUserName>USER</SubjectUserName><SubjectDomainName>HOST</SubjectDomainName><Channel>{channel}</Channel>" +
            "<BackupPath></BackupPath></LogFileCleared></UserData>");

    /// <summary>The System part as Windows renders it, with the payload the caller gives.</summary>
    private static string Event(Header header, string payload) =>
        $"<Event xmlns='{Space}'><System><Provider Name='{header.Provider}'/>" +
        $"<EventID Qualifiers='16384'>{header.Id}</EventID><Version>0</Version><Level>4</Level>" +
        "<TimeCreated SystemTime='2026-10-15T08:26:57.0738968Z'/><EventRecordID>531</EventRecordID>" +
        $"<Channel>System</Channel><Computer>HOST</Computer><Security UserID='{header.User}'/></System>{payload}</Event>";

    private sealed record Header(string Provider, string Id, string User);
}
