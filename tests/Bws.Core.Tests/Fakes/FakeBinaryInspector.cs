using System.Collections.Concurrent;
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
///
/// The records are concurrent collections because the second pass asks from several threads
/// since S5a1. This is not defensive tidying - a plain list appended from several threads
/// loses entries and sometimes throws, so every test here counting questions would have
/// become one that fails once a month for a reason nobody could reproduce. A flaky guard is
/// worse than no guard, because it teaches people to re-run instead of to look.
/// </summary>
internal sealed class FakeBinaryInspector : IBinaryInspector
{
    private readonly Dictionary<string, Reading<BinarySignature>> _signatures;
    private readonly Dictionary<string, Reading<string>> _versions;
    private readonly Dictionary<string, Reading<string>> _hashes;

    internal FakeBinaryInspector(
        Dictionary<string, Reading<BinarySignature>>? signatures = null,
        Dictionary<string, Reading<string>>? versions = null,
        Dictionary<string, Reading<string>>? hashes = null)
    {
        _signatures = signatures ?? new Dictionary<string, Reading<BinarySignature>>(StringComparer.OrdinalIgnoreCase);
        _versions = versions ?? new Dictionary<string, Reading<string>>(StringComparer.OrdinalIgnoreCase);
        _hashes = hashes ?? new Dictionary<string, Reading<string>>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every file the signature was asked about, repeats included.
    ///
    /// No longer in any particular order, and that is worth saying rather than leaving to be
    /// discovered: several threads ask at once, so the sequence records arrival and nothing
    /// about it is a property of the code under test. Count them and look for what is in
    /// there - do not read anything into which came first.
    /// </summary>
    internal ConcurrentQueue<string> SignaturesAsked { get; } = [];

    /// <summary>Every file the version was asked about, repeats included. Unordered, as above.</summary>
    internal ConcurrentQueue<string> VersionsAsked { get; } = [];

    public Reading<BinarySignature> ReadSignature(string file)
    {
        SignaturesAsked.Enqueue(file);

        // The file's own name in the publisher, so a test that mixes two files up cannot
        // pass by accident.
        return _signatures.TryGetValue(file, out var signature)
            ? signature
            : Reading<BinarySignature>.Present(
                new BinarySignature(SignatureStatus.Trusted, 0, $"Publisher of {Path.GetFileName(file)}"));
    }

    public Reading<string> ReadFileVersion(string file)
    {
        VersionsAsked.Enqueue(file);

        return _versions.TryGetValue(file, out var version)
            ? version
            : Reading<string>.Present($"1.0.0.{Path.GetFileName(file).Length}");
    }

    /// <summary>Every file the hash was asked about, repeats included. Unordered, as above.</summary>
    internal ConcurrentQueue<string> HashesAsked { get; } = [];

    public Reading<string> ReadHash(string file)
    {
        HashesAsked.Enqueue(file);

        // Derived from the name and shaped like the real thing, so a test comparing two
        // entries' hashes gets different answers for different files and the same answer for
        // the same one - which is the whole property a hash has.
        return _hashes.TryGetValue(file, out var hash)
            ? hash
            : Reading<string>.Present(
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(file.ToLowerInvariant()))));
    }
}
