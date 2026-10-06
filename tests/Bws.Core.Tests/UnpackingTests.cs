namespace Bws.Core.Tests;

/// <summary>
/// What an elevated window does about the folder its native libraries were unpacked into - security
/// report S-1, 2026-10-06. The rule takes its two questions about the disk as functions, so every
/// branch is asked here without a folder anywhere.
/// </summary>
public sealed class UnpackingTests
{
    private const string Home = @"C:\ProgramData\BetterWindowsServices";
    private const string Program = @"D:\Tools\BetterWindowsServices\";
    private const string Temporary = @"C:\Windows\Temp\.net\BetterWindowsServices\abc\";
    private const string Inside = Home + @"\BetterWindowsServices\abc\";

    private static readonly Func<string, FolderCheck> Ready = FolderCheck.Passed;
    private static readonly Func<string, string, FolderCheck> Sound = (_, folder) => FolderCheck.Passed(folder);

    private static UnpackingFacts Facts(string? search, string? variable = null) =>
        new(Elevated: true, Bundled: true, search, Program, Home, variable);

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Without_rights_or_outside_a_bundle_nothing_is_asked_of_the_disk(bool elevated, bool bundled)
    {
        var asked = 0;

        var decision = Unpacking.Decide(
            Facts(Temporary) with { Elevated = elevated, Bundled = bundled },
            home => { asked++; return FolderCheck.Passed(home); },
            (_, folder) => { asked++; return FolderCheck.Passed(folder); });

        Assert.Equal(UnpackingVerdict.Stay, decision.Verdict);
        Assert.Equal(0, asked);
    }

    [Fact]
    public void Libraries_under_the_temporary_folder_send_an_elevated_window_to_the_administrators_folder()
    {
        string? prepared = null;

        var decision = Unpacking.Decide(Facts(Temporary), home => { prepared = home; return FolderCheck.Passed(home); }, Sound);

        Assert.Equal(UnpackingVerdict.Elsewhere, decision.Verdict);
        Assert.Equal(Home, prepared);
        Assert.Equal(Home, decision.Check!.Path);
    }

    [Fact]
    public void An_administrators_folder_that_cannot_be_made_ready_refuses_instead_of_moving()
    {
        var decision = Unpacking.Decide(
            Facts(Temporary),
            home => FolderCheck.Failed(home, UnpackingFault.OthersCanChange),
            Sound);

        Assert.Equal(UnpackingVerdict.Refused, decision.Verdict);
        Assert.Equal(UnpackingFault.OthersCanChange, decision.Check!.Fault);
    }

    [Fact]
    public void Libraries_still_outside_after_the_variable_was_set_refuse_rather_than_start_again()
    {
        // The loop guard: the host was told where and did not go there, so a third start would only
        // be a third start.
        var decision = Unpacking.Decide(Facts(Temporary, variable: Home + @"\"), _ => throw new InvalidOperationException("prepared"), Sound);

        Assert.Equal(UnpackingVerdict.Refused, decision.Verdict);
        Assert.Equal(UnpackingFault.StillOutside, decision.Check!.Fault);
        Assert.Equal(Temporary.TrimEnd('\\'), decision.Check.Path);
    }

    [Fact]
    public void Libraries_inside_are_examined_and_stay_when_the_folder_is_sound()
    {
        var examined = new List<(string Home, string Folder)>();

        var decision = Unpacking.Decide(
            Facts(Inside, variable: Home),
            _ => throw new InvalidOperationException("prepared"),
            (home, folder) => { examined.Add((home, folder)); return FolderCheck.Passed(folder); });

        Assert.Equal(UnpackingVerdict.Stay, decision.Verdict);
        Assert.Equal([(Home, Inside.TrimEnd('\\'))], examined);
    }

    [Fact]
    public void A_folder_inside_that_fails_its_examination_refuses_with_that_folder()
    {
        var decision = Unpacking.Decide(
            Facts(Inside),
            Ready,
            (_, folder) => FolderCheck.Failed(folder + @"\wpfgfx_cor3.dll", UnpackingFault.OthersCanChange));

        Assert.Equal(UnpackingVerdict.Refused, decision.Verdict);
        Assert.EndsWith("wpfgfx_cor3.dll", decision.Check!.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bundle_whose_runtime_names_no_folder_refuses()
    {
        var decision = Unpacking.Decide(Facts(search: null), Ready, Sound);

        Assert.Equal(UnpackingVerdict.Refused, decision.Verdict);
        Assert.Equal(UnpackingFault.Unnamed, decision.Check!.Fault);
    }

    [Fact]
    public void Libraries_beside_the_program_are_trusted_as_the_program_is()
    {
        var decision = Unpacking.Decide(Facts(Program + ";"), _ => throw new InvalidOperationException("prepared"), Sound);

        Assert.Equal(UnpackingVerdict.Stay, decision.Verdict);
    }

    [Fact]
    public void One_folder_outside_among_several_inside_is_enough_to_move()
    {
        var decision = Unpacking.Decide(Facts(Inside + ";" + Temporary), Ready, Sound);

        Assert.Equal(UnpackingVerdict.Elsewhere, decision.Verdict);
    }

    [Theory]
    [InlineData(Home, true)]
    [InlineData(Home + @"\", true)]
    [InlineData(@"c:\programdata\betterwindowsservices\x\y", true)]
    [InlineData(Home + @"\..\Elsewhere", false)]
    [InlineData(Home + "Evil", false)]
    [InlineData(@"C:\ProgramData", false)]
    [InlineData("ProgramData\\BetterWindowsServices", false)]
    public void Inside_means_the_folder_or_under_it_judged_by_where_the_path_leads(string folder, bool inside)
    {
        Assert.Equal(inside, Unpacking.Within(folder, Home));
    }

    [Fact]
    public void Folders_leave_out_the_program_and_empty_entries_and_repeat_nothing()
    {
        var folders = Unpacking.Folders($" {Program} ;;{Inside};{Inside.ToUpperInvariant()}", Program);

        Assert.Equal([Inside.TrimEnd('\\')], folders);
    }

    [Theory]
    [InlineData(Home, true)]
    [InlineData(Home + @"\", true)]
    [InlineData(@"C:\Elsewhere", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void The_variable_is_ours_only_when_it_names_the_administrators_folder(string? variable, bool ours)
    {
        Assert.Equal(ours, Unpacking.Same(variable, Home));
    }

    [Fact]
    public void A_home_that_is_not_a_whole_path_refuses_rather_than_reaching_somewhere_relative()
    {
        var facts = Facts(Temporary) with { Home = string.Empty };

        var decision = Unpacking.Decide(facts, _ => throw new InvalidOperationException("prepared"), Sound);

        Assert.Equal(UnpackingVerdict.Refused, decision.Verdict);
    }
}
