using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The entry the window is looking at, and what the details panel does about it - the half that
/// needs no window.
///
/// <b>What the panel SAYS is guarded in <see cref="DetailsGuards"/> and is not repeated here.</b>
/// That class holds the one design decision - the panel is the column catalogue applied to one row.
/// This one holds the four things that only exist once a panel can be opened and closed: when it
/// opens, what closing gives back, whether it keeps up with a list that moves underneath it, and
/// what it says when the entry stops existing.
/// </summary>
public sealed class ChosenGuards
{
    /// <summary>
    /// Choosing a row does not open the panel.
    ///
    /// <b>`docs/11` opens with the sentence this protects: the list is a tool for searching.</b> A
    /// panel that appeared on every click would take a third of the width away from anybody who was
    /// only scrolling, and they never asked for it.
    /// </summary>
    [Fact]
    public void Choosing_a_row_does_not_open_the_panel()
    {
        var chosen = new Chosen { Row = EntryRow.Of(Rows.Entry("Spooler")) };

        Assert.False(chosen.Showing);
        Assert.Empty(chosen.Sections);
    }

    /// <summary>
    /// With nothing chosen there is nothing to show, and the press is handed back.
    ///
    /// A key marked handled by something that decided to do nothing is a key that silently stops
    /// working for whatever needed it next - the same reasoning Escape carries.
    /// </summary>
    [Fact]
    public void Enter_with_nothing_chosen_opens_nothing()
    {
        var chosen = new Chosen();

        Assert.False(chosen.Show());
        Assert.False(chosen.Showing);
    }

    [Fact]
    public void Opening_it_fills_it_with_everything_about_that_entry()
    {
        var chosen = new Chosen { Row = EntryRow.Of(Rows.Entry("Spooler", "Print Spooler")) };

        Assert.True(chosen.Show());

        Assert.True(chosen.Showing);
        Assert.Equal("Spooler", chosen.ShownName);
        Assert.Equal("Print Spooler", chosen.ShownLabel);

        // Five, and the count is asserted whole rather than "more than none": four groups of the
        // column picker plus the one naming what this window did not read. A group quietly
        // disappearing is exactly the shape that would leave a panel looking complete.
        Assert.Equal(4, chosen.Sections.Count);
    }

    /// <summary>
    /// Closing says whether there was anything to close, and a closed panel hands the press back.
    ///
    /// <b>This answer is what puts Escape in the right order.</b> The window closes the panel first
    /// and clears the query only when this says there was no panel - so a false answer here would
    /// take somebody's query away while they were reaching for the panel.
    /// </summary>
    [Fact]
    public void Closing_says_whether_there_was_a_panel_to_close()
    {
        var chosen = new Chosen { Row = EntryRow.Of(Rows.Entry("Spooler")) };

        Assert.False(chosen.Hide());

        chosen.Show();

        Assert.True(chosen.Hide());
        Assert.False(chosen.Showing);
    }

    /// <summary>
    /// The panel keeps up with the row it was opened on.
    ///
    /// <b>A row keeps its identity for the life of the window and its cells move underneath it</b> -
    /// see <c>EntryRow</c> - so a panel built once from the entry would show whatever was true at
    /// the moment somebody pressed Enter and would go on showing it, correctly, forever.
    /// </summary>
    [Fact]
    public void The_panel_moves_when_the_row_moves()
    {
        var entry = Rows.Entry("Spooler");
        var row = EntryRow.Of(entry);
        var chosen = new Chosen { Row = row };

        chosen.Show();

        var before = Says(chosen, "gui.column.account");

        row.Absorb(
            entry with { Account = Reading<string>.Denied(5, "Access is denied.") },
            DateTimeOffset.UnixEpoch);

        Assert.NotEqual(before, Says(chosen, "gui.column.account"));
    }

    /// <summary>
    /// A closed panel stops listening, which is the teardown this class would leak through.
    ///
    /// A row lives as long as the window does, so a handler left on it keeps rebuilding five
    /// sections once a second for a panel nobody is looking at - and keeps this object alive to do
    /// it. Asked by identity rather than by value: the lines are what they were because nothing
    /// rebuilt them, not because the answer happens to be the same.
    /// </summary>
    [Fact]
    public void A_closed_panel_stops_following_the_row()
    {
        var entry = Rows.Entry("Spooler");
        var row = EntryRow.Of(entry);
        var chosen = new Chosen { Row = row };

        chosen.Show();

        var built = chosen.Sections;

        chosen.Hide();

        row.Absorb(
            entry with { Account = Reading<string>.Denied(5, "Access is denied.") },
            DateTimeOffset.UnixEpoch);

        Assert.Same(built, chosen.Sections);
    }

    /// <summary>
    /// An entry that has left the listing is said out loud, and a narrowed list is not that.
    ///
    /// <b>Rule 8 in the place a details panel breaks it most easily.</b> A panel carrying on with
    /// the last reading answers questions about a service that is not there - and the opposite
    /// mistake is just as bad, because a row leaves the VISIBLE list on almost every keystroke.
    /// </summary>
    [Fact]
    public void An_entry_that_left_the_listing_is_said_and_a_narrowed_one_is_not()
    {
        var row = EntryRow.Of(Rows.Entry("Spooler"));
        var other = EntryRow.Of(Rows.Entry("Dnscache"));
        var chosen = new Chosen { Row = row };

        chosen.Show();

        chosen.StillIn([row, other]);

        Assert.False(chosen.Gone);
        Assert.Empty(chosen.Notice);

        chosen.StillIn([other]);

        Assert.True(chosen.Gone);
        Assert.NotEmpty(chosen.Notice);
    }

    /// <summary>
    /// The panel keeps answering for the entry it is OPEN ON, whatever the selection does after.
    ///
    /// <b>It asked about the selected row until 2026-08-26, and the two are deliberately not the
    /// same row.</b> Opening the panel is Enter, and clicking elsewhere afterwards leaves the panel
    /// where it was - this class says so twice. So from the moment somebody clicked another row,
    /// the one thing the panel can say about its own entry was being decided about a different one.
    ///
    /// The plainest way it broke is the scope switch, which sets the chosen row to nothing: the
    /// question then returned at the door and the panel silently lost the ability to notice its
    /// service had gone, for the rest of the session.
    /// </summary>
    [Fact]
    public void The_panel_answers_for_its_own_entry_rather_than_for_whatever_is_selected()
    {
        var shown = EntryRow.Of(Rows.Entry("Spooler"));
        var other = EntryRow.Of(Rows.Entry("Dnscache"));
        var chosen = new Chosen { Row = shown };

        chosen.Show();

        // Somebody clicks another row, and then the scope switch clears the selection outright.
        chosen.Row = other;
        chosen.StillIn([shown, other]);

        Assert.False(chosen.Gone);

        chosen.Row = null;
        chosen.StillIn([other]);

        // The entry the panel is showing is not in the listing any more, and it says so - with no
        // row selected at all.
        Assert.True(chosen.Gone);
        Assert.NotEmpty(chosen.Notice);
    }

    /// <summary>Closing puts the admission away with the panel, or the next one opens wearing it.</summary>
    [Fact]
    public void Closing_takes_the_admission_with_it()
    {
        var row = EntryRow.Of(Rows.Entry("Spooler"));
        var chosen = new Chosen { Row = row };

        chosen.Show();
        chosen.StillIn([]);

        Assert.True(chosen.Gone);

        chosen.Hide();

        Assert.False(chosen.Gone);
        Assert.Empty(chosen.Notice);
    }

    /// <summary>What one field says in the panel, found the way a person would - by its label.</summary>
    private static string Says(Chosen chosen, string labelKey) =>
        chosen.Sections
            .SelectMany(section => section.Lines)
            .First(line => line.Label == Texts.Of(labelKey))
            .Value;
}
