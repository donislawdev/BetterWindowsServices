using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// What the system thinks of each binary, checked against Get-AuthenticodeSignature.
///
/// PowerShell is the second opinion here rather than sc.exe, which has nothing to say about
/// signatures. It is the right authority for a different reason too: it walks both of the
/// ways Windows signs a file, and the whole risk in this slice is quietly walking only one.
///
/// The listing is fetched once and shared. Verifying signatures measured at roughly five
/// seconds over 810 entries on the machine this was written against, so a test class that
/// asked for it four times would add twenty seconds to every run for no more coverage.
///
/// Read-only. Asking who signed a file changes nothing.
/// </summary>
public sealed class SignatureContractTests
{
    private static readonly Lazy<JsonElement[]> Inspected =
        new(() => CommandLineTool.Listing("--signatures"));

    [Fact]
    public void An_ordinary_listing_does_not_verify_anything()
    {
        // The deferral, checked from the outside. If this ever starts coming back filled,
        // every listing on every machine just got five seconds slower and nobody asked.
        var plain = CommandLineTool.Listing();

        Assert.All(plain, entry =>
        {
            Assert.False(entry.TryGetProperty("signature", out var signature) &&
                signature.ValueKind != JsonValueKind.Null,
                "A listing nobody asked to verify came back with signatures.");
        });

        // And it says so rather than leaving a null that reads as "there is none".
        Assert.Contains(plain, entry => NotRead(entry).Contains("signature"));
    }

    [Fact]
    public void Asking_by_switch_and_asking_by_query_both_verify()
    {
        Assert.Contains(Inspected.Value, entry => Signature(entry) is not null);

        // No switch, and the query alone is enough. Answering signed:no from an unread
        // field would be a correct query returning what reads exactly like "there are none".
        var byQuery = CommandLineTool.Listing("--query", "signed:any");

        Assert.NotEmpty(byQuery);
        Assert.All(byQuery, entry => Assert.NotNull(Signature(entry)));
    }

    [Fact]
    public void Our_verdict_agrees_with_the_system_entry_by_entry()
    {
        // Taken from the machine rather than named here. Twelve is enough to cover both
        // ways a file can be signed on any real Windows install.
        var sampled = 0;

        foreach (var entry in Inspected.Value.Where(entry => Signature(entry) is not null).Take(12))
        {
            var file = CommandLineTool.Text(entry, "binaryFile");
            var ours = Signature(entry)!.Value.GetProperty("status").GetString();

            Assert.Equal(Expected(AccordingToPowerShell(file)), ours);
            sampled++;
        }

        Assert.Equal(12, sampled);
    }

    [Fact]
    public void The_catalogue_path_is_walked_and_not_quietly_skipped()
    {
        // The guard this slice most needs, and the one a count would miss. Windows signs
        // most of its own binaries through a catalogue rather than inside the file, so a
        // build that only looked inside files would call a third of the machine unsigned -
        // measured on the machine this was written against, 189 files exactly.
        //
        // Every file we call unsigned is put back to the system for a second opinion. One
        // that the system trusts means the catalogue was never consulted.
        var unsigned = Inspected.Value
            .Where(entry => Signature(entry)?.GetProperty("status").GetString() == "notSigned")
            .ToArray();

        foreach (var entry in unsigned)
        {
            var file = CommandLineTool.Text(entry, "binaryFile");

            Assert.NotEqual("Valid", AccordingToPowerShell(file));
        }

        // And the other direction, because the check above passes trivially on a build that
        // calls everything trusted. On a working machine almost everything is signed, so a
        // large unsigned count is the shape of a verification that gave up.
        var verified = Inspected.Value.Count(entry => Signature(entry) is not null);

        Assert.True(
            unsigned.Length < verified / 20,
            $"{unsigned.Length} of {verified} entries came back unsigned, which is the shape of " +
            "a build that stopped at the signature inside the file.");
    }

    [Fact]
    public void A_publisher_comes_back_with_a_trusted_signature()
    {
        // Reading the certificate is a separate step from verifying the chain, and it is
        // the step that would fail silently: a trusted file with nobody's name on it looks
        // like data rather than like a bug.
        var trusted = Inspected.Value
            .Where(entry => Signature(entry)?.GetProperty("status").GetString() == "trusted")
            .Take(12)
            .ToArray();

        Assert.NotEmpty(trusted);

        Assert.All(trusted, entry => Assert.False(
            string.IsNullOrWhiteSpace(Signature(entry)!.Value.GetProperty("publisher").GetString()),
            $"{CommandLineTool.Text(entry, "serviceName")} is trusted and nobody signed it."));
    }

    private static JsonElement? Signature(JsonElement entry) =>
        entry.TryGetProperty("signature", out var signature) && signature.ValueKind != JsonValueKind.Null
            ? signature
            : null;

    private static string[] NotRead(JsonElement entry) =>
        entry.TryGetProperty("notRead", out var fields) && fields.ValueKind == JsonValueKind.Array
            ? [.. fields.EnumerateArray().Select(field => field.GetString() ?? string.Empty)]
            : [];

    /// <summary>
    /// Our name for what PowerShell called it. Only the two statuses a real machine
    /// produces are mapped - anything else fails the comparison rather than being quietly
    /// accepted, which is the point of having a second opinion at all.
    ///
    /// The verdict is compared and the publisher deliberately is not. Measured on
    /// 2026-08-01 over 535 files: the verdict agrees on every one, and the name differs on
    /// 44 of them, where a file carries both an embedded signature and a catalogue entry
    /// naming different signers. PowerShell reports the catalogue's, this tool reports the
    /// file's, and the file's is the one that travels with the binary rather than with the
    /// machine reading it. Written here so the difference is not rediscovered as a bug.
    /// </summary>
    private static string Expected(string status) => status switch
    {
        "Valid" => "trusted",
        "NotSigned" => "notSigned",
        _ => status
    };

    private static string AccordingToPowerShell(string file) =>
        CommandLineTool.PowerShell(
            $"(Get-AuthenticodeSignature -LiteralPath '{file.Replace("'", "''")}').Status.ToString()");
}
