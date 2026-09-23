using System.Buffers.Text;
using System.Text;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What a window without rights hands to the one it starts with them - UX-GUI-004 (c).
///
/// <b>Most of these are the hostile half, and that is the point of the file.</b> The argument lands
/// in a process with administrator rights, and anyone running as this user can start that process
/// with any argument they like. Each case below is a text this program could not have written, and
/// each is refused whole - nothing half applied.
/// </summary>
public sealed class HandOverTests
{
    [Fact]
    public void What_crosses_is_what_was_on_screen()
    {
        var sent = new HandOver
        {
            Scope = EntryScope.Drivers,
            Query = "status:running \"a b\" name:/x\\y/",
            Picked = ["Spooler", "W32Time"],
            Asked = ActionKind.SetStartType,
            To = StartType.Manual
        };

        var (carried, refused) = HandOver.Read([HandOver.Argument, sent.Encode()]);

        Assert.False(refused);
        Assert.NotNull(carried);
        Assert.Equal(sent.Scope, carried.Scope);
        Assert.Equal(sent.Query, carried.Query);
        Assert.Equal(sent.Picked, carried.Picked);
        Assert.Equal(sent.Asked, carried.Asked);
        Assert.Equal(sent.To, carried.To);
        Assert.False(carried.PickedLeftBehind);
    }

    /// <summary>Nothing the shell's quoting could get wrong: no space, quote or backslash in the value.</summary>
    [Fact]
    public void The_value_is_letters_digits_and_two_marks_only()
    {
        var value = new HandOver { Scope = EntryScope.Everything, Query = "\"quoted\" \\ back", Picked = [] }.Encode();

        Assert.Matches("^[A-Za-z0-9_-]+$", value);
    }

    [Fact]
    public void Without_the_argument_nothing_is_handed_over_and_nothing_is_refused()
    {
        Assert.Equal((null, false), HandOver.Read([]));
        Assert.Equal((null, false), HandOver.Read(["--catalogue"]));
    }

    /// <summary>
    /// TOO MANY PICKED TO FIT: the list and the query still cross, the picked rows and the plan over
    /// them do not, and the hand-over says so - a selection that quietly came back short would look
    /// complete (rule 8).
    /// </summary>
    [Fact]
    public void A_selection_too_large_to_carry_is_left_behind_and_says_so()
    {
        var names = Enumerable.Range(0, 1000).Select(index => $"SomeServiceWithALongName{index:D4}").ToArray();

        var value = new HandOver
        {
            Scope = EntryScope.Services,
            Query = "status:running",
            Picked = names,
            Asked = ActionKind.Stop
        }.Encode();

        Assert.True(value.Length <= HandOver.LongestArgument);

        var (carried, refused) = HandOver.Read([HandOver.Argument, value]);

        Assert.False(refused);
        Assert.NotNull(carried);
        Assert.Equal("status:running", carried.Query);
        Assert.Empty(carried.Picked);
        Assert.Null(carried.Asked);
        Assert.True(carried.PickedLeftBehind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("!!! not base64 !!!")]
    [InlineData("bm90IGpzb24")] // "not json"
    public void Text_that_is_not_a_hand_over_is_refused(string value)
    {
        Assert.Equal((null, true), HandOver.Read([HandOver.Argument, value]));
    }

    [Fact]
    public void The_argument_with_nothing_after_it_is_refused()
    {
        Assert.Equal((null, true), HandOver.Read([HandOver.Argument]));
    }

    /// <summary>
    /// Refused for its LENGTH alone: everything inside would pass, so the only reason left is the
    /// cap - which is what keeps a hostile argument from being decoded at any size it likes.
    /// </summary>
    [Fact]
    public void A_value_longer_than_this_program_writes_is_refused_before_it_is_decoded()
    {
        var names = string.Join(",", Enumerable.Range(0, 200).Select(index => $"\"Service{index:D12}\""));
        var json = $$"""{"V":1,"Scope":"Services","Query":"{{new string('a', 4000)}}","Picked":[{{names}}]}""";
        var value = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

        Assert.True(value.Length > HandOver.LongestArgument);
        Assert.Equal((null, true), HandOver.Read([HandOver.Argument, value]));
    }

    [Theory]
    [InlineData("""{"V":2,"Scope":"Services","Query":"","Picked":[]}""")]
    [InlineData("""{"V":1,"Scope":"3","Query":"","Picked":[]}""")]
    [InlineData("""{"V":1,"Scope":"Services, Drivers","Query":"","Picked":[]}""")]
    [InlineData("""{"V":1,"Scope":"services","Query":"","Picked":[]}""")]
    [InlineData("""{"V":1,"Scope":"Services","Picked":[]}""")]
    [InlineData("""{"V":1,"Scope":"Services","Query":"","Picked":["Spool\ner"]}""")]
    [InlineData("""{"V":1,"Scope":"Services","Query":"","Picked":[""]}""")]
    [InlineData("""{"V":1,"Scope":"Services","Query":"","Picked":["Spooler"],"Asked":"Delete"}""")]
    [InlineData("""{"V":1,"Scope":"Services","Query":"","Picked":["Spooler"],"Asked":"Stop","To":"Manual"}""")]
    [InlineData("""{"V":1,"Scope":"Services","Query":"","Picked":["Spooler"],"Asked":"SetStartType"}""")]
    [InlineData("""{"V":1,"Scope":"Services","Query":"","Picked":["Spooler"],"Asked":"SetStartType","To":"Boot, System"}""")]
    [InlineData("""[[[[[[[[[[]]]]]]]]]]""")]
    public void A_hand_over_this_program_could_not_have_written_is_refused_whole(string json)
    {
        var value = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

        Assert.Equal((null, true), HandOver.Read([HandOver.Argument, value]));
    }

    [Fact]
    public void A_query_longer_than_the_box_would_hold_is_refused()
    {
        var json = $$"""{"V":1,"Scope":"Services","Query":"{{new string('a', 5000)}}","Picked":[]}""";

        Assert.Equal((null, true), HandOver.Read([HandOver.Argument, Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json))]));
    }
}
