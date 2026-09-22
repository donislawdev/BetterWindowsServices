namespace Bws.Site;

/// <summary>
/// Where the repository is, found rather than assumed.
///
/// The generator is run from three places with three working directories: a terminal at the
/// root, a test host deep under <c>tests/.../bin</c>, and a workflow step. Walking up until the
/// solution file appears answers all three with one rule, and answers "not in a checkout" with
/// an exception that names what was looked for instead of a later file-not-found somewhere else.
/// </summary>
internal static class SourceTree
{
    private const string Marker = "BetterWindowsServices.slnx";

    internal static string Root(string? given = null)
    {
        if (given is not null)
        {
            var full = Path.GetFullPath(given);
            return File.Exists(Path.Combine(full, Marker))
                ? full
                : throw new InvalidOperationException($"{full} does not hold {Marker}, so it is not the repository root.");
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, Marker)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, Marker)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No {Marker} above {AppContext.BaseDirectory} or {Directory.GetCurrentDirectory()}. Pass --root.");
    }
}
