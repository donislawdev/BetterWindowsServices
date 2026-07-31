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
}
