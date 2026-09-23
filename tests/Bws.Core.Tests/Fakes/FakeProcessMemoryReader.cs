namespace Bws.Core.Tests.Fakes;

/// <summary>
/// Stands in for the real processes.
///
/// Counts how many times it was asked, because "one question per process rather than one
/// per entry" is a property worth a test: five services sharing a process must cost one
/// call, and the mistake that breaks it leaves every answer correct and only the cost
/// wrong - which no assertion about values would ever catch.
/// </summary>
internal sealed class FakeProcessMemoryReader(
    IReadOnlyDictionary<int, Reading<ProcessMemory>>? answers = null) : IProcessMemoryReader
{
    private readonly List<int> _asked = [];

    /// <summary>Every process id this was asked about, in order, repeats included.</summary>
    internal IReadOnlyList<int> Asked => _asked;

    public Reading<ProcessMemory> Read(int processId)
    {
        _asked.Add(processId);

        if (answers is not null && answers.TryGetValue(processId, out var answer))
        {
            return answer;
        }

        // A plausible default so a test that does not care about the numbers does not have
        // to name them. SharedBy is one here on purpose: the pass is what works that out,
        // and a fake handing back the right answer would hide the pass never doing it.
        return Reading<ProcessMemory>.Present(new ProcessMemory(
            WorkingSet: processId * 1024L * 1024,
            Commit: processId * 512L * 1024,
            SharedBy: 1));
    }
}
