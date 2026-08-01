using Bws.Core;

namespace Bws.Core.Tests.Fakes;

/// <summary>
/// Stands in for the file system and the trust providers.
///
/// Counts what it was asked, which is the point rather than a convenience: the second pass
/// is the most expensive thing this tool does, and the only way to hold "one question per
/// distinct file" to its word is to count the questions.
///
/// Answers differ per file by default. A double that gave every file the same answer would
/// let a test about which file was asked pass while checking nothing - the trap ADR-10
/// spells out and this project has already fallen into once.
/// </summary>
internal sealed class FakeBinaryInspector : IBinaryInspector
{
    private readonly Dictionary<string, Reading<BinarySignature>> _signatures;
    private readonly Dictionary<string, Reading<string>> _versions;

    internal FakeBinaryInspector(
        Dictionary<string, Reading<BinarySignature>>? signatures = null,
        Dictionary<string, Reading<string>>? versions = null)
    {
        _signatures = signatures ?? new Dictionary<string, Reading<BinarySignature>>(StringComparer.OrdinalIgnoreCase);
        _versions = versions ?? new Dictionary<string, Reading<string>>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Every file the signature was asked about, in order, repeats included.</summary>
    internal List<string> SignaturesAsked { get; } = [];

    /// <summary>Every file the version was asked about, in order, repeats included.</summary>
    internal List<string> VersionsAsked { get; } = [];

    public Reading<BinarySignature> ReadSignature(string file)
    {
        SignaturesAsked.Add(file);

        // The file's own name in the publisher, so a test that mixes two files up cannot
        // pass by accident.
        return _signatures.TryGetValue(file, out var signature)
            ? signature
            : Reading<BinarySignature>.Present(
                new BinarySignature(SignatureStatus.Trusted, 0, $"Publisher of {Path.GetFileName(file)}"));
    }

    public Reading<string> ReadFileVersion(string file)
    {
        VersionsAsked.Add(file);

        return _versions.TryGetValue(file, out var version)
            ? version
            : Reading<string>.Present($"1.0.0.{Path.GetFileName(file).Length}");
    }
}
