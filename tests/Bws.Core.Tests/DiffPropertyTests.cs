using Bws.Core.Snapshots;
using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// Properties of the comparison engine - the fourth and last surface point B of the quality
/// queue asks for, after the query language, the snapshot reader and the launch path resolver.
///
/// <b>Why this surface is worth generated input rather than more examples.</b> The engine has
/// thirteen tests next door and five mutation entries, and every one of them is a pair somebody
/// thought of. The examples say nothing about the pairs nobody thought of, and this is the place
/// in the product where being wrong is quietest: the answer goes straight into
/// <c>--exit-code</c>, so a comparison finding nothing when something changed reads exactly like
/// a machine that did not drift. Rule 8 of CLAUDE.md, arriving through a number.
///
/// It has happened once already from the other side. A snapshot re-encoded to a code page,
/// compared against itself, reported <c>Changed (297)</c> - two hundred and ninety-seven entries
/// changed, not one of them changed - and a person running the tool found it, not a test.
///
/// The machines these compare are generated in <see cref="Machines"/>.
/// </summary>
public sealed class DiffPropertyTests
{
    /// <summary>
    /// Two readings of a machine that did not change say so.
    ///
    /// <b>`ADR-6` as a statement about every input rather than about one pair.</b> The promise is
    /// that two snapshots of an unchanged machine differ in one line, the one with the time in
    /// it, and the example next door checks it for two entries somebody wrote out. Here the
    /// entries are generated, including the awkward ones - a field the machine refused, a field
    /// nobody asked about, a process identifier that moved - and the answer still has to be
    /// nothing.
    ///
    /// Two separately built snapshots rather than one compared with itself, because the second
    /// would also pass if the engine short-circuited on the object being the same one, and no
    /// promise anybody made is about that.
    /// </summary>
    [Fact]
    public void Two_readings_of_a_machine_that_did_not_change_differ_in_nothing()
    {
        Machines.Any.Sample(
            entries =>
            {
                var diff = Compared(Machines.Taken(entries, at: 100), Machines.Taken(entries, at: 900));

                return !diff.Any && diff.Added.Count == 0 && diff.Removed.Count == 0 && diff.Changed.Count == 0;
            },
            iter: 5_000,
            print: entries => $"unchanged machine reported as changed: {Machines.Naming(entries)}");
    }

    /// <summary>
    /// Turning the comparison round turns the answer round with it.
    ///
    /// What appeared one way round went away the other, and a field that read X then Y reads Y
    /// then X. Nothing stated this before, and an engine getting it wrong would be believed:
    /// somebody comparing yesterday against today and today against yesterday would hold two
    /// reports that disagree, with nothing to say which is the true one.
    ///
    /// <b>Both sides are given the same elevation, and that exclusion is the interesting part
    /// rather than a convenience.</b> <c>Invisible</c> is asymmetric on purpose - an entry
    /// missing from an unelevated snapshot may only be missing from view, while one missing from
    /// the elevated side is really gone - so with elevation differing this property is false, and
    /// it is false because the engine is right. Written into the property rather than left out of
    /// it, the way the query parser's exclusions are.
    /// </summary>
    [Fact]
    public void Turning_the_comparison_round_swaps_what_it_found()
    {
        Gen.Select(Machines.Any, Machines.Any).Sample(
            pair =>
            {
                var (before, after) = pair;

                var forwards = Compared(Machines.Taken(before), Machines.Taken(after));
                var backwards = Compared(Machines.Taken(after), Machines.Taken(before));

                return Same(forwards.Added, backwards.Removed)
                    && Same(forwards.Removed, backwards.Added)
                    && Mirrored(forwards.Changed, backwards.Changed);
            },
            iter: 5_000,
            print: pair => "the comparison disagrees with itself reversed: " +
                $"{Machines.Naming(pair.Item1)} against {Machines.Naming(pair.Item2)}");
    }

    /// <summary>
    /// No entry is reported in two places at once.
    ///
    /// Six lists come out of a comparison and a person reads them as a partition - what was
    /// added, what went away, what changed, what could not be fully compared, what only one side
    /// could see. A name in two of them at once is a report counting the same service twice, and
    /// this project has met that shape once already on the other side of the product: two rows
    /// swapping places left the window showing one service twice, and the invariant watching the
    /// list could not see it, because in a swap both rows are wanted.
    /// </summary>
    [Fact]
    public void No_entry_is_reported_in_two_places_at_once()
    {
        Gen.Select(Machines.Any, Machines.Any, Gen.Bool).Sample(
            trio =>
            {
                var (before, after, elevated) = trio;

                var diff = Compared(Machines.Taken(before, elevated: elevated), Machines.Taken(after));

                var named = diff.Added.Concat(diff.Removed).Concat(diff.Uncertain)
                    .Select(entry => entry.ServiceName)
                    .Concat(diff.Changed.Concat(diff.NotFullyCompared).Select(entry => entry.ServiceName))
                    .ToList();

                return named.Count == named.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            },
            iter: 5_000);
    }

    /// <summary>
    /// Nothing sits under "changed" with nothing changed about it.
    ///
    /// <b>A bug that really happened, written down as a property.</b> An elevated snapshot
    /// compared against a restricted one put five entries under "changed" that had nothing
    /// changed about them - only a security descriptor one side had been refused. The summary
    /// read "5 changed, 0 differences", <c>Any</c> was true, and <c>--exit-code</c> would have
    /// failed a pipeline over a comparison that found nothing at all. It was found by running the
    /// thing against a real pair of files, and nothing in the code says it may not come back.
    ///
    /// The other half is the same claim from the far side: an entry filed as not fully compared
    /// has to have something it could not compare, or it is an admission about nothing.
    /// </summary>
    [Fact]
    public void What_is_filed_as_changed_has_something_changed_and_what_is_not_has_not()
    {
        Gen.Select(Machines.Any, Machines.Any, Gen.Bool).Sample(
            trio =>
            {
                var (before, after, elevated) = trio;

                var diff = Compared(Machines.Taken(before, elevated: elevated), Machines.Taken(after));

                return diff.Changed.All(entry => entry.Differences.Count > 0)
                    && diff.NotFullyCompared.All(
                        entry => entry.Differences.Count == 0 && entry.Incomparable.Count > 0);
            },
            iter: 5_000);
    }

    /// <summary>
    /// A field one side could not read is never reported as a difference.
    ///
    /// The most expensive mistake this engine could make, and the reason the four-state reading
    /// exists at all. A refused field is null on that side, so comparing it would report a change
    /// from a value to nothing - a difference nobody caused, in a tool that is believed because
    /// it is an audit tool.
    ///
    /// <b>Two ways a field goes uncompared, and the first version of this property knew about
    /// one.</b> It said what was REFUSED is never a difference, which is true and too narrow: a
    /// field exactly one side asked about is equally uncomparable, and the engine treats the two
    /// the same way for the same reason. Asking what the property would let through turned up a
    /// mutation - widening that intersection into a union - which would report every one-sided
    /// unread field as drift, and which no unit test in the repository catches. The claim is now
    /// the engine's rule whole, and the oracle for it is worked out from the two readings rather
    /// than read off the answer.
    /// </summary>
    [Fact]
    public void A_field_one_side_could_not_read_is_never_a_difference()
    {
        Gen.Select(Machines.Any, Machines.Any).Sample(
            pair =>
            {
                var (before, after) = pair;

                var diff = Compared(Machines.Taken(before), Machines.Taken(after));

                foreach (var entry in diff.Changed.Concat(diff.NotFullyCompared))
                {
                    var differing = entry.Differences
                        .Select(difference => difference.Field).ToHashSet(StringComparer.Ordinal);

                    if (differing.Overlaps(entry.Incomparable))
                    {
                        return false;
                    }

                    var uncomparable = Machines.Uncomparable(before, after, entry.ServiceName);

                    if (uncomparable.Any(field => !entry.Incomparable.Contains(field, StringComparer.Ordinal))
                        || uncomparable.Overlaps(differing))
                    {
                        return false;
                    }
                }

                return true;
            },
            iter: 5_000);
    }

    /// <summary>
    /// A process identifier that moved is not drift.
    ///
    /// Everything running gets a new one after a restart, so an engine counting it would mark
    /// most of a machine as changed for saying nothing at all - burying the six changes that
    /// matter under six hundred that do not. Owner's decision, 2026-08-01.
    ///
    /// The example next door changes one identifier on one entry. This changes every identifier
    /// on every entry and nothing else, which is what a reboot actually does.
    /// </summary>
    [Fact]
    public void Every_process_identifier_moving_at_once_is_still_not_a_difference()
    {
        Gen.Select(Machines.Any, Gen.Int[0, 40_000]).Sample(
            pair =>
            {
                var (entries, moved) = pair;

                var rebooted = entries
                    .Select(entry => entry with { ProcessId = Reading<int>.Present(moved) })
                    .ToList();

                return !Compared(Machines.Taken(entries), Machines.Taken(rebooted)).Any;
            },
            iter: 5_000);
    }

    /// <summary>
    /// The generated pairs reach every outcome the properties above are about.
    ///
    /// <b>Here because the six above would all pass on nothing.</b> Every one of them is of the
    /// form "whatever came back holds together", and a generator producing pairs that never
    /// differ satisfies all six while checking none - the lists would be empty and every claim
    /// about their contents vacuously true.
    ///
    /// Not a hypothetical worry in this project. A parity test between the window and the command
    /// line asked about <c>^sql</c> on a machine with no SQL Server, both sides answered with
    /// nothing, the two nothings were equal, and the test stayed green while the switch it was
    /// about was turned off. <b>Two empty results are equal</b>, so a property over generated
    /// input has to say out loud that the input got somewhere.
    ///
    /// Six counters rather than one, because the outcomes are reached by different routes and a
    /// single total would hide a route that stopped being taken - which is how this would rot.
    /// </summary>
    [Fact]
    public void The_generated_pairs_reach_every_outcome_these_properties_are_about()
    {
        int added = 0, removed = 0, changed = 0, partly = 0, refused = 0, unasked = 0;

        Gen.Select(Machines.Any, Machines.Any).Sample(
            pair =>
            {
                var (before, after) = pair;
                var diff = Compared(Machines.Taken(before), Machines.Taken(after));

                added += diff.Added.Count;
                removed += diff.Removed.Count;
                changed += diff.Changed.Count;
                partly += diff.NotFullyCompared.Count;

                var (denied, never) = Machines.Unread(before);
                refused += denied;
                unasked += never;

                return true;
            },
            iter: 2_000,
            threads: 1);

        Assert.True(added > 0, "no pair ever gained an entry");
        Assert.True(removed > 0, "no pair ever lost an entry");
        Assert.True(changed > 0, "no pair ever changed a field");
        Assert.True(partly > 0, "nothing was ever left incompletely compared");
        Assert.True(refused > 0, "the machine never refused a field");
        Assert.True(unasked > 0, "no field was ever left unasked");
    }

    /// <summary>
    /// Any two snapshots give back a comparison or a reason, never both and never neither.
    ///
    /// The same contract the snapshot reader has, and it arrived on the day this was written -
    /// until 2026-08-04 the engine fell over from inside a dictionary on a document holding one
    /// service name twice, with a message naming neither side.
    ///
    /// <b>The broken shapes are built deliberately rather than waited for.</b> The generator
    /// makes well-formed machines on purpose, because one producing mostly rubbish would leave
    /// every other property here checking that rubbish is refused. So the four ways a snapshot
    /// can fail to be one are constructed by hand and put through the same door.
    /// </summary>
    [Fact]
    public void Any_two_snapshots_give_back_a_comparison_or_a_reason()
    {
        Gen.Select(Machines.Any, Gen.Int[0, 4], Gen.Int[0, 4]).Sample(
            trio =>
            {
                var (entries, spoilBefore, spoilAfter) = trio;

                var compared = SnapshotDiff.TryBetween(
                    Machines.Spoiled(Machines.Taken(entries), spoilBefore),
                    Machines.Spoiled(Machines.Taken(entries), spoilAfter),
                    out var diff,
                    out var failure);

                // Exactly one of the two comes back, whichever way it went - and a refusal says
                // something, because a reason nobody can read is the silence being put back.
                return compared == (diff is not null)
                    && (diff is null) ^ (failure is null)
                    && (failure is null || failure.Length > 0);
            },
            iter: 5_000);
    }

    /// <summary>
    /// Compares, and refuses to carry on if the engine refused.
    ///
    /// Every property but the last is about well-formed snapshots, so a refusal there is a fault
    /// in the generator rather than a finding - and a helper swallowing it would turn that fault
    /// into six properties quietly passing on input they never saw.
    /// </summary>
    private static SnapshotDiff Compared(Snapshot before, Snapshot after)
    {
        Assert.True(SnapshotDiff.TryBetween(before, after, out var diff, out var failure), failure);

        return diff!;
    }

    private static bool Same(IReadOnlyList<EntryPresence> left, IReadOnlyList<EntryPresence> right) =>
        left.Select(entry => entry.ServiceName).OrderBy(name => name, StringComparer.Ordinal)
            .SequenceEqual(
                right.Select(entry => entry.ServiceName).OrderBy(name => name, StringComparer.Ordinal),
                StringComparer.Ordinal);

    /// <summary>
    /// The same entries changed in the same fields, with every before and after the other way
    /// round.
    /// </summary>
    private static bool Mirrored(IReadOnlyList<ChangedEntry> forwards, IReadOnlyList<ChangedEntry> backwards)
    {
        if (forwards.Count != backwards.Count)
        {
            return false;
        }

        return forwards.Zip(backwards).All(pair =>
            string.Equals(pair.First.ServiceName, pair.Second.ServiceName, StringComparison.Ordinal)
            && pair.First.Differences.Count == pair.Second.Differences.Count
            && pair.First.Differences.Zip(pair.Second.Differences).All(fields =>
                string.Equals(fields.First.Field, fields.Second.Field, StringComparison.Ordinal)
                && fields.First.Group == fields.Second.Group
                && string.Equals(fields.First.Before, fields.Second.After, StringComparison.Ordinal)
                && string.Equals(fields.First.After, fields.Second.Before, StringComparison.Ordinal)));
    }
}
