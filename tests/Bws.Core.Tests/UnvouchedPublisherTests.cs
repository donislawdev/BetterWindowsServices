using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// A publisher the signature does not vouch for - security report S-6, owner's decisions of
/// 2026-10-06.
///
/// The name is read back without walking the chain, so beside any verdict other than trusted it is
/// only what the certificate claims. These hold the rule the query keeps about that: a question
/// about what the name SAYS is unsure, with a reservation of its own, and a question about whether
/// there IS a name is not.
/// </summary>
public sealed class UnvouchedPublisherTests
{
    private const string Microsoft = "Microsoft Windows";

    [Fact]
    public void Only_a_trusted_verdict_vouches_for_the_name()
    {
        // Every verdict there is, so a verdict added later has to be decided rather than inherited.
        foreach (var status in Enum.GetValues<SignatureStatus>())
        {
            Assert.Equal(status == SignatureStatus.Trusted, new BinarySignature(status, 0, Microsoft).VouchesForPublisher);
        }
    }

    [Fact]
    public void Leaving_microsoft_out_keeps_the_microsoft_file_somebody_changed_and_says_so()
    {
        // The audit question the field exists for, and the one S-6 found answered wrongly: the
        // changed file was the one entry this exclusion dropped, with full confidence.
        var result = Filter("!publisher:microsoft", Machine());

        Assert.Equal(new[] { "Changed", "Nobody" }, Names(result));
        Assert.Equal(1, result.Unvouched);

        // Its own reservation. The field was read, so "could not be read" would be untrue.
        Assert.Equal(0, result.Unreadable);
    }

    [Fact]
    public void Asking_for_microsoft_leaves_the_changed_file_out_and_says_so()
    {
        var result = Filter("publisher:microsoft", Machine());

        Assert.Equal(new[] { "Genuine" }, Names(result));
        Assert.Equal(1, result.Unvouched);
    }

    [Fact]
    public void Every_verdict_but_trusted_makes_a_named_publisher_unsure()
    {
        foreach (var status in Enum.GetValues<SignatureStatus>())
        {
            var result = Filter("publisher:contoso", [Signed("Entry", status, "Contoso")]);
            var trusted = status == SignatureStatus.Trusted;

            Assert.Equal(trusted ? 1 : 0, result.Entries.Count);
            Assert.Equal(trusted ? 0 : 1, result.Unvouched);
        }
    }

    [Fact]
    public void Whether_there_is_a_name_stays_a_certain_answer()
    {
        // none, any and ? ask about the reading, and the reading went fine - there is a name, and
        // the window shows it. Only a question about what it says rests on believing it.
        var changed = Signed("Changed", SignatureStatus.Tampered, Microsoft);

        var any = Filter("publisher:any", [changed]);
        var none = Filter("publisher:none", [changed]);
        var unanswered = Filter("publisher:?", [changed]);

        Assert.Equal(new[] { "Changed" }, Names(any));
        Assert.Empty(none.Entries);
        Assert.Empty(unanswered.Entries);
        Assert.Equal(0, any.Unvouched + none.Unvouched + unanswered.Unvouched);
        Assert.Equal(0, any.Unreadable + none.Unreadable + unanswered.Unreadable);
    }

    [Fact]
    public void A_file_nobody_signed_is_certainly_not_from_microsoft()
    {
        // The second case the rule was tried against before it was written down. Nobody signed it,
        // so Microsoft did not - a certain no, and counting it would put every unsigned file on the
        // machine into the admission.
        var nobody = Signed("Nobody", SignatureStatus.NotSigned, null);

        var asked = Filter("publisher:microsoft", [nobody]);
        var excluded = Filter("!publisher:microsoft", [nobody]);

        Assert.Empty(asked.Entries);
        Assert.Equal(new[] { "Nobody" }, Names(excluded));
        Assert.Equal(0, asked.Unvouched + excluded.Unvouched);
    }

    [Fact]
    public void A_refused_signature_is_said_to_be_unreadable_rather_than_unvouched()
    {
        // The order of the two questions. A refused signature has no name to doubt, and its own
        // reservation is the truer sentence.
        var refused = Entries.Any with
        {
            ServiceName = "Refused",
            Signature = Reading<BinarySignature>.Denied(Entries.AccessDenied, "Access is denied.")
        };

        var result = Filter("publisher:microsoft", [refused]);

        Assert.Equal(1, result.Unreadable);
        Assert.Equal(0, result.Unvouched);
    }

    [Fact]
    public void Alternatives_inside_one_member_keep_the_reservation()
    {
        // A match wins inside a member and the reservations gathered along the way are kept, so
        // writing the publisher twice does not wash the doubt out.
        var result = Filter("publisher:contoso,microsoft", Machine());

        Assert.Equal(new[] { "Genuine" }, Names(result));
        Assert.Equal(1, result.Unvouched);
    }

    private static ScmEntry[] Machine() =>
    [
        Signed("Genuine", SignatureStatus.Trusted, Microsoft),
        Signed("Changed", SignatureStatus.Tampered, Microsoft),
        Signed("Nobody", SignatureStatus.NotSigned, null)
    ];

    private static ScmEntry Signed(string name, SignatureStatus status, string? publisher) => Entries.Any with
    {
        ServiceName = name,
        Signature = Reading<BinarySignature>.Present(new BinarySignature(status, 0, publisher))
    };

    private static QueryResult Filter(string query, IReadOnlyList<ScmEntry> entries)
    {
        var parsed = QueryParser.Parse(query);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));

        return parsed.Query!.Filter(entries);
    }

    private static string[] Names(QueryResult result) => [.. result.Entries.Select(entry => entry.ServiceName)];
}
