using Bws.Core;

namespace Bws.Integration.Tests;

/// <summary>
/// A file changed after it was signed is called Tampered, not unsigned - backlog 531, 2026-10-06.
///
/// <b>Until that day it was called NotSigned.</b> The inspector asked WinVerifyTrust with
/// WTD_SAFER_FLAG, which Microsoft documents only as "Not supported", and with it a bad digest comes
/// back as "no signature at all" - so the file went on to the catalogues, was found in none, and the
/// one verdict that means somebody changed it never arrived. Measured on copies of three files with
/// tools/security-probe/tampered-copy.ps1 before anything was changed.
///
/// <b>Real WinVerifyTrust on a real signed file, changed in a folder of this test's own.</b> The
/// runtime's core library carries an embedded signature wherever these tests can run at all, so the
/// specimen is never missing. Nothing outside the temporary folder is touched, and the copy is never
/// loaded or run - it is only asked about.
/// </summary>
public sealed class TamperedSignatureTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bws-tampered-" + Guid.NewGuid().ToString("N")[..8]);

    public TamperedSignatureTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    [Trait("runs", "anywhere")]
    public void A_file_changed_after_it_was_signed_is_tampered_and_nobody_vouches_for_its_publisher()
    {
        var source = typeof(object).Assembly.Location;
        var inspector = new WindowsBinaryInspector();

        var before = inspector.Inspect(Copied(source, "unchanged.dll", change: false)).Signature;
        var after = inspector.Inspect(Copied(source, "changed.dll", change: true)).Signature;

        // The canary. Unless the untouched copy comes back trusted, whatever the changed one says is
        // about the specimen rather than about the change.
        Assert.Equal(ReadOutcome.Present, before.Outcome);
        Assert.Equal(SignatureStatus.Trusted, before.Value!.Status);

        Assert.Equal(ReadOutcome.Present, after.Outcome);
        Assert.Equal(SignatureStatus.Tampered, after.Value!.Status);

        // The certificate is still the real one, so the name is still there - and S-6 is why it no
        // longer answers for who made the file.
        Assert.NotNull(after.Value.Publisher);
        Assert.False(after.Value.VouchesForPublisher);
    }

    /// <summary>
    /// A copy of the file, with one byte turned over halfway between the end of the headers and the
    /// start of the certificate table - inside what the signature's hash covers.
    /// </summary>
    private string Copied(string source, string name, bool change)
    {
        var bytes = File.ReadAllBytes(source);

        if (change)
        {
            var table = CertificateTable(bytes);

            Assert.True(table > 0x800, $"{source} carries no certificate table of its own, so it cannot be the specimen.");

            var offset = 0x400 + ((table - 0x400) / 2);
            bytes[offset] ^= 0xFF;
        }

        var copy = Path.Combine(_folder, name);
        File.WriteAllBytes(copy, bytes);

        return copy;
    }

    /// <summary>Where the certificate table starts, from the PE security directory (data directory 4).</summary>
    private static int CertificateTable(byte[] bytes)
    {
        var optional = BitConverter.ToInt32(bytes, 0x3C) + 4 + 20;
        var directories = BitConverter.ToUInt16(bytes, optional) == 0x20B ? optional + 112 : optional + 96;

        return BitConverter.ToInt32(bytes, directories + (4 * 8));
    }
}
