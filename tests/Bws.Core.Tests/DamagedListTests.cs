using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// A null among the names of fields nobody read - stability report D-3, measured 2026-09-29.
///
/// <b>What it did before, on the real tool:</b> a copy of a real snapshot with <c>"notRead": [null]</c>
/// in one entry ended <c>bws snapshot diff</c> with "Object reference not set to an instance of an
/// object." and code 1 - the code for the tool falling over, on a file that is simply damaged. The
/// comparison turns each name on that list into the field it speaks about, and a null is not one.
///
/// Structurally valid JSON, so the property test that damages snapshots by cutting and flipping
/// characters never reaches it. Both readers are asked, because both lean on the same rule.
/// </summary>
public sealed class DamagedListTests
{
    [Fact]
    public void The_reader_refuses_the_file_and_names_the_entry()
    {
        var entry = Specimens.All.First(specimen => EntryDocument.From(specimen).NotRead is { Count: > 0 });

        var text = SnapshotJson.Render(Snapshot.Of([entry], note: null, new FakeClock()))
            .Replace("\"notRead\": [", "\"notRead\": [\n        null,", StringComparison.Ordinal);

        Assert.False(SnapshotJson.TryRead(text, out var read, out var failure));
        Assert.Null(read);
        Assert.Contains(entry.ServiceName, failure!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_comparison_refuses_a_document_built_in_code_rather_than_throwing()
    {
        // The second caller - anything handing the engine a document that never went through the
        // reader. The engine asks the same question of whatever it is given.
        var fine = Snapshot.Of([Entries.Any], note: null, new FakeClock());
        var damaged = fine with { Entries = [fine.Entries[0] with { NotRead = ["triggers", null!] }] };

        Assert.False(SnapshotDiff.TryBetween(fine, damaged, out var diff, out var failure));
        Assert.Null(diff);
        Assert.Contains("later", failure!, StringComparison.Ordinal);
    }
}
