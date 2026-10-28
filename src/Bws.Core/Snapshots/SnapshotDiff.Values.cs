using System.Text.Json.Nodes;

namespace Bws.Core.Snapshots;

/// <summary>
/// How two values of one field are compared - which lists ignore order and spelling, which names ignore
/// case, and since 2026-10-27 how a launch command is split into the path that ignores case and the
/// arguments that do not (logic report R06, owner's decision "the path without case, the arguments with it").
///
/// <b>Its own file since that day</b>, when the launch command joined and the comparison stood over the share
/// of its file ceiling the size guard counts as near. One subject - the form a value is compared in - so one
/// place.
///
/// <b>Until R06 the whole command was compared without case</b>, on the argument of backlog 249 that Windows
/// paths ignore case. True of the path, and silent about the rest: the arguments are read by the program the
/// service runs, and <c>--tenant=ACME</c> becoming <c>--tenant=acme</c> may be a different tenant. The
/// comparison called that no change, and <c>--exit-code</c> ended with nought.
/// </summary>
public sealed partial record SnapshotDiff
{
    /// <summary>
    /// Fields whose value is a list where the order carries no meaning - and, for all but one of them,
    /// neither does the spelling (<see cref="ItemsIgnoreCase"/>).
    ///
    /// <b>Four fields, and each one is on this list for a written reason rather than because it
    /// happens to be an array.</b>
    ///
    /// <c>requiredPrivileges</c> - the manager hands these back exactly as each service declared
    /// them, and the spelling varies between services on ONE machine: Schedule declares
    /// SeSystemTimePrivilege and Sense declares SeSystemtimePrivilege, and they are the same
    /// privilege. <see cref="ScmEntry.RequiredPrivileges"/> has said since it was written that
    /// comparison is case-insensitive everywhere and that a diff comparing them as plain text
    /// would report a change between two machines that had none. It did exactly that until
    /// 2026-08-26.
    ///
    /// <c>dependsOn</c> - service names, which Windows itself compares without case (`ADR-14`),
    /// in whatever order the manager answered. That order is promised nowhere. This file's own
    /// neighbour sorts ENTRIES for precisely that reason, having seen the order move.
    ///
    /// <c>triggers</c> - same argument about order. <b>Its spelling counts since 2026-10-27</b> (logic
    /// report C01, owner's decision "yes, exactly"): until schema seven a trigger was two words this
    /// build writes, so folding their case cost nothing. Since seven it carries the manager's data,
    /// and a network endpoint trigger fires on "an exact match" of it - see <see cref="TriggerData"/>.
    ///
    /// <c>requiredBy</c> - the other direction of dependsOn, added 2026-09-06, and the same
    /// argument twice over: service names, compared without case, in an order the manager promises
    /// nowhere. IT WAS MISSED HERE ON THE DAY IT WAS ADDED and the guard below did not say so,
    /// because it read a rendered specimen and this field is unread in every one - so a list field
    /// that happened to be null was not an array and was invisible to the check whose entire
    /// subject is a field nobody came back for. That guard asks the TYPE now.
    /// </summary>
    /// <remarks>
    /// <b>Internal rather than private so that a guard can check it covers every list in the
    /// document.</b> The way this list goes wrong is not that a name in it is wrong - it is that a
    /// field is ADDED to the document and nobody comes here, which brings order-sensitivity back
    /// for that one field, silently, on a surface whose whole job is telling real drift from noise.
    /// </remarks>
    internal static readonly string[] Unordered = ["dependsOn", "requiredBy", "requiredPrivileges", "triggers"];

    /// <summary>
    /// The lists of <see cref="Unordered"/> whose items are names Windows compares without case - every
    /// one of them but <c>triggers</c>, for the reason given there.
    /// </summary>
    private static readonly string[] ItemsIgnoreCase = ["dependsOn", "requiredBy", "requiredPrivileges"];

    /// <summary>The one list whose items are narrowed when one side is older than whole triggers.</summary>
    private const string Triggers = "triggers";

    /// <summary>The two entries in the form the comparison reads them, narrowed when one side is older than whole triggers.</summary>
    private static (JsonObject Was, JsonObject Now) Documents(EntryDocument before, EntryDocument after, bool kindsOnly)
    {
        var was = SnapshotJson.Document(before);
        var now = SnapshotJson.Document(after);

        if (kindsOnly)
        {
            KindsAndActions(was);
            KindsAndActions(now);
        }

        return (was, now);
    }

    /// <summary>
    /// Narrows every trigger in a document to its kind and action - what a file older than schema seven
    /// holds - so the two sides of <see cref="ComparisonCaveats.TriggerDetailDiffers"/> compare the same
    /// question. A document without triggers is left as it is, and a trigger held as nothing stays nothing
    /// rather than becoming one of no kind and no action (the review of PR #98), so <see cref="Change"/>
    /// sees it for what it is.
    /// </summary>
    private static void KindsAndActions(JsonObject document)
    {
        if (document[Triggers] is not JsonArray triggers)
        {
            return;
        }

        document[Triggers] = new JsonArray(
        [
            .. triggers.Select(trigger => trigger is null
                ? null
                : new JsonObject
                {
                    ["kind"] = trigger["kind"]?.DeepClone(),
                    ["action"] = trigger["action"]?.DeepClone()
                })
        ]);
    }

    /// <summary>
    /// The triggers each side holds and the other does not, for the triggers field - null for any other.
    ///
    /// <b>Keyed by the very text <see cref="Compared"/> sorts</b>, so this agrees with the decision that the
    /// field differs: exact case since schema seven, and the narrowed kind and action when one side is older
    /// (the two documents arrive already narrowed by <see cref="Documents"/>).
    ///
    /// <b>Counted rather than searched, since the review of PR #98.</b> The first version looked for every
    /// earlier trigger in the later list and took it out, which is quadratic - harmless at the thirteen one
    /// entry holds on the machine this was written on, and a snapshot is a file anybody can hand over.
    /// Measured with <c>tools/snapshot-probe/trigger-shapes.ps1</c>, forty thousand triggers in one entry,
    /// five runs after a first one not counted: 12.8 to 20.4 s searching, 1.78 to 1.82 s counting, and 1.33
    /// to 1.56 s for the build before this list existed, which compared two texts and named no trigger. The
    /// copies of one trigger that are matched are still its first ones on the later side, so the lines come
    /// out as they did.
    ///
    /// <b>Null as well when either side holds a trigger that is not one to say</b> - null as a whole, or a
    /// data item without its type or its values, which no build writes and a file older than schema seven is
    /// not refused for (<see cref="Snapshot"/> asks it of a seven). Each interface then shows the two whole
    /// lists, as it did before backlog 625, rather than a line it would have to make up: until that review a
    /// null trigger came out as a line reading "/", and a data item of nulls ended the comparison with an
    /// exception and code 1.
    /// </summary>
    private static TriggerChange? Change(string field, JsonNode? was, JsonNode? now)
    {
        if (field != Triggers)
        {
            return null;
        }

        var earlier = Listed(was).ToList();
        var later = Listed(now).ToList();

        if (earlier.Concat(later).Any(item => item.Trigger is not { } trigger || !Sayable(trigger)))
        {
            return null;
        }

        var copies = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (key, _) in later)
        {
            copies[key] = copies.GetValueOrDefault(key) + 1;
        }

        var matched = new Dictionary<string, int>(StringComparer.Ordinal);
        var removed = new List<TriggerDocument>();

        foreach (var (key, trigger) in earlier)
        {
            if (matched.GetValueOrDefault(key) < copies.GetValueOrDefault(key))
            {
                matched[key] = matched.GetValueOrDefault(key) + 1;
            }
            else
            {
                removed.Add(trigger!);
            }
        }

        var added = new List<TriggerDocument>();

        foreach (var (key, trigger) in later)
        {
            if (matched.GetValueOrDefault(key) > 0)
            {
                matched[key]--;
            }
            else
            {
                added.Add(trigger!);
            }
        }

        return new TriggerChange(removed, added);
    }

    /// <summary>
    /// The triggers of one side with the text they are compared by - a trigger the side holds as nothing
    /// comes with none. A side with no list holds an empty one.
    /// </summary>
    private static IEnumerable<(string Key, TriggerDocument? Trigger)> Listed(JsonNode? node) =>
        node is JsonArray items
            ? items.Select(item => (Text(item) ?? string.Empty, SnapshotJson.Trigger(item)))
            : [];

    /// <summary>A trigger whose data, where it has any, is whole - see <see cref="TriggerDataDocument.IsWhole"/>.</summary>
    private static bool Sayable(TriggerDocument trigger) => trigger.Data?.All(TriggerDataDocument.IsWhole) ?? true;

    /// <summary>
    /// Fields whose value names something Windows itself compares without case.
    ///
    /// <b>Three fields, four from 2026-08-26 until 2026-10-27, and each one is here because the
    /// SYSTEM says so rather than because looser felt safer.</b>
    ///
    /// <c>account</c> - an account is a SID, and the name is the translated label on it. `ADR-14`
    /// is the whole of that argument: identity travels by identifier and the name is for showing a
    /// person. <c>LocalSystem</c> and <c>localsystem</c> are one account.
    ///
    /// <c>binaryFile</c> - Windows paths are case-insensitive, so two spellings of one path run one
    /// file. A snapshot reporting drift because somebody retyped a path in different capitals would
    /// be reporting a change that changed nothing about what runs. <b><c>binaryPath</c> left this list
    /// on 2026-10-27</b> (logic report R06): it holds the arguments too, and only its path ignores case
    /// - see <see cref="CommandKey"/>.
    ///
    /// <c>loadOrderGroup</c> - a group name is matched by the manager without case, the same way a
    /// service name is.
    ///
    /// <b>What is deliberately NOT here, and it is the half that matters:</b> <c>binaryHash</c>,
    /// <c>securityDescriptor</c>, <c>displayName</c> and <c>description</c>. A hash differing in
    /// case is a different hash. An SDDL string is read character for character by the system that
    /// parses it. And the two human-facing names are TEXT rather than identifiers - somebody
    /// recapitalising a display name really did change what an administrator will read.
    /// </summary>
    private static readonly string[] TheSystemIgnoresCase =
        ["account", "binaryFile", "loadOrderGroup"];

    /// <summary>
    /// A value in the form the comparison uses, which is NOT always the form a person is shown.
    ///
    /// <b>The two were one representation until 2026-08-26, and splitting them was deliberate
    /// rather than a convenience.</b> The old sentence here said that keeping one form meant what
    /// the comparison decided and what somebody is shown could never disagree - a real property,
    /// and it was paid for with a false difference on every list whose order or spelling moved
    /// without anything changing. A tool whose whole subject is telling real drift from noise may
    /// not report drift that is not there.
    ///
    /// <b>So the split goes one way only.</b> This form exists to decide whether two values differ
    /// and is never shown to anybody. What a person reads is still <see cref="Text"/> of the value
    /// as it was written - original spelling, original order - so a difference that IS reported
    /// still shows both sides exactly as the two snapshots hold them.
    ///
    /// Everything not named in <see cref="Unordered"/> is compared exactly as before. That is the
    /// half of this that is easy to get wrong: comparing every field without case would quietly
    /// stop <c>binaryHash</c> and <c>sddl</c> from reporting changes that are real.
    /// </summary>
    private static string? Compared(string field, JsonNode? node)
    {
        if (node is JsonArray items && Unordered.Contains(field, StringComparer.Ordinal))
        {
            var caseless = ItemsIgnoreCase.Contains(field, StringComparer.Ordinal);

            // The unit separator, because it is the one character none of these values can hold -
            // joining on a comma would let two lists made of different pieces compare equal. A trigger
            // is written as JSON here, where the separator would come out escaped, so it holds for them too.
            return string.Join(
                '\u001f',
                items
                    .Select(item => Text(item) ?? string.Empty)
                    .Select(text => caseless ? text.ToLowerInvariant() : text)
                    .OrderBy(text => text, StringComparer.Ordinal));
        }

        if (field == Command)
        {
            return Text(node) is { } command ? CommandKey(command) : null;
        }

        return TheSystemIgnoresCase.Contains(field, StringComparer.Ordinal)
            ? Text(node)?.ToLowerInvariant()
            : Text(node);
    }

    /// <summary>
    /// A value as text, for showing, and for comparing everything <see cref="Compared"/> does not
    /// take a view on.
    ///
    /// Strings come out as themselves rather than quoted, because a quoted path in a table reads
    /// like a mistake.
    /// </summary>
    private static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => node.ToJsonString()
    };

    /// <summary>The one field whose value is a path followed by arguments.</summary>
    private const string Command = "binaryPath";

    /// <summary>
    /// A launch command in the form the comparison uses: the part naming the file without case, the rest
    /// exactly as written.
    /// </summary>
    internal static string CommandKey(string command)
    {
        var end = FileEnds(command);

        return command[..end].ToLowerInvariant() + command[end..];
    }

    /// <summary>
    /// Where the file a launch command names ends, read from the text alone.
    ///
    /// <b>Without the disk, which is the difference from <see cref="BinaryPathResolver"/></b>: the two
    /// commands come from two snapshots, and the earlier one may describe another machine. So an unquoted
    /// command with spaces - where only the disk can say which prefix is the file - is cut where the first
    /// prefix ends in a name Windows runs, and with no such prefix at the first space. <b>The price, said:</b>
    /// a path with a space and no extension compares what follows the space exactly, so a change of case
    /// there is reported, with both spellings shown. A difference shown that a person can dismiss costs less
    /// than one hidden in an audit.
    ///
    /// Quotes settle it, as they do for Windows. A command with a quote opened and never closed is all file
    /// name - the resolver reads it the same way.
    /// </summary>
    internal static int FileEnds(string command)
    {
        var start = command.Length - command.TrimStart().Length;

        if (start == command.Length)
        {
            return start;
        }

        if (command[start] == '"')
        {
            var close = command.IndexOf('"', start + 1);

            return close < 0 ? command.Length : close + 1;
        }

        int? firstSpace = null;

        for (var space = command.IndexOf(' ', start); space >= 0; space = command.IndexOf(' ', space + 1))
        {
            if (BinaryPathResolver.HasExecutableExtension(command[start..space]))
            {
                return space;
            }

            firstSpace ??= space;
        }

        return BinaryPathResolver.HasExecutableExtension(command[start..]) ? command.Length : firstSpace ?? command.Length;
    }
}
