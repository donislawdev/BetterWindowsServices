using System.Windows;
using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The menu under a right click on a column heading - the owner's ask of 2026-09-05: click Status,
/// choose Running, without learning the query language first.
///
/// <b>Mostly written against the view model rather than against the window, and the split is
/// stated rather than left to be noticed.</b> What a heading OFFERS, whether a tick is lit, and
/// what clicking it writes into the box have no WPF in them, so they are asked here without a
/// desktop. The last test is the exception and has to be: the walk from whatever the pointer was
/// over up to the heading, and the step from a heading to which of our columns it is, exist only
/// inside a real visual tree.
///
/// <b>What is still left to a person</b>: that the menu is legible, lands where the pointer is,
/// and reads as an offer rather than a list of jargon. No test here sees a screen.
/// </summary>
public sealed class ColumnMenuGuards
{
    /// <summary>
    /// Every column that names a field names one this language actually has.
    ///
    /// <b>THE CHEAPEST GUARD IN THIS FILE AND THE ONE MOST WORTH HAVING.</b> The map from column to
    /// field is nineteen lines of two strings, eleven of which agree with each other and eight of
    /// which do not - the start type column is <c>start</c>, the process id is <c>pid</c>, the
    /// signature is <c>signed</c>. A word wrong in that table is not a compiler error and not a
    /// crash: <c>QueryFields.ValuesOf</c> answers with an empty list for a name nobody knows, which
    /// is exactly what it answers for a column that takes free text. So the menu would simply have
    /// no values, and it would look precisely like a column that was never meant to have any.
    /// </summary>
    [Fact]
    public void Every_column_that_names_a_query_field_names_one_the_language_has()
    {
        var named = 0;
        var wrong = new List<string>();

        foreach (var column in Columns.All)
        {
            if (Columns.FieldOf(column.Id) is not { } field)
            {
                continue;
            }

            named++;

            if (!QueryFields.Names.Contains(field, StringComparer.Ordinal))
            {
                wrong.Add($"  {column.Id} says its field is '{field}', which this language does not have");
            }
        }

        // Without this the loop passes over an empty map and proves nothing.
        Assert.True(named > 0, "No column names a field, so the check below saw nothing at all.");

        Assert.True(
            wrong.Count == 0,
            "A column names a query field that does not exist. Nothing fails when this happens - "
            + "the heading simply offers no values, which is indistinguishable from a column that "
            + "was never meant to have any:"
            + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    /// <summary>
    /// The status heading offers the states, and ticking one writes it into the box.
    ///
    /// <b>Both halves, because either alone is a control that lies.</b> A menu that offers the
    /// right words and writes nothing is a dead tick - one that writes without the words being the
    /// language's own writes a query the parser then refuses.
    /// </summary>
    [Fact]
    public void Ticking_a_value_writes_the_member_into_the_query_and_unticking_takes_it_out()
    {
        var text = string.Empty;
        var menu = new ColumnMenu("status", null, () => text, written => text = written);

        Assert.NotEmpty(menu.Values);

        var running = menu.Values.Single(value => value.Label == "running");

        Assert.False(running.IsOn);

        running.IsOn = true;

        // THE WHOLE MEMBER AND NOTHING ELSE, rather than Contains. A menu that wrote !status:running
        // would satisfy Contains and mean the opposite - the box would then be hiding running
        // services while the tick beside "running" was lit. Ticks in this menu narrow TO a value,
        // never away from one, and that is asserted by the text being exactly the member.
        Assert.Equal("status:running", text);
        Assert.True(running.IsOn);

        // AND OUT AGAIN, which is what makes it a tick rather than a one way door. A chip holds no
        // state, so this reads the text back rather than a flag it set itself.
        running.IsOn = false;

        Assert.DoesNotContain("status:running", text, StringComparison.Ordinal);
        Assert.False(running.IsOn);
    }

    /// <summary>
    /// A tick lights for a member somebody typed by hand, which is the `A5` promise from the other
    /// side: the query text is the one true copy and the menu is a view onto it.
    /// </summary>
    [Fact]
    public void A_tick_is_lit_by_the_query_text_rather_than_by_having_been_clicked()
    {
        var text = "status:stopped";
        var menu = new ColumnMenu("status", null, () => text, written => text = written);

        Assert.True(menu.Values.Single(value => value.Label == "stopped").IsOn);
        Assert.False(menu.Values.Single(value => value.Label == "running").IsOn);
    }

    /// <summary>
    /// A column the language cannot narrow to a set of words still opens a menu.
    ///
    /// <b>MY DECISION, on the owner's instruction to choose - the reasoning is at ColumnMenu.</b>
    /// A gesture that works on some headings and does nothing on others cannot be told from a
    /// broken one, and there is no way for the window to say which it is.
    ///
    /// Two shapes of column here and they fail differently, which is why both are asked: five have
    /// no field in the language at all, and several more have one that takes free text, a number
    /// or a size. Both end with an empty list of values and neither is a fault.
    /// </summary>
    [Fact]
    public void A_column_with_nothing_to_offer_still_offers_the_way_to_put_it_away()
    {
        var text = string.Empty;

        // No field in the language at all - four since 2026-09-06, when dependsOn gained one.
        string[] noField = ["fileVersion", "binaryHash", "loadOrderGroup", "errorControl"];

        // A field that takes anything somebody types, so there is no list to draw.
        string[] freeText = ["displayName", "account", "publisher", "processId", "memory", "dependsOn", "requiredBy"];

        foreach (var id in noField.Concat(freeText))
        {
            var menu = new ColumnMenu(id, null, () => text, written => text = written);

            Assert.Empty(menu.Values);
            Assert.NotEmpty(menu.HideLabel);
        }

        // And the ones that DO have something, so the two lists above are not simply everything.
        foreach (var id in new[] { "status", "startType", "entryType", "signature", "triggers" })
        {
            Assert.NotEmpty(new ColumnMenu(id, null, () => text, written => text = written).Values);
        }
    }

    /// <summary>
    /// Putting a column away goes through the same choice the picker owns, so both doors lead to
    /// one road - and the last column showing is refused by the item being unavailable.
    /// </summary>
    [Fact]
    public void A_column_is_put_away_through_the_pickers_own_choice_and_the_last_one_is_refused()
    {
        var bar = new ColumnBar();
        var text = string.Empty;

        var first = bar.Choices.First(choice => choice.IsShown);
        var menu = new ColumnMenu(first.Column.Id, first, () => text, written => text = written);

        Assert.True(menu.MayHide);

        menu.Hide();

        Assert.False(first.IsShown);

        // Now down to one, which the picker refuses to let go - a list with no columns at all is
        // 809 rows of nothing above a count line still saying 809.
        foreach (var choice in bar.Choices.Where(one => one.IsShown).Skip(1).ToList())
        {
            choice.IsShown = false;
        }

        var alone = bar.Choices.Single(one => one.IsShown);
        var last = new ColumnMenu(alone.Column.Id, alone, () => text, written => text = written);

        Assert.False(last.MayHide);
        Assert.NotEmpty(last.HideRefused);

        last.Hide();

        Assert.True(alone.IsShown, "The last column still showing was put away anyway.");
    }
    /// <summary>
    /// A right click on a column heading opens that column's own menu, and one on nothing opens
    /// nothing.
    ///
    /// <b>The one part of the heading menu that only a window can answer.</b> What it OFFERS is
    /// decided without WPF and asked in ColumnMenuGuards. What is here is the walk from whatever
    /// the pointer was over up to the heading, and the step from a heading to which of our columns
    /// it is - which goes through SortMemberPath, the same road ListSorting has used since sorting
    /// was turned on.
    ///
    /// <b>The grid is measured and arranged by hand</b>, because this host builds a window without
    /// showing one and WPF creates a header only when it has been asked to lay one out. Without
    /// those two lines the grid has columns and no headers, and this test would pass by finding
    /// nothing to fail on.
    /// </summary>
    [Fact]
    public void A_right_click_on_a_heading_opens_that_columns_menu_and_one_on_nothing_opens_nothing()
    {
        var window = WpfHost.Window();

        var heading = WpfHost.On(() =>
        {
            window.Entries.Measure(new Size(1400, 900));
            window.Entries.Arrange(new Rect(0, 0, 1400, 900));
            window.Entries.UpdateLayout();

            return Headings(window.Entries).FirstOrDefault(one => one.Column is not null);
        });

        Assert.NotNull(heading);

        // The heading itself, and then something inside it - a pointer lands on the text far more
        // often than on the header, and the walk is the whole reason this passes either way.
        Assert.True(WpfHost.On(() => window.OfferTheColumnMenu(heading!)), "The heading opened no menu.");
        Assert.True(
            WpfHost.On(() => window.OfferTheColumnMenu(Inside(heading!))),
            "A click on what is drawn INSIDE the heading opened no menu, so the walk up stops too early.");

        // AND THE PROMISE THAT WAS THERE BEFORE THIS MENU EXISTED: empty space offers nothing. The
        // grid is not inside a heading, so this is what the space under the last row looks like.
        Assert.False(
            WpfHost.On(() => window.OfferTheColumnMenu(window.Entries)),
            "Something that is not a heading opened a column menu.");

        Assert.False(WpfHost.On(() => window.OfferTheColumnMenu(new object())));

        // ONE AT A TIME, which is a promise of the window rather than tidiness here: a menu opened
        // from code is owned by nothing in the visual tree, so a second right click would put a
        // second live popup over the first. Two were opened above and one is standing.
        Assert.NotNull(WpfHost.On(() => window.HeadingMenu));

        // Closed, for the reason the test above closes its own: this host is shared, and a popup
        // left open sits over whatever the next test builds.
        WpfHost.On(() => window.HeadingMenu!.IsOpen = false);
    }

    /// <summary>Every column heading the grid has drawn, in whatever order the tree holds them.</summary>
    private static List<System.Windows.Controls.Primitives.DataGridColumnHeader> Headings(DependencyObject node)
    {
        var found = new List<System.Windows.Controls.Primitives.DataGridColumnHeader>();

        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(node, index);

            if (child is System.Windows.Controls.Primitives.DataGridColumnHeader heading)
            {
                found.Add(heading);
            }

            found.AddRange(Headings(child));
        }

        return found;
    }

    /// <summary>Something drawn inside a heading, which is what a pointer actually lands on.</summary>
    private static DependencyObject Inside(DependencyObject heading) =>
        System.Windows.Media.VisualTreeHelper.GetChildrenCount(heading) > 0
            ? System.Windows.Media.VisualTreeHelper.GetChild(heading, 0)
            : heading;
}
