namespace Bws.Core.History;

/// <summary>
/// Changes the System log recorded more than once, identical in everything but their time - one
/// line where the log has many.
///
/// <b>WHY THIS EXISTS, MEASURED 2026-10-20 on the owner's machine</b> (docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md
/// section 10, F28 to F30): 328 changes in nine days, of which 262 were Windows switching BITS,
/// IsolationSession and TrustedInstaller between automatic and on demand by itself, and 56 were one
/// driver installed again every time a Python tool ran. Grouped, that is 21 lines, and nothing is left
/// out: each line says how many times and between when. Owner's decision J2-b, 2026-10-20.
///
/// <b>IDENTICAL MEANS EVERY FIELD BUT THE TIME</b>, and two halves of that are decisions rather than
/// defaults. The internal name compares without regard to case, the way the service manager compares
/// names and the way <see cref="ChangeHistory.Of"/> ties a change to an entry. And the account
/// compares by SID alone, never by name - rule 3 of the project notes, the same reason
/// <see cref="Actor"/> carries both. Everything else compares exactly, so a driver installed from two
/// different folders stays two lines, which is what an administrator would want to see.
///
/// <b>Upper case the invariant way stands for "without regard to case" here</b>, and that is not
/// proven to be the same comparison <see cref="ChangeHistory.Of"/> makes for every string Windows could
/// write - NOT CHECKED against the names on any machine. Where the two rules would part, the cost is a
/// change on a line of its own that could have shared one, never a change lost.
///
/// <b>Not a contract.</b> <c>bws history</c> lists every change separately and its JSON has no groups.
/// </summary>
/// <param name="Latest">
/// The member that came first in the order the changes were handed in - for a history, which lists
/// the newest first, the newest one. What the line shows besides the count.
/// </param>
/// <param name="Times">How many changes the group holds. One for a change recorded once.</param>
/// <param name="First">The earliest time among them, or null when none of them carried a time.</param>
/// <param name="Last">The latest time among them, or null when none of them carried a time.</param>
public sealed record ChangeGroup(ServiceChange Latest, int Times, DateTimeOffset? First, DateTimeOffset? Last)
{
    /// <summary>
    /// The changes gathered into groups of identical ones, in the order their first member was handed
    /// in - so a history listed newest first gives groups listed by their newest member.
    ///
    /// <b>By position rather than by time</b>, because a change may carry no time at all (see
    /// <see cref="ServiceChange.At"/>), and an order that had to decide where those go would be a second
    /// opinion about the order the history already has.
    /// </summary>
    public static IReadOnlyList<ChangeGroup> Of(IReadOnlyList<ServiceChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var members = new List<List<ServiceChange>>();
        var where = new Dictionary<Identity, int>();

        foreach (var change in changes)
        {
            var identity = Identity.Of(change);

            if (where.TryGetValue(identity, out var at))
            {
                members[at].Add(change);
            }
            else
            {
                where[identity] = members.Count;
                members.Add([change]);
            }
        }

        return [.. members.Select(Gathered)];
    }

    /// <summary>
    /// Every change on its own, in the same shape - what the window shows when somebody asks for each
    /// change separately, so the table has one kind of row whichever way it is read.
    /// </summary>
    public static IReadOnlyList<ChangeGroup> Each(IReadOnlyList<ServiceChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        return [.. changes.Select(change => new ChangeGroup(change, 1, change.At, change.At))];
    }

    private static ChangeGroup Gathered(List<ServiceChange> members)
    {
        var times = members.Where(member => member.At is not null).Select(member => member.At!.Value).ToList();

        return new ChangeGroup(
            members[0],
            members.Count,
            times.Count == 0 ? null : times.Min(),
            times.Count == 0 ? null : times.Max());
    }

    /// <summary>
    /// What two identical changes share: everything but the time - see the summary of
    /// <see cref="ChangeGroup"/> for the two fields that are not compared as written.
    ///
    /// <b>A value the compiler compares, rather than a comparer written by hand</b> - the record's own
    /// equality takes every field, so a field added here is compared without anybody remembering to.
    /// The internal name goes in upper case the invariant way, which is how an ordinal comparison that
    /// ignores case decides, and the account goes in as its SID alone.
    /// </summary>
    private readonly record struct Identity(
        ChangeKind Kind, string? ServiceName, string? DisplayName, string? Sid, string? BinaryPath, string? Account, WindowsWords Words)
    {
        internal static Identity Of(ServiceChange change) => new(
            change.Kind,
            change.ServiceName?.ToUpperInvariant(),
            change.DisplayName,
            change.By?.Sid,
            change.BinaryPath,
            change.Account,
            change.Words);
    }
}
