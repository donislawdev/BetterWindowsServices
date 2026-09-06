using Bws.Core.Planning;

namespace Bws.Core.Tests.Fakes;

/// <summary>
/// The measured machine the plan tests reason about, and how they ask it for a plan.
///
/// <b>Its own file because the size ratchet said so on 2026-09-06, and the seam it pointed at was
/// already written down at the head of <c>PlanBuilderTests</c>.</b> That summary describes a
/// MACHINE - who depends on whom, measured on a real one - and the tests beside it describe what a
/// plan must say about such a machine. Those are different subjects, and the first one is now
/// needed by more than one file.
///
/// <b>Not a partial class of the tests.</b> Continuing the same type in a second file would satisfy
/// a ratchet that counts files while leaving the type exactly as large, which this project calls
/// gaming a guard rather than answering it. This is the shape <c>Entries</c> and <c>Specimens</c>
/// already use: the fixture is a thing with a name, and the tests are about something else.
///
/// The chain was measured on a real machine on 2026-08-01: MRxSmb20 is needed by
/// LanmanWorkstation, which is needed by SessionEnv and Netlogon. The manager's own answer already
/// reaches past the first hop, so the double states the transitive sets exactly as the manager
/// gives them.
/// </summary>
internal static class DependencyChain
{
    internal static FakeScmCatalog Chain(IReadOnlyList<string>? asListed = null)
    {
        var entries = new List<ScmEntry>(Specimens.All)
        {
            Running("MRxSmb20", "SMB 2.0 Redirector"),
            Running("LanmanWorkstation", "Stacja robocza"),
            Running("SessionEnv", "Konfiguracja pulpitu zdalnego"),
            Running("Netlogon", "Logowanie do sieci")
        };

        var listed = asListed ?? ["SessionEnv", "Netlogon", "LanmanWorkstation"];

        return new FakeScmCatalog(entries)
            .DependedOnBy("MRxSmb20", [.. listed])
            .DependedOnBy("LanmanWorkstation", "SessionEnv", "Netlogon");
    }

    internal static ScmEntry Running(string serviceName, string displayName) =>
        Entries.Named(serviceName, displayName) with
        {
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            ProcessId = Reading<int>.Present(4444)
        };

    /// <summary>
    /// Disabled and running at once, which is not a contradiction and is the case that
    /// matters here. Measured on a real machine: switching a service to disabled leaves it
    /// running until something stops it, which is glossary pitfall P7.
    /// </summary>
    internal static ScmEntry Disabled(string serviceName, string displayName) =>
        Running(serviceName, displayName) with
        {
            StartType = Reading<StartType>.Present(Core.StartType.Disabled)
        };

    internal static FakeScmCatalog Rebuild(
        FakeScmCatalog catalog, string serviceName, Func<ScmEntry, ScmEntry> change)
    {
        var entries = catalog.ReadAll()
            .Select(entry => string.Equals(entry.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)
                ? change(entry)
                : entry)
            .ToList();

        return new FakeScmCatalog(entries)
            .DependedOnBy("MRxSmb20", "SessionEnv", "Netlogon", "LanmanWorkstation")
            .DependedOnBy("LanmanWorkstation", "SessionEnv", "Netlogon");
    }

    /// <summary>
    /// Builds with the cascade included, which is what most of these are about. The plain
    /// form, where it is not, has tests of its own.
    /// </summary>
    internal static OperationPlan Plan(ActionKind kind, string serviceName, FakeScmCatalog? catalog = null) =>
        Plan(kind, serviceName, includeDependents: true, catalog);

    internal static OperationPlan Plan(
        ActionKind kind, string serviceName, bool includeDependents, FakeScmCatalog? catalog = null)
    {
        catalog ??= Specimens.Catalog();

        return new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(kind, serviceName, includeDependents));
    }

    internal static PlanWarning Warning(OperationPlan plan, PlanWarningKind kind) =>
        Assert.Single(plan.Warnings, warning => warning.Kind == kind);
}
