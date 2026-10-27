namespace Bws.Core.Tests;

/// <summary>
/// What the second catalogue question, asked with SHA-1, may answer - logic report C03, owner's decision of
/// 2026-10-26 "two cases apart". A catalogue found and then not verified is a refusal with Windows' number.
/// No catalogue, or no way to ask, is "not signed" as before.
///
/// <b>The rule without Windows</b>, because no machine here has a catalogue that fails once found - the
/// owner's has no file only an SHA-1 catalogue names at all (R-6, 2026-10-05). <b>And since the review of PR
/// #96 the questions too</b>, through <see cref="WindowsBinaryInspector.Rescued"/>: the rule held on its own
/// would stay green if the answer to the SHA-1 question stopped reaching it.
/// </summary>
public sealed class CatalogueRescueTests
{
    private const int TrustFailure = unchecked((int)0x80096010);
    private const int NoSignature = unchecked((int)0x800B0100);

    private static readonly WindowsBinaryInspector.CatalogueAnswer NotListed = new(Signature: null, Listed: false);

    [Fact]
    public void A_file_only_an_sha1_catalogue_lists_that_then_refuses_is_a_refusal_with_its_number()
    {
        var asked = new List<string>();

        var answer = WindowsBinaryInspector.Rescued(algorithm =>
        {
            asked.Add(algorithm);

            return algorithm == "SHA1"
                ? new(Reading<BinarySignature>.Denied(TrustFailure, "The digital signature of the object did not verify."), Listed: true)
                : NotListed;
        });

        Assert.Equal(["SHA256", "SHA1"], asked);
        Assert.Equal(ReadOutcome.Denied, answer.Outcome);
        Assert.Equal(TrustFailure, answer.ErrorCode);
    }

    [Fact]
    public void A_file_an_sha256_catalogue_lists_is_never_asked_about_again()
    {
        var signed = Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.Trusted, 0, Publisher: "Microsoft Windows"));
        var asked = new List<string>();

        var answer = WindowsBinaryInspector.Rescued(algorithm =>
        {
            asked.Add(algorithm);

            return new(signed, Listed: true);
        });

        Assert.Equal(["SHA256"], asked);
        Assert.Equal(signed, answer);
    }

    [Fact]
    public void A_file_no_catalogue_lists_under_either_is_not_signed()
    {
        var answer = WindowsBinaryInspector.Rescued(_ => NotListed);

        Assert.Equal(SignatureStatus.NotSigned, answer.Value!.Status);
    }

    [Fact]
    public void A_catalogue_found_and_not_verified_is_a_refusal_with_its_number()
    {
        var refused = Reading<BinarySignature>.Denied(TrustFailure, "The digital signature of the object did not verify.");

        var answer = WindowsBinaryInspector.AfterSha1(refused, listed: true);

        Assert.Equal(ReadOutcome.Denied, answer.Outcome);
        Assert.Equal(TrustFailure, answer.ErrorCode);
    }

    [Fact]
    public void A_catalogue_found_and_verified_is_its_verdict()
    {
        var signed = Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.Trusted, 0, Publisher: "Microsoft Windows"));

        Assert.Equal(signed, WindowsBinaryInspector.AfterSha1(signed, listed: true));
    }

    [Fact]
    public void No_catalogue_is_not_signed()
    {
        var answer = WindowsBinaryInspector.AfterSha1(null, listed: false);

        Assert.Equal(SignatureStatus.NotSigned, answer.Value!.Status);
        Assert.Equal(NoSignature, answer.Value.ResultCode);
    }

    [Fact]
    public void A_machine_that_will_not_ask_with_sha1_is_not_signed_as_before()
    {
        // Refused before any catalogue was looked for - the context, or the hash - so nothing is known
        // about a catalogue, and the SHA-256 question already answered that the file is in none.
        var refused = Reading<BinarySignature>.Denied(TrustFailure, "SHA-1 is not available.");

        var answer = WindowsBinaryInspector.AfterSha1(refused, listed: false);

        Assert.Equal(ReadOutcome.Present, answer.Outcome);
        Assert.Equal(SignatureStatus.NotSigned, answer.Value!.Status);
    }
}
