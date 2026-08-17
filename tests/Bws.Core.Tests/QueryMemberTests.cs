using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// Putting a member into a query somebody typed, and taking it back out.
///
/// <b>This is the half of `A5` that can be checked without a window.</b> A chip is a control
/// that stands for one member: clicking it puts the member in the box, clicking again takes it
/// out, and typing the member by hand lights the chip. All three are text going in and text
/// coming out, so all three live here rather than in the interface - the same argument that put
/// the drivers switch's two helpers in the language rather than beside the checkbox.
///
/// <b>What these assert is not "a member was added".</b> It is that the rest of the line came
/// back untouched. The box is the person's, they are looking at it, and a control that quietly
/// reformats what they typed teaches them not to trust it.
/// </summary>
public sealed class QueryMemberTests
{
    [Fact]
    public void A_member_goes_on_the_end_of_what_was_already_there()
    {
        Assert.Equal("status:stopped", QueryMembers.With(null, "status", "stopped", negated: false));
        Assert.Equal("status:stopped", QueryMembers.With("", "status", "stopped", negated: false));

        Assert.Equal(
            "spool status:stopped",
            QueryMembers.With("spool", "status", "stopped", negated: false));

        Assert.Equal(
            "spool !type:driver",
            QueryMembers.With("spool", "type", "driver", negated: true));
    }

    /// <summary>
    /// Clicking a chip that is already on cannot add it twice - otherwise turning it off would
    /// take two clicks, and the second one would look like the control was broken.
    /// </summary>
    [Fact]
    public void A_member_that_is_already_there_is_not_added_again()
    {
        const string typed = "status:stopped spool";

        Assert.Equal(typed, QueryMembers.With(typed, "status", "stopped", negated: false));
    }

    /// <summary>
    /// THE CASE THE DRIVERS SWITCH COULD NEVER DO. Its helper takes the member off the END of
    /// the line, which is right for one control and wrong for the second one somebody clicks -
    /// after that the first member is no longer last and a tail cut can never find it.
    /// </summary>
    [Fact]
    public void A_member_in_the_middle_comes_out_and_the_rest_is_untouched()
    {
        Assert.Equal(
            "spool !type:driver",
            QueryMembers.Without("spool status:stopped !type:driver", "status", "stopped", negated: false));

        Assert.Equal(
            "status:stopped spool",
            QueryMembers.Without("status:stopped !type:driver spool", "type", "driver", negated: true));
    }

    [Fact]
    public void A_member_at_either_end_comes_out_without_leaving_a_gap()
    {
        Assert.Equal(
            "spool",
            QueryMembers.Without("status:stopped spool", "status", "stopped", negated: false));

        Assert.Equal(
            "spool",
            QueryMembers.Without("spool status:stopped", "status", "stopped", negated: false));

        Assert.Equal(
            string.Empty,
            QueryMembers.Without("status:stopped", "status", "stopped", negated: false));
    }

    /// <summary>
    /// Everything that was not cut comes back as it was typed - the spacing included.
    ///
    /// <b>The even claim, and the one that decided how the cut is written.</b> Collapsing all
    /// whitespace afterwards is shorter and passes every test above, and it also rewrites a line
    /// somebody is looking at. So the cut swallows exactly the one gap it made and nothing else.
    /// </summary>
    [Fact]
    public void Spacing_the_cut_did_not_touch_is_left_alone()
    {
        // Two gaps of two spaces, one member between them. ONE gap survives whole rather than
        // the two being welded into four - the cut takes the run in front of the member with it,
        // and the run behind is left exactly as it was typed.
        //
        // This expectation was written as "four spaces" first and was wrong about the code
        // rather than about the behaviour. Kept as a note because the arithmetic is the sort
        // somebody redoes at a glance and gets wrong the same way.
        Assert.Equal(
            "spool  winmgmt",
            QueryMembers.Without("spool  status:stopped  winmgmt", "status", "stopped", negated: false));

        Assert.Equal(
            "spool\twinmgmt",
            QueryMembers.Without("spool\twinmgmt status:stopped", "status", "stopped", negated: false));
    }

    /// <summary>
    /// Somebody typed the member and then clicked the chip for it, so it is in twice through no
    /// fault of their own. A chip that turned off and left one behind is a control that visibly
    /// does not work.
    /// </summary>
    [Fact]
    public void Every_copy_of_the_member_comes_out_not_the_first()
    {
        Assert.Equal(
            "spool",
            QueryMembers.Without(
                "status:stopped spool status:stopped", "status", "stopped", negated: false));
    }

    /// <summary>
    /// SPELLING IS FOLDED, WHICH IS THE LANGUAGE'S RULE RATHER THAN THIS FILE'S. A chip written
    /// one way has to find the member typed another, or it lights for text that is plainly there.
    /// </summary>
    [Fact]
    public void A_member_spelled_differently_is_still_the_same_member()
    {
        Assert.True(QueryMembers.Carries("!TYPE:Driver", "type", "driver", negated: true));

        Assert.Equal(
            string.Empty,
            QueryMembers.Without("!TYPE:Driver", "type", "driver", negated: true));
    }

    /// <summary>
    /// The two sides are two chips. Without this a filter for "stopped" would light on a query
    /// that excludes stopped entries, which is the opposite of what it says.
    /// </summary>
    [Fact]
    public void An_exclusion_is_a_different_member_from_the_same_value_included()
    {
        Assert.True(QueryMembers.Carries("!status:stopped", "status", "stopped", negated: true));
        Assert.False(QueryMembers.Carries("!status:stopped", "status", "stopped", negated: false));
        Assert.False(QueryMembers.Carries("status:stopped", "status", "stopped", negated: true));

        // And a cut aimed at one side leaves the other where it is.
        Assert.Equal(
            "!status:stopped",
            QueryMembers.Without("!status:stopped", "status", "stopped", negated: false));
    }

    /// <summary>
    /// QUOTING TAKES THE MEANING OFF, AND THAT IS WHY MATCHING IS PARSED RATHER THAN COMPARED.
    ///
    /// <c>"status:stopped"</c> in quotes is a search for those fifteen characters, not a member
    /// about the status field - the language says so and the window has to agree with it. A
    /// version comparing the written text would cut this. A version comparing it after unquoting
    /// would too.
    /// </summary>
    [Fact]
    public void A_quoted_member_is_text_and_is_left_alone()
    {
        const string quoted = "\"status:stopped\"";

        Assert.False(QueryMembers.Carries(quoted, "status", "stopped", negated: false));
        Assert.Equal(quoted, QueryMembers.Without(quoted, "status", "stopped", negated: false));
    }

    /// <summary>
    /// Half typed text is the ordinary state of a search box, not an error. A quote that has not
    /// been closed yet cannot be taken apart, so the line comes back whole rather than mangled -
    /// and the chip works on the keystroke that closes it.
    /// </summary>
    [Fact]
    public void Text_that_cannot_be_taken_apart_yet_comes_back_whole()
    {
        const string halfTyped = "status:stopped \"winmgmt";

        Assert.Equal(halfTyped, QueryMembers.Without(halfTyped, "status", "stopped", negated: false));
    }

    /// <summary>
    /// A field nobody knows is not a member, so nothing is carried and nothing is cut. Asked
    /// because a chip reads whatever is in the box, and the box holds whatever was typed.
    /// </summary>
    [Fact]
    public void A_field_nobody_knows_carries_nothing()
    {
        Assert.False(QueryMembers.Carries("nosuchfield:x", "nosuchfield", "x", negated: false));
        Assert.Equal("nosuchfield:x", QueryMembers.Without("nosuchfield:x", "nosuchfield", "x", negated: false));
    }

    /// <summary>
    /// EVERY VALUE OF A REPEATED FIELD IS CARRIED, NOT THE FIRST ONE - owner's report, 2026-08-13.
    ///
    /// <b>A term holds what it MATCHES and what it was WRITTEN as, and folding kept only the
    /// first.</b> Repeated mentions of one field are collapsed into an alternative, which is what
    /// makes <c>status:running status:stopped</c> mean either rather than both - and the fold
    /// merged the compiled values while dropping the spellings. So the query filtered on all three
    /// start types and admitted to exactly one.
    ///
    /// <b>Nothing else in this product could see it, and that is why it lived.</b> The list, the
    /// count, the command line and the parity guard between them all read the VALUES. Only
    /// <see cref="Query.Carries"/> reads the spellings, and only a clickable filter asks it - so
    /// the fault existed exactly where one control in one window looks and nowhere else.
    ///
    /// Measured against the machine before it was fixed: start:manual is 566 entries,
    /// start:disabled 50, start:boot 54, all three together 670. The answer was always right.
    /// </summary>
    [Fact]
    public void Every_value_of_a_repeated_field_is_carried_rather_than_the_first()
    {
        const string three = "start:manual start:disabled start:boot";

        Assert.Equal(
            "manual=True disabled=True boot=True",
            $"manual={QueryMembers.Carries(three, "start", "manual", negated: false)} "
            + $"disabled={QueryMembers.Carries(three, "start", "disabled", negated: false)} "
            + $"boot={QueryMembers.Carries(three, "start", "boot", negated: false)}");

        // And taking one out leaves the other two carried, which is what a chip turning off means.
        var left = QueryMembers.Without(three, "start", "disabled", negated: false);

        Assert.Equal("start:manual start:boot", left);
        Assert.True(QueryMembers.Carries(left, "start", "manual", negated: false));
        Assert.True(QueryMembers.Carries(left, "start", "boot", negated: false));
    }

    /// <summary>
    /// The round trip, which is the property a chip actually needs: on, then off, and the line
    /// is what it was.
    /// </summary>
    [Fact]
    public void Adding_a_member_and_taking_it_out_again_gives_back_what_was_there()
    {
        foreach (var typed in new[] { "", "spool", "spool winmgmt", "!type:driver", "name:spool*" })
        {
            var on = QueryMembers.With(typed, "status", "stopped", negated: false);

            Assert.True(QueryMembers.Carries(on, "status", "stopped", negated: false));
            Assert.Equal(typed, QueryMembers.Without(on, "status", "stopped", negated: false));
        }
    }
}
