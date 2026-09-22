using Bws.Core;

namespace Bws.Core.Tests;

/// <summary>
/// That refusing to reach the network never turns into a sentence about somebody's certificate.
///
/// <b>WHERE THIS CAME FROM.</b> On 2026-09-22 a runtime probe found that reading signatures
/// opened HTTP connections to a third party - measured three runs out of three, with
/// certificates.intel.com named out of the DNS cache. Turning revocation checking off, which
/// this code had done since the beginning and had a comment about, does not stop the chain
/// engine fetching a certificate it does not hold. WTD_CACHE_ONLY_URL_RETRIEVAL does.
///
/// <b>AND THAT FIX, ON ITS OWN, WOULD HAVE BOUGHT SILENCE WITH A LIE.</b> Once the engine may
/// not go and get an intermediate certificate, a properly signed file can come back non-zero -
/// and the classifier would then have reported OUR refusal as UntrustedRoot, which is a claim
/// about the machine's trust store. The owner named that risk before a line of it was written:
/// a tool whose subject is trust must not say a certificate is bad when what happened is that
/// it would not look it up.
///
/// <b>The direction that is closed by construction, and it is the half that would be
/// unforgivable.</b> Trusted comes only from a result of zero, and zero needs a chain that was
/// built and validated. A refused fetch can prevent a zero and cannot manufacture one. The last
/// test in this file is that claim, asserted over every code the others use rather than argued
/// for in a comment.
/// </summary>
public sealed class SignatureWithoutTheNetworkTests
{
    private const int Trusted = 0;
    private const int NoSignature = unchecked((int)0x800B0100);
    private const int Expired = unchecked((int)0x800B0101);
    private const int UntrustedRoot = unchecked((int)0x800B0109);
    private const int NoChain = unchecked((int)0x800B010A);
    private const int Revoked = unchecked((int)0x800B010C);
    private const int Tampered = unchecked((int)0x80096010);
    private const int Nameless = unchecked((int)0x80004005);

    /// <summary>
    /// A verdict that is a fact about the file or about its own certificate survives the quiet
    /// mode, because none of them needed the network to be reached.
    /// </summary>
    [Theory]
    [InlineData(Trusted, SignatureStatus.Trusted)]
    [InlineData(NoSignature, SignatureStatus.NotSigned)]
    [InlineData(Expired, SignatureStatus.Expired)]
    [InlineData(Tampered, SignatureStatus.Tampered)]
    public void A_verdict_about_the_file_itself_survives_the_quiet_mode(int result, SignatureStatus expected)
    {
        var reading = WindowsBinaryInspector.Settle(result, NetworkPaths.Skip, () => "Someone");

        Assert.Equal(ReadOutcome.Present, reading.Outcome);
        Assert.Equal(expected, reading.Value!.Status);
    }

    /// <summary>
    /// A verdict about the chain to a root does NOT, because in the quiet mode it cannot be
    /// told apart from this tool declining to complete the chain.
    /// </summary>
    [Theory]
    [InlineData(UntrustedRoot)]
    [InlineData(NoChain)]
    [InlineData(Nameless)]
    public void A_verdict_about_the_chain_becomes_a_refusal_in_the_quiet_mode(int result)
    {
        var reading = WindowsBinaryInspector.Settle(result, NetworkPaths.Skip, () => "Someone");

        Assert.Equal(ReadOutcome.Denied, reading.Outcome);

        // The number travels, exactly as it does for a refused read from the manager. Rule 8
        // forbids a failed read that looks like an answer, and it equally forbids one that
        // arrives without the means to diagnose it.
        Assert.Equal(result, reading.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(reading.Reason));
    }

    /// <summary>
    /// With the network allowed, every one of those is a verdict again. The switch has to work
    /// in both directions or it is not a switch, it is a removal.
    /// </summary>
    [Theory]
    [InlineData(UntrustedRoot, SignatureStatus.UntrustedRoot)]
    [InlineData(NoChain, SignatureStatus.Unknown)]
    [InlineData(Revoked, SignatureStatus.Revoked)]
    [InlineData(Nameless, SignatureStatus.Unknown)]
    public void The_same_result_is_a_verdict_again_once_the_network_is_allowed(
        int result, SignatureStatus expected)
    {
        var reading = WindowsBinaryInspector.Settle(result, NetworkPaths.Follow, () => "Someone");

        Assert.Equal(ReadOutcome.Present, reading.Outcome);
        Assert.Equal(expected, reading.Value!.Status);
        Assert.Equal(result, reading.Value.ResultCode);
    }

    /// <summary>
    /// The publisher is not read on the way to a refusal.
    ///
    /// Not a saving, although it is one - reading it opens the file a second time. It is that a
    /// refusal carries no value, so producing one would mean doing work to decorate an answer
    /// this code is declining to give.
    /// </summary>
    [Fact]
    public void Nothing_is_read_about_a_signature_this_code_is_refusing_to_report()
    {
        var asked = false;

        var reading = WindowsBinaryInspector.Settle(
            UntrustedRoot,
            NetworkPaths.Skip,
            () => { asked = true; return "Someone"; });

        Assert.Equal(ReadOutcome.Denied, reading.Outcome);
        Assert.False(asked, "the publisher was read in order to be thrown away with the refusal.");
    }

    /// <summary>
    /// And the publisher IS read when there is an answer to attach it to, which is the half
    /// that keeps the test above from passing on a function that never reads anything.
    /// </summary>
    [Fact]
    public void The_publisher_is_read_when_there_is_a_verdict_to_carry_it()
    {
        var reading = WindowsBinaryInspector.Settle(Trusted, NetworkPaths.Skip, () => "Microsoft Windows");

        Assert.Equal(ReadOutcome.Present, reading.Outcome);
        Assert.Equal("Microsoft Windows", reading.Value!.Publisher);
    }

    /// <summary>
    /// <b>NOTHING BECOMES TRUSTED THAT WAS NOT ALREADY A ZERO, IN EITHER MODE.</b>
    ///
    /// The one sentence in this file that would be a disaster to get wrong, asserted rather
    /// than reasoned about. Every other test here is about losing information honestly - this
    /// one is about never gaining any.
    /// </summary>
    [Fact]
    public void No_result_but_zero_is_ever_reported_as_trusted()
    {
        foreach (var mode in new[] { NetworkPaths.Skip, NetworkPaths.Follow })
        {
            // Zero is the one result that MAY be Trusted, so it is the one this loop skips.
            // It is in the shared list because the assertion below it - that a refusal always
            // carries a sentence - has to walk every shape, including the ones that never
            // become refusals at all.
            foreach (var code in EveryCode.Where(c => c != Trusted))
            {
                var reading = WindowsBinaryInspector.Settle(code, mode, () => "Someone");

                var claimsTrust =
                    reading.Outcome == ReadOutcome.Present
                    && reading.Value!.Status == SignatureStatus.Trusted;

                Assert.False(
                    claimsTrust,
                    $"result 0x{code:X8} in mode {mode} was reported as Trusted. Refusing to " +
                    "reach the network may cost an answer and may never produce one - a tool " +
                    "that called an unverified binary trusted would be worse than one that " +
                    "said nothing at all.");
            }
        }
    }

    /// <summary>
    /// <b>A refusal always says something, whatever number produced it.</b>
    ///
    /// Raised by the review of the pull request that introduced this file, and it was right:
    /// the first version handed <c>string.Empty</c> to the refusal whenever
    /// <c>Marshal.GetExceptionForHR</c> answered null, which it does for every non-negative
    /// HRESULT. <c>S_FALSE</c> is 1, it reaches that line, and it was already in the list this
    /// test walks - the earlier assertion simply did not ask about the sentence.
    ///
    /// A refusal with no sentence is exactly the shape rule 8 forbids: it looks like a field
    /// that was read and came back empty.
    /// </summary>
    [Fact]
    public void Every_refusal_carries_both_the_number_and_a_sentence()
    {
        foreach (var code in EveryCode)
        {
            var reading = WindowsBinaryInspector.Settle(code, NetworkPaths.Skip, () => "Someone");

            if (reading.Outcome != ReadOutcome.Denied)
            {
                continue;
            }

            // The number, either as it arrived or as Reading unwraps it. A result in the
            // FACILITY_WIN32 family is deliberately reduced to its Win32 code on the way in -
            // 0x80070005 becomes 5 - because access denied out of a signature and access denied
            // out of the manager were two different numbers for one fact, and that has its own
            // mutation entry. Asserting the raw value here would have quietly re-opened it.
            var unwrapped = ((uint)code & 0xFFFF0000u) == 0x80070000u
                ? (int)((uint)code & 0xFFFFu)
                : code;

            Assert.Equal(unwrapped, reading.ErrorCode);

            Assert.False(
                string.IsNullOrWhiteSpace(reading.Reason),
                $"the refusal produced by result 0x{code:X8} carries no sentence at all. The " +
                "number alone reaches the JSON, and a reader meets a field that was not read " +
                "and is told nothing about why.");
        }
    }

    /// <summary>
    /// Every shape a verification result can take, including the ones Windows would never
    /// produce. A policy asserted only over codes somebody expected is a policy with a hole
    /// the shape of what they did not.
    /// </summary>
    private static readonly int[] EveryCode =
    [
        Trusted, NoSignature, Expired, UntrustedRoot, NoChain, Revoked, Tampered, Nameless,
        unchecked((int)0x80070005), 1, -1, int.MinValue, int.MaxValue
    ];
}
