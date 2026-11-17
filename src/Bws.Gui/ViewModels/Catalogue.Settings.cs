namespace Bws.Gui.ViewModels;

public static partial class Catalogue
{
    /// <summary>
    /// The settings page over models of its own, for the sheet (GUI rule 4) - `docs/PROJEKT-USTAWIENIA-20261117.md`.
    /// Nothing here comes from the machine or the person: the languages, the searches and the paths are
    /// specimens, and every change the page hands out goes nowhere.
    ///
    /// <b>Four cells and no "loading"</b> - the page reads nothing that takes time. With data: a Windows
    /// language that has a translation, searches kept, the file and its folder there. Empty: an account
    /// without a profile and nothing kept. Wrong: a kept language this window cannot open in, and a Windows
    /// language without a translation, both sentences up, and a file not written yet. Extreme: ten languages, a
    /// plan being carried out (the restart greyed with its reason), a choice waiting for the next start, a
    /// path longer than the card, and a folder that is not there (Open folder greyed with its reason).
    ///
    /// <b>Whether the file is there is a specimen too</b>, never asked of the disk - the extreme path names a
    /// server, and asking about it would reach for the network from a sheet of components.
    ///
    /// <b>One factory and no methods</b>, for the reason Guiding gives - this type stands near the crowd line
    /// of methods per type in CodeShapeGuards.
    /// </summary>
    private static readonly Func<string, SettingsPage> SettingsIn = static state =>
    {
        const string File = @"D:\Profiles\Specimen\AppData\Roaming\BetterWindowsServices\bws-preferences.json";

        var facts = new SettingsFacts(["en", "pl"], "pl", "pl", null, RememberRecent: true, File);
        var hands = new SettingsHands(() => ["status:running", "name:sql*"], () => false, static _ => { }, static _ => { }, static () => (true, true));

        var page = state switch
        {
            "empty" => new SettingsPage(facts with { Found = ["en"], WindowsAsks = "en", Showing = "en", File = null }, hands with { Recent = () => [] }),
            "wrong" => new SettingsPage(facts with { Found = ["en"], WindowsAsks = "fr", Showing = "en", Kept = "de" }, hands with { OnDisk = static () => (false, true) }),
            "extreme" => new SettingsPage(
                facts with
                {
                    Found = ["cs", "da", "de", "en", "es", "fi", "fr", "it", "pl", "pt"],
                    RememberRecent = false,
                    File = @"\\fileserver.example.internal\profiles$\a.very.long.account.name.for.a.specimen\AppData\Roaming\BetterWindowsServices\bws-preferences.json"
                },
                hands with { Busy = () => true, OnDisk = static () => (false, false) }),
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
