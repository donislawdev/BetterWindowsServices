using System.Diagnostics;
using Bws.Core.History;

namespace Bws.Cli;

/// <summary>
/// <c>bws history [NAME]</c>: who changed the configuration of an entry, and when, out of the
/// System log.
///
/// <b>Its own type rather than a branch written out in Program</b>, which stands close to the size
/// ceiling the ratchet holds it to. Program asks this for an answer and prints it like every other.
///
/// <b>The service manager is not read unless it has to be.</b> The history is the System log alone,
/// so the whole machine's history never opens the manager - and the one question that needs it is
/// asked last: a NAME with no change behind it, which is either an entry nobody has touched or a
/// typo, and only the manager can say which.
/// </summary>
internal static class HistoryCommand
{
    /// <summary>
    /// The text for the data channel and the code to end on - or no text, when the run ends on a
    /// mistake that was already said on the error channel.
    /// </summary>
    /// <param name="options">What was typed.</param>
    /// <param name="reader">Where the history comes from.</param>
    /// <param name="entryNames">The names of the entries on this machine, asked only when needed.</param>
    internal static (string? Data, int Exit) Run(
        CommandLine options, IChangeHistoryReader reader, Func<IEnumerable<string>> entryNames)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(entryNames);

        var stopwatch = Stopwatch.StartNew();
        var history = reader.Read();
        stopwatch.Stop();

        var name = options.ServiceName.Length > 0 ? options.ServiceName : null;
        var shown = name is null ? history.Changes : history.Of(name);

        // CODE 2 FOR A NAME NOTHING ANSWERS TO, the way show answers it. Without this a misspelt name
        // and an entry nobody ever touched print the same sentence, and the first one is a mistake
        // somebody needs told about. An entry that is GONE but has changes in the log is not a typo -
        // it got past the first condition already.
        if (name is not null && shown.Count == 0
            && !entryNames().Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(Texts.Of("cli.history.noSuchEntry", name));

            return (null, ExitCode.Usage);
        }

        foreach (var line in HistoryText.Admissions(history, shown, name))
        {
            Console.Error.WriteLine(line);
        }

        if (options.Timing)
        {
            Console.Error.WriteLine(Texts.Of("cli.info.timingHistory", stopwatch.ElapsedMilliseconds));
        }

        return (options.Json ? HistoryJson.Render(history.Log, shown) : HistoryText.Render(shown), ExitCode.Ok);
    }
}
