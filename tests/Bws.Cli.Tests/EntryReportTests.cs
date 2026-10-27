using Bws.Core;

namespace Bws.Cli.Tests;

/// <summary>
/// The report `bws show` prints for one entry.
///
/// <b>What these hold to is the one thing --full must never be able to do.</b> The switch decides
/// whether fields that are genuinely empty are printed. It must not decide anything about fields
/// nobody could read - rule 8 of CLAUDE.md forbids swallowing a failed read, and a switch that
/// could hide one would be that rule broken and spelled as an option. Two of the tests below say
/// so from the two sides, because a single one asserting "denied is printed" would still pass on
/// a build where --full was the thing that printed it.
/// </summary>
public sealed class EntryReportTests
{
    [Fact]
    public void A_field_that_is_genuinely_empty_is_left_out_until_it_is_asked_for()
    {
        var entry = Entry with { LoadOrderGroup = Reading<string>.Absent() };

        Assert.DoesNotContain("Load order group", EntryReport.Render(entry, full: false), StringComparison.Ordinal);
        Assert.Contains("Load order group", EntryReport.Render(entry, full: true), StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_nobody_could_read_is_printed_without_being_asked_for()
    {
        // The half that matters. Most entries have more empty fields than filled ones, so the
        // default hides them - and a refusal hidden by the same rule would be the listing's
        // empty cell all over again, one interface later.
        var entry = Entry with { SecurityDescriptor = Reading<string>.Denied(5, "Access is denied.") };

        var report = EntryReport.Render(entry, full: false);

        Assert.Contains("Security descriptor", report, StringComparison.Ordinal);

        // The number travels with it. ReadOutcome.Denied says at length why: a service can be
        // gone by the time it is asked about, and the manager answers 1060 rather than 5. Only
        // the number tells those apart.
        Assert.Contains("5", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_a_refusal_of_access_is_called_no_access()
    {
        // Stability report R-7, owner's decision 2026-10-05: every failure printed "no access" until
        // that day, 1060 included - a service gone between the listing and the question about it.
        // The listing's cell and the report say it the same way, so both are asked here.
        var gone = Entry with { SecurityDescriptor = Reading<string>.Denied(1060, "Gone.") };
        var refused = Entry with { SecurityDescriptor = Reading<string>.Denied(AccessDenied.Code, "Access is denied.") };

        Assert.Contains("(could not be read, code 1060)", EntryReport.Render(gone, full: false), StringComparison.Ordinal);
        Assert.Contains("(no access, code 5)", EntryReport.Render(refused, full: false), StringComparison.Ordinal);
        Assert.Contains("(could not be read)", ListingTable.Render([gone with { StartType = Reading<StartType>.Denied(1060, "Gone.") }]), StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_nobody_asked_about_is_printed_too()
    {
        // Not-read is not empty either. It means this build never went and looked, which is a
        // fact about the run rather than about the service, and hiding it would let a report
        // read as though the answer were "none".
        var entry = Entry with { FileVersion = Reading<string>.NotRead() };

        Assert.Contains("File version", EntryReport.Render(entry, full: false), StringComparison.Ordinal);
    }

    [Fact]
    public void A_heading_with_nothing_under_it_is_left_out()
    {
        // Every field of one section empty, and the section goes with them. A heading over
        // nothing is a reader wondering what they missed.
        var entry = Entry with
        {
            LoadOrderGroup = Reading<string>.Absent(),
            ErrorControl = Reading<ErrorControl>.Absent(),
            DependsOn = Reading<IReadOnlyList<string>>.Absent(),
            RequiredBy = Reading<IReadOnlyList<string>>.NotRead(),
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
            RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
            SecurityDescriptor = Reading<string>.Absent()
        };

        Assert.DoesNotContain("ADVANCED", EntryReport.Render(entry, full: false), StringComparison.Ordinal);
        Assert.Contains("ADVANCED", EntryReport.Render(entry, full: true), StringComparison.Ordinal);
    }

    [Fact]
    public void The_per_user_role_is_reported_and_it_is_the_field_the_window_has_no_column_for()
    {
        // Named here rather than left to a reader: on 2026-08-25 this is the one field the two
        // catalogues differ by, because the window's half of A11 is a slice of its own. A test
        // saying so is what will fail to be interesting once the window catches up.
        var entry = Entry with { PerUserRole = PerUserRole.Template };

        Assert.Contains("Template", EntryReport.Render(entry, full: false), StringComparison.Ordinal);
    }

    [Fact]
    public void A_signature_that_was_read_and_names_nobody_leaves_the_publisher_empty_rather_than_blank()
    {
        // An unsigned file has no publisher, and printing the word beside nothing would be the
        // empty cell this tool spends its rules avoiding. Absent, so it follows --full.
        var entry = Entry with
        {
            Signature = Reading<BinarySignature>.Present(
                new BinarySignature(SignatureStatus.NotSigned, 0, Publisher: null))
        };

        Assert.DoesNotContain("Publisher", EntryReport.Render(entry, full: false), StringComparison.Ordinal);

        // "Not signed" rather than "NotSigned" since backlog 260. This assertion was never about
        // the spelling - it is here to prove the verdict is printed at all while the publisher is
        // not - but it named the value, so the word change reached it.
        Assert.Contains("Not signed", EntryReport.Render(entry, full: false), StringComparison.Ordinal);
    }

    /// <summary>
    /// What sets a trigger off is printed in words, and what it then does keeps its own name.
    ///
    /// <b>Backlog 260, owner's decision 2026-09-01.</b> Seven of the nine kinds read as two or
    /// three words jammed together - <c>DeviceArrival</c> here - and the window has never spelled
    /// them that way. Same fault as backlog 124, third and last family.
    ///
    /// <b>THE ACTION IS ASSERTED UNCHANGED ON PURPOSE, and that is the point of this test rather
    /// than an accident of the specimen.</b> Its three members are <c>Unknown</c>, <c>Start</c> and
    /// <c>Stop</c>, so the value name and the word for a person are already the same string. A
    /// repair that swept it too would have bought a file and three keys to change nothing, and a
    /// later reader would have no way to tell that was measured rather than missed.
    /// </summary>
    [Fact]
    public void A_trigger_kind_of_two_words_is_printed_as_two_words_and_its_action_is_left_alone()
    {
        var entry = Entry with
        {
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
                [new ServiceTrigger(TriggerKind.DeviceArrival, TriggerAction.Start)])
        };

        var report = EntryReport.Render(entry, full: true);

        Assert.Contains("Device arrival", report, StringComparison.Ordinal);
        Assert.DoesNotContain("DeviceArrival", report, StringComparison.Ordinal);

        Assert.Contains("Start", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A trigger per line with its subtype, and each data item on a line under it</b> - backlog 625, the
    /// owner's decision "B - data under the trigger" of 2026-10-27. Every line after the first starts where
    /// the first line's value starts, and an item sits two further in. A documented subtype is a word, any
    /// other the GUID. Shapes from the owner's machine: the firewall port's one item of four values, a
    /// device trigger's two items, the system state's eight bytes, and a level.
    /// </summary>
    [Fact]
    public void Each_trigger_is_a_line_with_its_subtype_and_each_data_item_a_line_under_it()
    {
        var entry = Entry with
        {
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
            [
                new ServiceTrigger(
                    TriggerKind.FirewallPort, TriggerAction.Start, new Guid("b7569e07-8421-4ee0-ad10-86915afdad09"),
                    [new TriggerData(TriggerDataType.String, ["500", "UDP", @"%windir%\system32\svchost.exe", "IKEEXT"])]),
                new ServiceTrigger(
                    TriggerKind.DeviceArrival, TriggerAction.Start, new Guid("4d1e55b2-f16f-11cf-88cb-001111000030"),
                    [new TriggerData(TriggerDataType.String, ["HID_DEVICE_UP:000D_U:0001"]), new TriggerData(TriggerDataType.String, ["HID_DEVICE_UP:000D_U:0002"])]),
                new ServiceTrigger(
                    TriggerKind.CustomSystemStateChange, TriggerAction.Start, new Guid("2d7a2816-0c5e-45fc-9ce7-570e5ecde9c9"),
                    [new TriggerData(TriggerDataType.Binary, ["7518bca328009213"])]),
                new ServiceTrigger(
                    TriggerKind.Custom, TriggerAction.Stop, new Guid("a9f076c5-7e2b-48c4-8812-77bf7a616dfe"),
                    [new TriggerData(TriggerDataType.Level, ["4"])])
            ])
        };

        var lines = EntryReport.Render(entry, full: false).Split(Environment.NewLine);
        var at = Array.FindIndex(lines, line => line.TrimStart().StartsWith("Triggers ", StringComparison.Ordinal));
        var column = lines[at].IndexOf("Firewall port/Start", StringComparison.Ordinal);
        var block = lines.Skip(at).Take(9).ToList();

        Assert.All(block.Skip(1), line => Assert.True(string.IsNullOrWhiteSpace(line[..column]), line));
        Assert.Equal(
            [
                "Firewall port/Start  Port opened",
                @"  500, UDP, %windir%\system32\svchost.exe, IKEEXT",
                "Device arrival/Start  4d1e55b2-f16f-11cf-88cb-001111000030",
                "  HID_DEVICE_UP:000D_U:0001",
                "  HID_DEVICE_UP:000D_U:0002",
                "System state/Start  2d7a2816-0c5e-45fc-9ce7-570e5ecde9c9",
                "  75 18 bc a3 28 00 92 13",
                "Custom/Stop  a9f076c5-7e2b-48c4-8812-77bf7a616dfe",
                "  level 4"
            ],
            block.Select(line => line[column..]));
    }

    /// <summary>
    /// <b>Only the triggers are several lines - a line break inside any other value stays written out</b>,
    /// on the line it belongs to (stability report C-4). The first version of the triggers' layout split
    /// every value at its line breaks, which let a description print a line looking like a field of this
    /// report, and <c>TerminalTextTests</c> caught it - this says the same next to the layout itself. The
    /// trigger's own data takes the same door: a line break in it is written out too.
    /// </summary>
    [Fact]
    public void A_line_break_inside_a_value_stays_written_out_and_only_the_triggers_are_several_lines()
    {
        var entry = Entry with
        {
            Description = Reading<string>.Present("First line.\r\nAccount  LocalSystem"),
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
                [new ServiceTrigger(TriggerKind.Custom, TriggerAction.Start, Guid.Empty, [new TriggerData(TriggerDataType.String, ["a\nb"])])])
        };

        var report = EntryReport.Render(entry, full: false);

        Assert.Contains("First line.<U+000D><U+000A>Account  LocalSystem", report, StringComparison.Ordinal);
        Assert.Contains("  a<U+000A>b", report, StringComparison.Ordinal);
    }

    private static ScmEntry Entry => Entries.Full;
}
