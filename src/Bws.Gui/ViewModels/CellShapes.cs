namespace Bws.Gui.ViewModels;

/// <summary>
/// The vocabulary of shapes a cell can wear.
///
/// <b>A code, not a colour.</b> The view model may not name a brush: `Bws.Integration.Tests`
/// references this assembly deliberately WITHOUT UseWPF, so the fact that no view model knows
/// what WPF is gets proved by that project compiling at all. A brush here would end that, and
/// end it silently.
///
/// So the mapping from a code to a colour lives in Themes/List.xaml, in the mark styles that
/// trigger on these codes, and the colours they reach for are named in Themes/Values.xaml, where
/// `ADR-23` says every appearance value lives. These strings are the joint between the two. They are constants
/// rather than an enum because the other end of the joint is a XAML DataTrigger, which compares
/// against text.
/// </summary>
public static class CellShapes
{
    /// <summary>
    /// What makes a code a code.
    ///
    /// <b>The prefix is not decoration and it was put there by a test.</b> Without it the code
    /// for a status nobody could read was the string "unknown" and so was the word for it, and
    /// a guard asking whether any code equals any word had to be told to look the other way for
    /// that one case. A code that can never collide with a translated word proves the same
    /// thing by construction instead, and it says what it is at the other end of the joint -
    /// a DataTrigger reading Value="shape.running" is plainly not matching English.
    /// </summary>
    private const string Prefix = "shape.";

    public const string Running = Prefix + "running";
    public const string Stopped = Prefix + "stopped";

    /// <summary>On the way somewhere. Four of the manager's states, and none of them lasts.</summary>
    public const string Transit = Prefix + "transit";

    /// <summary>Not running and not stopped either. A state somebody put it in on purpose.</summary>
    public const string Paused = Prefix + "paused";

    /// <summary>Nobody was able to find out. Never the same as "there is none" - rule 8.</summary>
    public const string Unknown = Prefix + "unknown";

    /// <summary>Nothing about this state needs pointing at. The start column stopped producing it
    /// on 2026-08-17, when each start type got a mark of its own - the disagreement mark still
    /// does, for an entry doing exactly what its start type says.</summary>
    public const string Ordinary = Prefix + "ordinary";

    /// <summary>
    /// The four start types, each with a mark of its own - owner's decision, 2026-08-17. Until
    /// then all four shared <see cref="Ordinary"/>, which is to say they shared no mark at all.
    ///
    /// <b>PREFIXED, UNLIKE EVERY OTHER CODE HERE, AND FOR A REASON WORTH ONE LINE.</b> The obvious
    /// name for the second of them is <c>System</c>, and a constant called that inside this class
    /// shadows the namespace of the same name for everything written after it. The compiler would
    /// not complain here today and would complain somewhere else later, which is the worst shape a
    /// name can have. The <c>shape.start.</c> prefix also says which column a code belongs to,
    /// which the older ones leave to the reader.
    /// </summary>
    public const string StartBoot = Prefix + "start.boot";

    public const string StartSystem = Prefix + "start.system";

    public const string StartAutomatic = Prefix + "start.automatic";

    public const string StartManual = Prefix + "start.manual";

    /// <summary>Switched off. A fact about the entry rather than a shade of one - `docs/11` 3.1.</summary>
    public const string Disabled = Prefix + "disabled";

    /// <summary>The file the manager would run is not there, so it cannot do anything at all.</summary>
    public const string Missing = Prefix + "missing";

    /// <summary>
    /// Set to start automatically and not running, with nothing waiting to start it.
    ///
    /// <b>Narrower than the column's own name, and that was checked rather than assumed.</b>
    /// <c>ScmEntry.Judge</c> answers false for anything whose start type is not Automatic - so a
    /// service that is switched off and running anyway is NOT this. The first draft of the tooltip
    /// beside this said it was, which would have been a sentence on screen that the code disagrees
    /// with.
    ///
    /// <b>Its own code rather than one of the two above, and its own shape family in the theme.</b>
    /// This is a PERSISTENT disagreement between two settings, not a state something is passing
    /// through, so it may share neither the vocabulary of the running state nor the amber that
    /// means "on its way somewhere". `docs/11` 3.1 asks for shape and colour and word, and the
    /// column had the word alone until 2026-08-12 - backlog 165.
    /// </summary>
    public const string Against = Prefix + "against";
}
