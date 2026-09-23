namespace Bws.Core.Tests;

/// <summary>
/// The three answers a description can be, and the one the manager reports by NOT failing.
///
/// <b>These are the shapes measured on a real machine on 2026-08-12, not shapes somebody imagined:</b>
/// 384 entries of 819 have no description, 398 of the 444 in the registry are indirections into a
/// binary's resources, the manager resolves 388 of them, and ten come back unresolved.
/// </summary>
public sealed class ServiceDescriptionTests
{
    [Fact]
    public void A_sentence_is_a_description()
    {
        var read = ServiceDescription.Of("This service spools print jobs.");

        Assert.Equal(ReadOutcome.Present, read.Outcome);
        Assert.Equal("This service spools print jobs.", read.Value);
    }

    /// <summary>
    /// <b>The case this type exists for.</b> The call succeeds and hands back the indirection it
    /// was supposed to resolve, so nothing in the return code says the read failed. Treated as
    /// prose, this would put an at sign and a file path in front of a person and call it a
    /// description - and would compare clean in a snapshot against a machine where it resolved.
    /// </summary>
    [Theory]
    [InlineData(@"@%SystemRoot%\system32\adpsvc.dll,-103")]
    [InlineData(@"@%SystemRoot%\system32\drivers\tcpip.sys,-40007")]
    [InlineData("@todo.dll,-100")]
    public void An_unresolved_indirection_is_a_failed_read_rather_than_a_description(string handed)
    {
        var read = ServiceDescription.Of(handed);

        Assert.Equal(ReadOutcome.Denied, read.Outcome);
        Assert.Equal(ServiceDescription.ResourceNotFound, read.ErrorCode);
        Assert.Null(read.Value);
    }

    /// <summary>
    /// Malformed rather than well formed, and it still counts as unresolved. What makes this a
    /// failed read is that the manager did not resolve it, and the at sign is the marker for that
    /// whatever follows - a stricter pattern would let this through as prose.
    /// </summary>
    [Fact]
    public void An_indirection_with_nothing_after_it_is_still_not_a_description()
    {
        Assert.Equal(ReadOutcome.Denied, ServiceDescription.Of("@").Outcome);
    }

    /// <summary>
    /// Nothing to answer is a fact about the entry, and the ordinary one: nearly every driver.
    /// Empty and null arrive from different places and mean the same thing here.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_description_is_absent_rather_than_an_empty_one(string? handed)
    {
        var read = ServiceDescription.Of(handed);

        Assert.Equal(ReadOutcome.Absent, read.Outcome);
        Assert.Null(read.Value);
    }

    /// <summary>
    /// <b>Absent and refused must not be the same answer</b>, which is rule 8 of the project notes
    /// in the one field where both really happen on an ordinary elevated session. Asserted as a
    /// pair rather than separately, because what matters is the difference between them.
    /// </summary>
    [Fact]
    public void Having_no_description_and_having_an_unreadable_one_are_different_answers()
    {
        var none = ServiceDescription.Of(null);
        var unreadable = ServiceDescription.Of("@shell32.dll,-1");

        Assert.NotEqual(none.Outcome, unreadable.Outcome);
        Assert.Equal(0, none.ErrorCode);
        Assert.NotEqual(0, unreadable.ErrorCode);
    }

    /// <summary>
    /// A description carrying a line break survives whole, which is a fact whatever displays it has
    /// to hold: two entries of 819 on this machine contain one, and the longest is 1251 characters.
    /// </summary>
    [Fact]
    public void A_description_with_a_line_break_is_kept_as_it_came()
    {
        var text = "First line." + Environment.NewLine + "Second line.";

        Assert.Equal(text, ServiceDescription.Of(text).Value);
    }
}
