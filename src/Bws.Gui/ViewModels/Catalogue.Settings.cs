namespace Bws.Gui.ViewModels;

public static partial class Catalogue
{
    /// <summary>
    /// The settings page over models of its own, for the sheet (GUI rule 4) - `docs/PROJEKT-USTAWIENIA-20261117.md`.
    /// Nothing here comes from the machine or the person: the languages, the searches and the paths are
    /// specimens, and every change the page hands out goes nowhere.
    ///
    /// <b>Four cells and no "loading"</b> - the page reads nothing that takes time. With data: a Windows
    /// language that has a translation, searches kept. Empty: an account without a profile and nothing kept.
    /// Wrong: a kept language this window cannot open in, and a Windows language without a translation, both
    /// sentences up. Extreme: ten languages, a plan being carried out (the restart greyed with its reason), a
    /// choice waiting for the next start, and a path longer than the card.
    ///
    /// <b>One factory and no methods</b>, for the reason Guiding gives - this type stands near the crowd line
    /// of methods per type in CodeShapeGuards.
    /// </summary>
    private static readonly Func<string, SettingsPage> SettingsIn = static state =>
    {
        const string File = @"D:\Profiles\Specimen\AppData\Roaming\BetterWindowsServices\bws-preferences.json";

        var facts = new SettingsFacts(["en", "pl"], "pl", "pl", null, RememberRecent: true, File);
        var hands = new SettingsHands(() => ["status:running", "name:sql*"], () => false, static _ => { }, static _ => { });

        var page = state switch
        {
            "empty" => new SettingsPage(facts with { Found = ["en"], WindowsAsks = "en", Showing = "en", File = null }, hands with { Recent = () => [] }),
            "wrong" => new SettingsPage(facts with { Found = ["en"], WindowsAsks = "fr", Showing = "en", Kept = "de" }, hands),
            "extreme" => new SettingsPage(
                facts with
                {
                    Found = ["cs", "da", "de", "en", "es", "fi", "fr", "it", "pl", "pt"],
                    RememberRecent = false,
                    File = @"\\fileserver.example.internal\profiles$\a.very.long.account.name.for.a.specimen\AppData\Roaming\BetterWindowsServices\bws-preferences.json"
                },
                hands with { Busy = () => true }),
            _ => new SettingsPage(facts, hands)
        };

        // The extreme cell after somebody chose the last language on its list - the sentence and the restart up.
        if (state == "extreme")
        {
            page.Languages[^1].IsOn = true;
        }

        return page;
    };
}
