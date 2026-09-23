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
    /// What this asserts is the shape that repair depends on: every entry is one of the three kinds,
    /// a heading opens each run, and the selector tells them apart - because a selector that
    /// answered the same style for two of them would put a tick box on a heading.
    ///
    /// <b>THREE KINDS SINCE 2026-08-25, and the third is the way back:</b> what a person chooses
    /// here is written to disk, so without an item that restores the usual columns the only road
    /// back from a layout somebody no longer wants is turning columns off one at a time.
    /// </summary>
    [Fact]
    public void The_picker_is_a_submenu_per_group_and_every_column_is_inside_exactly_one()
    {
        var bar = new ColumnBar();

        // FOUR GROUPS AND THE WAY BACK, WHICH IS THE POINT OF THE SHAPE. The flat list was 32
        // items against a menu capped at 420 units, and counted from outside on 2026-09-05 that
        // showed thirteen and hid nineteen - see ColumnGroup.
        Assert.Equal(5, bar.Entries.Count);
        Assert.IsType<ColumnGroup>(bar.Entries[0]);

        // LAST, because it is about the whole list rather than about one column. A restore in the
        // middle of the groups would read as a column somebody could turn on.
        Assert.IsType<ColumnReset>(bar.Entries[^1]);

        var inside = new List<ColumnChoice>();

        // Every entry is one of the two kinds - a third would fall through the selector and wear
        // whatever the menu's default is.
        foreach (var entry in bar.Entries)
        {
            if (entry is ColumnGroup group)
            {
                Assert.NotEmpty(group.Label);
                Assert.NotEmpty(group.Choices);

                inside.AddRange(group.Choices);
            }
            else
            {
                Assert.NotEmpty(Assert.IsType<ColumnReset>(entry).Label);
            }
        }

        // EVERY COLUMN IS REACHABLE, AND THIS BECAME WORTH ASSERTING THE DAY THE LIST STOPPED
        // BEING FLAT. A flat menu held every choice by construction, so a column going missing was
        // arithmetic anybody could see in the count. Nested, a choice that never lands in a group
        // is simply not in the menu - the picker still opens, still looks right, and one column can
        // no longer be turned on by anyone.
        Assert.Equal(bar.Choices.Count, inside.Count);
        Assert.Equal(bar.Choices.OrderBy(choice => choice.Label, StringComparer.Ordinal), inside.OrderBy(choice => choice.Label, StringComparer.Ordinal));

        var styles = new Bws.Gui.ColumnEntryStyles();

        // FORCED, AND WITHOUT THIS LINE THIS TEST WAS GREEN BECAUSE OF ITS NEIGHBOURS. The selector
        // finds its two styles through the container's resource lookup, which falls back to the
        // application - so with no dictionaries merged it returns null for both and the assertions
        // below fail. Run on its own, before this line, it failed against a product that works -
        // inside the class it passed, because another test had already merged them. Found on
        // 2026-08-12 when two tests added elsewhere changed the order, which is the same way WpfHost
        // records this arrangement biting once before.
        _ = WpfHost.Resources;

        WpfHost.On(() =>
        {
            // A real container, because the selector reads the styles out of whatever tree it is
            // handed - a null container is the case where it silently returns nothing.
            var container = new System.Windows.Controls.MenuItem();

            var forGroup = styles.SelectStyle(bar.Entries[0], container);
            var forChoice = styles.SelectStyle(bar.Choices[0], container);
            var forReset = styles.SelectStyle(bar.Entries[^1], container);

            Assert.NotNull(forGroup);
            Assert.NotNull(forChoice);
            Assert.NotNull(forReset);
            Assert.NotSame(forGroup, forChoice);

            // THE THIRD IS ITS OWN STYLE RATHER THAN A CHOICE'S, and the difference is not
            // cosmetic: a choice is a tick box bound to IsShown, so the way back wearing that style
            // would come up as a check mark that answers nothing.
            Assert.NotSame(forReset, forChoice);
            Assert.NotSame(forReset, forGroup);
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
    /// <b>The second half is what makes the grouping worth having.</b> Four headings over a
    /// catalogue this size help - the number is asserted in ColumnGuards and deliberately not
    /// repeated here, because a count of a growing set written in prose goes stale and nothing
    /// ever comes back for it. A heading over one item is a category pretending to be one, and
    /// since 2026-09-05 it costs more than a line: each heading is now a submenu, so a group of one
    /// is a whole opening and closing to reach a single tick box.
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
