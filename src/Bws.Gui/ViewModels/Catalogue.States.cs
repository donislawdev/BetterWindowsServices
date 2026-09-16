using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The states of a component the sheet can put it into - wrong and chosen - each read off the
/// style itself rather than off a list of keys, so a component that learns a state appears in
/// its column with no edit anywhere.
///
/// Its own file since 2026-09-16, when the chosen column arrived and Catalogue.cs went past the
/// length ceiling. The seam is the subject: this file decides WHICH state a style has, the
/// samples file decides how a built element is measured and drawn.
/// </summary>
public static partial class Catalogue
{
    /// <summary>
    /// The component checked, selected or switched on, where its style says it can be.
    ///
    /// <b>Read off the style like the wrong state, but one level deeper.</b> A wrong state lives
    /// in a style trigger on <c>Validation.HasError</c>. A chosen state lives where WPF UI's
    /// templates put it - in the CONTROL TEMPLATE's triggers on <c>IsChecked</c> or
    /// <c>IsSelected</c> - or, for a menu item, in a setter that binds <c>IsChecked</c> to the
    /// model. So this walks the style's own triggers, the triggers of the template it sets, and
    /// its setters, up the BasedOn chain. A style that gains any of the three appears in this
    /// column with no edit here.
    ///
    /// <b>Put into the state the way the product puts it there</b>: the property is set, and the
    /// template draws whatever it draws for it. Nothing here paints a highlight.
    /// </summary>
    private static Sample Chosen(string target, Style style, Limits limits)
    {
        if (!Chooses(style))
        {
            return Sample.None(NoSuchState);
        }

        var made = Make(target, style, Short, enabled: true, limits);

        switch (made.Element)
        {
            case ToggleButton toggle:
                toggle.IsChecked = true;

                return made;

            case ListBoxItem item:
                item.IsSelected = true;

                return made;

            case MenuItem:
                // A menu item with no menu around it has the TopLevelItem role - the default of
                // MenuItem.Role - and the library's template for that role draws no check mark,
                // because a top-level item on a menu bar is never checkable. The product's items
                // sit in a context menu, where they are SubmenuItems and the mark is drawn. A
                // sample with IsChecked set would show the same empty box as the one beside it
                // and call it chosen - measured on 2026-09-16 rather than assumed.
                return Sample.None("its check mark is drawn only inside an open menu - open the column picker on the window and look");

            case null:
                return made;

            default:
                return Sample.None($"has a chosen state, but nothing here knows how to choose a {target}");
        }
    }

    /// <summary>Whether a style, its template or a base of either answers to being checked or selected.</summary>
    private static bool Chooses(Style style)
    {
        static bool IsChoosing(DependencyProperty property) =>
            property == ToggleButton.IsCheckedProperty
            || property == MenuItem.IsCheckedProperty
            || property == Selector.IsSelectedProperty
            || property == ListBoxItem.IsSelectedProperty;

        static bool Anywhere(TriggerCollection triggers) =>
            triggers.OfType<Trigger>().Any(trigger => IsChoosing(trigger.Property))
            || triggers.OfType<MultiTrigger>().Any(multi => multi.Conditions.Any(condition => IsChoosing(condition.Property)));

        for (var at = style; at is not null; at = at.BasedOn)
        {
            if (Anywhere(at.Triggers))
            {
                return true;
            }

            foreach (var setter in at.Setters.OfType<Setter>())
            {
                if (IsChoosing(setter.Property))
                {
                    return true;
                }

                if (setter.Property == Control.TemplateProperty && setter.Value is ControlTemplate template && Anywhere(template.Triggers))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The component holding something wrong, where its style says it can.
    ///
    /// <b>READ OFF THE STYLE RATHER THAN OFF A LIST OF KEYS, which is the property that keeps
    /// this column true after the interface is rebuilt.</b> A component has a wrong state when
    /// its style triggers on <c>Validation.HasError</c> - the framework's own flag, and the only
    /// one a theme file can trigger on without naming a type of this assembly (`docs/10` trap
    /// 10). So a style that gains such a trigger appears in this column with no edit here, and one
    /// that loses it drops out the same way. A dash for the rest, said once in the note at the top.
    ///
    /// <b>Put into the state the way the product puts it there</b>: the sample's text is bound and
    /// the binding is marked invalid, so <c>Validation.HasError</c> goes true on the control and
    /// the style's own trigger draws whatever it draws. Nothing here paints an edge - a sample the
    /// sheet coloured itself would be a picture of this file rather than of the component.
    /// </summary>
    private static Sample Wrong(string target, Style style, Limits limits)
    {
        if (!Triggers(style).Any(trigger => trigger.Property == Validation.HasErrorProperty))
        {
            return Sample.None(NoSuchState);
        }

        var made = Make(target, style, Short, enabled: true, limits);

        if (made.Element is not TextBox box)
        {
            // The one control this table knows how to hand something wrong to. A style over
            // another type that learns to be wrong is a row here saying so, rather than a sample
            // of a state that was never entered - the same honesty as the disabled column.
            return Sample.None($"has a wrong state, but nothing here knows how to make a {target} wrong");
        }

        box.SetBinding(TextBox.TextProperty, new Binding(nameof(Held.Text)) { Source = new Held(Short) });

        var expression = BindingOperations.GetBindingExpression(box, TextBox.TextProperty)!;

        Validation.MarkInvalid(expression, new ValidationError(new NeverRight(), expression, Short, null));

        return Sample.Of(box);
    }

    /// <summary>Every trigger a style carries, including the ones it inherits through BasedOn.</summary>
    private static IEnumerable<Trigger> Triggers(Style style)
    {
        for (var at = style; at is not null; at = at.BasedOn)
        {
            foreach (var trigger in at.Triggers.OfType<Trigger>())
            {
                yield return trigger;
            }
        }
    }

    /// <summary>Something for a sample's text to be bound to, so that its binding can be marked wrong.</summary>
    private sealed record Held(string Text);

    /// <summary>
    /// A rule that is never satisfied - the shape <c>Validation.MarkInvalid</c> asks for, and
    /// nothing else: the sample is wrong because the sheet says so, not because of what it holds.
    /// </summary>
    private sealed class NeverRight : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo) =>
            new(false, Short);
    }
}
