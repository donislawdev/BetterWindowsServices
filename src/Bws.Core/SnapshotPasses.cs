using System.Diagnostics;

namespace Bws.Core;

/// <summary>
/// What a snapshot needs read on top of a listing: who signed each binary and who depends on each
/// entry, each timed on its own.
///
/// <b>One place since 2026-10-14, and the window is why.</b> The command line read the two passes in
/// two places - taking a snapshot and comparing one against the machine - and the window taking and
/// comparing snapshots would have been the third copy of the same four lines. The third time a shape
/// appears it becomes a name. A copy that forgot one pass would take snapshots that compare against
/// the command line's as though every entry had lost its signature or its dependents, and nothing
/// would say so.
///
/// <b>Both passes, always, and no switch.</b> A snapshot is taken deliberately and kept for months,
/// so being comparable against the next one matters more than the second it costs (about 1.1-1.2 s
/// for the whole snapshot over 801 entries on sixteen processors, measured 2026-10-14) - and one
/// missing either family would compare against one that has it as though the machine had changed.
/// </summary>
public static class SnapshotPasses
{
    /// <summary>The listing with signatures and dependents filled in, new records as every pass returns.</summary>
    /// <param name="inspected">Milliseconds spent on the files, for the command line's --timing.</param>
    /// <param name="asked">Milliseconds spent asking who depends on each entry.</param>
    public static IReadOnlyList<ScmEntry> Fill(
        IReadOnlyList<ScmEntry> listed, IScmCatalog catalog, IBinaryInspector inspector, out long inspected, out long asked)
    {
        var started = Stopwatch.GetTimestamp();
        var signed = SecondPass.Fill(listed, inspector);
        inspected = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        started = Stopwatch.GetTimestamp();
        var answered = RequiredByPass.Fill(signed, catalog);
        asked = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        return answered;
    }
}
