namespace Bws.Core.Planning;

/// <summary>
/// The catalogue, with "who depends on this name" put to the manager once for the whole of one
/// bulk plan.
///
/// <b>Why it exists, and what it was measured to save (S-7 of the performance report, built
/// 2026-09-29).</b> A bulk plan asks the same question many times over. <see cref="DependentsFirst.Order"/>
/// asks once per selected name to put the selection in order, then each plan asks again about its
/// own target, and the cascades of neighbouring entries overlap. tools/plan-probe counted it on the
/// whole listing of 800 entries asked to stop: 1433 questions about 800 names, each one opening the
/// manager and then the service.
///
/// <b>One answer per name for one build, and no longer.</b> A preview is a picture of one moment, so
/// a name that has answered once answers the same for the rest of that picture. That includes a
/// refusal - asked again a moment later it could come out differently and leave two plans in one
/// preview disagreeing about the same entry. It lives exactly as long as the build that made it and
/// is never handed on, so nothing it remembers can go stale between two presses.
///
/// <b>Names compared as written, not without case.</b> The manager ignores case, so two spellings of
/// one name would at worst be asked twice, which costs one question. Folding them together would
/// instead be safe only because a real machine cannot hold two names that differ in case - the fake
/// catalogue deliberately does - and a rule that holds only while the input is real is the kind that
/// breaks where it is not.
///
/// Not safe for two threads at once, and not asked to be: one build runs on one thread.
/// </summary>
internal sealed class DependentsAskedOnce(IScmCatalog catalog) : IScmCatalog
{
    private readonly Dictionary<string, Reading<IReadOnlyList<string>>> _answers = new(StringComparer.Ordinal);

    public IReadOnlyList<ScmEntry> ReadAll() => catalog.ReadAll();

    public IReadOnlyList<ScmStatus> ReadStatuses() => catalog.ReadStatuses();

    public Reading<IReadOnlyList<string>> ReadDependents(string serviceName)
    {
        if (!_answers.TryGetValue(serviceName, out var answer))
        {
            answer = catalog.ReadDependents(serviceName);
            _answers[serviceName] = answer;
        }

        return answer;
    }
}
