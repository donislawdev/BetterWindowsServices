using System.Reflection;

namespace Bws.Gui;

/// <summary>
/// Which release this window is, for the help menu - UX-GUI-014, 2026-09-24. Until that day the
/// window said its version nowhere, so somebody reporting a fault had nothing to copy it from.
///
/// <b>The same reading as the command line's Release, and a copy of it rather than a shared one on
/// purpose.</b> Each binary reports the stamp on ITS OWN assembly - the one a person is running -
/// and the two are built from the one Version in Directory.Build.props, so they agree unless one
/// of them is a different build, which is exactly when a person should see the difference.
/// </summary>
internal static class Release
{
    /// <summary>
    /// The version, without the part after a plus that the SDK appends (the source revision) -
    /// or null when nothing was stamped, which the menu says in words (rule 8 of CLAUDE.md applied
    /// to one number, rather than a guess that looks like one).
    /// </summary>
    internal static string? Number
    {
        get
        {
            var stamped = typeof(Release).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (string.IsNullOrEmpty(stamped))
            {
                return null;
            }

            var build = stamped.IndexOf('+', StringComparison.Ordinal);
            return build < 0 ? stamped : stamped[..build];
        }
    }
}
