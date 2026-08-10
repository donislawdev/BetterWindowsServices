using Bws.Gui;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Pressing a letter on the list goes to the next entry that starts with it.
///
/// <b>The behaviour admins already have in <c>services.msc</c>, asked for by name by the owner</b>
/// - backlog 151. It belongs to the same family as Ctrl+F and Escape from slice 4: keys people
/// learned somewhere else, which a tool either honours or makes them unlearn.
///
/// The two halves are tested apart, because only one of them can be. What a press MEANS is
/// <see cref="Shortcuts.JumpLetter"/> and is a pure function. Where the selection LANDS is
/// <see cref="MainViewModel.NextStartingWith"/> and needs rows but no window. What is left in the
/// window is three lines that set a selection and scroll to it, and nothing here pretends to cover
/// them.
/// </summary>
public sealed class TypeToFindTests
{
    [Theory]
    [InlineData("f", 'f')]
    [InlineData("F", 'F')]
    [InlineData("7", '7')]
    // Written as an escape rather than as the letter itself, and that is not squeamishness: a
    // guard refuses anything outside plain ASCII in a version controlled file unless the file is
    // on a list with a reason, and one test case is a poor reason to lengthen that list. The
    // assertion is the same one either way - this is the case that says a jump is about the
    // character somebody typed and not about a key on an American keyboard.
    [InlineData("\u0119", '\u0119')]
    public void A_letter_or_a_digit_is_something_to_jump_to(string typed, char expected) =>
        Assert.Equal(expected, Shortcuts.JumpLetter(typed));

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("\t")]
    [InlineData("\u20AC")]
    [InlineData("ab")]
    public void Anything_else_is_left_to_whatever_has_focus(string? typed) =>
        Assert.Null(Shortcuts.JumpLetter(typed));

    [Fact]
    public void A_space_is_left_alone_because_the_list_already_uses_it()
    {
        // Named on its own rather than buried in the list above, because this one is a decision
        // rather than an obvious exclusion: space is a plausible thing to jump on and the grid
        // already selects with it. Taking it would break something that works to add something
        // nobody would use.
        Assert.Null(Shortcuts.JumpLetter(" "));
    }

    [Fact]
    public void The_jump_lands_on_the_first_entry_beginning_with_the_letter()
    {
        var model = Listing("Appinfo", "Dhcp", "Dnscache", "Fax", "FontCache");

        Assert.Equal("Fax", model.NextStartingWith('f')?.ServiceName);
    }

    [Fact]
    public void The_letter_does_not_have_to_be_typed_in_the_case_the_entry_uses()
    {
        var model = Listing("Appinfo", "Fax");

        Assert.Equal("Fax", model.NextStartingWith('F')?.ServiceName);
    }

    [Fact]
    public void Pressing_the_same_letter_again_walks_to_the_next_one()
    {
        // The half that makes this usable rather than a novelty: 810 entries hold a lot of names
        // starting with the same letter, and a jump that always lands on the first is a jump
        // somebody uses once.
        var model = Listing("Appinfo", "Fax", "FontCache", "FrameServer");

        model.Selected = model.NextStartingWith('f');

        Assert.Equal("FontCache", model.NextStartingWith('f')?.ServiceName);
    }

    [Fact]
    public void The_walk_comes_back_round_to_the_first_one()
    {
        var model = Listing("Appinfo", "Fax", "FontCache");

        model.Selected = model.NextStartingWith('f');
        model.Selected = model.NextStartingWith('f');

        Assert.Equal("Fax", model.NextStartingWith('f')?.ServiceName);
    }

    [Fact]
    public void A_letter_nothing_starts_with_moves_nothing()
    {
        // And the window leaves the press unhandled on this answer, so it carries on to whatever
        // else wanted it. A key swallowed by something that did nothing is the failure this
        // returns an answer to avoid.
        var model = Listing("Appinfo", "Dhcp");

        Assert.Null(model.NextStartingWith('z'));
    }

    [Fact]
    public void An_empty_list_moves_nothing()
    {
        Assert.Null(Listing().NextStartingWith('a'));
    }

    /// <summary>
    /// A window's worth of rows, in the order the machine handed them over.
    ///
    /// Loaded through the real view model rather than by pushing rows into the collection, because
    /// the order the list ends up in is part of what is being asserted - a jump that walks the
    /// wrong way through a differently ordered list would still pass against a hand-built one.
    /// </summary>
    private static MainViewModel Listing(params string[] names)
    {
        var model = new MainViewModel(new LiveMachine([.. names.Select(Rows.Entry)]), new SteppedClock());

        model.LoadAsync().GetAwaiter().GetResult();

        return model;
    }
}
