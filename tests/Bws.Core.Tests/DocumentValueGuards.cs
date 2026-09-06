using System.Text.Json;
using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Every enumerated value in the document is spelled the way its enumeration spells it.
///
/// <b>Half of them were not, from the day the second one was written until 2026-08-26.</b> Four
/// fields went through <c>ToString</c> and four through a lower-casing helper, so one document
/// carried <c>OwnProcess</c>, <c>Running</c> and <c>Automatic</c> beside <c>unrestricted</c>,
/// <c>normal</c>, <c>trusted</c> and <c>deviceArrival</c>. Nothing broke, which is why it lasted:
/// this project reads its own values back through a query language that normalises spelling, so
/// the only reader who could be hurt was somebody else's script - and it would be hurt silently,
/// by a comparison that is simply false.
///
/// <b>Parsed rather than pattern-matched, and case-sensitively on purpose.</b> A guard checking
/// that a value "starts with a capital" would pass for a word no enumeration has. This one demands
/// the value BE a member, spelled exactly - which is the property a script keying on it needs, and
/// which no amount of prose was holding.
///
/// <b>What it deliberately does not cover:</b> the field NAMES, which are camelCase and come from
/// the glossary, and the query language's own vocabulary, which is lower case and always has been.
/// Those are two other questions and both already have their own guards.
/// </summary>
public sealed class DocumentValueGuards
{
    [Fact]
    public void Every_enumerated_value_is_a_member_spelled_the_way_the_enumeration_spells_it()
    {
        var checkedValues = 0;

        // Through the product's own writer rather than a serialiser configured here. A second set
        // of options in a test is a second contract, and the first thing it would get wrong is the
        // one this guard is about - the first attempt at this test built its own and read PascalCase
        // KEYS, so every check below skipped itself and the count said zero.
        var rendered = JsonDocument
            .Parse(SnapshotJson.Render(Snapshot.Of(Specimens.Inspected, note: null, new FakeClock())))
            .RootElement.GetProperty("entries");

        foreach (var document in rendered.EnumerateArray())
        {
            checkedValues += Member<EntryType>(document, "entryType");
            checkedValues += Member<PerUserRole>(document, "perUserRole");
            checkedValues += Member<EntryStatus>(document, "status");
            checkedValues += Member<StartType>(document, "startType");
            checkedValues += Member<ServiceSidType>(document, "sidType");
            checkedValues += Member<ErrorControl>(document, "errorControl");

            if (document.TryGetProperty("signature", out var signature)
                && signature.ValueKind == JsonValueKind.Object)
            {
                checkedValues += Member<SignatureStatus>(signature, "status");
            }

            if (document.TryGetProperty("triggers", out var triggers)
                && triggers.ValueKind == JsonValueKind.Array)
            {
                foreach (var trigger in triggers.EnumerateArray())
                {
                    checkedValues += Member<TriggerKind>(trigger, "kind");
                    checkedValues += Member<TriggerAction>(trigger, "action");
                }
            }
        }

        // A GUARD THAT READ NOTHING PASSES, which is how this whole family goes quietly wrong -
        // one renamed field and every assertion above is skipped by its own null check.
        Assert.True(
            checkedValues > 20,
            $"Only {checkedValues} values were looked at, which is too few for the specimens this "
            + "reads. A field was probably renamed and the checks are now skipping themselves.");
    }

    /// <summary>
    /// Every list in the document has been decided about, one way or the other.
    ///
    /// <b>The way the comparison's list of unordered fields goes wrong is not a wrong name in it -
    /// it is a field ADDED to the document that nobody came back for.</b> Order-sensitivity then
    /// returns for that one field, silently, on the surface whose whole job is telling real drift
    /// from noise. That is the same shape as the column-to-heading map in the window, which this
    /// project already pays for with six red tests whenever somebody forgets it.
    ///
    /// <b>The other half is named rather than assumed:</b> a list whose order DOES carry meaning
    /// would belong in the second set below, and today there is none. An empty set is a decision
    /// here rather than an oversight - a step order or a run of results would be one.
    /// </summary>
    [Fact]
    public void Every_list_in_the_document_is_either_unordered_or_named_as_ordered()
    {
        string[] orderCarriesMeaning = [];

        // ASKED OF THE TYPE RATHER THAN OF A RENDERED SPECIMEN, SINCE 2026-09-06 - AND THE CHANGE
        // IS A REPAIR THAT A REAL MISS PAID FOR. This read the JSON of Specimens.Inspected and kept
        // whatever came out as an ARRAY. A list field that is null in that fixture is not an array,
        // so it was invisible here: `requiredBy` arrived that day, sat unread in every specimen,
        // and this guard - whose entire subject is a field nobody came back for - went green over
        // exactly that.
        //
        // The document's own shape cannot hide one. Every member typed as a list is a list whatever
        // any fixture happens to hold, and the names are camel-cased the way EntryDocument writes
        // them.
        var lists = typeof(EntryDocument)
            .GetProperties()
            .Where(property =>
                property.PropertyType != typeof(string)
                && property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            .Select(property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..])
            .Except(Bookkeeping, StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(lists);

        var decided = SnapshotDiff.Unordered
            .Concat(orderCarriesMeaning)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            lists.SequenceEqual(decided, StringComparer.Ordinal),
            "A list in the document has not been decided about. Every array field is either compared "
            + "as a set - SnapshotDiff.Unordered - or named here as one whose order means something. "
            + $"The document holds [{string.Join(", ", lists)}] and the two sets name "
            + $"[{string.Join(", ", decided)}].");
    }

    /// <summary>
    /// Arrays that name what could not be read rather than being values in their own right.
    ///
    /// <c>SnapshotDiff</c> keeps its own copy of this and drops both before comparing anything,
    /// which is why they are outside the question above.
    /// </summary>
    private static readonly string[] Bookkeeping = ["unreadable", "notRead"];

    /// <summary>
    /// One value parsed as a member of its enumeration, and says whether there was one to parse.
    ///
    /// Null is an answer rather than a gap - most entries declare no trigger and two thirds no
    /// privileges - so a field that is not there is counted as nothing checked rather than as a
    /// failure.
    /// </summary>
    private static int Member<T>(JsonElement holder, string field)
        where T : struct, Enum
    {
        if (!holder.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return 0;
        }

        var written = value.GetString()!;

        Assert.True(
            Enum.TryParse<T>(written, ignoreCase: false, out _),
            $"The document writes \"{field}\": \"{written}\", which is not how {typeof(T).Name} "
            + "spells any of its members. Values in this document are written the way the "
            + "enumeration writes them - see EntryDocument for the measurement that chose that.");

        return 1;
    }
}
