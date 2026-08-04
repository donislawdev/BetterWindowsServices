using System.Reflection;

namespace Bws.Core;

/// <summary>
/// Marks the assembly that holds everything the tool actually knows how to do:
/// reading the service control manager, snapshots, diffing, the query language and
/// operation plans.
///
/// This project must never learn that a user interface or a console exists. That is
/// not a preference, it is what keeps the CLI and the GUI honest about being two
/// views of one engine. Guards live in tests/Bws.Architecture.Tests.
/// </summary>
public static class CoreAssembly
{
    /// <summary>Stable handle for tests and diagnostics. Nothing depends on the value.</summary>
    public const string Name = "Bws.Core";

    /// <summary>
    /// What build this is, for anything written to a file that somebody will read back
    /// later.
    ///
    /// A snapshot carries it so that a difference in output between two files can be traced
    /// to a change in us rather than to a change in the machine. Read from the assembly
    /// rather than written out here, because a second place holding a version number is a
    /// second place for it to be wrong - the one and only source is the Version property in
    /// Directory.Build.props, which the owner sets.
    /// </summary>
    public static string Version { get; } =
        typeof(CoreAssembly).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0]
        ?? "unknown";
}
