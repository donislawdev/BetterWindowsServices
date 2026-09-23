using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The example queries offered beside the search box - `P5`, 2026-08-12.
///
/// <b>An example that does not work teaches the language wrong and blames the person for it.</b>
/// The syntax used to live in the placeholder, where it vanished at the first keystroke - it lives
/// in these now, and the whole bet is that somebody clicks one, reads the query it wrote, and edits
/// it. That bet fails badly if what appears in the box is refused or answers nothing.
///
/// Its own file rather than a corner of the chip tests, because the claim is different: a chip
/// stands for ONE member and these are whole questions, several members long, with operators in
/// them.
/// </summary>
public sealed class QueryExampleTests
{
    /// <summary>
    /// EVERY EXAMPLE PARSES, asked of the parser rather than of a list kept here.
    ///
    /// The same guard the chips have and for the same reason: a query the language refuses
    /// composes an empty list and says nothing about why, which is the silence rule 8 forbids
    /// arriving through a control.
    /// </summary>
    [Fact]
    public void Every_example_is_a_query_the_language_accepts()
    {
        // ALL SIX, not the model's cut. Since backlog 358 the model hands out the examples that fit
        // the list on screen, and the window opens on Services - which leaves out the one example
        // that is for the whole machine. A claim about every example has to ask for every example.
        Assert.NotEmpty(QueryExamples.All);

        foreach (var example in QueryExamples.All)
        {
            var parsed = QueryParser.Parse(example.Query);

            Assert.True(
                parsed.IsValid,
                $"The example '{example.Label}' writes '{example.Query}', which the language "
                + "refuses: "
                + string.Join(", ", parsed.Problems.Select(problem => problem.Kind + " " + problem.Text)));
        }
    }

    /// <summary>
    /// AND NONE OF THEM ASKS FOR SOMETHING THE WINDOW DID NOT READ.
    ///
    /// <b>This is the half a parser cannot catch.</b> <c>signed:no</c> and <c>memory:&gt;500MB</c>
    /// are valid queries that the window answers with nothing at all, because the signature and
    /// memory readings are second phase and the window has no second phase - it says so in a
    /// sentence when somebody types one. An EXAMPLE that did it would be the tool teaching a
    /// question it cannot answer, which is worse than not offering the example.
    /// </summary>
    [Fact]
    public void No_example_asks_for_a_reading_the_window_does_not_take()
    {
        foreach (var example in QueryExamples.All)
        {
            var parsed = QueryParser.Parse(example.Query);

            Assert.True(
                parsed.Query!.Needs == ExtraRead.None,
                $"The example '{example.Label}' writes '{example.Query}', which needs "
                + $"{parsed.Query.Needs} - a reading this window does not take, so clicking it "
                + "would empty the list and explain it as a limitation rather than as an answer.");
        }
    }

    /// <summary>
    /// THE QUESTION "SHOULD BE RUNNING AND IS NOT" IS ASKED ONE WAY, WHEREVER IT IS ASKED FROM.
    ///
    /// <b>UX-GUI-008 of the audit of 2026-09-23.</b> The example wrote
    /// <c>start:auto !status:running</c> and the chip beside the box writes <c>mismatch:stopped</c>,
    /// and on the machine the audit was taken on the first answered 7 services and the second 1 -
    /// the literal query also catches per-user templates and services a trigger starts, which are
    /// stopped because that is where they are meant to sit. Somebody asking the same question from
    /// the box and from the chip got two numbers and no way to guess why.
    ///
    /// Asked of the chip rather than of a spelling written here, so that whichever of the two moves
    /// next, this test says so.
    /// </summary>
    [Fact]
    public void The_question_about_what_should_be_running_is_the_one_the_chip_asks()
    {
        var chip = FilterChips.Grouped(() => string.Empty, _ => { })
            .SelectMany(group => group.Chips)
            .Single(chip => chip.Label == Texts.Of("gui.filter.shouldRun"));

        var example = Assert.Single(
            QueryExamples.All,
            example => example.Label == Texts.Of("gui.example.shouldBeRunning"));

        Assert.Equal(chip.Member, example.Query);
    }
}
