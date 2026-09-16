using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The list under the search box as the window builds it: what it says, what it looks like, and
/// what it must never be.
///
/// <b>Three claims, three different instruments, and the difference is the point.</b> A sentence
/// for every field is asked of the language and the language file - a field added without one
/// would reach the screen as its own key, because <c>Texts.Of</c> answers a missing key with the
/// key. The chosen row's colour is asked of a PIXEL, because a setter in a trigger can be there and
/// reach nothing (`docs/10` trap 6). The hover colour is asked of the trigger only, because a pixel
/// under a pointer needs a pointer, and <see cref="RowStateGuards"/> has drawn the same line since
/// 2026-08-05 - the hover pixel is <c>tools/gui-probe/check.ps1</c>'s, with a person.
///
/// <b>What none of this measures, said rather than left to be assumed:</b> the popup itself. It is
/// a window of its own on screen and these tests show none, so whether a click on a row keeps the
/// keyboard in the box and whether moving the window closes it are the live window's questions -
/// section 6 of `docs/PROJEKT-PODPOWIEDZI-20260915.md`.
/// </summary>
public sealed class SuggestionGuards
{
    private const int Width = 400;

    private const int Height = 200;

    /// <summary>Well inside a row and past the end of a short word, where only the row's face is painted.</summary>
    private const int SampleX = 360;

    [Fact]
    public void Every_field_of_the_language_has_a_sentence_beside_it_and_no_sentence_names_a_field_that_is_not_there()
    {
        var fields = QueryFields.Names.ToHashSet(StringComparer.Ordinal);
        var explained = Meanings.Fields.Select(meaning => meaning.Word).ToHashSet(StringComparer.Ordinal);

        Assert.True(
            fields.SetEquals(explained),
            "The fields with a sentence and the fields of the language are not the same list. Missing: ["
            + string.Join(", ", fields.Except(explained)) + "], invented: ["
            + string.Join(", ", explained.Except(fields)) + "]. A field without a sentence shows its key "
            + "on screen, because Texts.Of answers a missing key with the key.");

        var words = QueryFields.ReservedWords.ToHashSet(StringComparer.Ordinal);

        Assert.True(words.SetEquals(Meanings.Words.Select(meaning => meaning.Word)), "The three reserved words and their sentences drifted apart.");

        foreach (var meaning in Meanings.Fields.Concat(Meanings.Words))
        {
            var sentence = Texts.Of(meaning.Key);

            Assert.False(
                sentence == meaning.Key || string.IsNullOrWhiteSpace(sentence),
                $"{meaning.Word} has no sentence: {meaning.Key} is not in the language file, so the key itself would be shown.");
        }
    }

    /// <summary>
    /// The four fields that send the window to read every file, or to ask the manager about every
    /// entry, say so in the one place somebody decides whether to write them. Decision 11.
    /// </summary>
    [Theory]
    [InlineData("signed")]
    [InlineData("publisher")]
    [InlineData("requiredby")]
    [InlineData("memory")]
    public void A_field_that_costs_a_second_reading_says_so_beside_its_name(string field)
    {
        var meaning = Meanings.Fields.Single(entry => entry.Word == field);
        var sentence = Texts.Of(meaning.Key);

        Assert.True(
            sentence.Contains("every file", StringComparison.Ordinal)
                || sentence.Contains("each entry", StringComparison.Ordinal)
                || sentence.Contains("each process", StringComparison.Ordinal),
            $"{field} is a second-phase field and its sentence does not say what it costs: \"{sentence}\".");
    }

    /// <summary>
    /// `docs/10` trap 8, asked of built elements rather than of the markup: a ListBox and each of
    /// its items are focusable and tab stops by default, and the first click on a focusable thing
    /// in the popup would take the keyboard out of the box and close the list under the pointer.
    /// </summary>
    [Fact]
    public void Nothing_in_the_list_under_the_box_can_take_the_keyboard()
    {
        var focusable = WpfHost.On(() =>
        {
            var (surface, _) = Built(chosen: -1);

            return Descendants(surface)
                .Where(element => element is UIElement { Focusable: true })
                .Select(element => element.GetType().Name)
                .ToList();
        });

        Assert.True(
            focusable.Count == 0,
            "Something in the list under the box can take focus: [" + string.Join(", ", focusable)
            + "]. The keyboard has to stay in the box while the list is open, or a click on a row "
            + "closes the list before the click lands.");
    }

    [Fact]
    public void The_box_keeps_the_automation_id_every_probe_looks_for_and_the_list_has_one_of_its_own()
    {
        var window = WpfHost.Window();

        var (box, list) = WpfHost.On(() => (
            UIElementAutomationPeer.CreatePeerForElement(window.Search.Box)!.GetAutomationId(),
            AutomationProperties.GetAutomationId(window.Search.List)));

        WpfHost.On(window.Close);

        // Four live probes find the field by this name and ten write to it - changing it is
        // changing them. The design kept the field and gave the list a name of its own.
        Assert.Equal("QueryBox", box);
        Assert.Equal("Suggestions", list);
    }

    /// <summary>
    /// A probe, and a screen reader, read the WORD from a row - what a person would type - and
    /// the sentence beside it stays in the tree as a child, which is why the row is a
    /// DataTemplate and not drawn inside the item's ControlTemplate (`docs/10` trap 26).
    /// </summary>
    [Fact]
    public void A_row_is_named_for_its_word_and_keeps_its_sentence_in_the_automation_tree()
    {
        var (names, sentences) = WpfHost.On(() =>
        {
            var (_, items) = Built(chosen: -1);

            return (
                items.Select(item => UIElementAutomationPeer.CreatePeerForElement(item)!.GetName()).ToList(),
                items.Select(item => Descendants(item).OfType<TextBlock>().Select(block => block.Text).ToList()).ToList());
        });

        Assert.Equal(["status", "start", "sddl"], names);
        Assert.All(sentences, texts => Assert.Equal(2, texts.Count));
        Assert.Equal(Texts.Of("gui.suggest.field.status"), sentences[0][1]);
    }

    [Fact]
    public void The_chosen_row_is_painted_in_the_selected_surface_and_the_others_are_not()
    {
        var (chosen, other) = WpfHost.On(() =>
        {
            var (surface, items) = Built(chosen: 1);

            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(surface);

            return (At(bitmap, SampleX, MiddleOf(items[1], surface)), At(bitmap, SampleX, MiddleOf(items[0], surface)));
        });

        Assert.Equal(WpfHost.Declared("SurfaceSelected"), chosen);
        Assert.NotEqual(WpfHost.Declared("SurfaceSelected"), other);
        Assert.Equal(WpfHost.Declared("SurfacePanel"), other);
    }

    /// <summary>
    /// The hover state, asked of the trigger and not of a pixel - a pixel under a pointer needs a
    /// pointer, and the line RowStateGuards drew on 2026-08-05 holds here. What can be asked is
    /// that the trigger exists, paints the face, paints it in the hover surface, and stands
    /// BEFORE the selection trigger so that selection wins on the one row that is both.
    /// </summary>
    [Fact]
    public void A_row_under_the_pointer_wears_the_hover_surface_by_its_own_trigger_and_selection_wins_over_it()
    {
        var (hoverAt, hoverBrush, hoverTarget, selectedAt) = WpfHost.On(() =>
        {
            var template = Template("SuggestionItem");
            var triggers = template.Triggers.OfType<Trigger>().ToList();

            var hover = triggers.FindIndex(trigger => trigger.Property == UIElement.IsMouseOverProperty);
            var selected = triggers.FindIndex(trigger => trigger.Property == ListBoxItem.IsSelectedProperty);
            var setter = hover < 0 ? null : triggers[hover].Setters.OfType<Setter>().SingleOrDefault(s => s.Property == Border.BackgroundProperty);

            return (hover, (setter?.Value as SolidColorBrush)?.Color, setter?.TargetName, selected);
        });

        Assert.True(hoverAt >= 0, "The item template has no trigger on IsMouseOver, so a row under the pointer looks like every other row.");
        Assert.Equal("Face", hoverTarget);
        Assert.Equal(WpfHost.Declared("SurfaceHover"), hoverBrush);
        Assert.True(selectedAt > hoverAt, "The selection trigger stands before the hover one, so pointing at the chosen row takes its colour away.");
    }

    [Fact]
    public void The_list_is_capped_at_the_height_of_a_menu_and_the_row_face_takes_the_pointer()
    {
        var (maxHeight, menu, face) = WpfHost.On(() =>
        {
            var list = (Style)WpfHost.Resources["SuggestionList"];
            var cap = list.Setters.OfType<Setter>().Single(setter => setter.Property == FrameworkElement.MaxHeightProperty).Value;

            var (_, items) = Built(chosen: -1);
            var border = Descendants(items[0]).OfType<Border>().First();

            return ((double)cap, (double)WpfHost.Resources["HeightMenu"], border.Background);
        });

        Assert.Equal(menu, maxHeight);

        // `docs/10` trap 11: a transparent brush takes the pointer and no brush does not. Without
        // it hover works over the letters only.
        Assert.NotNull(face);
    }

    /// <summary>A list of three rows, arranged on the popup's own surface, with the row at the given index chosen.</summary>
    private static (FrameworkElement Surface, IReadOnlyList<ListBoxItem> Items) Built(int chosen)
    {
        var resources = WpfHost.Resources;

        var rows = new[] { "status", "start", "sddl" }
            .Select(word => new Suggestion(word, Texts.Of("gui.suggest.field." + word), 0..0, word + ":"))
            .ToList();

        var list = new ListBox
        {
            ItemsSource = rows,
            Style = (Style)resources["SuggestionList"],
            ItemContainerStyle = (Style)resources["SuggestionItem"],
            ItemTemplate = (DataTemplate)resources["SuggestionRow"]
        };

        Grid.SetIsSharedSizeScope(list, true);

        var panel = new Border { Style = (Style)resources["SuggestionPanel"], Child = list };
        var surface = new Grid { Resources = resources, Width = Width, Height = Height };

        surface.SetResourceReference(Panel.BackgroundProperty, "ApplicationBackgroundBrush");
        surface.Children.Add(panel);

        surface.Measure(new Size(Width, Height));
        surface.Arrange(new Rect(0, 0, Width, Height));
        surface.UpdateLayout();

        var items = Enumerable.Range(0, rows.Count)
            .Select(index => list.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        Assert.True(items.Count == rows.Count, $"The list realised {items.Count} of {rows.Count} rows.");

        if (chosen >= 0)
        {
            list.SelectedIndex = chosen;
            surface.UpdateLayout();
        }

        return (surface, items);
    }

    private static ControlTemplate Template(string style) =>
        (ControlTemplate)((Style)WpfHost.Resources[style]).Setters.OfType<Setter>()
            .Single(setter => setter.Property == Control.TemplateProperty).Value;

    private static int MiddleOf(FrameworkElement element, Visual surface)
    {
        var top = element.TransformToAncestor(surface).Transform(default).Y;

        Assert.True(element.ActualHeight > 0, "The row was never laid out, so there is no pixel to take.");

        return (int)(top + (element.ActualHeight / 2));
    }

    private static Color At(RenderTargetBitmap bitmap, int x, int y)
    {
        var pixel = new byte[4];

        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);

        return Color.FromRgb(pixel[2], pixel[1], pixel[0]);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            yield return child;

            foreach (var below in Descendants(child))
            {
                yield return below;
            }
        }
    }
}
