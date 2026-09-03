namespace Bws.Core;

/// <summary>
/// Everything that went wrong, out of one exception that may be carrying several.
///
/// <b>BACKLOG 307, AND THE FAULT WAS A LOOP THAT LOOKED COMPLETE.</b> Both surfaces walked
/// <c>InnerException</c> from the top down, which is the whole chain for an ordinary exception and
/// exactly one branch of a tree for an <see cref="AggregateException"/>. That type is not exotic
/// here: the listing and the second pass both read several entries at once, which is precisely
/// the shape that fails several times at once, and it hands back one exception holding all of them.
/// What a person got was "One or more errors occurred." and one cause - and the sentence saying
/// which entries were refused was in the ones it dropped.
///
/// <b>The same file that lost them describes the trap.</b> <c>WindowsScmCatalog</c> says in as many
/// words that through the parallel loop a failure "arrives wrapped in an AggregateException". Written
/// down, read by nobody, and one layer up the loop went down the first branch.
///
/// <b>In the core rather than in each surface, and the two surfaces still write their own
/// sentence.</b> What is shared is which causes there are, in what order. A terminal puts them on
/// separate lines and a window has one line under the list, so the joining belongs to whoever is
/// speaking - the same split the core already makes about warnings, which carry facts and no words.
///
/// <b>Two things bound the answer, and both are deliberate.</b> Repeats are dropped, because eight
/// hundred entries refused for one reason produce eight hundred identical sentences and one of them
/// is the news. And the whole is capped, because a status line is a line: something has to give
/// when a failure genuinely has more distinct causes than anybody can read, and dropping the
/// hundredth is better than a window with no room for the list.
/// </summary>
public static class Causes
{
    /// <summary>
    /// How many distinct causes are worth saying.
    ///
    /// Ten rather than a number chosen to look modest. Every real failure this project has seen has
    /// one or two, so the cap is not a summary of anything - it is the point past which a sentence
    /// has stopped being a sentence.
    /// </summary>
    private const int Most = 10;

    /// <summary>
    /// What went wrong, innermost causes included, without repeats and in the order they were
    /// reached.
    /// </summary>
    public static IReadOnlyList<string> Of(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var said = new List<string>();
        var already = new HashSet<string>(StringComparer.Ordinal);

        Gather(failure, said, already);

        return said;
    }

    private static void Gather(Exception? level, List<string> said, HashSet<string> already)
    {
        while (level is not null && said.Count < Most)
        {
            // The wrapper's own message is "One or more errors occurred", which tells nobody
            // anything - so it is stepped over rather than said, and only when there is something
            // underneath it to say instead. An aggregate holding an aggregate is handled by this
            // walk arriving at it again, which is nesting being an artefact of how the work was
            // arranged rather than anything a person did.
            //
            // NOT THROUGH Flatten, AND A TEST CAUGHT THE DIFFERENCE. That method reorders: it
            // takes the plain exceptions first and the ones it had to unpack afterwards, so a
            // failure would be read out in an order nobody produced. Recursing keeps the order the
            // causes were collected in, which is the only order there is any claim to.
            if (level is AggregateException several && several.InnerExceptions.Count > 0)
            {
                foreach (var one in several.InnerExceptions)
                {
                    Gather(one, said, already);
                }

                return;
            }

            if (already.Add(level.Message))
            {
                said.Add(level.Message);
            }

            level = level.InnerException;
        }
    }
}
