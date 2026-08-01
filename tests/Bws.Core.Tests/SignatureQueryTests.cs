using Bws.Core.Querying;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Asking who signed the file an entry runs.
///
/// The specification wrote <c>signed:no</c> into its own example long before there was any
/// data behind it, and the query language document has reserved the field ever since. This
/// is where it starts answering.
/// </summary>
public sealed class SignatureQueryTests
{
    [Fact]
    public void No_means_windows_would_not_run_it_quietly_rather_than_carries_no_signature()
    {
        // The distinction the field exists for. An expired certificate is a signature, and
        // answering "yes, signed" about one would be true and useless to somebody auditing
        // a machine.
        Assert.Contains("BTHMODEM", Match("signed:no"));
        Assert.DoesNotContain("Spooler", Match("signed:no"));

        Assert.Contains("Spooler", Match("signed:yes"));
        Assert.DoesNotContain("BTHMODEM", Match("signed:yes"));
    }

    [Fact]
    public void A_status_can_be_asked_for_by_its_own_name_underneath_the_two_groups()
    {
        Assert.Contains("BTHMODEM", Match("signed:notSigned"));
        Assert.Equal(Match("signed:no"), Match("signed:notSigned"));
    }

    [Fact]
    public void Unknown_is_not_swept_in_with_the_untrusted_ones()
    {
        // A result this code could not name is a gap in our naming, not a finding about the
        // file. Folding it into "no" would turn our own ignorance into an accusation - the
        // same mistake the trigger kinds nearly made, where the value the interop metadata
        // could not name turned out to be the second most common on the machine.
        var unnamed = Entries.Any with
        {
            ServiceName = "UnnamedResult",
            Signature = Reading<BinarySignature>.Present(
                new BinarySignature(SignatureStatus.Unknown, unchecked((int)0x80096005), Publisher: null)),
            FileVersion = Reading<string>.Absent()
        };

        Assert.Empty(Over([unnamed], "signed:no"));
        Assert.Contains("UnnamedResult", Over([unnamed], "signed:unknown"));
    }

    [Fact]
    public void The_publisher_is_askable_and_absent_on_something_nobody_signed()
    {
        Assert.Contains("Spooler", Match("publisher:microsoft"));

        // Read, and there is genuinely nobody. Different from a file nobody looked at, and
        // different again from one whose configuration was refused.
        Assert.Contains("BTHMODEM", Match("publisher:none"));
        Assert.DoesNotContain("BTHMODEM", Match("publisher:any"));
    }

    [Fact]
    public void A_refusal_further_up_is_not_answered_as_unsigned()
    {
        // Rule 8 on this field. The entry whose configuration was refused knows nothing
        // about its file, so it is in neither group and is found by asking for what could
        // not be read.
        Assert.DoesNotContain("Locked", Match("signed:no"));
        Assert.DoesNotContain("Locked", Match("signed:yes"));
        Assert.Contains("Locked", Match("signed:?"));
    }

    [Fact]
    public void Asking_about_signatures_says_the_signatures_are_needed()
    {
        // What makes "bws list --query signed:no" work without a switch. Answering it from
        // an unread field would be a correct query returning what reads exactly like
        // "there are none".
        Assert.Equal(ExtraRead.Signatures, Needs("signed:no"));
        Assert.Equal(ExtraRead.Signatures, Needs("publisher:microsoft"));

        // And the ordinary listing stays fast, which is the whole point of asking.
        Assert.Equal(ExtraRead.None, Needs("start:auto !status:running"));
        Assert.Equal(ExtraRead.None, Needs("file:missing"));
        Assert.Equal(ExtraRead.None, Needs("spooler"));
    }

    [Fact]
    public void Asking_about_memory_does_not_drag_the_signatures_along()
    {
        // The reason this answer is a set of flags rather than a yes. Verifying every
        // signature on the machine measured 4620-7656 ms, and reading what the processes
        // are using measured under a millisecond - so answering a question about memory by
        // doing both would cost four thousand times what was asked for.
        Assert.Equal(ExtraRead.Memory, Needs("memory:>100MB"));
        Assert.Equal(ExtraRead.Signatures | ExtraRead.Memory, Needs("memory:>100MB signed:no"));
    }

    private static ExtraRead Needs(string query) => QueryParser.Parse(query).Query!.Needs;

    [Fact]
    public void An_unread_signature_answers_nothing_at_all()
    {
        // The state the whole switch exists to avoid presenting as an answer. Over the
        // ordinary catalogue - the one nobody ran a second pass on - every one of these is
        // empty, and that is correct rather than broken.
        Assert.Empty(Over(Specimens.All, "signed:yes"));
        Assert.Empty(Over(Specimens.All, "signed:no"));
        Assert.Empty(Over(Specimens.All, "publisher:any"));
    }

    [Fact]
    public void A_bare_word_does_not_reach_the_publisher()
    {
        // Deliberate, and the reason is worth keeping: the publisher is not read unless
        // somebody asks, so including it would make a bare word mean one thing with
        // --signatures and another without it. A query whose meaning depends on a switch
        // elsewhere is worse than one that covers less.
        Assert.Empty(Over(Specimens.Inspected, "microsoft")
            .Except(Over(Specimens.Inspected, "publisher:microsoft"), StringComparer.OrdinalIgnoreCase)
            .Except(["Spooler"], StringComparer.OrdinalIgnoreCase));

        Assert.DoesNotContain("BTHMODEM", Over(Specimens.Inspected, "microsoft windows"));
    }

    private static List<string> Match(string query) => Over(Specimens.Inspected, query);

    private static List<string> Over(IReadOnlyList<ScmEntry> entries, string query)
    {
        var parsed = QueryParser.Parse(query);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));

        return [.. parsed.Query!.Filter(entries).Entries.Select(entry => entry.ServiceName)];
    }
}
