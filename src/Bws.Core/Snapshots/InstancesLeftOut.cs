namespace Bws.Core.Snapshots;

/// <summary>
/// How many per-user session copies each side held and the comparison left out.
///
/// <b>Counted rather than dropped, since 2026-09-30 - stability report D-1, owner's decision.</b> A
/// copy belongs to one signed-in session and comes and goes with it, so comparing them reported
/// drift nobody made - the glossary calls it a session copy. Leaving them out without a number
/// would be the silence rule 8 forbids: somebody reading "no differences" is owed how much was not
/// looked at.
/// </summary>
public sealed record InstancesLeftOut(int Earlier, int Later)
{
    /// <summary>Whether there is anything to say at all.</summary>
    public bool Any => Earlier > 0 || Later > 0;
}
