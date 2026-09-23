namespace Bws.Core.Tests;

/// <summary>
/// What a person is shown when the manager's own label is not words.
///
/// <b>The two specimens below are the real ones, measured on this machine on 2026-09-02 over 799
/// entries, and they are the whole reason this rule exists:</b> <c>Tcpip6</c> and <c>tcpipreg</c>
/// are the only two whose display name comes back as an indirection nobody resolved. They differ
/// in the one way that matters - one carries a comment a person can read and the other does not -
/// so together they cover both arms of the rule.
/// </summary>
public sealed class ServiceDisplayNameTests
{
    [Fact]
    public void An_ordinary_label_is_kept_exactly_as_it_came()
    {
        Assert.Equal("Print Spooler", ServiceDisplayName.Of("Print Spooler", "Spooler"));
    }

    /// <summary>
    /// <b>The specimen this type was built for.</b> The comment after the first semicolon is the
    /// half a person was meant to read, and here it is exactly right - better than anything this
    /// code could invent from the internal name.
    /// </summary>
    [Fact]
    public void An_unresolved_indirection_shows_the_comment_a_person_was_meant_to_read()
    {
        Assert.Equal(
            "Microsoft IPv6 Protocol Driver",
            ServiceDisplayName.Of("@todo.dll,-100;Microsoft IPv6 Protocol Driver", "Tcpip6"));
    }

    /// <summary>
    /// <b>The other real specimen, and the reason the rule needs two arms.</b> It ends in a comma
    /// with nothing after it, so there is no comment to fall back on and the internal name is the
    /// only true thing left to say.
    /// </summary>
    [Fact]
    public void An_indirection_with_no_comment_shows_the_internal_name()
    {
        Assert.Equal(
            "tcpipreg",
            ServiceDisplayName.Of(@"@%SystemRoot%\System32\drivers\tcpipreg.sys,-10110,", "tcpipreg"));
    }

    /// <summary>
    /// A comment made only of spaces is no comment. Falling through to it would put an empty label
    /// in a column the list sorts by, which is worse than the at sign it replaced.
    /// </summary>
    [Theory]
    [InlineData("@file.dll,-1;")]
    [InlineData("@file.dll,-1;   ")]
    public void An_indirection_with_an_empty_comment_shows_the_internal_name(string handed)
    {
        Assert.Equal("Whatever", ServiceDisplayName.Of(handed, "Whatever"));
    }

    /// <summary>
    /// The FIRST semicolon ends the indirection and everything after it belongs to the comment,
    /// semicolons included. Splitting on the last one would cut a legitimate label in half.
    /// </summary>
    [Fact]
    public void A_comment_may_itself_contain_a_semicolon()
    {
        Assert.Equal(
            "Sync host; second half",
            ServiceDisplayName.Of("@file.dll,-1;Sync host; second half", "SyncHost"));
    }

    /// <summary>
    /// <b>The marker is the LEADING character, matched the same way <see cref="ServiceDescription"/>
    /// matches it.</b> An at sign in the middle of a label is somebody's label, not an indirection,
    /// and a rule that went looking for one anywhere would eat it.
    /// </summary>
    [Fact]
    public void An_at_sign_that_is_not_the_first_character_is_part_of_the_label()
    {
        Assert.Equal("Mail@Home Agent", ServiceDisplayName.Of("Mail@Home Agent", "MailHome"));
    }

    /// <summary>
    /// Nothing at all still has to produce something, because a label is not optional: the list
    /// shows it, sorts by it and searches it. This is where it parts from a description, which is
    /// allowed to answer that nobody could turn it into words.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_label_at_all_shows_the_internal_name(string? handed)
    {
        Assert.Equal("SomeService", ServiceDisplayName.Of(handed, "SomeService"));
    }

    /// <summary>
    /// <b>An at sign is not enough, and this is where the rule parts from
    /// <see cref="ServiceDescription"/> on purpose.</b> That one matches the leading character
    /// alone, because a description that guesses wrong answers "nobody could read this" and invents
    /// nothing. A label that guesses wrong is REPLACED by the internal name, so a display name the
    /// machine really holds stops being visible - which in an audit tool is the worse mistake.
    ///
    /// <b><c>@SUM(A1)</c> is in this list because a guard written for something else found it.</b>
    /// The export guard carries exactly that string through this field to prove a value a
    /// spreadsheet would evaluate is made inert, and the first version of this rule ate it before
    /// the exporter saw it. It is a payload rather than an indirection, and whoever is auditing the
    /// service carrying it needs to see it.
    /// </summary>
    [Theory]
    [InlineData("@")]
    [InlineData("@SUM(A1)")]
    [InlineData("@Home Backup Service")]
    [InlineData("@file.dll,100")]
    [InlineData("@file.dll,-")]
    public void An_at_sign_without_a_resource_number_is_somebody_s_label(string handed)
    {
        Assert.Equal(handed, ServiceDisplayName.Of(handed, "Whatever"));
    }

    /// <summary>
    /// The fallback has to exist for the rule to be total, so being handed nothing to fall back on
    /// is a programming error rather than a shape of the data.
    /// </summary>
    [Fact]
    public void The_internal_name_is_required_because_it_is_the_last_resort()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceDisplayName.Of("@file.dll,-1", null!));
    }
}
