using System.Text.Json;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Core.Snapshots;

namespace Bws.Cli.Tests;

/// <summary>
/// Every JSON document this tool prints is ASCII, and still says what the machine said.
///
/// <b>Stability report C-1, owner's decision 2026-09-30.</b> The listing and the comparison wrote
/// display names as themselves, and a console in code page 852 turned them into bytes no UTF-8
/// reader accepts on the way into a file. Each document is asked both halves: that nothing above
/// U+007E is in it, and that a parser hands back the name exactly - an escape that lost a
/// character would pass the first half and fail the second.
///
/// The name carries every shape at once: Polish letters, an ampersand and an apostrophe (which
/// must stay readable), a character outside the basic plane, a direction override and an escape
/// sequence.
/// </summary>
public sealed class AsciiDocumentTests
{
    // Built from numbers rather than written as escapes, because an escape typed through the editing
    // tools lands on disk as the character itself - and a published file holding a letter outside
    // ASCII fails PublicSurfaceGuards. Paid on this very file, 2026-10-05.
    private static readonly string Hostile = string.Concat(
        "Us", Code(0x0142), "uga ", Code(0x017C), Code(0x00F3), Code(0x0142), "ta & co's ",
        Code(0x1F600), " ", Code(0x202E), "exe.txt ", Code(0x001B), "[31m");

    [Fact]
    public void A_listing_is_ascii_and_says_the_name_the_machine_gave()
    {
        var written = ListingJson.Render([Entries.Plain("Aaa", Hostile)]);

        Plain(written);
        Assert.Equal(Hostile, JsonDocument.Parse(written).RootElement[0].GetProperty("displayName").GetString());
    }

    [Fact]
    public void One_entry_is_ascii_and_says_the_name_the_machine_gave()
    {
        var written = ListingJson.One(Entries.Plain("Aaa", Hostile));

        Plain(written);
        Assert.Equal(Hostile, JsonDocument.Parse(written).RootElement.GetProperty("displayName").GetString());
    }

    [Fact]
    public void A_plan_and_its_run_are_ascii_and_say_the_name_the_machine_gave()
    {
        var step = new PlanStep("Aaa", Hostile, StepOperation.Stop, StepReason.Requested);
        var plan = new OperationPlan
        {
            Action = new ServiceAction(ActionKind.Stop, "Aaa"),
            Steps = [step],
            Warnings = [],
            Problems = []
        };
        var run = new PlanRun
        {
            Plan = plan,
            Results = [new StepResult
            {
                Step = step,
                Outcome = StepOutcome.Failed,
                SkippedBecause = null,
                Status = EntryStatus.Running,
                ProcessId = Reading<int>.Present(1234),
                ErrorCode = 1051,
                Error = Hostile,
                Milliseconds = 10
            }],
            Cancelled = false,
            Ceiling = TimeSpan.FromSeconds(60)
        };

        foreach (var written in new[] { PlanJson.Render(plan), PlanJson.Render(run) })
        {
            Plain(written);
            Assert.Equal(Hostile, JsonDocument.Parse(written).RootElement.GetProperty("steps")[0].GetProperty("displayName").GetString());
        }

        Assert.Equal(Hostile, JsonDocument.Parse(PlanJson.Render(run)).RootElement.GetProperty("results")[0].GetProperty("error").GetString());
    }

    [Fact]
    public void A_comparison_is_ascii_and_says_the_name_the_file_held()
    {
        var diff = new SnapshotDiff(
            [new EntryPresence("Aaa", Hostile)], [], [], [], [], [],
            new ComparisonCaveats(false, false, false, false, false, false, false, []),
            new InstancesLeftOut(0, 0));

        var written = DiffJson.Render(diff);

        Plain(written);
        Assert.Equal(Hostile, JsonDocument.Parse(written).RootElement.GetProperty("added")[0].GetProperty("displayName").GetString());
    }

    [Fact]
    public void The_receipt_of_a_snapshot_is_ascii_and_says_the_machine_it_named()
    {
        var snapshot = new Snapshot(
            new SnapshotMetadata
            {
                SchemaVersion = Snapshot.CurrentSchemaVersion,
                Machine = Hostile,
                OperatingSystem = "Microsoft Windows NT 10.0.26200.0",
                TakenAt = DateTimeOffset.UnixEpoch,
                TakenBy = "TESTBOX\\somebody",
                Elevated = true,
                Note = null,
                Tool = "0.1.0"
            },
            []);

        var written = SnapshotText.Render(snapshot, "before.json", asJson: true);

        Plain(written);
        Assert.Equal(Hostile, JsonDocument.Parse(written).RootElement.GetProperty("machine").GetString());
    }

    /// <summary>
    /// The punctuation a person reads stays as itself, and the boundary sits where it says.
    ///
    /// The framework's default encoder would have passed every test above and still written
    /// every apostrophe of every English description as a six character escape - the reason AsciiJson escapes after
    /// serialising rather than choosing a stricter encoder.
    /// </summary>
    [Fact]
    public void Ascii_punctuation_stays_readable_and_the_first_character_past_the_tilde_is_escaped()
    {
        var options = new JsonSerializerOptions { Encoder = AsciiJson.Encoder };

        Assert.Contains("& co's", ListingJson.Render([Entries.Plain("Aaa", Hostile)]), StringComparison.Ordinal);
        Assert.Equal("\"~\\u007F\\u0080\"", AsciiJson.Serialize("~\u007F\u0080", options));
    }

    private static string Code(int point) => char.ConvertFromUtf32(point);

    private static void Plain(string document)
    {
        var outside = document.Where(character => character > '~').Distinct().ToList();

        Assert.True(
            outside.Count == 0,
            "A document for a console carried characters above U+007E: "
            + string.Join(", ", outside.Select(character => $"U+{(int)character:X4}")));
    }
}
