namespace Bws.Gui.Tests;

/// <summary>
/// The pin in Bws.Gui.Tests.csproj reached this process, and Texts obeyed it - `docs/PROJEKT-USTAWIENIA-20261117.md`,
/// collision 1. Without this, a pin that stopped arriving would be found only by a red English sentence on a
/// machine whose Windows or settings say another language - never in CI.
/// </summary>
public sealed class LanguagePinGuards
{
    [Fact]
    public void The_test_host_pins_the_window_to_english()
    {
        Assert.Equal("en", AppContext.GetData(Languages.Pinned) as string);
        Assert.Equal("en", Texts.Showing);
        Assert.Null(Texts.Chosen);
        Assert.Null(Texts.ChosenButMissing);
    }
}
