namespace Bws.Core.Planning;

/// <summary>
/// The startup settings this tool can be asked for, and how each is spelled on a command line.
///
/// <b>One table read in both directions, rather than a renderer here and a parser in the command
/// line.</b> <see cref="EquivalentCommand"/> carries the argument for its own verbs and it applies
/// here word for word: this text is not prose. It has to be exactly what the tool accepts, or it is
/// a line that looks like a command and is not one. Two tables would be two answers to a question
/// with one right answer, and they would disagree the first time somebody reworded one of them.
///
/// <b>ONE WORD PER <see cref="StartSetting"/>, and the table cannot name anything else.</b> Until
/// 2026-09-24 this spelled three of the six read-side start types and had to explain why the other
/// three were refused. The writing side has its own type now, so there is nothing left to refuse
/// here: Boot and System have no setting, and neither has "nobody said".
///
/// <b>"delayed" IS THE WORD THE QUERY LANGUAGE ALREADY USES</b> (`start:delayed`, `docs/07`), and
/// that is the owner's decision of 2026-09-24 over sc.exe's "delayed-auto". One vocabulary for
/// asking which entries are late and for making one late.
/// </summary>
public static class StartTypeWords
{
    /// <summary>
    /// The whole surface, in the order a person reading the usage text meets it.
    ///
    /// Automatic first because it is the one an administrator sets deliberately, its late variant
    /// straight after it because that is where the menu in the window puts it, disabled last
    /// because it is the one worth pausing over.
    /// </summary>
    private static readonly (StartSetting Setting, string Word)[] Table =
    [
        (StartSetting.Automatic, "automatic"),
        (StartSetting.AutomaticDelayed, "delayed"),
        (StartSetting.Manual, "manual"),
        (StartSetting.Disabled, "disabled")
    ];

    private static readonly string[] Spellings = [.. Table.Select(entry => entry.Word)];

    /// <summary>
    /// Every word this tool takes for a startup setting.
    ///
    /// For the sentence that offers them when somebody types something else. Built from the same
    /// table the reader uses, so a word that stops being accepted stops being offered in the same
    /// change rather than one release later.
    /// </summary>
    public static IReadOnlyList<string> All => Spellings;

    /// <summary>
    /// How a startup setting is written on a command line, or nothing where there is none.
    ///
    /// <b>Takes a nullable on purpose.</b> "No setting is involved here" is the state every stop,
    /// start and restart is in, and a caller obliged to check that first would be a caller who could
    /// forget to.
    /// </summary>
    public static string? Of(StartSetting? setting)
    {
        foreach (var entry in Table)
        {
            if (setting == entry.Setting)
            {
                return entry.Word;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads one word as a startup setting, or nothing where it is not one.
    ///
    /// <b>Case insensitive, like every other word this tool reads</b> - a verb and a switch are
    /// both matched that way, and a value that alone insisted on lower case would be a rule nobody
    /// could guess from the rest of the surface.
    ///
    /// <b>A nullable rather than a bool and an out value</b>, because every value of the out
    /// parameter would have been a real setting - so a caller that forgot the bool would have
    /// carried on with Automatic.
    /// </summary>
    public static StartSetting? Read(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        foreach (var entry in Table)
        {
            if (word.Equals(entry.Word, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Setting;
            }
        }

        return null;
    }
}
