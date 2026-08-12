using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The column PICKER - what a person opens to choose what the list shows.
///
/// <b>Its own file since 2026-08-12, and the size ratchet is what asked.</b> ColumnGuards crossed
/// five hundred lines and the seam it pointed at is real: what a COLUMN is - its identifier, what
/// its cell says, what it sorts by - is a different subject from what the PICKER is, which is a
/// menu of headings and tick boxes with an automation tree that has already been broken once.
///
/// <b>What that break was, because it is the reason these tests exist at all:</b> the picker was
/// grouped with GroupStyle, which looked right on screen and took the whole menu out of the
/// automation tree. With it open, the real window offered twelve togglable elements - every one a
/// filter chip - and none of the seventeen columns. WPF puts a GroupItem between a menu and its
/// items and the menu peer does not reach through it, so a screen reader sees what the probe saw.
/// </summary>
public sealed class ColumnPickerGuards
{
    /// <summary>
    /// The picker's list is headings and choices in one flat sequence, and each wears its own style.
    ///
    /// <b>Flat rather than grouped, and that is a repair with a measurement behind it.</b> The
    /// first version grouped the menu with <c>GroupStyle</c>, which looked right and took the whole
    /// menu out of the automation tree: with the picker open, the real window offered twelve
    /// togglable elements, every one of them a filter chip, and none of the seventeen columns. WPF
    /// puts a <c>GroupItem</c> between a menu and its items and the menu's peer does not reach
    /// through it, so a screen reader sees exactly what the probe saw.
    ///
    /// What this asserts is the shape that repair depends on: every entry is one of two kinds, a
    /// heading opens each run, and the selector tells them apart - because a selector that answered
    /// the same style for both would put a tick box on a heading.
    /// </summary>
    [Fact]
    public void The_picker_is_headings_and_choices_in_one_list_each_with_its_own_style()
    {
        var bar = new ColumnBar();

        Assert.Equal(bar.Choices.Count + 4, bar.Entries.Count);
        Assert.IsType<ColumnHeading>(bar.Entries[0]);

        // Every heading says something, and every entry is one of the two kinds - a third kind
        // would fall through the selector and wear whatever the menu's default is.
        foreach (var entry in bar.Entries)
        {
            if (entry is ColumnHeading heading)
            {
                Assert.NotEmpty(heading.Label);
            }
            else
            {
                Assert.IsType<ColumnChoice>(entry);
            }
        }

        var styles = new Bws.Gui.ColumnEntryStyles();

        // FORCED, AND WITHOUT THIS LINE THIS TEST WAS GREEN BECAUSE OF ITS NEIGHBOURS. The selector
        // finds its two styles through the container's resource lookup, which falls back to the
        // application - so with no dictionaries merged it returns null for both and the assertions
        // below fail. Run on its own, before this line, it failed against a product that works;
        // inside the class it passed, because another test had already merged them. Found on
        // 2026-08-12 when two tests added elsewhere changed the order, which is the same way WpfHost
        // records this arrangement biting once before.
        _ = WpfHost.Resources;

        WpfHost.On(() =>
        {
            // A real container, because the selector reads the styles out of whatever tree it is
            // handed - a null container is the case where it silently returns nothing.
            var container = new System.Windows.Controls.MenuItem();

            var forHeading = styles.SelectStyle(bar.Entries[0], container);
            var forChoice = styles.SelectStyle(bar.Choices[0], container);

            Assert.NotNull(forHeading);
            Assert.NotNull(forChoice);
            Assert.NotSame(forHeading, forChoice);
        });
    }

    /// <summary>
    /// Every column sits under a heading in the picker, and no heading holds one column.
    ///
    /// <b>The first half is what a default would have hidden.</b> A column added without a group
    /// would fall under whichever heading was least wrong, silently, and the person looking for it
    /// would find it somewhere they had no reason to look. A missing group is a failed test here
    /// instead.
    ///
    /// <b>The second half is what makes the grouping worth having.</b> Four headings over seventeen
    /// items help; a heading over one item is a divider pretending to be a category, and it costs a
    /// line of a menu that already needs a scrollbar.
    /// </summary>
    [Fact]
    public void Every_column_sits_under_a_heading_and_no_heading_holds_one_column()
    {
        var bar = new ColumnBar();

        var ungrouped = bar.Choices
            .Where(choice => string.IsNullOrEmpty(choice.Group))
            .Select(choice => choice.Label)
            .ToList();

        Assert.True(
            ungrouped.Count == 0,
            "These columns have no heading in the picker, so nobody decided where a person would "
            + "look for them: " + string.Join(", ", ungrouped));

        var thin = bar.Choices
            .GroupBy(choice => choice.Group, StringComparer.Ordinal)
            .Where(group => group.Count() < 2)
            .Select(group => group.Key)
            .ToList();

        Assert.True(
            thin.Count == 0,
            "These headings hold a single column, which is a divider with a name rather than a "
            + "group: " + string.Join(", ", thin));
    }

}
