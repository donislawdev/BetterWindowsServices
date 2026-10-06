using System.Diagnostics;
using System.Security.Cryptography;

namespace Bws.Core.Tests;

/// <summary>
/// One opening carries the whole inspection of a file - package SB, security report S-5, 2026-10-06.
///
/// <b>Real WinVerifyTrust on real files, copied into a folder of this test's own.</b> The runtime's
/// core library carries an embedded signature and a version wherever these tests can run, and this
/// test assembly carries neither the signature nor that version - so a handle on one and a path to
/// the other disagree in every field, which is the only arrangement in which "the answer is about the
/// handle" can be told apart from "the answer is about the path". Nothing outside the folder is
/// touched, and no copy is loaded or run.
/// </summary>
public sealed class OneOpeningTests : IDisposable
{
    /// <summary>ERROR_SHARING_VIOLATION, the low word of the HResult the opening fails with.</summary>
    private const int SharingViolation = 32;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bws-one-opening-" + Guid.NewGuid().ToString("N")[..8]);

    public OneOpeningTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Every_field_is_about_the_file_the_handle_holds_and_none_about_the_path_beside_it()
    {
        var held = Copied(typeof(object).Assembly.Location, "held.dll");
        var named = Copied(typeof(OneOpeningTests).Assembly.Location, "named.dll");
        var inspector = new WindowsBinaryInspector();

        // The canaries: the path on its own would give a different answer in every field.
        Assert.NotEqual(SignatureStatus.Trusted, inspector.Inspect(named).Signature.Value!.Status);
        Assert.NotEqual(FileVersionInfo.GetVersionInfo(held).FileVersion, FileVersionInfo.GetVersionInfo(named).FileVersion);

        using var handle = File.OpenHandle(held, FileMode.Open, FileAccess.Read, FileShare.Read);
        var inspection = inspector.Inspect(named, handle);

        Assert.Equal(SignatureStatus.Trusted, inspection.Signature.Value!.Status);
        Assert.NotNull(inspection.Signature.Value.Publisher);
        Assert.Equal(FileVersionInfo.GetVersionInfo(held).FileVersion, inspection.Version.Value);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(held))), inspection.Hash.Value);
    }

    [Fact]
    public void The_signature_is_the_one_on_the_handle_even_beside_another_file_s_path()
    {
        // Asked of the signature alone, because through Inspect the path beside the handle is the
        // handle's own final path - both name one file, and a guard there could not tell whether the
        // handle was handed to WinTrust at all. The mutation registry said so on 2026-10-06.
        var held = Copied(typeof(object).Assembly.Location, "held.dll");
        var named = Copied(typeof(OneOpeningTests).Assembly.Location, "named.dll");

        using var handle = File.OpenHandle(held, FileMode.Open, FileAccess.Read, FileShare.Read);
        var signature = new WindowsBinaryInspector().Signature(named, handle);

        Assert.Equal(ReadOutcome.Present, signature.Outcome);
        Assert.Equal(SignatureStatus.Trusted, signature.Value!.Status);
    }

    [Fact]
    public void A_file_somebody_holds_open_for_writing_is_refused_in_all_three_fields_alike()
    {
        // The owner's decision of 2026-10-06: until then only the hash said so, and the signature
        // beside it was read by path anyway - a verdict about a file whose hash nobody computed.
        var file = Copied(typeof(object).Assembly.Location, "written.dll");

        using var writer = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        var inspection = new WindowsBinaryInspector().Inspect(file);

        Assert.Equal(ReadOutcome.Denied, inspection.Signature.Outcome);
        Assert.Equal(ReadOutcome.Denied, inspection.Version.Outcome);
        Assert.Equal(ReadOutcome.Denied, inspection.Hash.Outcome);
        Assert.Equal(SharingViolation, inspection.Hash.ErrorCode & 0xFFFF);
        Assert.Equal(inspection.Hash.ErrorCode, inspection.Signature.ErrorCode);
    }

    [Fact]
    public void A_pipe_is_not_opened_even_when_the_network_may_be_followed()
    {
        // A name nobody serves, so nothing could be reached even if the shape rule were gone - the
        // test then fails on "absent" rather than on anything a pipe server could do.
        var pipe = @"\\.\pipe\bws-no-such-pipe-" + Guid.NewGuid().ToString("N");

        var inspection = new WindowsBinaryInspector(NetworkPaths.Follow).Inspect(pipe);

        Assert.Equal(ReadOutcome.NotRead, inspection.Signature.Outcome);
        Assert.Equal(ReadOutcome.NotRead, inspection.Version.Outcome);
        Assert.Equal(ReadOutcome.NotRead, inspection.Hash.Outcome);
    }

    private string Copied(string source, string name)
    {
        var copy = Path.Combine(_folder, name);
        File.Copy(source, copy);

        return copy;
    }
}
