using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Two marks that mean different things have to LOOK different.
///
/// <b>Written 2026-08-10 after the owner asked what the dots in the start column meant, and the
/// honest answer turned out to be that two of them meant different things and were drawn the
/// same.</b> "Stopped" was a hollow ring in #A0A0A0 and "nobody could read this" was a hollow ring
/// in #9E9E9E - two points of grey per channel apart, at eight pixels across, never side by side.
/// The start column had the same pair for "disabled" and "could not read".
///
/// <c>CellFaces.cs</c> opens by promising that shape and word are two channels and neither is
/// optional, because colour alone fails WCAG 2.2 SC 1.4.1. That promise was kept for every state
/// except the one where the window knows the least.
///
/// <b>WHY THIS IS NOT THE WCAG FORMULA, and the distinction matters.</b> `ContrastGuards` asks
/// whether text can be read against the surface behind it, which is a foreground-on-background
/// question with a published threshold. This asks something else: whether two marks, never seen
/// side by side, can be told apart at all. A plain channel distance answers that and is honest
/// about being a rule of thumb - the alternative was borrowing a number from a formula written for
/// a different question, which is how a threshold ends up looking authoritative and meaning
/// nothing.
/// </summary>
public sealed class MarkDistinctionGuards
{
    /// <summary>
    /// How far apart two colours must be on their widest channel before this will call them
    /// different, when nothing else about the two marks differs.
    ///
    /// Twenty four out of two hundred and fifty five. Measured against what the theme already
    /// carries rather than picked from the air: the pair this guard was written for is 2 apart,
    /// and the closest pair it must NOT complain about - running green against transit amber - is
    /// 147. Anything in that gap would be a judgement call nobody has had to make yet.
    /// </summary>
    private const int Apart = 24;

    /// <summary>
    /// Which codes each column can actually produce, taken from the two switches in CellFaces.
    ///
    /// Written out rather than "every constant on CellShapes", because the two columns do not
    /// share a vocabulary - a status is never "disabled" and a start type is never "paused" - and
    /// comparing marks that can never appear in the same cell would invent complaints.
    /// </summary>
    private static readonly string[] StatusVocabulary =
    [
        CellShapes.Running, CellShapes.Stopped, CellShapes.Paused, CellShapes.Transit, CellShapes.Unknown
    ];

    /// <summary>
    /// The start column, four codes longer since 2026-08-17 - each start type has a mark of its
    /// own now instead of the four of them sharing no mark at all.
    ///
    /// <b><c>Ordinary</c> STAYS, AND IT WAS TAKEN OUT FOR ONE BUILD BEFORE A TEST PUT IT BACK.</b>
    /// The reasoning for removing it was that the column could no longer produce it - which was
    /// true of the four named types and false of the fifth case: a start type the manager gave us
    /// and we have no word for still gets no mark, because a broken ring would say "nobody could
    /// read this" about a reading that succeeded. The list has to match what the switch can return,
    /// and checking that by reading the switch beats reasoning about it.
    /// </summary>
    private static readonly string[] StartVocabulary =
    [
        CellShapes.Missing, CellShapes.Disabled, CellShapes.Unknown, CellShapes.Ordinary,
        CellShapes.StartBoot, CellShapes.StartSystem, CellShapes.StartAutomatic, CellShapes.StartManual
    ];

    /// <summary>
    /// The third column, from 2026-08-12 - backlog 165. Three codes and no more: an entry either
    /// contradicts its start type, does not, or nobody could work it out.
    /// </summary>
    private static readonly string[] AgainstVocabulary =
    [
        CellShapes.Against, CellShapes.Ordinary, CellShapes.Unknown
    ];

    /// <summary>
    /// Which codes each mark can be asked about, by the field it reads.
    ///
    /// <b>A lookup rather than a conditional, and that is the third column paying for itself
    /// already.</b> What stood here was a two way ternary on "is this the status one", which had
    /// no room for a third answer and would have handed the start column's vocabulary to a mark
    /// that cannot produce any of it - comparing shapes that can never appear in the same cell,
    /// which is the thing the comment above this pair exists to prevent.
    /// </summary>
    private static readonly Dictionary<string, string[]> Vocabularies = new(StringComparer.Ordinal)
    {
        ["StatusShape"] = StatusVocabulary,
        ["StartShape"] = StartVocabulary,
        ["AgainstShape"] = AgainstVocabulary
    };

    [Theory]
    [InlineData("StatusMark", "StatusShape")]
    [InlineData("StartMark", "StartShape")]
    [InlineData("AgainstMark", "AgainstShape")]
    public void No_two_marks_differ_only_by_a_shade_of_the_same_colour(string style, string field)
    {
        var marks = Marks(style, field, Vocabularies[field]);
        var complaints = new List<string>();

        foreach (var (first, second) in Pairs(marks.Keys))
        {
            var left = marks[first];
            var right = marks[second];

            if (left.ShapeDiffers(right))
            {
                continue;
            }

            var distance = Math.Max(Distance(left.Fill, right.Fill), Distance(left.Stroke, right.Stroke));

            if (distance < Apart)
            {
                complaints.Add(
                    $"{first} and {second} are the same shape and {distance} apart on their widest " +
                    "channel, so nothing on screen tells them apart");
            }
        }

        Assert.True(
            complaints.Count == 0,
            $"Two marks in {style} mean different things and look the same. Give one of them a " +
            "different shape - a dashed outline, a fill, a size - rather than another shade:" +
            Environment.NewLine + string.Join(Environment.NewLine, complaints));
    }

    [Fact]
    public void The_mark_for_something_nobody_could_read_is_a_broken_ring()
    {
        // The specific answer the rule above was satisfied with, pinned so that a later session
        // cannot satisfy it again by nudging a grey. A broken outline for broken knowledge - and it
        // is the only shape in any of the three columns that is not a whole one.
        //
        // THE PAIRS COME FROM THE LOOKUP RATHER THAN FROM A CONDITIONAL HERE, which is what makes
        // a fourth mark one line rather than an edit to a ternary that already had no room for the
        // third.
        foreach (var (field, vocabulary) in Vocabularies)
        {
            var style = field.Replace("Shape", "Mark", StringComparison.Ordinal);
            var marks = Marks(style, field, vocabulary);

            Assert.True(
                marks["shape.unknown"].Dashes,
                $"{style} draws an unreadable value as a whole outline, like every value it did read.");

            foreach (var (shape, mark) in marks)
            {
                Assert.True(
                    shape == "shape.unknown" || !mark.Dashes,
                    $"{style} draws {shape} broken, which is the mark reserved for what could not be read.");
            }
        }
    }

    /// <summary>
    /// A mark is moved by the field of its OWN column and by nothing else.
    ///
    /// <b>Backlog 166, and the two guards above were green through the whole life of it.</b> The
    /// start type's mark was <c>BasedOn</c> the status mark, so it inherited every one of that
    /// style's triggers - and those read <c>StatusShape</c>, the run state. An entry that was
    /// running hit the "running" trigger, which sets Fill and Stroke - the start column's own
    /// "disabled" trigger then set Stroke and left the Fill where it was. So a service that was
    /// running while set to Disabled wore a GREEN FILLED DOT in the column about its next start -
    /// a column saying something confident and false about the present. Measured on the pixel:
    /// #6CCB5F, the same green as the status dot two columns to its left.
    ///
    /// <b>Why nothing caught it.</b> <see cref="Marks"/> reads <c>declared.Triggers</c> - a style's
    /// OWN triggers - and never walks <c>BasedOn</c>. Inherited triggers were invisible to it, so
    /// the cross-wiring could not appear in any comparison it made. A guard that reads one link of
    /// a chain cannot see what the chain does.
    ///
    /// <b>This is the general rule rather than the instance.</b> Repairing 166 by hand would have
    /// left the next mark free to inherit the same way. What is asserted is the property the theme
    /// has to keep: a column's mark may not change because of a field belonging to another column.
    /// </summary>
    [Theory]
    [InlineData("StatusMark", "StatusShape")]
    [InlineData("StartMark", "StartShape")]
    [InlineData("AgainstMark", "AgainstShape")]
    public void A_mark_is_moved_only_by_the_field_of_its_own_column(string style, string field)
    {
        var foreign = WpfHost.On(() =>
        {
            var complaints = new List<string>();

            // The whole chain, because that is what WPF applies. A style with no triggers of its
            // own is not a style with no triggers.
            for (var declared = (Style?)WpfHost.Resources[style]; declared is not null; declared = declared.BasedOn)
            {
                foreach (var trigger in declared.Triggers.OfType<DataTrigger>())
                {
                    if (trigger.Binding is Binding binding
                        && binding.Path?.Path is { Length: > 0 } path
                        && !string.Equals(path, field, StringComparison.Ordinal))
                    {
                        complaints.Add($"a trigger on {path} = {trigger.Value}");
                    }
                }
            }

            return complaints;
        });

        Assert.True(
            foreign.Count == 0,
            $"{style} draws the column that shows {field}, and something else moves it. Whatever "
            + "that other field says, this mark will repeat it in a column that is not about it - "
            + "which is how a running service came to wear a green dot under its start type:"
            + Environment.NewLine + string.Join(Environment.NewLine, foreign));
    }

    /// <summary>What one shape code makes the mark look like, read out of the theme itself.</summary>
    private sealed record Mark(Color Fill, Color Stroke, bool Dashes, Visibility Shown)
    {
        internal bool ShapeDiffers(Mark other) =>
            Dashes != other.Dashes
            || Shown != other.Shown
            || (Fill.A == 0) != (other.Fill.A == 0);
    }

    /// <summary>
    /// What the style draws for every shape code the column can produce.
    ///
    /// <b>THE VOCABULARY COMES FROM THE CODE, NOT FROM THE XAML, AND THE FIRST VERSION OF THIS
    /// GUARD DID THE OPPOSITE.</b> It collected only the codes that had a trigger - so removing a
    /// trigger removed the shape from the comparison instead of failing it, and the mutation entry
    /// for the status column came back MISSED. That is the exact defect this file was written
    /// about: the status column drew "nobody could read this" with NO TRIGGER AT ALL, letting the
    /// style's own setters paint something indistinguishable from a stopped entry.
    ///
    /// A guard that can only see what somebody remembered to declare cannot notice the thing
    /// nobody declared.
    ///
    /// Read from the merged resources rather than from the file as text, because a trigger that
    /// does not apply and a trigger that is not there look identical in a file and completely
    /// different on screen.
    /// </summary>
    private static Dictionary<string, Mark> Marks(string style, string field, string[] vocabulary) =>
        WpfHost.On(() =>
        {
            var declared = (Style)WpfHost.Resources[style];
            var found = new Dictionary<string, Mark>(StringComparer.Ordinal);

            foreach (var shape in vocabulary)
            {
                var trigger = Chain(declared)
                    .SelectMany(step => step.Triggers.OfType<DataTrigger>())
                    .FirstOrDefault(candidate =>
                        candidate.Binding is Binding binding
                        && binding.Path?.Path == field
                        && (string)candidate.Value == shape);

                found[shape] = Read(declared, trigger);
            }

            return found;
        });

    /// <summary>
    /// A style and everything it is based on, the base first - which is the order WPF applies them
    /// in, so a derived setter laid over a base one wins the way it does on screen.
    ///
    /// <b>Added 2026-08-12, and the mutation registry is what asked for it.</b> When the marks were
    /// split into Themes/Cells.xaml the geometry and the default colours moved into a shared base,
    /// <c>MarkShape</c> - and this file went on reading a style's OWN setters, which were now
    /// empty. The case it exists to catch, a shape with no trigger at all, stopped reading as "the
    /// style's default" and started reading as transparent, so the entry for it came back MISSED
    /// while every test was green. A guard that reads one link of a chain cannot see what the
    /// chain does, which is the same sentence the trigger guard above is written from.
    /// </summary>
    private static IEnumerable<Style> Chain(Style declared)
    {
        var steps = new List<Style>();

        for (var step = (Style?)declared; step is not null; step = step.BasedOn)
        {
            steps.Add(step);
        }

        steps.Reverse();

        return steps;
    }

    /// <summary>
    /// The style's values, with the trigger's laid over them - which is what WPF does. A shape
    /// with no trigger gets the style's values and nothing else, which is exactly the case
    /// this guard exists to catch.
    /// </summary>
    private static Mark Read(Style declared, DataTrigger? trigger)
    {
        var setters = Chain(declared)
            .SelectMany(step => step.Setters.OfType<Setter>())
            .Concat(trigger?.Setters.OfType<Setter>() ?? [])
            .ToList();

        return new Mark(
            ColourOf(setters, Shape.FillProperty),
            ColourOf(setters, Shape.StrokeProperty),
            Last(setters, Shape.StrokeDashArrayProperty) is DoubleCollection dashes && dashes.Count > 0,
            Last(setters, UIElement.VisibilityProperty) is Visibility shown ? shown : Visibility.Visible);
    }

    private static Color ColourOf(List<Setter> setters, DependencyProperty property) =>
        Last(setters, property) is SolidColorBrush brush ? brush.Color : Colors.Transparent;

    private static object? Last(List<Setter> setters, DependencyProperty property) =>
        setters.LastOrDefault(setter => setter.Property == property)?.Value;

    private static int Distance(Color left, Color right) => Math.Max(
        Math.Abs(left.R - right.R),
        Math.Max(Math.Abs(left.G - right.G), Math.Abs(left.B - right.B)));

    private static IEnumerable<(string, string)> Pairs(IEnumerable<string> names)
    {
        var all = names.ToList();

        for (var i = 0; i < all.Count; i++)
        {
            for (var j = i + 1; j < all.Count; j++)
            {
                yield return (all[i], all[j]);
            }
        }
    }
}
