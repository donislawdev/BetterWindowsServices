using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Names every native call whose answer is thrown away, because this project has now made the
/// same mistake three times and found it three different ways.
///
/// <b>The mistake: deciding what a native call did by looking at something other than what it
/// returned.</b> Windows does not clear the last error on success, so a call that worked and had
/// nothing to hand over leaves whatever the previous call on this thread put there - and code that
/// reads only the reported size, or only the error, reads that stale number as this call's verdict.
/// <c>WindowsScmCatalog.Enumerate</c> carries the argument in full.
///
/// <b>Three findings, and the third came from outside.</b> It was found in <c>Enumerate</c> on
/// 2026-08-03, found again in <c>ReadDependents</c> on 2026-08-26 after a comment in this very
/// repository claimed that call had always been right, and found in six more places by a report
/// from outside on 2026-09-02 - backlog 303. A fault that keeps coming back is a missing guard
/// rather than a run of bad luck.
///
/// <b>What this guard actually checks is narrower than the mistake, and that is worth saying
/// plainly.</b> It cannot tell whether a returned value is USED WELL. It only insists that it is
/// taken at all - that no call into the interop layer stands as a bare statement with its answer
/// dropped on the floor. That is enough to stop the seventh occurrence, because every one of the
/// six was written in exactly that shape.
///
/// <b>Blind spot, named rather than left for somebody to trust this without it.</b> A call whose
/// name begins a continuation line inside a larger expression would look the same to this pattern.
/// Nothing in the project is written that way today, and if something is, this guard will ask about
/// it rather than miss it - the failure direction is a question, not a silence.
/// </summary>
public sealed class NativeCallGuards
{
    /// <summary>
    /// The calls that may have their answer dropped, and why, one line each.
    ///
    /// <b>Adding a name here is the deliberate act.</b> Failing this test is the question being
    /// asked, and the answer belongs in this list rather than in a comment beside the call.
    /// </summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["CryptCATAdminReleaseCatalogContext"] =
            "Handing a catalogue context back, in a finally. There is nothing a caller could do " +
            "differently on a failure to release, and turning it into an answer would replace a " +
            "real verdict about the file with a complaint about tidying up after it.",

        ["CryptCATAdminReleaseContext"] =
            "The same, one level out - the administrator context this file opened to ask at all.",

        ["WinVerifyTrust"] =
            "The SECOND of two calls, the one carrying WTD_STATEACTION_CLOSE. The verdict came " +
            "from the first, whose result IS read, and this one exists to let go of the state " +
            "that call left behind. Both are in Ask, four lines apart."
    };

    [Fact]
    public void Every_native_call_that_drops_its_answer_has_argued_for_dropping_it()
    {
        var found = Dropped();
        var strangers = found.Keys.Where(name => !Allowed.ContainsKey(name)).ToArray();

        Assert.True(
            strangers.Length == 0,
            "A call into the interop layer stands as a statement with its answer thrown away. " +
            "Either the answer decides something and this code should be reading it - which is " +
            "backlog 303, found three times now - or it genuinely cannot, and this name belongs " +
            "in the list with its reason:" +
            Environment.NewLine +
            string.Join(
                Environment.NewLine,
                strangers.Select(name => $"{name}  in {string.Join(", ", found[name])}")));
    }

    [Fact]
    public void The_list_does_not_name_calls_that_stopped_dropping_anything()
    {
        // The other direction, and the one that lets a list rot into a wish. A name here that no
        // longer appears reads as an argument still being made for something nobody does.
        var found = Dropped();
        var gone = Allowed.Keys.Where(name => !found.ContainsKey(name)).ToArray();

        Assert.True(
            gone.Length == 0,
            "These calls are listed as dropping their answer and no longer do. Remove them, so " +
            "the list keeps meaning what it says:" +
            Environment.NewLine + string.Join(Environment.NewLine, gone));
    }

    private static Dictionary<string, List<string>> Dropped()
    {
        var found = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in Sources.Shipped())
        {
            // A statement that BEGINS with the call is one whose answer goes nowhere. An
            // assignment, a condition, a return or an argument all put something before it.
            var matches = Regex.Matches(
                File.ReadAllText(file),
                @"(?m)^\s*PInvoke\.(\w+)\(",
                RegexOptions.None,
                Sources.Ceiling);

            foreach (Match match in matches)
            {
                var name = match.Groups[1].Value;

                if (!found.TryGetValue(name, out var files))
                {
                    files = [];
                    found[name] = files;
                }

                var where = Path.GetFileName(file);

                if (!files.Contains(where, StringComparer.Ordinal))
                {
                    files.Add(where);
                }
            }
        }

        return found;
    }
}
