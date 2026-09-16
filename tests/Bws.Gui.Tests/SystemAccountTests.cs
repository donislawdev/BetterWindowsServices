using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the account cell says, and what it keeps behind what it says.
///
/// <b>Asked through <see cref="EntryRow"/> where the window binds, and through the column where
/// the export and the panel read</b>, for the reason <see cref="CellFaceTests"/> gives: a test
/// against the mapping alone could stay green while the row stopped passing it on. The row and
/// the column are two paths to one cell - <c>EntryRow</c> keeps the shown account to decide
/// whether a row moved, the column reads it for the grid - and both are asked here so that they
/// cannot part.
///
/// <b>The spellings are the ones measured on the owner's machine on 2026-09-15</b>, not a list
/// written from memory: 336 services, nine distinct spellings, and each of the three system
/// accounts arriving in two cases. The comment at the head of <see cref="SystemAccounts"/> carries
/// the counts.
/// </summary>
public sealed class SystemAccountTests
{
    /// <summary>
    /// Each documented spelling, in either case the registry holds it in, is shown by its name.
    ///
    /// Both cases per account on purpose: a window comparing with case would show 18 services as
    /// "localSystem" beside 197 as "Local System", which is the fault this file was built to end.
    /// </summary>
    [Theory]
    [InlineData("LocalSystem", "Local System")]
    [InlineData("localSystem", "Local System")]
    [InlineData(@"NT AUTHORITY\LocalService", "Local Service")]
    [InlineData(@"NT Authority\LocalService", "Local Service")]
    [InlineData(@"NT AUTHORITY\NetworkService", "Network Service")]
    [InlineData(@"NT Authority\NetworkService", "Network Service")]
    public void A_system_account_is_shown_by_its_name_whatever_its_case(string held, string shown)
    {
        var row = EntryRow.Of(Rows.Entry("Spooler") with { Account = Reading<string>.Present(held) });

        Assert.Equal(shown, row["account"]);
        Assert.Equal(held, Columns.Of("account")!.Holds!(row.Entry));
    }

    /// <summary>
    /// An account outside the three is shown exactly as the manager holds it, and holds nothing
    /// behind that - a virtual account, a local one, a domain one, and an empty string.
    ///
    /// <b>The forms with a space are deliberately among these.</b> "NT AUTHORITY\Local Service" is
    /// what the account is CALLED, and whether the manager accepts it as a start name was not
    /// checked - the table's own comment says so. Until it is, that spelling is shown as read,
    /// which costs the person the label and nothing else. A test that mapped it would be a test
    /// pinning a guess.
    /// </summary>
    [Theory]
    [InlineData(@"NT SERVICE\McmSvc")]
    [InlineData(@".\svc-backup")]
    [InlineData(@"CONTOSO\svc-sql")]
    [InlineData(@"NT AUTHORITY\Local Service")]
    [InlineData(@"NT AUTHORITY\SYSTEM")]
    [InlineData("")]
    public void An_account_outside_the_three_is_shown_as_held_and_holds_nothing_behind(string held)
    {
        var row = EntryRow.Of(Rows.Entry("Spooler") with { Account = Reading<string>.Present(held) });

        Assert.Equal(held, row["account"]);
        Assert.Null(Columns.Of("account")!.Holds!(row.Entry));
    }

    /// <summary>
    /// The three states of a reading that carry no value say what every other cell says in them,
    /// and hold nothing behind that.
    ///
    /// Rule 8 arriving in one more cell: a refused account must read as refused, never as an
    /// account, and never as the name of one.
    /// </summary>
    [Fact]
    public void A_reading_with_no_value_says_so_and_holds_nothing()
    {
        var denied = EntryRow.Of(Rows.Entry("Spooler") with { Account = Reading<string>.Denied(5, "Access is denied.") });
        var absent = EntryRow.Of(Rows.Entry("Spooler") with { Account = Reading<string>.Absent() });
        var unread = EntryRow.Of(Rows.Entry("Spooler") with { Account = Reading<string>.NotRead() });

        Assert.Equal(Texts.Of("gui.cell.noAccess"), denied["account"]);
        Assert.Equal(string.Empty, absent["account"]);
        Assert.Equal(Texts.Of("gui.cell.unknown"), unread["account"]);

        var holds = Columns.Of("account")!.Holds!;

        Assert.Null(holds(denied.Entry));
        Assert.Null(holds(absent.Entry));
        Assert.Null(holds(unread.Entry));
    }

    /// <summary>
    /// The export writes what the list shows - the name, not the spelling.
    ///
    /// <b>A decision rather than a consequence, taken 2026-09-15 with the owner's "whatever
    /// confuses the person least".</b> The file is opened beside the window, by the person who
    /// exported it, and the export's own tooltip promises what the list shows. The spelling is
    /// one hover away in the window and in every line of the command line's JSON, which is what a
    /// script reads.
    /// </summary>
    [Fact]
    public void The_export_writes_the_name_the_list_shows()
    {
        var row = EntryRow.Of(Rows.Entry("Spooler") with
        {
            Account = Reading<string>.Present(@"NT AUTHORITY\NetworkService")
        });

        var text = Exporting.AsCsv(["serviceName", "account"], [row]);

        Assert.Contains("Spooler,Network Service", text, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkService", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A spelling changing case under a row is not the row moving.
    ///
    /// The row decides "moved" against what it SHOWS, and it shows the name - so an installer
    /// rewriting <c>LocalSystem</c> as <c>localSystem</c> changes nothing a person can see and
    /// must not light the row up as changed. Before the name, it did.
    /// </summary>
    [Fact]
    public void The_same_account_in_another_case_does_not_count_as_the_row_moving()
    {
        var row = EntryRow.Of(Rows.Entry("Spooler") with { Account = Reading<string>.Present("LocalSystem") });

        var moved = row.Absorb(
            Rows.Entry("Spooler") with { Account = Reading<string>.Present("localSystem") },
            DateTimeOffset.UtcNow);

        Assert.False(moved);
        Assert.Equal("Local System", row["account"]);
    }
}
