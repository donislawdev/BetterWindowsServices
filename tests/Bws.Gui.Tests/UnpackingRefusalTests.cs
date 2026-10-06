using Bws.Core;

namespace Bws.Gui.Tests;

/// <summary>
/// What an elevated window says when it will not start because of the folder its native libraries
/// came from, and what it knows about itself before WPF exists - security report S-1, 2026-10-06.
/// The box itself is user32's and waits for a person, so it is never shown here: what is held is the
/// sentence handed to it.
/// </summary>
public sealed class UnpackingRefusalTests
{
    private const string Folder = @"C:\ProgramData\BetterWindowsServices\BetterWindowsServices\abc";

    [Theory]
    [InlineData(UnpackingFault.NotMade)]
    [InlineData(UnpackingFault.OthersCanChange)]
    [InlineData(UnpackingFault.Link)]
    [InlineData(UnpackingFault.Unreadable)]
    [InlineData(UnpackingFault.StillOutside)]
    [InlineData(UnpackingFault.NotStarted)]
    public void Every_refusal_names_its_folder_and_says_the_program_did_not_start(UnpackingFault fault)
    {
        var sentence = Program.Refusal(FolderCheck.Failed(Folder, fault));

        Assert.StartsWith(Texts.Of("gui.unpacking.lead"), sentence, StringComparison.Ordinal);
        Assert.Contains(Folder, sentence, StringComparison.Ordinal);
        Assert.EndsWith(Texts.Of("gui.unpacking.withoutRights"), sentence, StringComparison.Ordinal);
        Assert.DoesNotContain("gui.unpacking", sentence, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UnpackingFault.OthersCanChange, true)]
    [InlineData(UnpackingFault.Link, true)]
    [InlineData(UnpackingFault.Unreadable, true)]
    [InlineData(UnpackingFault.NotMade, false)]
    [InlineData(UnpackingFault.StillOutside, false)]
    public void Deleting_the_folder_is_advised_only_where_deleting_it_helps(UnpackingFault fault, bool advised)
    {
        var sentence = Program.Refusal(FolderCheck.Failed(Folder, fault));

        Assert.Equal(advised, sentence.Contains(Texts.Of("gui.unpacking.deleteIt", AdministratorsFolder.Home), StringComparison.Ordinal));
    }

    [Fact]
    public void What_the_system_said_is_carried_and_a_folder_nobody_named_still_gets_a_reason()
    {
        var carried = Program.Refusal(FolderCheck.Failed(Folder, UnpackingFault.Unreadable, "Access is denied."));
        var unnamed = Program.Refusal(FolderCheck.Failed(string.Empty, UnpackingFault.Unnamed));

        Assert.Contains("Access is denied.", carried, StringComparison.Ordinal);
        Assert.Contains(Texts.Of("gui.unpacking.unnamed"), unnamed, StringComparison.Ordinal);
    }

    [Fact]
    public void A_test_host_is_not_a_bundle_so_the_window_stays_where_it_is()
    {
        var facts = Program.Facts();

        Assert.False(facts.Bundled);
        Assert.Equal(AdministratorsFolder.Home, facts.Home);
        Assert.Equal(
            UnpackingVerdict.Stay,
            Unpacking.Decide(facts, _ => throw new InvalidOperationException("prepared"), (_, _) => throw new InvalidOperationException("examined")).Verdict);
    }
}
