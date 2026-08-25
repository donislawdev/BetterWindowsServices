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
        Assert.Contains("NotSigned", EntryReport.Render(entry, full: false), StringComparison.Ordinal);
    }

    private static ScmEntry Entry => new()
    {
        ServiceName = "Spooler",
        DisplayName = "Print Spooler",
        Description = Reading<string>.Present("Spools print jobs."),
        EntryType = EntryType.OwnProcess,
        PerUserRole = PerUserRole.None,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1234),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        Account = Reading<string>.Present("LocalSystem"),
        DependsOn = Reading<IReadOnlyList<string>>.Present(["RPCSS"]),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
        BinaryPath = Reading<string>.Present(@"C:\Windows\System32\spoolsv.exe"),
        BinaryFile = Reading<string>.Present(@"C:\Windows\System32\spoolsv.exe"),
        BinaryOnDisk = Reading<bool>.Present(true),
        Signature = Reading<BinarySignature>.Present(
            new BinarySignature(SignatureStatus.Trusted, 0, "Microsoft Windows")),
        FileVersion = Reading<string>.Present("10.0.26100.1"),
        BinaryHash = Reading<string>.Present("abc123"),
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Present(["SeTcbPrivilege"]),
        SidType = Reading<ServiceSidType>.Present(ServiceSidType.Unrestricted),
        SecurityDescriptor = Reading<string>.Present("O:SYG:SYD:(A;;CCLCSWLOCRRC;;;AU)"),
        ErrorControl = Reading<ErrorControl>.Present(Core.ErrorControl.Normal),
        LoadOrderGroup = Reading<string>.Present("SpoolerGroup"),
        Memory = Reading<ProcessMemory>.Present(new ProcessMemory(18_000_000, 20_000_000, 1))
    };
}
