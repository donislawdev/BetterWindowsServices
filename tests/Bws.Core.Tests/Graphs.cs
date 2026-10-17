using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;
using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// Small machines with a dependency graph, generated, for the properties that build a plan over one.
///
/// <b>Its own file rather than more of <see cref="Machines"/>, because it answers a different
/// question.</b> Machines answers "what can a comparison see". This answers "what can stand in the way
/// of a stop": who depends on whom, who shares a process, who is already down, and what the manager
/// says when it is asked who breaks.
///
/// <b>The manager's answer is worked out from the edges, the way the manager gives it.</b> Measured on
/// 2026-08-01 (the header of PlanBuilderTests): the answer already reaches past the first hop, so the
/// answer about a name is everything that depends on it at any distance. Over a cycle that answer names
/// the name among its own dependants. What the real manager answers over a cycle is NOT CHECKED - the
/// API refuses to write one (ChangeServiceConfig fails with ERROR_CIRCULAR_DEPENDENCY, Microsoft Learn,
/// read 2026-10-17) and the registry route was never tried - so this is the plain answer, said as such.
///
/// <b>Then spoiled, the ways a real answer can be.</b> Refused, naming somebody the listing does not
/// have (installed between the listing and the plan), naming one dependant twice in another case
/// (backlog 305), and in an order nobody promised.
///
/// <b>Two graphs in three are acyclic by construction</b>, because the order a plan stops things in is
/// only a question with one answer when nothing goes round in a circle - and a generator that drew
/// cycles most of the time would leave that half of the property checking almost nothing.
/// </summary>
internal static class Graphs
{
    private static readonly string[] Names = ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot"];

    /// <summary>A name the manager can give and the listing does not have.</summary>
    internal const string Newcomer = "Newcomer";

    /// <summary>Running three times in eight, because a stop is only interesting over running entries.</summary>
    private static readonly EntryStatus[] Statuses =
    [
        EntryStatus.Running, EntryStatus.Running, EntryStatus.Running, EntryStatus.Stopped,
        EntryStatus.Stopped, EntryStatus.StopPending, EntryStatus.StartPending, EntryStatus.Paused
    ];

    private static readonly ActionKind[] Kinds = Enum.GetValues<ActionKind>();

    private static readonly StartSetting[] Settings = Enum.GetValues<StartSetting>();

    /// <summary>
    /// One entry, with a name given later. A driver one time in ten, sharing a process half the time,
    /// and the three process numbers few enough that neighbours are common.
    /// </summary>
    private static readonly Gen<Func<string, ScmEntry>> AnEntry =
        Gen.Select(Gen.Int[0, Statuses.Length - 1], Gen.Int[0, 9], Gen.Int[1, 3], Gen.Int[0, 7], Gen.Int[0, 7])
            .Select(dice => (Func<string, ScmEntry>)(name => Shaped(name, dice)));

    private static ScmEntry Shaped(string name, (int Status, int Type, int Process, int Start, int Accepts) dice)
    {
        var status = Statuses[dice.Status];
        var type = dice.Type switch { 0 => EntryType.KernelDriver, < 6 => EntryType.SharedProcess, _ => EntryType.OwnProcess };
        var down = status == EntryStatus.Stopped;

        return Entries.Named(name, name + " display name") with
        {
            Status = status,
            EntryType = type,
            ProcessId = down || type == EntryType.KernelDriver ? Reading<int>.Absent() : Reading<int>.Present(100 * dice.Process),
            StartType = dice.Start switch
            {
                0 => Refused<StartType>(),
                < 4 => Reading<StartType>.Present(StartType.Automatic),
                < 6 => Reading<StartType>.Present(StartType.Manual),
                _ => Reading<StartType>.Present(StartType.Disabled)
            },
            DelayedAuto = Reading<bool>.Present(false),
            AcceptsStop = down ? Reading<bool>.Absent() : Reading<bool>.Present(dice.Accepts != 0)
        };
    }

    /// <summary>
    /// Who depends on whom, as pairs of positions: the second needs the first. Normalised to point
    /// forward when the graph is to have no cycle, which keeps every pair rather than throwing half away.
    /// </summary>
    private static Gen<IReadOnlyList<(int Needed, int Needing)>> Edges(int count) =>
        Gen.Select(Gen.Select(Gen.Int[0, count - 1], Gen.Int[0, count - 1]).List[0, 2 * count], Gen.Int[0, 2])
            .Select(drawn => (IReadOnlyList<(int, int)>)
            [
                .. drawn.Item1
                    .Select(pair => drawn.Item2 == 0 || pair.Item1 < pair.Item2 ? pair : (pair.Item2, pair.Item1))
                    .Where(pair => drawn.Item2 == 0 || pair.Item1 != pair.Item2)
                    .Distinct()
            ]);

    /// <summary>The ask, drawn wide and then made into one a person could make.</summary>
    private static Gen<ServiceAction> AnAsk(int count) =>
        Gen.Select(Gen.Int[0, Kinds.Length - 1], Gen.Int[0, count], Gen.Int[0, 5], Gen.Int[0, Settings.Length - 1], Gen.Int[0, 7])
            .Select(dice => Asked(dice, count));

    private static ServiceAction Asked((int Kind, int Target, int Case, int Setting, int Flags) dice, int count)
    {
        var kind = Kinds[dice.Kind];
        var name = dice.Target == count ? "Nobody" : Names[dice.Target];
        var to = Settings[dice.Setting];

        // An immediate ask carries no cascade and a stop rides only on Disabled - the two shapes no
        // person can make, which the builder throws over on purpose (PlanBuilder.ThrowIfNobodyCouldAsk).
        var immediate = ForcedStop.Asked(kind) && (dice.Flags & 1) != 0;
        var includes = !immediate && (dice.Flags & 2) != 0;
        var alsoStop = kind == ActionKind.SetStartType && to == StartSetting.Disabled && (dice.Flags & 4) != 0;

        return new ServiceAction(
            kind,
            dice.Case == 0 ? name.ToUpperInvariant() : name,
            IncludeDependents: includes,
            To: kind == ActionKind.SetStartType ? to : null,
            Immediate: immediate,
            AlsoStop: alsoStop);
    }

    /// <summary>A reading the machine refused, in the words the other doubles use.</summary>
    internal static Reading<T> Refused<T>() => Reading<T>.Denied(Entries.AccessDenied, "access denied");

    /// <summary>A whole machine: two to six entries, the edges between them, the answers, and an ask.</summary>
    internal static readonly Gen<Graph> Any =
        Gen.Int[2, Names.Length].SelectMany(count =>
            Gen.Select(AnEntry.Array[count], Edges(count), Gen.Int[0, 11].Array[count], AnAsk(count))
                .Select(drawn => Graph.Of(Names[..count], drawn)));
}

/// <summary>
/// One generated machine with the ask made of it.
/// </summary>
/// <param name="Entries">The listing the plan is built from.</param>
/// <param name="Edges">Who needs whom, by name: the second needs the first.</param>
/// <param name="Answers">
/// What the manager says about each listed name when asked who breaks if it stops. A name missing from
/// here was never taught to the double, which answers it as absent.
/// </param>
/// <param name="Ask">What somebody asked for.</param>
internal sealed record Graph(
    IReadOnlyList<ScmEntry> Entries,
    IReadOnlyList<(string Needed, string Needing)> Edges,
    IReadOnlyDictionary<string, Reading<IReadOnlyList<string>>> Answers,
    ServiceAction Ask)
{
    /// <summary>The machine the dice describe: a shape per name, the edges by position, a spoiling per answer, and the ask.</summary>
    internal static Graph Of(
        string[] names,
        (Func<string, ScmEntry>[] Shapes, IReadOnlyList<(int Needed, int Needing)> Edges, int[] Spoils, ServiceAction Ask) drawn)
    {
        IReadOnlyList<(string, string)> named = [.. drawn.Edges.Select(edge => (names[edge.Needed], names[edge.Needing]))];

        var answers = names
            .Select((name, at) => (name, Answer: Spoiled(Reaching(name, named), drawn.Spoils[at])))
            .ToDictionary(pair => pair.name, pair => pair.Answer, StringComparer.OrdinalIgnoreCase);

        return new Graph([.. names.Select((name, at) => drawn.Shapes[at](name))], named, answers, drawn.Ask);
    }

    /// <summary>The double the plan asks, taught exactly these answers.</summary>
    internal FakeScmCatalog Catalog()
    {
        var catalog = new FakeScmCatalog(Entries);

        foreach (var (name, answer) in Answers)
        {
            if (answer.Outcome == ReadOutcome.Denied)
            {
                catalog.RefuseDependentsFor.Add(name);
            }
            else if (answer.IsPresent)
            {
                catalog.DependedOnBy(name, [.. answer.Value!]);
            }
        }

        return catalog;
    }

    /// <summary>The entry listed under this name, compared without case as Windows compares it.</summary>
    internal ScmEntry? Listed(string name) =>
        Entries.FirstOrDefault(entry => string.Equals(entry.ServiceName, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the manager, asked about <paramref name="needed"/>, named <paramref name="needing"/>.</summary>
    internal bool Names(string needed, string needing) =>
        Answers.TryGetValue(needed, out var answer)
        && answer.IsPresent
        && answer.Value!.Contains(needing, StringComparer.OrdinalIgnoreCase);

    public override string ToString() =>
        $"entries [{string.Join(", ", Entries.Select(Described))}], "
        + $"edges [{string.Join(", ", Edges.Select(edge => $"{edge.Needed}<-{edge.Needing}"))}], "
        + $"answers [{string.Join(", ", Answers.Select(pair => $"{pair.Key}: {Said(pair.Value)}"))}], "
        + $"ask {Ask}";

    /// <summary>Everything that depends on the name at any distance - the manager's answer, measured.</summary>
    private static List<string> Reaching(string name, IReadOnlyList<(string Needed, string Needing)> edges)
    {
        var reached = new List<string>();
        var waiting = new Queue<string>([name]);

        while (waiting.TryDequeue(out var next))
        {
            foreach (var (_, needing) in edges.Where(edge => edge.Needed == next))
            {
                if (!reached.Contains(needing))
                {
                    reached.Add(needing);
                    waiting.Enqueue(needing);
                }
            }
        }

        return reached;
    }

    /// <summary>
    /// The answer as a real one can arrive: refused, with a newcomer, with a repeat in another case, or
    /// backwards. Nobody at all is ABSENT rather than an empty list, because that is what the real
    /// catalogue answers (WindowsScmCatalog.ReadDependents) - a generator saying it the other way left
    /// the builder's branch for it unrun.
    /// </summary>
    private static Reading<IReadOnlyList<string>> Spoiled(List<string> answer, int how) => how switch
    {
        0 => Graphs.Refused<IReadOnlyList<string>>(),
        1 => Reading<IReadOnlyList<string>>.Present([.. answer, Graphs.Newcomer]),
        2 when answer.Count > 0 => Reading<IReadOnlyList<string>>.Present([.. answer, answer[0].ToUpperInvariant()]),
        _ when answer.Count == 0 => Reading<IReadOnlyList<string>>.Absent(),
        3 => Reading<IReadOnlyList<string>>.Present([.. Enumerable.Reverse(answer)]),
        _ => Reading<IReadOnlyList<string>>.Present(answer)
    };

    private static string Described(ScmEntry entry) =>
        $"{entry.ServiceName} {entry.Status} {entry.EntryType} pid {Said(entry.ProcessId)} "
        + $"start {Said(entry.StartType)} accepts {Said(entry.AcceptsStop)}";

    private static string Said<T>(Reading<T> reading) => reading.Outcome switch
    {
        ReadOutcome.Present when reading.Value is IReadOnlyList<string> names => $"[{string.Join(" ", names)}]",
        ReadOutcome.Present => $"{reading.Value}",
        _ => reading.Outcome.ToString()
    };
}
