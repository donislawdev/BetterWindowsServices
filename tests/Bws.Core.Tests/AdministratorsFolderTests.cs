using System.Security.AccessControl;
using System.Security.Principal;

namespace Bws.Core.Tests;

/// <summary>
/// The folder only administrators can change - security report S-1, 2026-10-06. The rule is asked of
/// descriptors written in SDDL, so it needs no folder and no rights. The folder itself is made and
/// examined under the temporary directory, and what that can show depends on the token running the
/// tests: elevated, the folder is made and passes. Without elevation the owner it is made with cannot
/// be given (measured that day under a restricted token: IOException 0x8007051B, nothing left behind),
/// so those tests assert that refusal instead of going red.
/// </summary>
public sealed class AdministratorsFolderTests : IDisposable
{
    private const string Admins = "O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)";
    private const string SomebodyElse = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    private readonly string _parent = Path.Combine(Path.GetTempPath(), "bws-admins-" + Guid.NewGuid().ToString("N")[..8]);

    public AdministratorsFolderTests() => Directory.CreateDirectory(_parent);

    public void Dispose() => Directory.Delete(_parent, recursive: true);

    private static bool Judged(string sddl) => AdministratorsFolder.OnlyAdministratorsCanChange(new RawSecurityDescriptor(sddl));

    [Theory]
    [InlineData(Admins)]
    [InlineData(Admins + "(A;OICI;0x1200a9;;;BU)")]
    [InlineData(Admins + "(D;;FA;;;BU)")]
    [InlineData("O:SYG:SYD:P(A;OICI;FA;;;SY)")]
    [InlineData("O:S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464D:P(A;;FA;;;S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464)")]
    [InlineData(Admins + "(A;;FA;;;OW)")]
    public void Only_the_three_may_change_and_reading_or_denying_is_harmless(string sddl)
    {
        Assert.True(Judged(sddl));
    }

    [Theory]
    [InlineData("0x2")]
    [InlineData("0x4")]
    [InlineData("0x10")]
    [InlineData("0x40")]
    [InlineData("0x100")]
    [InlineData("0x10000")]
    [InlineData("0x40000")]
    [InlineData("0x80000")]
    [InlineData("GA")]
    [InlineData("GW")]
    [InlineData("0x2000000")]
    public void Every_right_that_changes_something_given_to_others_fails(string right)
    {
        Assert.True(Judged(Admins + "(A;;0x1200a9;;;BU)"));
        Assert.False(Judged(Admins + $"(A;;{right};;;BU)"));
    }

    [Theory]
    [InlineData("(A;OICI;FA;;;WD)")]
    [InlineData("(A;OICIIO;FA;;;CO)")]
    [InlineData("(A;OICI;0x2;;;" + SomebodyElse + ")")]
    // An object entry, read-only, that the rule cannot judge. Written with an object identifier on
    // purpose: without one, SDDL turns OA into a plain allow entry - measured 2026-10-06, and the first
    // version of this case was red for exactly that reason before any mutation touched the rule.
    [InlineData("(OA;;0x1200a9;bf967aba-0de6-11d0-a285-00aa003049e2;;BU)")]
    public void Write_for_everybody_for_a_future_creator_for_one_account_or_an_entry_of_another_kind_fails(string entry)
    {
        Assert.False(Judged(Admins + entry));
    }

    [Theory]
    [InlineData("D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)", false)]
    [InlineData("D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;0x1200a9;;;OW)", true)]
    [InlineData("D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICIIO;0x1200a9;;;OW)", false)]
    [InlineData("D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;FA;;;OW)", false)]
    public void An_owner_outside_the_three_passes_only_when_owner_rights_hold_it_back(string list, bool passes)
    {
        // Without an OWNER RIGHTS entry that applies to the object itself, the owner may rewrite the
        // list - and the same account without elevation is that owner.
        Assert.Equal(passes, Judged("O:" + SomebodyElse + list));
    }

    [Fact]
    public void No_list_at_all_means_everybody_may_do_everything()
    {
        Assert.False(Judged("O:BAD:NO_ACCESS_CONTROL"));
    }

    [Fact]
    public void The_list_the_folder_is_made_with_passes_its_own_rule()
    {
        var closed = AdministratorsFolder.Closed();
        var sddl = closed.GetSecurityDescriptorSddlForm(AccessControlSections.Owner | AccessControlSections.Access);

        Assert.True(Judged(sddl));
        Assert.True(closed.AreAccessRulesProtected);
    }

    [Theory]
    [InlineData(@"C:\WINDOWS", @"C:\ProgramData\BetterWindowsServices")]
    [InlineData(@"D:\Win", @"D:\ProgramData\BetterWindowsServices")]
    [InlineData("", "")]
    public void Home_is_on_the_drive_of_windows(string windows, string home)
    {
        Assert.Equal(home, AdministratorsFolder.HomeOn(windows));
        Assert.Equal(AdministratorsFolder.HomeOn(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), AdministratorsFolder.Home);
    }

    [Fact]
    public void Levels_run_from_the_administrators_folder_down_to_the_libraries()
    {
        var levels = AdministratorsFolder.Levels(@"C:\Home\", @"C:\Home\App\abc");

        Assert.Equal([@"C:\Home", @"C:\Home\App", @"C:\Home\App\abc"], levels);
    }

    [Fact]
    public void A_folder_whose_parent_is_missing_is_not_made_and_its_parent_is_not_made_either()
    {
        var missing = Path.Combine(_parent, "absent", "BetterWindowsServices");

        var check = AdministratorsFolder.Prepare(missing);

        Assert.Equal(UnpackingFault.NotMade, check.Fault);
        Assert.False(Directory.Exists(Path.Combine(_parent, "absent")));
    }

    [Fact]
    public void The_folder_is_made_closed_and_passes_or_without_elevation_is_refused_and_not_made()
    {
        var home = Path.Combine(_parent, "BetterWindowsServices");

        var check = AdministratorsFolder.Prepare(home);

        if (!Session.IsElevated())
        {
            Assert.Equal(UnpackingFault.NotMade, check.Fault);
            Assert.NotNull(check.Cause);
            Assert.False(Directory.Exists(home));
            return;
        }

        Assert.Null(check.Fault);
        var made = new DirectoryInfo(home).GetAccessControl();
        Assert.Equal(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), made.GetOwner(typeof(SecurityIdentifier)));
        Assert.True(made.AreAccessRulesProtected);
    }

    [Fact]
    public void A_folder_somebody_made_first_is_judged_as_it_is_and_not_given_the_list()
    {
        // Made with whatever the temporary folder hands down - which gives somebody other than the
        // three a right to change it on every machine this has been run on, elevated or not.
        var home = Directory.CreateDirectory(Path.Combine(_parent, "BetterWindowsServices")).FullName;

        var check = AdministratorsFolder.Prepare(home);

        Assert.Equal(UnpackingFault.OthersCanChange, check.Fault);
        Assert.False(new DirectoryInfo(home).GetAccessControl().AreAccessRulesProtected);
    }

    [Fact]
    public void Everything_under_the_libraries_is_examined_and_one_file_others_can_change_fails()
    {
        var home = Path.Combine(_parent, "BetterWindowsServices");

        if (AdministratorsFolder.Prepare(home).Fault is not null)
        {
            Assert.False(Session.IsElevated());
            return;
        }

        var libraries = Directory.CreateDirectory(Path.Combine(home, "App", "abc")).FullName;
        var inner = Directory.CreateDirectory(Path.Combine(libraries, "inner")).FullName;
        var file = Path.Combine(inner, "wpfgfx_cor3.dll");
        File.WriteAllBytes(file, [1, 2, 3]);

        Assert.Null(AdministratorsFolder.Examine(home, libraries).Fault);

        var opened = new FileInfo(file).GetAccessControl();
        opened.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.Write, AccessControlType.Allow));
        new FileInfo(file).SetAccessControl(opened);

        var check = AdministratorsFolder.Examine(home, libraries);

        Assert.Equal(UnpackingFault.OthersCanChange, check.Fault);
        Assert.Equal(file, check.Path);
    }

    [Fact]
    public void A_level_between_the_administrators_folder_and_the_libraries_that_others_can_change_fails()
    {
        var home = Path.Combine(_parent, "BetterWindowsServices");

        if (AdministratorsFolder.Prepare(home).Fault is not null)
        {
            Assert.False(Session.IsElevated());
            return;
        }

        var between = Directory.CreateDirectory(Path.Combine(home, "App")).FullName;
        var libraries = Directory.CreateDirectory(Path.Combine(between, "abc")).FullName;

        var opened = new DirectoryInfo(between).GetAccessControl();
        opened.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.CreateFiles, AccessControlType.Allow));
        new DirectoryInfo(between).SetAccessControl(opened);

        var check = AdministratorsFolder.Examine(home, libraries);

        Assert.Equal(UnpackingFault.OthersCanChange, check.Fault);
        Assert.Equal(between, check.Path);
    }

    [Fact]
    public void A_link_is_refused_as_a_link_and_never_followed()
    {
        var target = Directory.CreateDirectory(Path.Combine(_parent, "target")).FullName;
        var link = Path.Combine(_parent, "link");

        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception refused) when (refused is IOException or UnauthorizedAccessException)
        {
            // A symbolic link needs elevation or developer mode - without either there is no specimen.
            Assert.False(Session.IsElevated());
            return;
        }

        Assert.Equal(UnpackingFault.Link, AdministratorsFolder.Inspect(link).Fault);
    }

    [Fact]
    public void A_path_that_is_not_there_cannot_be_read_and_says_so()
    {
        var check = AdministratorsFolder.Inspect(Path.Combine(_parent, "nothing"));

        Assert.Equal(UnpackingFault.Unreadable, check.Fault);
        Assert.NotNull(check.Cause);
    }
}
