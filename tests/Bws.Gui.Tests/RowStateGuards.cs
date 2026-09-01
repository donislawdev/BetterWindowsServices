// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The colours of a row, asked of a PIXEL rather than of a property.
///
/// <b>This is the first guard in this product that renders anything, and it exists because of
/// the fault it would have caught.</b> Since the migration to WPF UI the three row states were
/// setters on <c>DataGridRow.Background</c>, which their template feeds to its border through a
/// TemplateBinding - so every property read said the colour was there, and no pixel ever had it.
/// Their template also carried an always-on trigger writing the same border's Background, and
/// inside a template a trigger outranks a TemplateBinding. The states had been dead since the
/// migration and four instruments agreed they were fine.
///
/// <b>What it renders is composed by WPF, which is what makes the claim honest.</b>
/// RenderTargetBitmap draws the visual tree the same way the screen does, with the real
/// Themes/Values.xaml, Themes/Controls.xaml and Themes/List.xaml read off disk and the real WPF
/// UI dictionaries merged underneath them, in the order App.xaml merges them. It is not a screenshot: it cannot
/// see what the desktop compositor does afterwards, which for a row background is nothing.
///
/// <b>Two of the four states are here and two are not, and that is stated rather than left to be
/// noticed.</b> Selected and moved-a-moment-ago can be set from code, so they are asserted.
/// Hover needs a pointer and focus needs an active window, neither of which exists in a test
/// process - <c>tools/gui-probe/check.ps1</c> remains the instrument for those, and the focus
/// ring gets the structural guard at the end of this file instead.
/// </summary>
public sealed class RowStateGuards
{
    /// <summary>Wide enough that the right-hand side of a row carries no cell and no text.</summary>
    private const int Width = 400;

    private const int Height = 200;

    /// <summary>Where a pixel is taken from: past the only column, so it is row and nothing else.</summary>
    private const int SampleX = 320;

    [Fact]
    public void A_selected_row_is_painted_in_our_colour()
    {
        var painted = Painted(rows => rows[1].IsSelected = true, row: 1);

        Assert.Equal(Declared("SurfaceSelected"), painted);
    }

    [Fact]
    public void A_row_that_moved_a_moment_ago_is_painted_at_all()
    {
        // The state that had nothing at all drawing it: WPF UI's template has a trigger for
        // hover and one for selection, and none for this. Whatever it looked like on screen was
        // an ordinary row.
        var painted = Painted(_ => { }, row: 2, changed: 2);

        Assert.Equal(Declared("SurfaceChanged"), painted);
    }

    /// <summary>
    /// The even claim, and without it the two above are worth much less.
    ///
    /// A guard that only ever asserts a colour IS present passes on a template that paints every
    /// row that colour. This one says an untouched row is neither, so the two above are measuring
    /// a difference rather than a constant. It is the shape backlog 128 asks for by name.
    /// </summary>
    [Fact]
    public void An_untouched_row_is_painted_in_neither_of_them()
    {
        var painted = Painted(_ => { }, row: 0);

        Assert.NotEqual(Declared("SurfaceSelected"), painted);
        Assert.NotEqual(Declared("SurfaceChanged"), painted);
    }

    /// <summary>
    /// A row carries a brush of its own, because a row with none is a hole the pointer falls
    /// through.
    ///
    /// <b>THIS IS A MEASUREMENT TURNED INTO A GUARD, AND THE MEASUREMENT IS WHY IT LOOKS ODD.</b>
    /// A null background takes no mouse hits at all. Before this was understood, the row was
    /// exactly that - and hover and selection worked only because WPF UI's cell template painted a
    /// fully transparent border per cell which DID take hits, twenty of them doing the work of one.
    /// Sampled with tools/gui-probe/row-cost.ps1 across a row: 106 of 603 points landed on a cell
    /// border rather than on the row. Backlog 221.
    ///
    /// <b>Asserted on a row the style has actually been applied to</b>, rather than on the setter
    /// in the markup: the value could equally arrive from the style this one is BasedOn, and what
    /// matters is the brush a real row ends up with rather than which file put it there.
    ///
    /// <b>It says nothing about WHICH brush</b>, on purpose. Transparent is what the list wants
    /// today and a zebra stripe would make it something else - backlog 123 - and neither is a hole.
    /// </summary>
    [Fact]
    public void A_row_carries_a_brush_of_its_own_so_the_pointer_lands_on_it()
    {
        var background = OnAStaThread(() =>
            new DataGridRow { Style = (Style)OurResources()["ListRow"] }.Background);

        Assert.True(
            background is not null,
            "A row styled by ListRow has no background brush at all. A null background is not a "
            + "colour, it is a hole: the row takes no mouse hits, so pointing at one lands on "
            + "whatever is inside the cells and misses everywhere else. Backlog 221 measured that "
            + "as 106 of 603 sampled points.");
    }

    /// <summary>
    /// The focus ring cannot be rendered here - keyboard focus needs a window the desktop has
    /// made active - so what is guarded is the one thing that killed the other three states:
    /// WHERE the setter lives. A ring moved back into a style trigger would leave this red.
    /// </summary>
    [Fact]
    public void The_focus_ring_is_set_from_inside_the_template_rather_than_from_a_style_trigger()
    {
        var style = OnAStaThread(() => (Style)OurResources()["ListRow"]);

        var template = OnAStaThread(() =>
            (ControlTemplate)style.Setters.OfType<Setter>()
                .Single(setter => setter.Property == Control.TemplateProperty)
                .Value);

        var triggers = OnAStaThread(() => template.Triggers
            .OfType<Trigger>()
            .Where(trigger => trigger.Property == UIElement.IsKeyboardFocusWithinProperty)
            .ToList());

        Assert.True(
            triggers.Count == 1,
            "The row template carries no trigger on IsKeyboardFocusWithin, so nothing shows where "
            + "the keyboard is. WCAG 2.2 SC 1.4.11 asks for a focus indicator, and ADR-24 removed "
            + "the only one this list had when it took the box off the focused cell.");

        Assert.True(
            triggers[0].Setters.OfType<Setter>().Any(setter => setter.TargetName == "FocusOutline"),
            "The focus trigger no longer targets the outline inside the template. A setter that "
            + "targets the row instead reaches the same dead end the three colours reached.");
    }

    /// <summary>
    /// One rendered row, as a colour.
    ///
    /// The grid is built the way MainWindow.xaml builds it - the same three styles by name - so
    /// this cannot pass on an arrangement the product does not use.
    /// </summary>
    private static Color Painted(Action<IReadOnlyList<DataGridRow>> arrange, int row, int changed = -1)
        => OnAStaThread(() =>
        {
            var resources = OurResources();

            var entries = new[] { "Alpha", "Beta", "Gamma", "Delta" }
                .Select(name => EntryRow.Of(Rows.Entry(name)))
                .ToList();

            if (changed >= 0)
            {
                entries[changed].RecentlyChanged = true;
            }

            var grid = new DataGrid
            {
                ItemsSource = entries,
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserSortColumns = false,
                CanUserResizeColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                SelectionMode = DataGridSelectionMode.Single,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Style = (Style)resources["ListGrid"],
                RowStyle = (Style)resources["ListRow"],
                CellStyle = (Style)resources["ListCell"]
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Binding = new Binding(nameof(EntryRow.ServiceName)),
                Width = new DataGridLength(100)
            });

            // The window's own arrangement: their background brush underneath, our dictionary in
            // scope so the templates resolve what they refer to.
            var surface = new Grid { Resources = resources, Width = Width, Height = Height };
            surface.SetResourceReference(Panel.BackgroundProperty, "ApplicationBackgroundBrush");
            surface.Children.Add(grid);

            surface.Measure(new Size(Width, Height));
            surface.Arrange(new Rect(0, 0, Width, Height));
            surface.UpdateLayout();

            var containers = Enumerable
                .Range(0, entries.Count)
                .Select(index => grid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow)
                .Where(container => container is not null)
                .Select(container => container!)
                .ToList();

            Assert.True(
                containers.Count == entries.Count,
                $"The grid realised {containers.Count} of {entries.Count} rows, so a claim about "
                + "row number " + row + " would be a claim about nothing.");

            arrange(containers);

            // A second pass, because the arrangement above is what the triggers answer to.
            surface.UpdateLayout();

            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(surface);

            var target = containers[row];
            var top = target.TransformToAncestor(surface).Transform(default).Y;
            var middle = (int)(top + (target.ActualHeight / 2));

            Assert.True(
                target.ActualHeight > 0 && middle < Height,
                $"Row {row} was laid out at {top} with height {target.ActualHeight}, which is not "
                + "somewhere a pixel can be taken from.");

            return At(bitmap, SampleX, middle);
        });

    /// <summary>Everything the window merges, in the order App.xaml merges it - see <see cref="WpfHost"/>.</summary>
    private static ResourceDictionary OurResources() => WpfHost.Resources;

    /// <summary>The colour the theme declares under a name, so no expected value is written twice.</summary>
    private static Color Declared(string name) => WpfHost.Declared(name);

    private static Color At(RenderTargetBitmap bitmap, int x, int y)
    {
        var pixel = new byte[4];

        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);

        return Color.FromRgb(pixel[2], pixel[1], pixel[0]);
    }

    /// <summary>WPF needs a single threaded apartment and xUnit does not provide one - <see cref="WpfHost"/>.</summary>
    private static T OnAStaThread<T>(Func<T> work) => WpfHost.On(work);
}
