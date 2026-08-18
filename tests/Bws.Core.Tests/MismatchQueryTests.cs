using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// Asking about the two ways an entry can disagree with itself - backlog 170 and 172, 2026-08-18.
///
/// <b>The window has carried the first of these as a column since backlog 165 and nobody could ask
/// about it.</b> A fact somebody can see and cannot filter on is half a feature: it answers "is
/// this one broken" for the row under the cursor and never "which of the eight hundred are".
///
/// <b>One field with two values rather than two boolean fields, and the values are OPPOSITES.</b>
/// The decision is recorded in backlog 172 with its reasons - a chip has to write itself as a
/// member somebody can read, one field is one addition to a surface people keep in scripts, and an
/// enumeration keeps the direction that backlog 170 refused to collapse into a single answer.
/// </summary>
public sealed class MismatchQueryTests
{
    [Fact]
    public void Each_direction_is_asked_for_by_its_own_name()
    {
        var entries = Machine();

        Assert.Equal(["Stopped"], Selected("mismatch:stopped", entries));
        Assert.Equal(["Running"], Selected("mismatch:running", entries));
    }

    /// <summary>
    /// <c>any</c> and <c>none</c> arrive from the reserved words every enumeration field has, which
    /// is what lets the pair above stay sharp: the convenience costs no information.
    /// </summary>
    [Fact]
    public void Any_covers_both_directions_and_none_covers_neither()
    {
        var entries = Machine();

        Assert.Equal(["Stopped", "Running"], Selected("mismatch:any", entries));
        Assert.Equal(["Ordinary"], Selected("mismatch:none", entries));
    }

    /// <summary>
    /// AN ENTRY NOBODY COULD JUDGE IS NOT AN ENTRY THAT AGREES WITH ITSELF.
    ///
    /// <b>Rule 8 at the exact point this field could break it.</b> The start type behind both
    /// halves can be refused, and answering <c>mismatch:none</c> for such an entry would be a
    /// confident all-clear about a service nobody managed to look at - which is how an audit tool
    /// produces a clean report that means nothing.
    /// </summary>
    [Fact]
    public void An_entry_whose_start_type_was_refused_answers_neither_way()
    {
        var refused = Entries.Any with
        {
            ServiceName = "Refused",
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Denied(5, "access denied")
        };

        Assert.Empty(Selected("mismatch:none", [refused]));
        Assert.Empty(Selected("mismatch:any", [refused]));
        Assert.Empty(Selected("mismatch:running", [refused]));
    }

    /// <summary>
    /// And it is reachable, through the operator this language has for exactly that question.
    ///
    /// <b>THE FIRST VERSION OF THIS TEST ASSERTED THE WRONG THING and the guard is what said so.</b>
    /// It expected <c>mismatch:any</c> over a refused entry to report the answer as incomplete.
    /// It does not, and that is the language working as designed rather than a gap: `any` and
    /// `none` are a plain comparison of the outcome, so a refusal is a definite "there is no value
    /// here" rather than a comparison nobody could make. Asking what could not be READ is what
    /// <c>?</c> is for, and without it somebody could see a partial result with no way to ask what
    /// is missing - QueryFields says so where the word is declared.
    ///
    /// What must never happen is the refused entry answering <c>mismatch:none</c>, and the test
    /// above pins that.
    /// </summary>
    [Fact]
    public void What_could_not_be_judged_is_asked_for_with_the_question_mark()
    {
        var refused = Entries.Any with
        {
            ServiceName = "Refused",
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Denied(5, "access denied")
        };

        Assert.Equal(["Refused"], Selected("mismatch:?", [refused]));

        // And an entry that WAS judged is not swept up by it.
        Assert.Empty(Selected("mismatch:?", [Machine()[2]]));
    }

    private static string[] Selected(string query, IReadOnlyList<ScmEntry> entries) =>
        [.. QueryParserTests.Valid(query).Filter(entries).Entries.Select(entry => entry.ServiceName)];

    /// <summary>
    /// Three entries, one for each answer, so no assertion here can pass by the field returning
    /// the same thing for everything - the trap `ADR-13` names about doubles that cannot tell the
    /// cases apart.
    /// </summary>
    private static ScmEntry[] Machine() =>
    [
        Entries.Any with
        {
            ServiceName = "Stopped",
            Status = EntryStatus.Stopped,
            StartType = Reading<StartType>.Present(StartType.Automatic),
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent()
        },
        Entries.Any with
        {
            ServiceName = "Running",
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Present(StartType.Disabled)
        },
        Entries.Any with
        {
            ServiceName = "Ordinary",
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Present(StartType.Automatic)
        }
    ];
}
