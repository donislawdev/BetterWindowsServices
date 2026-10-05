using Bws.Core;
using Bws.Core.Planning;
using Bws.Core.Snapshots;

namespace Bws.Cli.Tests;

/// <summary>
/// What reaches a terminal from the machine or from a file carries no character that moves the
/// terminal rather than printing.
///
/// <b>Stability report C-4, owner's decision 2026-10-05:</b> each such character is written out as
/// <c>&lt;U+XXXX&gt;</c>. The four renderers a person reads are asked with the same hostile value,
/// and the set the assertions use is spelled here from numbers, apart from Printable's own - a test
/// built on the code's list would agree with the code whatever the code said.
/// </summary>
public sealed class TerminalTextTests
{
    private static readonly string Escape = Code(0x1B);

    /// <summary>A tab, a line break, an escape sequence, a C1 control and a right to left override.</summary>
    private static readonly string Hostile =
        "Vendor" + Code(0x09) + "tool" + Code(0x0D) + Code(0x0A) + Escape + "[2J" + Code(0x9B) + Code(0x202E) + "fdp.exe";

    [Theory]
    [InlineData(0x00, true)]
    [InlineData(0x1F, true)]
    [InlineData(0x20, false)]
    [InlineData(0x7E, false)]
    [InlineData(0x7F, true)]
    [InlineData(0x9F, true)]
    [InlineData(0xA0, false)]
    [InlineData(0x200D, false)]
    [InlineData(0x200E, true)]
    [InlineData(0x200F, true)]
    [InlineData(0x2029, false)]
    [InlineData(0x202A, true)]
    [InlineData(0x202E, true)]
    [InlineData(0x202F, false)]
    [InlineData(0x2065, false)]
    [InlineData(0x2066, true)]
    [InlineData(0x2069, true)]
    [InlineData(0x206A, false)]
    public void A_character_is_written_out_exactly_when_it_is_in_the_set(int code, bool writtenOut)
    {
        var shown = Printable.Of("a" + Code(code) + "b");

        Assert.Equal(writtenOut ? $"a<U+{code:X4}>b" : "a" + Code(code) + "b", shown);
    }

    [Fact]
    public void Writing_out_twice_changes_nothing_the_first_time_did_not()
    {
        var once = Printable.Of(Hostile);

        Assert.Equal(once, Printable.Of(once));
    }

    [Fact]
    public void A_listing_row_stays_one_row_and_moves_nothing()
    {
        var table = ListingTable.Render([Entries.Plain("Aaa", Hostile), Entries.Plain("Bbb", "plain")]);

        Clean(table);
        Assert.Equal(3, table.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("<U+001B>[2J", table, StringComparison.Ordinal);
    }

    [Fact]
    public void The_report_of_one_entry_moves_nothing()
    {
        var entry = Entries.Full with
        {
            DisplayName = Hostile,
            Description = Reading<string>.Present(Hostile),
            BinaryPath = Reading<string>.Present(@"C:\Vendor\" + Hostile)
        };

        var report = EntryReport.Render(entry, full: true);

        Clean(report);
        Assert.Contains("<U+202E>fdp.exe", report, StringComparison.Ordinal);
    }

    [Fact]
    public void A_comparison_of_somebody_else_s_file_moves_nothing()
    {
        var diff = new SnapshotDiff(
            [new EntryPresence("Aaa", Hostile)], [],
            [new ChangedEntry("Bbb", Hostile, [new FieldDifference("account", DifferenceGroup.Configuration, Hostile, "LocalSystem")], [])],
            [], [Hostile], [new EntryPresence("Ccc", Hostile)],
            new ComparisonCaveats(false, false, false, false, false, false, []),
            new InstancesLeftOut(0, 0));

        Clean(DiffText.Render(diff));
    }

    [Fact]
    public void A_plan_and_what_came_of_it_move_nothing()
    {
        var step = new PlanStep(Hostile, Hostile, StepOperation.Stop, StepReason.Requested);
        var plan = new OperationPlan
        {
            Action = new ServiceAction(ActionKind.Stop, Hostile),
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

        Clean(PlanText.Render(plan));
        Clean(PlanText.Render(run));
    }

    /// <summary>
    /// Nothing that moves a terminal is left once this tool's own line breaks are taken out.
    /// The line breaks of a value are not this tool's, so they count.
    /// </summary>
    private static void Clean(string text)
    {
        var moving = text
            .Replace(Environment.NewLine, string.Empty, StringComparison.Ordinal)
            .Where(character => character < 0x20
                || character is >= (char)0x7F and <= (char)0x9F
                || character is >= (char)0x200E and <= (char)0x200F
                || character is >= (char)0x202A and <= (char)0x202E
                || character is >= (char)0x2066 and <= (char)0x2069)
            .Distinct()
            .ToList();

        Assert.True(
            moving.Count == 0,
            "Text for a terminal carried characters that move it: "
            + string.Join(", ", moving.Select(character => $"U+{(int)character:X4}")));
    }

    private static string Code(int point) => char.ConvertFromUtf32(point);
}
