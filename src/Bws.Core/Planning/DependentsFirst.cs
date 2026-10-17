namespace Bws.Core.Planning;

/// <summary>
/// Puts entries in the order they have to be taken down: an entry may stop only once everything
/// that depends on it has stopped.
///
/// <b>Its own class since 2026-08-18, and it is here because the question is now asked twice.</b>
/// It was worked out inside <see cref="PlanBuilder"/> for the members of one cascade. The bulk plan
/// of `C2` asks the identical question about the entries somebody SELECTED - if B depends on A and
/// both were picked, B has to be dealt with first, exactly as it would inside a cascade. Two
/// implementations of the same rule are the shape this project pays for most often, and this
/// particular rule is one where disagreeing means an outage rather than an oddity.
///
/// <b>The manager is asked who breaks, rather than us inverting what everything declares</b>, and the
/// argument for that is at <see cref="PlanBuilder"/>: an entry can declare a load order group and the
/// declaration does not say who belongs to it.
///
/// <b>The order is worked out rather than taken from the order the manager happened to answer in.</b>
/// That order looked right in every sample and is promised nowhere, and a stop order that is right
/// by luck fails on somebody else's machine at the worst moment.
/// </summary>
internal static class DependentsFirst
{
    /// <summary>
    /// The names, reordered so that nothing is taken down before the things that need it.
    ///
    /// Costs one question per name. That is affordable because these sets are small - and where one
    /// is not, the plan is about to take down half the machine and a moment spent getting the order
    /// right is not the expensive part.
    /// </summary>
    /// <param name="catalog">Asked who depends on each name.</param>
    /// <param name="names">
    /// The set to order. Only relationships INSIDE this set matter - something outside it that
    /// depends on a member is not a step, it is either a cascade the caller already resolved or an
    /// entry the manager will refuse to let go, and both are somebody else's answer.
    /// </param>
    internal static List<string> Order(IScmCatalog catalog, IReadOnlyList<string> names)
    {
        var inside = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blockedBy = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            var theirs = catalog.ReadDependents(name);

            // An unreadable answer orders nothing rather than refusing. Turning missing information
            // into a decision is the opposite of what the four read outcomes exist for, and the
            // caller is the one holding the warning that says the cascade could not be read.
            //
            // Without case, like every other set here: the cycle test below asks these sets about a
            // name as the listing spells it, and the manager is free to spell it otherwise.
            blockedBy[name] = theirs.IsPresent
                ? new HashSet<string>(theirs.Value!.Where(inside.Contains), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var ordered = new List<string>(names.Count);
        var remaining = new List<string>(names);
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (remaining.Count > 0)
        {
            // A BLOCK BOTH WAYS IS A CYCLE, NOT AN ORDER, AND IS NOT WAITED ON - since 2026-10-17, the
            // owner's decision on finding C of the fuzzing project. The manager's answer reaches past
            // the first hop, so every member of a cycle names every other member AND ITSELF. Waited
            // on, those blocks left the members never ready, and the fallback below then took
            // whatever stood first - typically an entry the cycle still needed, which the manager
            // refuses to stop while the cycle runs. Skipping them orders everything outside the
            // cycle correctly and the members among themselves in the order given, where no order is
            // right. Without a cycle no block runs both ways, so nothing else changes.
            var ready = remaining.FirstOrDefault(name =>
                blockedBy[name].All(other => done.Contains(other) || blockedBy[other].Contains(name)));

            // What is left for a cycle the answers do not show both ways - one only a first hop
            // reveals, which the measured manager does not give. Looping forever over one is a far
            // worse answer than an order the manager will reject with a message the step outcome
            // can report.
            ready ??= remaining[0];

            ordered.Add(ready);
            done.Add(ready);
            remaining.Remove(ready);
        }

        return ordered;
    }

    /// <summary>
    /// The same order, for entries rather than names - each entry handed back exactly once.
    ///
    /// <b>Here since 2026-10-17, when a second caller arrived</b> (the neighbours of a forced stop,
    /// finding B of the fuzzing project) beside the cascade PlanBuilder orders, and the mapping back from
    /// names would otherwise have been written twice. Matched by the very string handed over rather than
    /// through a dictionary, so two entries spelled alike - which a real machine cannot hold and the
    /// specimen catalogue holds on purpose - come back as two rather than throwing or folding into one.
    /// </summary>
    internal static List<ScmEntry> Ordered(IScmCatalog catalog, IReadOnlyList<ScmEntry> entries)
    {
        var left = entries.ToList();

        return [.. Order(catalog, [.. entries.Select(entry => entry.ServiceName)]).Select(name => Taken(left, name))];
    }

    private static ScmEntry Taken(List<ScmEntry> left, string name)
    {
        var at = left.FindIndex(entry => string.Equals(entry.ServiceName, name, StringComparison.Ordinal));
        var entry = left[at];

        left.RemoveAt(at);

        return entry;
    }
}
