namespace Bws.Gui.ViewModels;

/// <summary>
/// What the rows somebody picked stand for - the names a plan is built from, and whether they are drivers alone.
///
/// <b>Out of MainWindow since 2026-11-24</b>, when the recovery form became a second door that needs both and
/// the window stood at the ceiling of members a shipped type may have (CodeShapeGuards). Both are facts about
/// rows rather than about a window, so they can be asked without one.
/// </summary>
internal static class Picking
{
    /// <summary>
    /// Every entry the picked rows stand for, by the manager's name, a folded family with its instances.
    ///
    /// Internal names rather than display names, because that is what identity is - `ADR-14`. A session's copy
    /// carries no display name of its own at all: measured on the machine this was written on, all 23 of them
    /// answer with their own service name where the template answers with a translated sentence.
    /// </summary>
    internal static List<string> Names(IReadOnlyList<EntryRow> picked)
    {
        var names = new List<string>(picked.Count);

        foreach (var row in picked)
        {
            names.Add(row.ServiceName);
            names.AddRange(row.Instances.Select(instance => instance.ServiceName));
        }

        return names;
    }

    /// <summary>
    /// Whether every picked row is a driver - the core's own fact about the entry, not one the window works
    /// out. A folded row is a per-user family, which is always services, so rows and not the names they stand
    /// for are enough to ask.
    /// </summary>
    internal static bool OnlyDrivers(IReadOnlyList<EntryRow> picked) =>
        picked.Count > 0 && picked.All(row => row.Entry.IsDriver);
}
