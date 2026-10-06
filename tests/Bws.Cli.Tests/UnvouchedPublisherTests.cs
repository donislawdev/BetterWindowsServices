using Bws.Core;
using Bws.Core.Querying;

namespace Bws.Cli.Tests;

/// <summary>
/// A publisher the signature does not vouch for, as the terminal says it - security report S-6,
/// owner's decisions of 2026-10-06. The name never stands bare, and a query resting on one says
/// so in its own sentence. The JSON keeps the bare name, which ListingJson and the snapshot
/// contract tests hold.
/// </summary>
public sealed class UnvouchedPublisherTests
{
    private const string Microsoft = "Microsoft Windows";

    [Fact]
    public void Show_marks_a_publisher_nobody_vouches_for()
    {
        var changed = EntryReport.Render(Signed(SignatureStatus.Tampered), full: false);
        var genuine = EntryReport.Render(Signed(SignatureStatus.Trusted), full: false);

        Assert.Contains(Texts.Of("cli.show.publisherNotVerified", Microsoft), changed, StringComparison.Ordinal);
        Assert.Contains(Microsoft, genuine, StringComparison.Ordinal);
        Assert.DoesNotContain(Texts.Of("cli.show.publisherNotVerified", Microsoft), genuine, StringComparison.Ordinal);
    }

    [Fact]
    public void The_listing_cell_says_it_inside_the_brackets()
    {
        // The verdict stands right beside the name here, and the mark goes in anyway - one rule for
        // every place a name appears.
        var changed = ListingTable.Render([Signed(SignatureStatus.Tampered)]);
        var genuine = ListingTable.Render([Signed(SignatureStatus.Trusted)]);

        Assert.Contains("Tampered (Microsoft Windows, not verified)", changed, StringComparison.Ordinal);
        Assert.Contains("Trusted (Microsoft Windows)", genuine, StringComparison.Ordinal);
    }

    [Fact]
    public void A_query_says_how_many_entries_rest_on_a_publisher_nobody_vouches_for()
    {
        var all = Execution.QueryAdmissions(new QueryResult([], Unreadable: 1, TooCostly: 2, Unvouched: 3));

        Assert.Equal(
            new[]
            {
                Texts.Of("cli.warning.queryIncomplete.one", 1),
                Texts.Of("cli.warning.queryUnvouched.many", 3),
                Texts.Of("cli.warning.queryTooCostly.many", 2)
            },
            all);

        var one = Execution.QueryAdmissions(new QueryResult([], Unreadable: 0, TooCostly: 0, Unvouched: 1));

        Assert.Equal(new[] { Texts.Of("cli.warning.queryUnvouched.one", 1) }, one);
        Assert.Empty(Execution.QueryAdmissions(new QueryResult([], Unreadable: 0, TooCostly: 0, Unvouched: 0)));
    }

    private static ScmEntry Signed(SignatureStatus status) => Entries.Full with
    {
        Signature = Reading<BinarySignature>.Present(new BinarySignature(status, 0, Microsoft))
    };
}
