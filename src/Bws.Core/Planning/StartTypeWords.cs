namespace Bws.Core.Planning;

/// <summary>
/// The start types this tool can be asked for, and how each is spelled on a command line.
///
/// <b>One table read in both directions, rather than a renderer here and a parser in the command
/// line.</b> <see cref="EquivalentCommand"/> carries the argument for its own verbs and it applies
/// here word for word: this text is not prose. It has to be exactly what the tool accepts, or it is
/// a line that looks like a command and is not one. Two tables would be two answers to a question
/// with one right answer, and they would disagree the first time somebody reworded one of them.
///
/// <b>THREE WORDS WHERE THE ENUMERATION HAS SIX, and the other three are refused rather than
/// spelled.</b> Boot and System belong to entries this tool will not operate on, and Unknown is not
/// a type at all - it is what a reading says when the manager did not answer. WindowsScmControl
/// refuses exactly those three at the moment of writing, so a word for any of them would be a line
/// this tool accepts and then declines to carry out.
///
/// <b>WHAT THIS CANNOT SAY, AND IT COSTS A WAY BACK FOR ABOUT ONE AUTOMATIC SERVICE IN SIX.</b> An
/// automatic entry can also be marked to start late, and that flag is a field of its own on the
/// entry rather than a sixth start type - 13 of 78 automatic services carry it on a real machine,
/// measured 2026-08-01. So "automatic" here does not say which of the two an entry ends up as: it
/// writes the start type and leaves the flag exactly as it found it, measured on a real machine
/// 2026-08-25. Nothing built on this table may therefore claim to put such an entry back.
/// PlanBuilder.WayBackTo is where that refusal lives, with all three runs that decided it.
/// </summary>
public static class StartTypeWords
{
    /// <summary>
    /// The whole surface, in the order a person reading the usage text meets it.
    ///
    /// Automatic first because it is the one an administrator sets deliberately, disabled last
    /// because it is the one worth pausing over.
    /// </summary>
    private static readonly (StartType Type, string Word)[] Table =
    [
        (StartType.Automatic, "automatic"),
        (StartType.Manual, "manual"),
        (StartType.Disabled, "disabled")
    ];

    private static readonly string[] Spellings = [.. Table.Select(entry => entry.Word)];

    /// <summary>
    /// Every word this tool takes for a start type.
    ///
    /// For the sentence that offers them when somebody types something else. Built from the same
    /// table the reader uses, so a word that stops being accepted stops being offered in the same
    /// change rather than one release later.
    /// </summary>
    public static IReadOnlyList<string> All => Spellings;

    /// <summary>
    /// How a start type is written on a command line, or nothing for one that cannot be asked for.
    ///
    /// <b>Takes a nullable on purpose.</b> "No start type is involved here" is the state every stop,
    /// start and restart is in, and a caller obliged to check that first would be a caller who could
    /// forget to.
    /// </summary>
    public static string? Of(StartType? type)
    {
        foreach (var entry in Table)
        {
            if (type == entry.Type)
            {
                return entry.Word;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads one word as a start type, or says it is not one.
    ///
    /// <b>Case insensitive, like every other word this tool reads</b> - a verb and a switch are
    /// both matched that way, and a value that alone insisted on lower case would be a rule nobody
    /// could guess from the rest of the surface.
    /// </summary>
    public static bool TryRead(string word, out StartType type)
    {
        ArgumentNullException.ThrowIfNull(word);

        foreach (var entry in Table)
        {
            if (word.Equals(entry.Word, StringComparison.OrdinalIgnoreCase))
            {
                type = entry.Type;
                return true;
            }
        }

        // Unknown rather than a guess, and it is the honest value: the caller is being told this
        // word named no start type, and Unknown is what this product already uses for "nobody
        // said". Callers read the bool.
        type = StartType.Unknown;
        return false;
    }
}
