using System.Globalization;
using System.IO;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the settings page shows and what pressing on it changes - `docs/PROJEKT-USTAWIENIA-20261117.md`,
/// owner's decisions D1-D4 and E1-E4 of 2026-11-17.
///
/// <b>It decides what the page SAYS and hands every change out.</b> Writing goes through the two actions it
/// is given (KeptColumns, by way of SettingsDoor), and so does nothing else - a test builds one of these with
/// lists and lambdas and no window, file or process.
///
/// <b>Every change takes effect when it is made</b>, so there is no Save and no Cancel - the reason the page
/// is a page and not a sheet (decision E1, `docs/11` 3.12). The language is the one change that waits for
/// the next start, and the page says so in the words of decision D2.
/// </summary>
public sealed class SettingsPage : Observable
{
    private readonly Func<IReadOnlyList<string>> _recent;
    private readonly Func<bool> _busy;
    private readonly Action<string?> _choose;
    private readonly Action<bool> _remember;

    /// <summary>
    /// The language the window is showing now, as a code. A choice that opens the same language needs no
    /// restart - the first picture of 2026-11-17 asked for one after "English" was chosen on a window
    /// already in English, because "same as Windows" and "English" are two settings for one window.
    /// </summary>
    private readonly string _showing;

    private bool _rememberRecent;

    /// <param name="facts">What the session and the file say, read once when the window opens.</param>
    /// <param name="hands">What the page asks while it is up, and where its changes go.</param>
    internal SettingsPage(SettingsFacts facts, SettingsHands hands)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(hands);

        _recent = hands.Recent;
        _busy = hands.Busy;
        _choose = hands.Choose;
        _remember = hands.Remember;
        _showing = facts.Showing;
        _rememberRecent = facts.RememberRecent;
        FileWhere = facts.File;

        var (found, windowsAsks, kept) = (facts.Found, facts.WindowsAsks, facts.Kept);

        bool Found(string? code) => code is not null && found.Contains(code, StringComparer.OrdinalIgnoreCase);

        var windowsUses = Found(windowsAsks) ? windowsAsks : Texts.Fallback;

        // A KEPT CODE THIS SESSION CANNOT OPEN IN MARKS NO LINE. Marking "same as Windows" would say the
        // person chose it, and they did not - the sentence under the list says what happened instead.
        Languages =
        [
            new LanguageOption(null, windowsUses, Texts.Of("gui.settings.language.windows", NativeName(windowsUses)), NativeName(windowsUses), kept is null, Chose),
            .. found
                .Select(code => new LanguageOption(code, code, NativeName(code), NativeName(code), string.Equals(code, kept, StringComparison.OrdinalIgnoreCase), Chose))
                .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
        ];

        WindowsMissing = Found(windowsAsks) ? null : Texts.Of("gui.settings.language.windowsMissing", NativeName(windowsAsks));
        ChoiceMissing = kept is not null && !Found(kept) ? Texts.Of("gui.settings.language.missing", NativeName(kept)) : null;
    }

    /// <summary>"Same as Windows" first, then every language found, by its own name.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; }

    /// <summary>The sentence about a Windows language this window has no translation for, or nothing.</summary>
    public string? WindowsMissing { get; }

    /// <summary>Whether that sentence is said.</summary>
    public bool SaysWindowsMissing => WindowsMissing is not null;

    /// <summary>The sentence about a kept language this session cannot open in, or nothing - rule 8.</summary>
    public string? ChoiceMissing { get; private set; }

    /// <summary>Whether that sentence is said.</summary>
    public bool SaysChoiceMissing => ChoiceMissing is not null;

    /// <summary>The sentence about the language the next start opens in, or nothing while it is the same.</summary>
    public string? Pending { get; private set; }

    /// <summary>Whether that sentence and the restart are on the page.</summary>
    public bool SaysPending => Pending is not null;

    /// <summary>Whether the restart can be pressed - not while a plan is being carried out.</summary>
    public bool CanRestart => !_busy();

    /// <summary>What the restart button says about itself, including why it cannot be pressed.</summary>
    public string RestartHint => CanRestart ? Texts.Of("gui.settings.language.restart.hint") : Texts.Of("gui.settings.language.restart.busy");

    /// <summary>Whether recent searches are remembered. Off forgets the ones kept so far - decision E4.</summary>
    public bool RememberRecent
    {
        get => _rememberRecent;
        set
        {
            if (Set(ref _rememberRecent, value))
            {
                _remember(value);
                RethinkRecent();
            }
        }
    }

    /// <summary>Whether there is anything to forget.</summary>
    public bool CanForget => _rememberRecent && _recent().Count > 0;

    /// <summary>What the forget button says about itself, including why it cannot be pressed.</summary>
    public string ForgetHint => CanForget ? Texts.Of("gui.settings.search.forget.hint") : Texts.Of("gui.settings.search.none");

    /// <summary>The full path of the preferences file, or nothing when Windows names no folder for it.</summary>
    public string? FileWhere { get; }

    /// <summary>Whether the path is shown - an account without a profile has a sentence instead.</summary>
    public bool HasAPlace => FileWhere is not null;

    /// <summary>Whether that account's sentence is said.</summary>
    public bool SaysNoPlace => FileWhere is null;

    /// <summary>Whether the file has been written yet.</summary>
    public bool SaysFileNotYet => FileWhere is not null && !File.Exists(FileWhere);

    /// <summary>Whether the folder can be opened - it has to be there first.</summary>
    public bool CanOpenFolder => FileWhere is not null && Directory.Exists(Path.GetDirectoryName(FileWhere));

    /// <summary>What the folder button says about itself, including why it cannot be pressed.</summary>
    public string OpenFolderHint => CanOpenFolder ? Texts.Of("gui.settings.file.open.hint") : Texts.Of("gui.settings.file.open.missing");

    /// <summary>Asks the page again what it says - when it comes on screen, and after a change it did not make.</summary>
    internal void Rethink()
    {
        RethinkRecent();
        Raise(nameof(CanRestart));
        Raise(nameof(RestartHint));
        Raise(nameof(SaysFileNotYet));
        Raise(nameof(CanOpenFolder));
        Raise(nameof(OpenFolderHint));
    }

    private void RethinkRecent()
    {
        Raise(nameof(CanForget));
        Raise(nameof(ForgetHint));
    }

    private void Chose(LanguageOption option)
    {
        _choose(option.Code);

        // A choice made here answers the sentence about a kept one that could not be used.
        ChoiceMissing = null;
        Raise(nameof(ChoiceMissing));
        Raise(nameof(SaysChoiceMissing));

        var same = string.Equals(option.Opens, _showing, StringComparison.OrdinalIgnoreCase);

        Pending = same ? null : Texts.Of("gui.settings.language.pending", option.Name);
        Raise(nameof(Pending));
        Raise(nameof(SaysPending));
        Raise(nameof(CanRestart));
        Raise(nameof(RestartHint));
    }

    /// <summary>
    /// A language's name in that language, with its first letter raised the way that language raises it -
    /// .NET gives "polski" in lower case (measured 2026-11-17), and a list of names reads as a list of names
    /// only when they all start the same way. A code .NET does not know is shown as the code.
    ///
    /// <b>Looked up among the neutral cultures rather than asked for by name</b>, because asking for one .NET
    /// does not know throws, and the code may come from a file beside the program. Nothing to catch.
    /// </summary>
    internal static string NativeName(string code)
    {
        var culture = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .FirstOrDefault(known => string.Equals(known.Name, code, StringComparison.OrdinalIgnoreCase));

        return culture is { NativeName.Length: > 0 } ? char.ToUpper(culture.NativeName[0], culture) + culture.NativeName[1..] : code;
    }
}

/// <summary>
/// What the settings page is told about this session and the preferences file, read once when the window
/// opens. Found: the codes this session could open in (Languages.Found). WindowsAsks: the code Windows asks
/// for. Showing: the code the window shows (Texts.Showing). Kept: the code the file keeps, null for "same as
/// Windows", possibly one this session cannot open in. File: where the file is, null without a profile.
/// </summary>
internal sealed record SettingsFacts(
    IReadOnlyList<string> Found, string WindowsAsks, string Showing, string? Kept, bool RememberRecent, string? File);

/// <summary>
/// What the settings page asks while it is up - the searches kept now, whether a plan is being carried out -
/// and where its two changes go: a language (a code, or null for "same as Windows") and the switch.
/// </summary>
internal sealed record SettingsHands(
    Func<IReadOnlyList<string>> Recent, Func<bool> Busy, Action<string?> Choose, Action<bool> Remember);

/// <summary>
/// One line of the language list: a code (null for "same as Windows"), the code it opens the window in,
/// what the line says, the name of that language, and its mark.
/// </summary>
public sealed class LanguageOption : Observable
{
    private readonly Action<LanguageOption> _chosen;
    private bool _isOn;

    internal LanguageOption(string? code, string opens, string label, string name, bool isOn, Action<LanguageOption> chosen)
    {
        Code = code;
        Opens = opens;
        Label = label;
        Name = name;
        _isOn = isOn;
        _chosen = chosen;
    }

    /// <summary>The code kept in the file, or null for "same as Windows".</summary>
    public string? Code { get; }

    /// <summary>The code of the language the window opens in on this line - for "same as Windows", the one it resolves to.</summary>
    public string Opens { get; }

    public string Label { get; }

    /// <summary>The language this line opens the window in, by its own name.</summary>
    public string Name { get; }

    /// <summary>
    /// The radio button's mark. Only the arriving true is acted on, for the reason ScopeChoice.IsOn gives:
    /// the group tells the line being left that it is off before it tells the new one it is on.
    /// </summary>
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (Set(ref _isOn, value) && value)
            {
                _chosen(this);
            }
        }
    }
}
