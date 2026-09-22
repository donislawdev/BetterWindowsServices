using Bws.Core.Snapshots;

namespace Bws.Cli;

/// <summary>
/// The two questions this tool answers without needing anything.
///
/// Not a verb, not options that make sense, not a service manager it can reach - which is why
/// they are answered before all three are consulted. Split out of <c>Program.cs</c> on
/// 2026-08-05 when the size ratchet asked that file for a seam, and this was the one already
/// described by a comment in it.
///
/// <b>ASKING A QUESTION IS NOT A MISTAKE, and until 2026-08-02 this tool answered as though it
/// were:</b> <c>--help</c> came back "Unknown option: --help", on the error channel, with the
/// code reserved for what somebody typed wrongly. No arguments at all did the same without the
/// first line. Help goes on the data channel with a code of zero, and it is every user's first
/// reflex.
/// </summary>
internal static class Immediate
{
    /// <summary>
    /// Answers if there is something to answer, and says nothing otherwise.
    /// </summary>
    /// <returns>The code to end on, or null when this run has real work to do.</returns>
    internal static int? Answer(CommandLine options)
    {
        if (options.Version)
        {
            // Through Output rather than straight to the console, and a guard insisted twice:
            // first that the channel be named, then that there be exactly one place naming it.
            // Both were right - this is what the run produces, so it belongs beside the listing
            // and the JSON.
            //
            // THE SCHEMA VERSION GOES OUT BESIDE THE PROGRAM VERSION, AND THE REASON IS FORENSIC.
            // These two numbers move independently: a build can change without the file shape
            // changing, and the file shape cannot change without a reader somewhere needing to
            // know. When the same snapshot gives a different answer in six months, a log holding
            // only the program version cannot say which of the two moved.
            Output.Data(Texts.Of("cli.version", Release.Number, Snapshot.CurrentSchemaVersion));

            return ExitCode.Ok;
        }

        if (options.Help)
        {
            // The data channel on purpose. Somebody piping the help into a pager or a file is
            // asking for the text, so the text is the output of the run rather than a diagnostic
            // beside it.
            Output.Data(Texts.Of("cli.usage"));

            return ExitCode.Ok;
        }

        // THE THIRD QUESTION THAT NEEDS NOTHING, AND THE ONE THAT WAITS FOR THE LINE TO BE CLEAN.
        //
        // It belongs here for the same reason as the two above: it reads no service manager, no
        // disk and no network, so making it wait for a machine would be making it wait for
        // something it never asks. It answers out of a register compiled into this executable.
        //
        // <b>NothingWrong is the whole difference, and leaving it out would have been a bug of
        // exactly the kind the belonging table exists to prevent.</b> Help and version are read
        // BEFORE what somebody typed is judged, on purpose - a line that went wrong is still a
        // line whose author may be asking for the help. A verb is not that: `bws license --json`
        // with the rule above would have printed the notice, exited zero and said nothing about
        // the switch it ignored. So anything with a complaint against it falls through to
        // Refusals, which already has the sentence for every one of them.
        if (options.Kind == CommandKind.License && options.NothingWrong)
        {
            Output.Data(Licence.Answer(options.Components));

            return ExitCode.Ok;
        }

        return null;
    }
}
