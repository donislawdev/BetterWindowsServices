// Explicit, and it has to be. This file is linked into Bws.Gui.Tests, which sets UseWPF, and
// that swaps the implicit using set and takes System.IO out of it. Fourth time this project
// has paid for that line - the two files in that project already carry the same note, and a
// file shared across projects has to satisfy the strictest of them.
using System.IO;

namespace Bws.Tests;

/// <summary>
/// Where the source tree begins, for the guards that read files rather than assemblies.
///
/// <b>Linked into three test projects rather than referenced from one</b>, because a shared
/// test assembly would be a new reference edge and this project holds those under guard. One
/// physical file, three <c>Compile Include</c> entries, no new project - the same trade
/// <c>Sources.cs</c> already made for "which files the guards read", and for the same reason:
/// every copy of a rule is another chance for one of them to quietly stop being true.
///
/// <b>Anchored on the solution file, not on .git, and that is the whole point of this file
/// existing.</b> Until 2026-08-04 there were four private copies of a walk looking for a .git
/// DIRECTORY, and 126 of the 141 integration tests failed on the second machine this project
/// was ever built on - not because the machine differed, but because the tree had arrived as
/// files rather than as a clone. The message they gave was "Repository root not found", which
/// tells a stranger nothing about what to do.
///
/// .git says how the source got here. The solution file says what the source is, travels in
/// any copy of it, and is what a GPL tarball will carry when somebody builds this without ever
/// having cloned anything.
/// </summary>
internal static class SourceTree
{
    /// <summary>The one file that is at the top of this tree and nowhere else in it.</summary>
    private const string Marker = "BetterWindowsServices.slnx";

    internal static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, Marker)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException(
            $"No '{Marker}' in any directory above '{AppContext.BaseDirectory}'. These guards read " +
            "the source tree, so they have to find it. Run them from a checkout or an unpacked " +
            "copy of the sources, not from a copy of the build output on its own.");
    }
}
