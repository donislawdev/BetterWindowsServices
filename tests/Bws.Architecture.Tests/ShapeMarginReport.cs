using Xunit.Abstractions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Prints the room under every shape ceiling, for tools/margin.ps1 to read.
///
/// That tool counted raw lines itself until 2026-09-23. The day the ceilings moved to lines of code
/// measured by a parser, a PowerShell copy of the measure would have been a second thing to get
/// wrong - and the old counting would have gone on reporting room that is not there. So the tool
/// reads this, and there is one measure with two readers.
///
/// One line per axis, then one per thing at the top of it, five deep:
///   MARGIN|axis|name|unit|largest|ceiling|near now|near recorded
///   MARGIN|item|name|rank|value|where
///
/// <b>It asserts one thing only: that every axis was printed.</b> A report is not a guard - the
/// ceilings are held by <see cref="CodeShapeGuards"/> and <see cref="SizeRatchetGuards"/> - but a
/// report that quietly dropped an axis would read as that axis having room.
/// </summary>
public sealed class ShapeMarginReport(ITestOutputHelper output)
{
    [Fact]
    public void Prints_the_room_under_every_ceiling_for_tools_margin()
    {
        var axes = SizeRatchetGuards.Axes.Concat(CodeShapeGuards.Axes).ToArray();

        foreach (var axis in axes)
        {
            var items = axis.Items();
            var largest = items.Count == 0 ? 0 : items[0].Value;
            output.WriteLine($"MARGIN|axis|{axis.Name}|{axis.Unit}|{largest}|{axis.Ceiling}|{items.Count(item => axis.Near(item.Value))}|{axis.Crowd}");

            foreach (var (item, rank) in items.Take(5).Select((item, index) => (item, index + 1)))
            {
                output.WriteLine($"MARGIN|item|{axis.Name}|{rank}|{item.Value}|{item.Where}");
            }
        }

        Assert.Equal(13, axes.Length);
    }
}
