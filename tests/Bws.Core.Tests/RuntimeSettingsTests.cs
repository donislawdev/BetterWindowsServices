namespace Bws.Core.Tests;

/// <summary>
/// Which .NET settings in an environment can load code into a process or make it write files -
/// security report S-2. Asked of an environment the test hands in, never of this process's own.
/// </summary>
public sealed class RuntimeSettingsTests
{
    [Fact]
    public void A_setting_that_is_on_is_named()
    {
        var set = Session.RuntimeSettingsFrom(Environment(("CORECLR_ENABLE_PROFILING", "1")), extractsNatives: false);

        Assert.Equal(["CORECLR_ENABLE_PROFILING"], set);
    }

    /// <summary>
    /// The even claim, and the shape of "set": present, not empty and not 0. Without it the test above
    /// passes on a list that names every variable it knows about.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("   ")]
    public void A_setting_that_is_off_or_empty_is_not_named(string value)
    {
        var set = Session.RuntimeSettingsFrom(Environment(("CORECLR_ENABLE_PROFILING", value)), extractsNatives: false);

        Assert.Empty(set);
    }

    /// <summary>
    /// Both spellings of one knob, because a runtime honours the older prefix as well, and a later
    /// one the newer prefix for the profiler.
    /// </summary>
    [Fact]
    public void Both_spellings_of_a_knob_are_named()
    {
        var set = Session.RuntimeSettingsFrom(
            Environment(("COMPlus_DbgEnableMiniDump", "1"), ("DOTNET_ENABLE_PROFILING", "1")),
            extractsNatives: false);

        Assert.Equal(["DOTNET_ENABLE_PROFILING", "COMPlus_DbgEnableMiniDump"], set);
    }

    /// <summary>
    /// Where natives are unpacked matters only to a program that unpacks them - the window, not the
    /// terminal - so the terminal never names it.
    /// </summary>
    [Fact]
    public void The_unpacking_folder_is_named_only_for_a_program_that_unpacks()
    {
        var environment = Environment(("DOTNET_BUNDLE_EXTRACT_BASE_DIR", @"D:\Elsewhere"));

        Assert.Equal(["DOTNET_BUNDLE_EXTRACT_BASE_DIR"], Session.RuntimeSettingsFrom(environment, extractsNatives: true));
        Assert.Empty(Session.RuntimeSettingsFrom(environment, extractsNatives: false));
    }

    /// <summary>An environment of the given pairs, compared without case as Windows compares names.</summary>
    private static Func<string, string?> Environment(params (string Name, string Value)[] pairs)
    {
        var values = pairs.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return name => values.TryGetValue(name, out var value) ? value : null;
    }
}
