using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The bar of three positions over the search box - services, drivers, or both.
///
/// <b>Its own file since 2026-08-25, and the size ratchet is what asked.</b> WindowGuards crossed
/// five hundred lines when this arrived, and the seam it pointed at is real: that file is about
/// whether a window can be built at all, and this is about one control on it.
///
/// <b>What no guard here can say:</b> what any of it looks like. The owner rejected two versions of
/// this bar on 2026-08-19 while every test in this project passed over both.
/// </summary>
public sealed class ScopeBarGuards
{
    /// <summary>
    /// THE WHITE BORDER ON THE SCOPE BAR IS A KEYBOARD RING AGAIN, WHICH IS WHAT IT WAS ALWAYS
    /// MEANT TO BE - the owner's complaint of 2026-08-19, answered at its cause on 2026-08-25.
    ///
    /// <b>What was wrong.</b> The ring was a template trigger on IsKeyboardFocused, and that
    /// property says whether the control has keyboard focus - not how it got it. Clicking a
    /// RadioButton focuses it, so an ordinary mouse click lit the white border. The owner picked
    /// this out of four candidates when asked what "the switching" meant, and the other three were
    /// offered beside it.
    ///
    /// <b>Why a focus visual is the answer rather than a smaller trigger.</b> The framework draws a
    /// FocusVisualStyle only while somebody is actually working the keyboard, which is the
    /// distinction no property on the control can make. `docs/11` 9.1 is why the ring may not simply
    /// be deleted: an administrator moving by Tab has to see where they are.
    ///
    /// <b>Read off the style rather than off a realised control</b>, because containers inside an
    /// ItemsControl do not exist until a window has been laid out, and what is asserted here is a
    /// decision rather than a rendering. <b>What this does NOT claim, said plainly: nothing here
    /// sees a screen.</b> That the ring stays dark under a real mouse click is for a person to look
    /// at, and no probe in this project can click with a real mouse from a background session.
    /// </summary>
    [Fact]
    public void The_scope_ring_is_drawn_for_the_keyboard_rather_than_for_a_click()
    {
        var window = WpfHost.Window();

        // ALL OF IT ON THE INTERFACE THREAD, INCLUDING THE READING. A Style belongs to the thread
        // that built it and throws on the property that lists its setters - which is how the first
        // version of this failed, for a reason having nothing to do with what it was asking.
        var (hasRing, ringIsAStyle, onKeyboardFocus, onChecked) = WpfHost.On(() =>
        {
            var style = (Style)window.Scope.FindResource("ScopePosition");
            var setters = style.Setters.OfType<Setter>().ToList();
            var ring = setters.Find(setter => setter.Property == FrameworkElement.FocusVisualStyleProperty);

            var template = (ControlTemplate)setters
                .First(setter => setter.Property == Control.TemplateProperty).Value;

            var triggers = template.Triggers.OfType<Trigger>().ToList();

            return (
                ring is not null,
                ring?.Value is Style,
                triggers.Exists(trigger => trigger.Property == UIElement.IsKeyboardFocusedProperty),
                triggers.Exists(trigger =>
                    trigger.Property == System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty));
        });

        Assert.True(hasRing, "The positions have no focus visual, so the keyboard has nowhere to show.");
        Assert.True(ringIsAStyle, "The focus visual is not a style, so nothing would be drawn.");
        Assert.False(onKeyboardFocus, "The ring is back on IsKeyboardFocused, which lights it for a click.");

        // The one that IS the template's job stays: which position is on.
        Assert.True(onChecked);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Every position draws its count beside its label, and its NAME in the automation tree is
    /// still the label alone - point 8(c) of `docs/11` 2.14, both halves. The count is in the
    /// template rather than in the content for exactly the second half: three probes find a
    /// position by the name "Services", and content that became a panel would have taken that
    /// name away, as the Filters toggle's comment records it once did.
    ///
    /// <b>Read off a laid-out window</b>, because a template draws nothing until a layout pass
    /// runs and the number is a binding - which is the kind of thing this project has found dead
    /// with every test green.
    /// </summary>
    [Fact]
    public async Task Every_position_draws_its_count_and_keeps_its_name()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler"), Rows.Stopped("BITS"), Rows.Driver("disk")),
            new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        var positions = WpfHost.On(() =>
        {
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;
            window.Show();
            window.UpdateLayout();

            return Descendants(window.Scope)
                .OfType<RadioButton>()
                .Select(position => (
                    Name: System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(position)!.GetName(),
                    Drawn: Descendants(position).OfType<TextBlock>().Select(text => text.Text).ToArray(),
                    Choice: (ScopeChoice)position.DataContext))
                .ToArray();
        });

        Assert.Equal(3, positions.Length);

        foreach (var (name, drawn, choice) in positions)
        {
            Assert.Equal(WpfHost.On(() => choice.Label), name);
            Assert.Contains(WpfHost.On(() => choice.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)), drawn);
        }

        WpfHost.On(window.Close);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var at = 0; at < VisualTreeHelper.GetChildrenCount(root); at++)
        {
            var child = VisualTreeHelper.GetChild(root, at);

            yield return child;

            foreach (var under in Descendants(child))
            {
                yield return under;
            }
        }
    }
}
