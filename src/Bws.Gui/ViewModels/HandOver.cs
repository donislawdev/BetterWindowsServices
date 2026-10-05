using System.Buffers.Text;
using System.Text.Json;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What a window without administrator rights hands to the one it starts with them - the list it
/// was on, what was typed, the entries picked and the plan that was open. UX-GUI-004 (c) of the
/// audit of 2026-09-23.
///
/// <b>Why it exists.</b> Restarting as administrator used to start from nothing, so stopping one
/// service was five steps twice: type, pick, ask for the plan, read it, restart - and then type,
/// pick and ask again. `docs/PROJEKT-ZAPIS-BEZ-SKUTKU-20260923.md` section 3.2 weighs the three
/// ways of carrying this across and why this one.
///
/// <b>A QUESTION CROSSES, NEVER AN ANSWER.</b> Nothing here is a fact about the machine: the new
/// session reads the machine itself, matches the names against what it read, and builds the plan
/// again from scratch - the way MainWindow.Force does and for its reason, that a carried plan would
/// be an answer about a machine that has since moved. Nothing is carried out either: rule 9 of the
/// project holds, and the new window only fills in what the old one showed.
///
/// <b>THE ARGUMENT REACHES A PROCESS WITH ADMINISTRATOR RIGHTS, SO IT IS READ AS HOSTILE.</b> Anyone
/// running as this user can start this program elevated with any argument - behind a UAC prompt
/// naming this program - so the text is capped before it is decoded, decoded strictly, and every
/// field checked against what this window could itself have written. Anything else is refused whole
/// and said, never half applied. `docs/09` has the section.
///
/// <b>One opaque argument rather than a readable command line</b>: it is plumbing between two copies
/// of one program, not an interface anybody types, and base64url carries no space, quote or
/// backslash for the shell's quoting to get wrong. It is not a contract - the version inside is how
/// a mismatched pair refuses rather than guesses.
/// </summary>
internal sealed record HandOver
{
    /// <summary>The argument that carries it. The only other one this program takes is --catalogue.</summary>
    internal const string Argument = "--carry";

    /// <summary>Longer than this and the argument is not one this program wrote. Also the budget the writer keeps under.</summary>
    internal const int LongestArgument = 8192;

    /// <summary>
    /// 2 since 2026-09-30, when <see cref="Dependents"/> arrived. A copy of the program without that
    /// field would read a newer hand-over and quietly drop the dependants from the plan it asks
    /// again - a different plan from the one on the screen - so a mismatched pair refuses instead.
    /// 3 since 2026-10-05, when <see cref="QueryLeftBehind"/> arrived, for the same reason: an older
    /// copy would open an empty box and say nothing about the query it was not given.
    /// </summary>
    private const int Version = 3;
    private const int LongestQuery = 4096;
    private const int LongestName = 256;

    /// <summary>Which list was on screen.</summary>
    public required EntryScope Scope { get; init; }

    /// <summary>What was in the search box, exactly as typed.</summary>
    public required string Query { get; init; }

    /// <summary>The manager's names of the picked rows - identity, `ADR-14` - never display names.</summary>
    public required IReadOnlyList<string> Picked { get; init; }

    /// <summary>The plan that was open, if one was - asked again in the new session, never carried.</summary>
    public ActionKind? Asked { get; init; }

    /// <summary>
    /// The startup setting that plan was to write, for a start type plan and nothing else. One of
    /// the four settings since 2026-09-24 - until then any read-side start type crossed here,
    /// Boot and System included, and a plan asked again from one would have shown "set to Boot"
    /// over a write the writer refuses.
    /// </summary>
    public StartSetting? To { get; init; }

    /// <summary>
    /// Whether that plan also stopped the entry - the offer under "keeps running" had been taken.
    /// Carried because the sheet on the screen had two steps, and asking again without it would
    /// open a different plan from the one somebody restarted to carry out.
    /// </summary>
    public bool AlsoStop { get; init; }

    /// <summary>
    /// Whether that plan also stopped the running dependants first - the offer under them had been
    /// taken (W-4, 2026-09-30). Carried for the reason <see cref="AlsoStop"/> is.
    /// </summary>
    public bool Dependents { get; init; }

    /// <summary>
    /// Whether the picked rows were too many to fit, so they and the plan over them were left out -
    /// the new window says so rather than showing a selection that looks complete (rule 8).
    /// </summary>
    public bool PickedLeftBehind { get; init; }

    /// <summary>
    /// Whether the query was too long to carry, so it was left behind whole - the new window opens
    /// with an empty box and says why (rule 8). Owner's decision 2026-10-05, G-7 of the external
    /// stability report: a query cut short would be a different question, so it does not cross at
    /// all rather than crossing in part.
    /// </summary>
    public bool QueryLeftBehind { get; init; }

    /// <summary>
    /// The argument's value, always one the reading side accepts.
    ///
    /// <b>Until 2026-10-05 that was not true, and a long query lost everything.</b> The query was
    /// never held to <see cref="LongestQuery"/>, which the reading side checks - so 4097 characters
    /// pasted into the box crossed and the new window refused the whole hand-over, list, picks and
    /// plan. And a query of Polish letters, six characters each once escaped, could blow the budget
    /// with nothing picked at all, which the one fallback here did not cover.
    ///
    /// <b>The picks before the query, when only one of them fits.</b> The picks and the plan over
    /// them are what somebody restarted to carry out, and a list with an empty box still holds every
    /// row they picked. The query goes first only when keeping it is what does not fit.
    /// </summary>
    internal string Encode()
    {
        var carried = Query.Length <= LongestQuery ? this : this with { Query = string.Empty, QueryLeftBehind = true };
        var withoutQuery = carried with { Query = string.Empty, QueryLeftBehind = carried.QueryLeftBehind || carried.Query.Length > 0 };
        var withoutPicks = carried with { Picked = [], Asked = null, To = null, AlsoStop = false, Dependents = false, PickedLeftBehind = true };

        return Fitting(carried)
            ?? Fitting(withoutQuery)
            ?? Fitting(withoutPicks)
            ?? Write(withoutPicks with { Query = string.Empty, QueryLeftBehind = withoutQuery.QueryLeftBehind });
    }

    /// <summary>The argument for this hand-over, or nothing when it is over the budget.</summary>
    private static string? Fitting(HandOver handOver) =>
        Write(handOver) is { Length: <= LongestArgument } written ? written : null;

    /// <summary>
    /// What the program was started with: nothing handed over, a hand-over, or a hand-over refused -
    /// the last one is said by the window, because a person who restarted expects to find their
    /// work and would otherwise not know why it is not there.
    /// </summary>
    internal static (HandOver? Carried, bool Refused) Read(IReadOnlyList<string> arguments)
    {
        var at = -1;

        for (var index = 0; index < arguments.Count; index++)
        {
            if (string.Equals(arguments[index], Argument, StringComparison.Ordinal))
            {
                at = index;
                break;
            }
        }

        if (at < 0)
        {
            return (null, false);
        }

        var carried = at + 1 < arguments.Count ? Parse(arguments[at + 1]) : null;

        return (carried, carried is null);
    }

    private static string Write(HandOver handOver)
    {
        var wire = new Wire
        {
            V = Version,
            Scope = handOver.Scope.ToString(),
            Query = handOver.Query,
            Picked = [.. handOver.Picked],
            Asked = handOver.Asked?.ToString(),
            To = handOver.To?.ToString(),
            Stop = handOver.AlsoStop,
            Deps = handOver.Dependents,
            Left = handOver.PickedLeftBehind,
            QLeft = handOver.QueryLeftBehind
        };

        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(wire));
    }

    /// <summary>Null for anything this program could not itself have written.</summary>
    private static HandOver? Parse(string text)
    {
        if (text.Length is 0 or > LongestArgument || !Base64Url.IsValid(text))
        {
            return null;
        }

        Wire? wire;

        try
        {
            wire = JsonSerializer.Deserialize<Wire>(Base64Url.DecodeFromChars(text), Strict);
        }
        catch (JsonException)
        {
            return null;
        }

        return wire is null ? null : Checked(wire);
    }

    private static HandOver? Checked(Wire wire)
    {
        if (wire.V != Version
            || !Named(wire.Scope, out EntryScope scope)
            || wire.Query is not { Length: <= LongestQuery } query
            || (wire.QLeft && query.Length > 0)
            || wire.Picked is not { } picked
            || !picked.All(IsName)
            || !Plan(wire, out var asked, out var to))
        {
            return null;
        }

        // A plan over nothing picked is no plan, so it is dropped rather than asked for.
        return new HandOver
        {
            Scope = scope,
            Query = query,
            Picked = picked,
            Asked = picked.Length > 0 ? asked : null,
            To = picked.Length > 0 ? to : null,
            AlsoStop = picked.Length > 0 && wire.Stop,
            Dependents = picked.Length > 0 && wire.Deps,
            PickedLeftBehind = wire.Left,
            QueryLeftBehind = wire.QLeft
        };
    }

    /// <summary>
    /// The plan that was open, checked: a kind that exists, a setting exactly when the kind is a
    /// start type plan - such a plan always has one and no other plan ever does - and a stop riding
    /// on it only beside Disabled, which is the one shape this window can have put on the screen.
    /// The dependants only beside the four kinds that can take them (EquivalentCommand renders
    /// --dependents for exactly those), never a start and never a startup setting.
    /// </summary>
    private static bool Plan(Wire wire, out ActionKind? asked, out StartSetting? to)
    {
        asked = null;
        to = null;

        if (wire.Asked is null)
        {
            return wire.To is null && !wire.Stop && !wire.Deps;
        }

        if (!Named(wire.Asked, out ActionKind kind))
        {
            return false;
        }

        asked = kind;

        if (kind != ActionKind.SetStartType)
        {
            return wire.To is null && !wire.Stop
                && (!wire.Deps || kind is ActionKind.Stop or ActionKind.Restart or ActionKind.ForceStop or ActionKind.ForceRestart);
        }

        if (wire.Deps || !Named(wire.To, out StartSetting setting) || (wire.Stop && setting != StartSetting.Disabled))
        {
            return false;
        }

        to = setting;

        return true;
    }

    /// <summary>A manager's name as this window could have read it: not empty, not huge, no control characters.</summary>
    private static bool IsName(string? name) =>
        name is { Length: > 0 and <= LongestName } && !name.Any(char.IsControl);

    /// <summary>
    /// An enum member by its exact name and nothing else. Enum.TryParse alone would also take "3",
    /// " Stop" and "Stop, Start" - so the value has to be a member, and write itself back as the very
    /// same text. The first half alone let "3" through: an undefined value prints as "3" too, which
    /// HandOverTests caught on its first run.
    /// </summary>
    private static bool Named<T>(string? text, out T value)
        where T : struct, Enum
    {
        value = default;

        return text is not null
            && Enum.TryParse(text, ignoreCase: false, out value)
            && Enum.IsDefined(value)
            && string.Equals(value.ToString(), text, StringComparison.Ordinal);
    }

    private static readonly JsonSerializerOptions Strict = new() { MaxDepth = 4 };

    /// <summary>The shape on the wire - plain strings, so every value is checked by hand above rather than trusted to a converter.</summary>
    private sealed class Wire
    {
        public int V { get; init; }
        public string? Scope { get; init; }
        public string? Query { get; init; }
        public string[]? Picked { get; init; }
        public string? Asked { get; init; }
        public string? To { get; init; }
        public bool Stop { get; init; }
        public bool Deps { get; init; }
        public bool Left { get; init; }
        public bool QLeft { get; init; }
    }
}
