namespace Bws.Site;

/// <summary>
/// Everything the build found wrong, kept rather than thrown at the first one.
///
/// A generator that stops at the first missing key is run six times to find six missing keys.
/// This collects them all, the run prints the list, and <c>--strict</c> decides whether the list
/// is a failure. Without <c>--strict</c> the site is still written, so a page can be looked at
/// while its Polish is half done - and the list says exactly how half.
/// </summary>
internal sealed class Problems
{
    private readonly List<string> _found = [];

    internal IReadOnlyList<string> Found => _found;

    internal int Count => _found.Count;

    internal void Add(string problem)
    {
        if (!_found.Contains(problem, StringComparer.Ordinal))
        {
            _found.Add(problem);
        }
    }
}
