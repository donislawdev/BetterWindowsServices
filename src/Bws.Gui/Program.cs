using System.Diagnostics.CodeAnalysis;
using Bws.Core;

namespace Bws.Gui;

/// <summary>
/// Where the window starts, and the one decision taken before anything of WPF exists: whether its
/// native libraries came from a folder only administrators can change. Security report S-1, the
/// owner's decisions of 2026-10-06 - <see cref="Unpacking"/> carries the argument.
///
/// <para>
/// <b>WHY THE WINDOW HAS A MAIN OF ITS OWN.</b> Until 2026-10-06 it used the one WPF writes for
/// <c>App.xaml</c>, which constructs the application on its first line - and measured that day,
/// <c>new Application()</c> already loads <c>PresentationNative_cor3.dll</c> from the folder in
/// question. So the decision has to come before it, and <c>App.xaml</c> is a Page rather than the
/// application definition, which is what stops WPF writing a Main.
/// </para>
/// <para>
/// <b>NOTHING HERE NAMES A TYPE OF WPF</b>, and nothing it calls before the decision does either:
/// the rule is in the core, the folder is examined by the core, the words come from <see cref="Texts"/>,
/// which reads a JSON resource, and a refusal is said by user32 rather than by a WPF box
/// (<see cref="Elevation.Refuse"/>). That a type merely mentioning a WPF parameter loads nothing native
/// when it is loaded is a conclusion from the measurement above, not a measurement of its own.
/// </para>
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var facts = Facts();
        var decision = Unpacking.Decide(facts, AdministratorsFolder.Prepare, AdministratorsFolder.Examine);

        if (decision is { Verdict: UnpackingVerdict.Elsewhere, Check: { } home })
        {
            // Owner's decision 2026-10-06: this process ends as soon as the next one is on its way, so
            // Task Manager shows one window's worth of process, as it always has.
            return Elevation.Again(args, home.Path) is { } failed ? Elevation.Refuse(Refusal(failed)) : 0;
        }

        if (decision is { Verdict: UnpackingVerdict.Refused, Check: { } refused })
        {
            return Elevation.Refuse(Refusal(refused));
        }

        // The variable this program set for itself is not a setting somebody else made, and left in
        // place it would be named on the status line as one (Session.RuntimeSettingsFromEnvironment)
        // and inherited by anything started from here.
        if (Unpacking.Same(facts.BaseVariable, facts.Home))
        {
            Environment.SetEnvironmentVariable(Unpacking.BaseVariable, null);
        }

        var application = new App();
        application.InitializeComponent();

        return application.Run();
    }

    /// <summary>What this process is and where its native libraries came from.</summary>
    internal static UnpackingFacts Facts() => new(
        Session.IsElevated(),
        Bundled(),
        AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string,
        AppContext.BaseDirectory,
        AdministratorsFolder.Home,
        Environment.GetEnvironmentVariable(Unpacking.BaseVariable));

    /// <summary>
    /// Whether this program is a single-file bundle - asked of the one fact Microsoft documents for it:
    /// an assembly inside the bundle has no location, so its Location is empty.
    ///
    /// <b>The one place in the product that silences IL3000, owner's decision 2026-10-06, counted in
    /// AnalyzerRuleGuards.</b> The analyser warns that the path is empty in a bundle, and the emptiness
    /// is exactly what is asked. The two other ways were refused: a property of the host that is not
    /// documented, and a file beside the program, which an account without rights can put there when
    /// the program lies in its Downloads - and with it switch all of this off.
    /// </summary>
    [UnconditionalSuppressMessage(
        "SingleFile",
        "IL3000:Avoid accessing Assembly file path when publishing as a single file",
        Justification = "The empty path is the fact asked for - learn.microsoft.com/dotnet/core/deploying/single-file/overview, API incompatibility.")]
    private static bool Bundled() => typeof(Program).Assembly.Location.Length == 0;

    /// <summary>
    /// What the box says: that the program did not start, which folder and why, what the system said,
    /// and what to do. Every key written where it is chosen, so TextKeyGuards can find each one.
    /// </summary>
    internal static string Refusal(FolderCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        var reason = check.Fault switch
        {
            UnpackingFault.NotMade => Texts.Of("gui.unpacking.notMade", check.Path),
            UnpackingFault.OthersCanChange => Texts.Of("gui.unpacking.othersCanChange", check.Path),
            UnpackingFault.Link => Texts.Of("gui.unpacking.link", check.Path),
            UnpackingFault.Unreadable => Texts.Of("gui.unpacking.unreadable", check.Path),
            UnpackingFault.StillOutside => Texts.Of("gui.unpacking.stillOutside", check.Path),
            UnpackingFault.NotStarted => Texts.Of("gui.unpacking.notStarted", check.Path),
            _ => Texts.Of("gui.unpacking.unnamed")
        };

        var advice = check.Fault switch
        {
            UnpackingFault.OthersCanChange or UnpackingFault.Link or UnpackingFault.Unreadable =>
                Texts.Of("gui.unpacking.deleteIt", AdministratorsFolder.Home),
            UnpackingFault.NotMade => Texts.Of("gui.unpacking.checkAbove"),
            _ => null
        };

        string?[] lines = [Texts.Of("gui.unpacking.lead"), reason, check.Cause, advice, Texts.Of("gui.unpacking.withoutRights")];

        return string.Join(Environment.NewLine + Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }
}
