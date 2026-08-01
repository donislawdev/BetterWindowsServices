using Bws.Core;

namespace Bws.Core.Tests.Fakes;

/// <summary>
/// A service control manager that is not one.
///
/// This is the seam ADR-10 exists for: the real manager cannot be persuaded to produce a
/// particular situation on demand, so anything that reasons about entries is tested
/// against this instead. Two situations matter enough on their own to justify it.
///
/// The refusal path cannot be reached on the owner's machine at all. 05-PRZYPADKI-BRZEGOWE
/// records that reading configuration through the manager was refused zero times out of
/// 811 without administrator rights, and says in as many words that the refusal path
/// remains unverified on a real system. Here it is one line.
///
/// The other is timing. An entry that is halfway through starting exists for a second or
/// two on a real machine and never when a test wants one.
/// </summary>
internal sealed class FakeScmCatalog : IScmCatalog
{
    private readonly IReadOnlyList<ScmEntry> _entries;

    internal FakeScmCatalog(params ScmEntry[] entries) => _entries = entries;

    internal FakeScmCatalog(IEnumerable<ScmEntry> entries) => _entries = [.. entries];

    /// <summary>How many times somebody asked. Reading is not supposed to be repeated per entry.</summary>
    internal int Reads { get; private set; }

    public IReadOnlyList<ScmEntry> ReadAll()
    {
        Reads++;
        return _entries;
    }
}
