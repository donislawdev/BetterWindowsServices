using System.Security.Principal;

namespace Bws.Core;

/// <summary>
/// What is true about the session this process is running in.
///
/// <b>One reader of this fact, not two.</b> Elevation was answered inside the snapshot's own
/// metadata and nowhere else, so the window had no way to say what a snapshot says plainly -
/// that a session without administrator rights is handed fewer entries than the machine has.
/// Two readers of the same fact drift at the first edit, and this one is measured rather than
/// obvious.
/// </summary>
public static class Session
{
    /// <summary>
    /// Whether this session has administrator rights.
    ///
    /// <b>Measured, and the number is the reason this is worth saying out loud:</b> without
    /// elevation the manager enumerates 807 entries where an elevated session sees 810 on the
    /// same machine, and the security descriptor is refused for five more. A listing taken
    /// without elevation is not a shorter listing - it is a different document.
    ///
    /// <b>Asked through the built-in role, which compares the well-known identifier.</b> Never
    /// by the name of the group: that comparison reads "Administrators" and answers no on a
    /// machine where the group is called something else, which is how a set of measurements in
    /// this project came to be recorded under the wrong heading.
    /// </summary>
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();

        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
