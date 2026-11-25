using Bws.Core.Planning;

namespace Bws.Core.Tests.Fakes;

/// <summary>
/// Recovery on the fake machine: what each entry has, what was written, and a refusal to hand out - the same
/// machine the plan reads (it is the plan's <see cref="IRecoveryReader"/> as well) and the run writes to.
/// Since 2026-11-24, backlog 555 part (a2).
/// </summary>
internal sealed partial class FakeScmControl : IRecoveryReader
{
    private readonly Dictionary<string, Reading<RecoverySettings>> _recoveries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every recovery write this was asked for, in order.</summary>
    internal List<(string Name, RecoveryChange Change)> RecoveryWrites { get; } = [];

    /// <summary>The number the next recovery write is refused with, or nothing to let it land.</summary>
    internal int? RecoveryRefusedWith { get; set; }

    /// <summary>Gives an entry a recovery - or, with <see cref="Reading{T}.Denied"/>, a refusal to read it.</summary>
    internal FakeScmControl WithRecovery(string serviceName, Reading<RecoverySettings> recovery)
    {
        _recoveries[serviceName] = recovery;
        return this;
    }

    /// <summary>Changes an entry's recovery behind the plan's back, as somebody else would between preview and press.</summary>
    internal void ChangeRecoveryBehindTheBack(string serviceName, RecoverySettings recovery) =>
        _recoveries[serviceName] = Reading<RecoverySettings>.Present(recovery);

    public ControlAnswer ConfigureRecovery(string serviceName, RecoveryChange change)
    {
        RecoveryWrites.Add((serviceName, change));

        if (RecoveryRefusedWith is { } refused)
        {
            return ControlAnswer.Refused(refused, "Access is denied.");
        }

        var now = ReadRecovery(serviceName);

        _recoveries[serviceName] = Reading<RecoverySettings>.Present(change.Over(now.IsPresent ? now.Value! : RecoveryChange.Nothing));

        return ControlAnswer.Done();
    }

    /// <summary>An entry nobody gave a recovery has none - an empty list, no texts, the flag off.</summary>
    public Reading<RecoverySettings> ReadRecovery(string serviceName) =>
        _recoveries.GetValueOrDefault(serviceName, Reading<RecoverySettings>.Present(RecoveryChange.Nothing));

    public Reading<RecoverySettings> ReadWhole(string serviceName) => ReadRecovery(serviceName);
}
