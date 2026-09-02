using System.Globalization;
using Bws.Core;

namespace Bws.Cli;

/// <summary>
/// Everything this tool knows about one entry, written for a person to read.
///
/// <b>The command line half of what the window shows in its details panel, and the third field
/// catalogue in this product rather than a shared one.</b> That is a consequence of rule 2 of
/// CLAUDE.md and not an oversight: the window's catalogue is Columns.All in Bws.Gui, the core may
/// not depend on it, and ListingTable already carries its own seven. What keeps the three from
/// drifting is the glossary - every name below is a term with a binding "w kodzie" column in
/// docs/03, and tools/audit/audit.ps1 compares them.
///
/// <b>The four headings and the order inside them are the window's, copied deliberately.</b>
/// Somebody who has used one of the two interfaces should recognise the shape of the other. Where
/// the window puts a field is an argument that was already had - memory beside the process id
/// because they are two halves of one measurement, the signature under Binary because it is a
/// fact about the file - and repeating it here is cheaper than having it again differently.
///
/// <b>One field is here that the window has no column for: perUserRole.</b> It arrived on
/// 2026-08-25 and the window's half of that work is a slice of its own, so for now the two
/// catalogues differ by exactly one row, and this sentence is the record of why.
/// </summary>
internal static class EntryReport
{
    /// <summary>
    /// A value ready to be printed, with the story of how it was come by still attached.
    ///
    /// The story cannot be flattened into the text, because the whole point of the report is
    /// that "there is nothing" and "nobody would tell me" print differently and one of them is
    /// never hidden.
    /// </summary>
    private readonly record struct Told(ReadOutcome Outcome, string Text, int Code);

    private sealed record Field(string Key, Func<ScmEntry, Told> Reads);

    private const string Identity = "cli.show.group.basics";
    private const string About = "cli.show.group.about";
    private const string Binary = "cli.show.group.binary";
    private const string Advanced = "cli.show.group.advanced";

    private static readonly (string Heading, Field[] Fields)[] Catalogue =
    [
        (Identity,
        [
            new Field("cli.show.serviceName", entry => Given(entry.ServiceName)),
            new Field(
                "cli.show.displayName",
                entry => Given(ServiceDisplayName.Of(entry.DisplayName, entry.ServiceName))),
            new Field("cli.show.description", entry => Say(entry.Description, value => value)),
            new Field("cli.show.status", entry => Given(StatusWords.Of(entry.Status))),
            new Field("cli.show.startType", entry => Say(entry.StartType, value => value.ToString())),
            new Field("cli.show.delayedAuto", entry => Say(entry.DelayedAuto, YesOrNo)),
            new Field("cli.show.account", entry => Say(entry.Account, value => value)),
            new Field("cli.show.processId", entry => Say(entry.ProcessId, value => value.ToString(CultureInfo.InvariantCulture))),
            new Field("cli.show.memory", entry => Say(entry.Memory, Memory))
        ]),

        (About,
        [
            new Field("cli.show.entryType", entry => Given(entry.EntryType.ToString())),
            new Field("cli.show.perUserRole", entry => Given(entry.PerUserRole.ToString())),
            new Field("cli.show.runsAgainstItsStartType", entry => Say(entry.RunsAgainstItsStartType, YesOrNo)),
            new Field("cli.show.runsWhileDisabled", entry => Say(entry.RunsWhileDisabled, YesOrNo)),
            new Field("cli.show.sidType", entry => Say(entry.SidType, value => value.ToString()))
        ]),

        (Binary,
        [
            new Field("cli.show.binaryPath", entry => Say(entry.BinaryPath, value => value)),
            new Field("cli.show.binaryFile", entry => Say(entry.BinaryFile, value => value)),
            new Field("cli.show.binaryOnDisk", entry => Say(entry.BinaryOnDisk, YesOrNo)),
            // SignatureWords rather than ToString - backlog 260. The listing table carries the same
            // repair, and this is the second of its two surfaces.
            new Field("cli.show.signature", entry => Say(entry.Signature, value => SignatureWords.Of(value.Status))),
            new Field("cli.show.publisher", entry => Publisher(entry)),
            new Field("cli.show.fileVersion", entry => Say(entry.FileVersion, value => value)),
            new Field("cli.show.binaryHash", entry => Say(entry.BinaryHash, value => value))
        ]),

        (Advanced,
        [
            new Field("cli.show.loadOrderGroup", entry => Say(entry.LoadOrderGroup, value => value)),
            new Field("cli.show.errorControl", entry => Say(entry.ErrorControl, value => value.ToString())),
            new Field("cli.show.dependsOn", entry => Say(entry.DependsOn, List)),
            new Field("cli.show.triggers", entry => Say(entry.Triggers, Triggers)),
            new Field("cli.show.requiredPrivileges", entry => Say(entry.RequiredPrivileges, List)),
            new Field("cli.show.securityDescriptor", entry => Say(entry.SecurityDescriptor, value => value))
        ])
    ];

    /// <summary>
    /// The report for one entry.
    ///
    /// <b>What <paramref name="full"/> governs is exactly one of the four read states.</b> A field
    /// that is genuinely absent is left out without it, because most entries have more empty fields
    /// than filled ones and a wall of "none" is a report nobody reads to the end. A field nobody
    /// could read is printed either way - rule 8 of CLAUDE.md forbids swallowing a failed read, and
    /// a switch able to hide one would be that rule broken and spelled as an option.
    ///
    /// A heading with nothing under it is left out too, for the same reason and by the same rule:
    /// it is empty because its fields were, not because anything went wrong.
    /// </summary>
    internal static string Render(ScmEntry entry, bool full)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var lines = new List<string>();

        foreach (var (heading, fields) in Catalogue)
        {
            var shown = fields
                .Select(field => (field.Key, Told: field.Reads(entry)))
                .Where(pair => full || pair.Told.Outcome != ReadOutcome.Absent)
                .ToList();

            if (shown.Count == 0)
            {
                continue;
            }

            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add(Texts.Of(heading));

            // The width of the widest label in THIS section rather than in the whole report, so a
            // long name in one section does not push every other section's values across the
            // screen. Sections are read one at a time and a column that lines up inside one is
            // what makes it scannable.
            var width = shown.Max(pair => Texts.Of(pair.Key).Length);

            lines.AddRange(shown.Select(pair => Texts.Of(
                "cli.show.line",
                Texts.Of(pair.Key).PadRight(width),
                Spell(pair.Told))));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// How one value reads once its state is taken into account.
    ///
    /// The refusal carries its number, because the number is the only thing that tells "you are
    /// not allowed" from "it was gone by the time we asked" - ReadOutcome.Denied says so at
    /// length, and flattening the two into one sentence is the mistake it warns about.
    /// </summary>
    private static string Spell(Told told) => told.Outcome switch
    {
        ReadOutcome.Present => told.Text,
        ReadOutcome.Absent => Texts.Of("cli.show.absent"),
        ReadOutcome.Denied => Texts.Of("cli.show.denied", told.Code),
        _ => Texts.Of("cli.show.notRead")
    };

    /// <summary>A value that is always there, because it came with the enumeration itself.</summary>
    private static Told Given(string text) => new(ReadOutcome.Present, text, 0);

    private static Told Say<T>(Reading<T> reading, Func<T, string> render) => reading.Outcome switch
    {
        ReadOutcome.Present => new Told(ReadOutcome.Present, render(reading.Value!), 0),
        ReadOutcome.Denied => new Told(ReadOutcome.Denied, string.Empty, reading.ErrorCode),
        var outcome => new Told(outcome, string.Empty, 0)
    };

    /// <summary>
    /// Who signed the file, which is a field of the signature rather than a reading of its own.
    ///
    /// A signature that was read and names nobody is absent here rather than present and empty -
    /// an unsigned file has no publisher, and printing a blank beside the word would be the empty
    /// cell this tool spends its rules avoiding.
    /// </summary>
    private static Told Publisher(ScmEntry entry)
    {
        var signature = entry.Signature;

        if (signature.Outcome != ReadOutcome.Present)
        {
            return Say(signature, value => value.Status.ToString());
        }

        var publisher = signature.Value!.Publisher;

        return string.IsNullOrEmpty(publisher)
            ? new Told(ReadOutcome.Absent, string.Empty, 0)
            : new Told(ReadOutcome.Present, publisher, 0);
    }

    private static string YesOrNo(bool value) => Texts.Of(value ? "cli.show.yes" : "cli.show.no");

    private static string List(IReadOnlyList<string> values) => string.Join(", ", values);

    // THE KIND GETS A WORD AND THE ACTION KEEPS ITS NAME, and that asymmetry is measured rather
    // than careless - backlog 260. Seven of the nine kinds read as words jammed together, so they
    // go through TriggerWords the way the state and the signature do. The action has three members
    // - Unknown, Start and Stop - so its value name and the word for a person are the same string
    // and there is nothing to disagree about. Turning it into a lookup would buy one more file and
    // three more keys to say what it already says.
    private static string Triggers(IReadOnlyList<ServiceTrigger> triggers) => string.Join(
        ", ",
        triggers.Select(trigger => Texts.Of(
            "cli.show.trigger",
            TriggerWords.Of(trigger.Kind),
            trigger.Action.ToString())));

    /// <summary>
    /// Megabytes with one decimal, the same as the listing, so the two agree about one entry.
    ///
    /// The sharing is said rather than left to be worked out, for the reason ListingTable gives at
    /// length: five entries quoting the same 36 MB is correct and adds up to five times the truth.
    /// </summary>
    private static string Memory(ProcessMemory memory)
    {
        var size = Texts.Of(
            "cli.cell.megabytes",
            (memory.WorkingSet / (1024.0 * 1024)).ToString("N1", CultureInfo.InvariantCulture));

        return memory.IsShared
            ? Texts.Of("cli.cell.memoryShared", size, memory.SharedBy)
            : size;
    }
}
