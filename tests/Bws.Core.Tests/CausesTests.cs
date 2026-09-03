namespace Bws.Core.Tests;

/// <summary>
/// What a person is told went wrong, when what went wrong is several things at once. Backlog 307.
///
/// <b>The loop this replaces looked complete and was one branch of a tree.</b> Walking
/// <c>InnerException</c> is the whole chain for an ordinary exception, and for an
/// <see cref="AggregateException"/> it is the first cause and nothing else. The listing and the
/// second pass both go through <c>Parallel.For</c>, which is exactly the shape that fails several
/// times at once - so the failure this tool is most likely to meet was the one it read least of.
/// </summary>
public sealed class CausesTests
{
    [Fact]
    public void An_ordinary_chain_is_read_from_the_outside_in()
    {
        var failure = new InvalidOperationException(
            "The service could not be opened.",
            new UnauthorizedAccessException("Access is denied."));

        Assert.Equal(["The service could not be opened.", "Access is denied."], Causes.Of(failure));
    }

    [Fact]
    public void A_wrapper_holding_several_failures_gives_all_of_them_rather_than_its_own_sentence()
    {
        // The exact shape Parallel.For hands back, and the exact sentence a person used to get
        // instead: "One or more errors occurred." with one cause under it.
        var failure = new AggregateException(
            new UnauthorizedAccessException("Access is denied."),
            new IOException("The device is not ready."),
            new TimeoutException("The manager did not answer."));

        Assert.Equal(
            ["Access is denied.", "The device is not ready.", "The manager did not answer."],
            Causes.Of(failure));

        Assert.DoesNotContain(
            Causes.Of(failure),
            cause => cause.Contains("One or more errors", StringComparison.Ordinal));
    }

    [Fact]
    public void A_wrapper_inside_a_wrapper_is_read_through()
    {
        // Nesting is an artefact of how the work was arranged rather than anything a person did,
        // so it says nothing and is stepped over.
        var failure = new AggregateException(
            new AggregateException(new IOException("The device is not ready.")),
            new TimeoutException("The manager did not answer."));

        Assert.Equal(
            ["The device is not ready.", "The manager did not answer."],
            Causes.Of(failure));
    }

    [Fact]
    public void One_reason_shared_by_many_entries_is_said_once()
    {
        // THE ONE THAT DECIDES WHETHER THIS IS READABLE AT ALL. Eight hundred entries refused for
        // one reason produce eight hundred identical sentences, and exactly one of them is news.
        var failure = new AggregateException(
            Enumerable
                .Range(0, 800)
                .Select(_ => (Exception)new UnauthorizedAccessException("Access is denied.")));

        Assert.Equal(["Access is denied."], Causes.Of(failure));
    }

    [Fact]
    public void A_failure_with_more_distinct_causes_than_anybody_can_read_is_cut_rather_than_printed()
    {
        // A status line is a line. Something has to give when a failure genuinely has this many
        // distinct causes, and dropping the eleventh is better than a window with no room left
        // for the list underneath.
        var failure = new AggregateException(
            Enumerable
                .Range(0, 40)
                .Select(index => (Exception)new IOException($"Reason {index}.")));

        var said = Causes.Of(failure);

        Assert.Equal(10, said.Count);
        Assert.Equal("Reason 0.", said[0]);
    }

    [Fact]
    public void A_wrapper_carrying_nothing_still_says_what_it_says()
    {
        // Rule 8: a failure with nothing underneath it must not come back as an empty list, which
        // would put a window and a terminal in front of somebody with no sentence at all.
        Assert.Equal(
            ["Nothing underneath this one."],
            Causes.Of(new AggregateException("Nothing underneath this one.")));
    }
}
