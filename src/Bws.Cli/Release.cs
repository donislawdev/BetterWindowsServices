using System.Reflection;

namespace Bws.Cli;

/// <summary>
/// What version of the tool this is.
///
/// Read off the running assembly rather than written down here, because rule 11 of CLAUDE.md
/// makes the number the owner's to set and Directory.Build.props is where they set it. A copy
/// in source would be a second place to change and a first place to forget.
///
/// It exists because until 2026-08-02 there was no way to ask. The number was stamped into
/// every binary and the tool had no <c>--version</c>, so an administrator wanting to know what
/// was on their production server had to open file properties.
/// </summary>
internal static class Release
{
    /// <summary>
    /// The version as somebody should read it.
    ///
    /// The informational version rather than the assembly version, because that is the one the
    /// SDK fills from the <c>Version</c> property - the assembly version drops anything after
    /// the third part and would report 0.1.0.0 for a build that called itself something else.
    ///
    /// Anything after a plus sign is the source revision the SDK appends. Useful to a build
    /// system and noise to a person, so it is cut here rather than explained on screen.
    /// </summary>
    internal static string Number
    {
        get
        {
            var stamped = typeof(Release).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (string.IsNullOrEmpty(stamped))
            {
                // Nothing to fall back to that would be true, so it says so. A tool that
                // invents a version number when it cannot read one is worse than a tool that
                // admits it, which is rule 8 applied to a very small thing.
                return "unknown";
            }

            var build = stamped.IndexOf('+', StringComparison.Ordinal);
            return build < 0 ? stamped : stamped[..build];
        }
    }
}
