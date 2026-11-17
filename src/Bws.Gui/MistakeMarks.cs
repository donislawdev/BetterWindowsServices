using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// A wavy line under each mistake in the search box - the owner's third choice of 2026-11-16: the
/// sentence under the box says what is wrong, and until this nothing in the box said where.
///
/// <b>An element laid over the text box in the same cell rather than an adorner</b>, because an
/// adorner draws in the window's adorner layer: it is not part of the row it marks, a host without
/// that layer draws nothing, and a picture of the row would not show it. Over the box it is part of
/// the row, clipped to its cell like the text it marks, and it takes no clicks - the box under it
/// does.
///
/// <b>It asks the box where its characters are, and draws nothing it cannot ask about</b> -
/// <c>TextBox.GetRectFromCharacterIndex</c>, which already accounts for the scroll and the padding.
/// A text the box no longer holds is the text the model read 400 ms ago, so the places would fall
/// under other letters: nothing is drawn until the box holds the text the places came from
/// (<see cref="QueryMistakes"/>). A character the box cannot place, because it was not laid out or
/// lies outside the text, ends that mark rather than the drawing.
///
/// <b>The look is the view's</b> - the brush, the thickness and the size of the wave come in as
/// properties from SearchRow.xaml, where they are tokens (GUI rule 1). A theme file cannot name a
/// type of this assembly (`docs/10`), so there is no style for this one.
/// </summary>
public sealed class MistakeMarks : FrameworkElement
{
    /// <summary>The box whose characters are marked.</summary>
    public static readonly DependencyProperty BoxProperty = DependencyProperty.Register(
        nameof(Box), typeof(TextBox), typeof(MistakeMarks), new PropertyMetadata(null, BoxChanged));

    /// <summary>Where the mistakes stand, and the text they were read from.</summary>
    public static readonly DependencyProperty MistakesProperty = DependencyProperty.Register(
        nameof(Mistakes), typeof(QueryMistakes), typeof(MistakeMarks),
        new FrameworkPropertyMetadata(QueryMistakes.None, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The colour of the marks.</summary>
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(MistakeMarks),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>How thick the line is.</summary>
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(MistakeMarks),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>How far one rise of the wave runs along the line. Zero draws a straight line.</summary>
    public static readonly DependencyProperty WaveProperty = DependencyProperty.Register(
        nameof(Wave), typeof(double), typeof(MistakeMarks),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>How far the wave rises and falls from its middle, and how far that middle stands under the letters.</summary>
    public static readonly DependencyProperty WaveHeightProperty = DependencyProperty.Register(
        nameof(WaveHeight), typeof(double), typeof(MistakeMarks),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public MistakeMarks()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    public TextBox? Box
    {
        get => (TextBox?)GetValue(BoxProperty);
        set => SetValue(BoxProperty, value);
    }

    public QueryMistakes Mistakes
    {
        get => (QueryMistakes)GetValue(MistakesProperty);
        set => SetValue(MistakesProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double Wave
    {
        get => (double)GetValue(WaveProperty);
        set => SetValue(WaveProperty, value);
    }

    public double WaveHeight
    {
        get => (double)GetValue(WaveHeightProperty);
        set => SetValue(WaveHeightProperty, value);
    }

    /// <summary>
    /// The line under each mistake, as two points in this element's own space - left end and
    /// right end, at the height the line runs. Empty when there is nothing to mark, or when the box
    /// holds a text other than the one the places came from. What a test asks, and what
    /// <see cref="OnRender"/> draws.
    /// </summary>
    internal IReadOnlyList<(Point From, Point To)> Lines()
    {
        if (Box is not { } box || Mistakes is not { Places.Count: > 0 } mistakes
            || !string.Equals(box.Text, mistakes.Text, StringComparison.Ordinal))
        {
            return [];
        }

        var lines = new List<(Point From, Point To)>();

        foreach (var place in mistakes.Places)
        {
            var (start, length) = place.GetOffsetAndLength(mistakes.Text.Length);

            if (length < 1)
            {
                continue;
            }

            var first = box.GetRectFromCharacterIndex(start);
            var last = box.GetRectFromCharacterIndex(start + length - 1, trailingEdge: true);

            if (first.IsEmpty || last.IsEmpty)
            {
                continue;
            }

            var y = Math.Max(first.Bottom, last.Bottom) + WaveHeight;

            lines.Add((box.TranslatePoint(new Point(first.Left, y), this), box.TranslatePoint(new Point(last.Right, y), this)));
        }

        return lines;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        if (Stroke is null || Box is null)
        {
            return;
        }

        var lines = Lines();

        if (lines.Count == 0)
        {
            return;
        }

        var pen = new Pen(Stroke, StrokeThickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();

        // THE TEXT'S OWN WINDOW AND NO WIDER - a long query scrolls inside the box, and a mark under
        // a member scrolled out of sight would otherwise be drawn over the magnifier or the
        // sentence at the right end of the field.
        drawingContext.PushClip(new RectangleGeometry(new Rect(RenderSize)));

        foreach (var (from, to) in lines)
        {
            drawingContext.DrawGeometry(null, pen, Shape(from, to));
        }

        drawingContext.Pop();
    }

    /// <summary>
    /// The line from one end to the other: straight when there is no wave, otherwise up and down
    /// by <see cref="WaveHeight"/> every <see cref="Wave"/> along it, ending exactly at the right end.
    /// </summary>
    private StreamGeometry Shape(Point from, Point to)
    {
        // One point every Wave along the line, up and down in turn, then the right end itself.
        var turns = Wave > 0 && WaveHeight > 0 ? (int)Math.Ceiling((to.X - from.X) / Wave) - 1 : 0;
        var points = Enumerable.Range(1, Math.Max(0, turns))
            .Select(at => new Point(from.X + at * Wave, from.Y + (at % 2 == 1 ? -WaveHeight : WaveHeight)))
            .Append(to)
            .ToList();

        var shape = new StreamGeometry();

        using (var line = shape.Open())
        {
            line.BeginFigure(from, isFilled: false, isClosed: false);
            line.PolyLineTo(points, isStroked: true, isSmoothJoin: true);
        }

        shape.Freeze();

        return shape;
    }

    /// <summary>
    /// Follows the box: its text, its scroll and its size all move the characters under a mark, and
    /// none of them changes a property of this element.
    /// </summary>
    private static void BoxChanged(DependencyObject owner, DependencyPropertyChangedEventArgs change)
    {
        var marks = (MistakeMarks)owner;

        if (change.OldValue is TextBox before)
        {
            before.TextChanged -= marks.Moved;
            before.SizeChanged -= marks.Moved;
            before.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(marks.Moved));
        }

        if (change.NewValue is TextBox after)
        {
            after.TextChanged += marks.Moved;
            after.SizeChanged += marks.Moved;
            after.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(marks.Moved));
        }

        marks.InvalidateVisual();
    }

    private void Moved(object sender, EventArgs e) => InvalidateVisual();
}
