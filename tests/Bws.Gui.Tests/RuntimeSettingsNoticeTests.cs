using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the line under the list says when the window runs with administrator rights and the
/// environment carries .NET settings that can load code into it or make it write files - security
/// report S-2, named rather than refused by the owner's decision of 2026-10-06.
///
/// The settings are handed to <see cref="Says"/> rather than set in the environment, because every
/// test in this assembly shares one process and its environment.
/// </summary>
public sealed class RuntimeSettingsNoticeTests
{
    [Fact]
    public async Task An_elevated_window_names_the_settings_it_carries()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock())
        {
            Says = new Says { Elevated = true, RuntimeSettings = ["CORECLR_ENABLE_PROFILING", "DOTNET_BUNDLE_EXTRACT_BASE_DIR"] }
        };

        await model.LoadAsync();

        Assert.Equal(
            Texts.Of("gui.status.runtimeSettings", "CORECLR_ENABLE_PROFILING, DOTNET_BUNDLE_EXTRACT_BASE_DIR"),
            model.Says.Notice);
    }

    /// <summary>
    /// <b>Without rights the sentence about a short list keeps the place</b>, and the settings are
    /// not named - they reach no further than the account that set them already reaches. The even
    /// claim to the one above, and without it that one passes on a window that names them always.
    /// </summary>
    [Fact]
    public async Task A_window_without_rights_says_the_list_is_short_and_nothing_about_settings()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock())
        {
            Says = new Says { Elevated = false, RuntimeSettings = ["CORECLR_ENABLE_PROFILING"] }
        };

        await model.LoadAsync();

        Assert.Equal(Texts.Of("gui.status.notElevated"), model.Says.Notice);
    }

    [Fact]
    public async Task An_elevated_window_with_no_such_settings_says_nothing()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock())
        {
            Says = new Says { Elevated = true, RuntimeSettings = [] }
        };

        await model.LoadAsync();

        Assert.Equal(string.Empty, model.Says.Notice);
    }
}
