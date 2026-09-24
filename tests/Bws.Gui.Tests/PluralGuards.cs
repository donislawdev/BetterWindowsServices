// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Text.RegularExpressions;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the plan panel says when there is exactly one of something.
///
/// <b>WRITTEN 2026-08-19 BECAUSE THE PANEL SHIPPED SAYING "Done. All 1 entries are where you
/// asked."</b> One row picked is the commonest selection there is, so that sentence was wrong far
/// more often than it was right, and it is the last thing somebody reads after changing a machine.
/// Seven sentences had the same shape: a count or a group pronoun with no singular beside it.
///
/// <b>The command line already had the pair, which is what makes this a regression rather than an
/// omission.</b> <c>cli.plan.warning.cascade.one</c> has existed since the command line learned to
/// warn, and its comment there says why in one line - a warning reading "1 other entries" spends
/// the trust the warning itself needs. The window said the same things later, from the same facts
/// in the core, and arrived with the plural half only.
///
/// <b>NOTHING IN THIS PROJECT WOULD HAVE GONE RED, AND THAT IS THE POINT OF THIS FILE.</b> 955
/// tests, a contrast guard, a type scale guard and a mutation register stood around this panel, and
/// the defect was found by reading the language file. Guards here read prose, which nothing else in
/// this project does.
/// </summary>
public sealed class PluralGuards
{
    /// <summary>
    /// The line under the search box, which is the sentence a person reads after every keystroke.
    ///
    /// <b>Backlog 207, the same fault outside the panel this file was written for.</b> Four keys in
    /// the window carried a counted plural with no singular beside them, and two of them are this
    /// line - so a machine holding one entry read "1 entries" under a box somebody had just typed
    /// in.
    ///
    /// <b>The full stop went with the repair and that is a separate decision, taken because the
    /// line is a label rather than a sentence.</b> It reads "810 entries" now, and the narrowed
    /// form beside it has never carried one.
    /// </summary>
    [Fact]
    public async Task The_count_line_has_a_singular_for_a_machine_holding_one_entry()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        Assert.Equal("1 entry", model.Says.Status);
        Assert.DoesNotContain(".", model.Says.Status, StringComparison.Ordinal);

        // AND THE NARROWED FORM, where the noun follows the SECOND number rather than the first -
        // "1 of 810 entries" is right and the trap is answering it from the count that changed.
        model.QueryText = "name:nothing-is-called-this";

        Assert.Equal("0 of 1 entry", model.Says.Status);
        Assert.False(CountedPlural.IsMatch(model.Says.Status), model.Says.Status);
    }

    /// <summary>
    /// What the window admits about an answer, when there is exactly one of whatever it admits to.
    ///
    /// <b>The reachable half of backlog 207's window list.</b> An entry that refused the field a
    /// query was judged on is an ordinary state on a real machine - one service the session may not
    /// read the configuration of - and the sentence about it counted in plurals only.
    ///
    /// <b>What this does NOT reach:</b> the same pair for an expression that ran out of time. Its
    /// singular is written and consumed the same way, and nothing here drives a query to a timeout.
    /// </summary>
    [Fact]
    public async Task What_the_window_admits_has_a_singular_for_one_entry()
    {
        var refused = Rows.Entry("Spooler") with
        {
            Account = Reading<string>.Denied(5, "Access is denied.")
        };

        var model = new MainViewModel(new LiveMachine(refused), new SteppedClock());

        await model.LoadAsync();

        model.QueryText = "account:LocalSystem";

        Assert.Equal(Bws.Gui.Texts.Of("gui.status.partial.one", 1), model.Says.Notice);

        // NOT ASKED OF CountedPlural, AND THE REASON IS THAT GUARD'S OWN LIMIT MEASURED ON A REAL
        // SENTENCE. Its pattern allows two words between the number and a word ending in s, so
        // "1 entry was judged" matches on "was" - a correct singular flagged as a plural. The
        // pattern is right for the panel it was written for and this family is outside it, so the
        // assertion above is an exact string instead. Backlog 225.
    }


    /// <summary>
    /// The window refuses a kind of step or ask it does not know, rather than calling it a start.
    ///
    /// <b>The same repair the core got on 2026-08-25, in the four places the window had it</b> - and
    /// one of those four was the title of the plan panel, which said "What restarting X would do"
    /// over a step that set a start type. A probe on a live window caught that one - nothing here
    /// did, so these two lines exist to make the refusals themselves executed rather than written.
    /// </summary>
    [Fact]
    public void The_window_refuses_a_kind_it_does_not_know_rather_than_naming_it_a_start()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanWords.Word((StepOperation)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanWords.Doing((ActionKind)99));
    }

    /// <summary>
    /// MOVED TO <see cref="CountedWords"/> ON 2026-08-26, when the size ratchet fired on this file.
    /// The history this pattern carries - why it is not <c>\b1 \w+s\b</c>, and what it deliberately
    /// does not catch - moved with it rather than staying here describing something that left.
    /// The vocabulary and the guard that keeps it honest are about the LANGUAGE FILES, while
    /// everything else here drives the panel - so the seam was already there to be found.
    /// </summary>
    private static readonly Regex CountedPlural = CountedWords.Plural;

    /// <summary>
    /// Every sentence the panel can say about a selection of one, read for a plural.
    ///
    /// <b>The panel is driven rather than the language file scanned</b>, because the fault is in the
    /// pairing of a sentence with a count and only the panel holds both. A file scan would have to
    /// guess which keys are ever asked for with a one in them, which is the guessing this catches.
    /// </summary>
    [Fact]
    public void Nothing_the_panel_says_about_one_entry_is_written_for_more_than_one()
    {
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(OneOfEverything()));
        panel.Finished(Arrived(panel));

        var said = Everything(panel);

        var plural = said.Where(line => CountedPlural.IsMatch(line)).ToList();

        Assert.True(
            plural.Count == 0,
            "The panel is holding one of something and counting it as though there were several. "
            + "A sentence reading \"1 entries\" is read by somebody deciding whether to change a "
            + "machine, and it spends the trust the sentence itself needs:"
            + Environment.NewLine + string.Join(Environment.NewLine, plural));
    }

    /// <summary>
    /// Without this the guard above passes on a panel that says nothing at all, which is the state
    /// every panel starts in and exactly what a broken fixture looks like.
    /// </summary>
    [Fact]
    public void The_panel_was_actually_made_to_say_something()
    {
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(OneOfEverything()));
        panel.Finished(Arrived(panel));

        Assert.True(Everything(panel).Count > 8, $"Only {Everything(panel).Count} sentences were read off the panel.");
    }

    /// <summary>
    /// The sentence that shipped wrong, at the count it shipped wrong at.
    ///
    /// <b>Asserted against the language file rather than against a quoted string</b>, so rewording
    /// the sentence does not redden this - what is held is that the singular one is chosen, not what
    /// it happens to say today.
    /// </summary>
    [Fact]
    public void A_run_over_one_entry_says_so_in_the_singular()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Selection("Spooler"));
        panel.Finished(Arrived(panel));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.notice.done.one"), panel.Notice);
    }

    [Fact]
    public void A_run_over_more_than_one_entry_still_counts_them()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Selection("Spooler", "W32Time"));
        panel.Finished(Arrived(panel));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.notice.done.many", 2), panel.Notice);
    }

    /// <summary>
    /// The half a count of arrivals would get wrong.
    ///
    /// <b>A run of one that failed reports zero arrived out of one</b>, so a singular chosen by the
    /// first number rather than the second would put "0 of 1 entries" back on screen by a different
    /// route. This is the second case rule 12 asks for, applied to the fix rather than to the fault.
    /// </summary>
    [Fact]
    public void One_entry_that_did_not_arrive_is_also_said_in_the_singular()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Selection("Spooler"));
        panel.Finished(Refused(panel));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.notice.partly.one"), panel.Notice);
    }

    [Fact]
    public void A_cascade_of_one_and_a_cascade_of_two_are_different_sentences()
    {
        var one = PlanWords.Describe(new PlanWarning(PlanWarningKind.Cascade, "Spooler", ["W32Time"]));
        var two = PlanWords.Describe(new PlanWarning(PlanWarningKind.Cascade, "Spooler", ["W32Time", "Dnscache"]));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.warning.cascade.one", "Spooler", 1, "W32Time"), one);
        Assert.Contains("2 other entries", two, StringComparison.Ordinal);
    }

    /// <summary>
    /// The four sentences where the plural is a pronoun rather than a number, which the pattern at
    /// the head of this file deliberately does not look for.
    ///
    /// <b>One assertion per kind rather than one loop, because the singular and the plural of each
    /// are different English and there is nothing to work out from the value.</b> What is held is
    /// that the two differ and that the count decides which arrives - not the wording, which stays
    /// changeable.
    /// </summary>
    [Fact]
    public void A_group_of_one_is_never_called_these_or_those()
    {
        Differ(
            PlanWords.Describe(new PlanWarning(PlanWarningKind.DependentsInTheWay, "Spooler", ["W32Time"])),
            PlanWords.Describe(new PlanWarning(PlanWarningKind.DependentsInTheWay, "Spooler", ["W32Time", "Dnscache"])));

        Differ(
            PlanWords.Describe(new PlanWarning(PlanWarningKind.SharedProcess, "Spooler", ["W32Time"])),
            PlanWords.Describe(new PlanWarning(PlanWarningKind.SharedProcess, "Spooler", ["W32Time", "Dnscache"])));

        Differ(
            PlanWords.Describe(new PlanProblem(PlanProblemKind.CascadeNotOperable, "Spooler", ["Beep"])),
            PlanWords.Describe(new PlanProblem(PlanProblemKind.CascadeNotOperable, "Spooler", ["Beep", "Null"])));

        Differ(
            PlanWords.Describe(new PlanProblem(PlanProblemKind.CannotComeBack, "Spooler", ["W32Time"])),
            PlanWords.Describe(new PlanProblem(PlanProblemKind.CannotComeBack, "Spooler", ["W32Time", "Dnscache"])));
    }

    /// <summary>
    /// Every singular has a plural beside it, read out of the language file.
    ///
    /// <b>The one guard here that is not about English, and it catches the half-built pair.</b> A
    /// key added as <c>.one</c> with no <c>.many</c> renders as its own key on somebody's screen -
    /// the failure `TextKeyGuards` describes, arriving through a route that guard cannot see,
    /// because both halves would be perfectly declared and perfectly referenced.
    /// </summary>
    [Fact]
    public void Every_singular_sentence_has_a_plural_beside_it()
    {
        var keys = Declared();

        var lonely = keys
            .Where(key => key.EndsWith(".one", StringComparison.Ordinal))
            .Where(key => !keys.Contains(string.Concat(key.AsSpan(0, key.Length - 4), ".many")))
            .Concat(keys
                .Where(key => key.EndsWith(".many", StringComparison.Ordinal))
                .Where(key => !keys.Contains(string.Concat(key.AsSpan(0, key.Length - 5), ".one"))))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            lonely.Count == 0,
            "These sentences have a singular or a plural and not the other one. Whichever is "
            + "missing renders on screen as its own key, and nothing else in this project reports "
            + "that:"
            + Environment.NewLine + string.Join(Environment.NewLine, lonely));
    }

    [Fact]
    public void The_language_file_was_actually_read()
    {
        Assert.True(Declared().Count > 20, $"Only {Declared().Count} keys were found in the language file.");
    }

    /// <summary>
    /// Both halves of every pair reach the loader as words rather than as their own name.
    ///
    /// <b>THIS CLOSES A HOLE THE ASSERTIONS ABOVE WOULD OTHERWISE LEAVE OPEN, and it is worth
    /// saying out loud because those assertions look airtight.</b> They compare what the panel says
    /// against <c>Texts.Of</c> of the same key - two calls into the same loader - so a key the
    /// assembly does not actually carry comes back as itself on both sides and they agree
    /// perfectly. That is the shape <see cref="LanguageGuards"/> was written for, arriving through
    /// a pair rather than through the whole file, so it is asked here for the pairs by name.
    /// </summary>
    [Fact]
    public void Both_halves_of_every_pair_say_words_rather_than_their_own_key()
    {
        var pairs = Declared()
            .Where(key => key.EndsWith(".one", StringComparison.Ordinal)
                || key.EndsWith(".many", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(pairs.Count >= 14, $"Only {pairs.Count} halves of a pair were found, which is fewer than exist.");

        var unloaded = pairs.Where(key => string.Equals(Bws.Gui.Texts.Of(key), key, StringComparison.Ordinal)).ToList();

        Assert.True(
            unloaded.Count == 0,
            "These keys are in the language file and the loader hands back their own name, which is "
            + "what a person would read on screen:"
            + Environment.NewLine + string.Join(Environment.NewLine, unloaded));
    }

    /// <summary>
    /// The sentence for one and the sentence for two are not the same sentence, and the one for a
    /// group of one does not point at the group as though it had members.
    ///
    /// <b>Both halves are needed and neither is enough.</b> Without the first, wording both keys
    /// identically would pass. Without the second, a "singular" that still said "these" would.
    /// </summary>
    private static void Differ(string one, string many)
    {
        Assert.NotEqual(one, many);
        Assert.DoesNotContain("these", one, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("those", one, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Everything the panel would put on a screen, as flat lines.</summary>
    private static List<string> Everything(Planned panel) =>
    [
        panel.Heading,
        panel.Notice,
        panel.Extra,
        panel.Overlapping,
        panel.Blocked,
        .. panel.Steps.Select(line => line.Text),
        .. panel.Warnings.Select(line => line.Text),
        .. panel.Warnings.Select(line => line.Label),
        .. panel.Problems,
        // BOTH HALVES OF A FAILURE SINCE 2026-09-07, because both reach a screen. The line became
        // an object when a way out arrived under it, and reading only the sentence would leave the
        // word on that button - the one press in this panel that leads to a process ending -
        // outside everything this file checks.
        .. panel.Failures.Select(failure => failure.Text),
        .. panel.Failures.Select(failure => failure.Label),
        .. panel.Commands,
        .. panel.WayBack,

        // THE BUTTON'S OWN WORD SINCE 2026-09-15, when it started counting entries - "Stop 3
        // entries" - and so became one more sentence a count could get wrong.
        panel.CarryOutLabel
    ];

    /// <summary>
    /// One selected entry that drags one other in, warns about one of everything, and sits beside
    /// one refusal of each kind. Every list in it is exactly one long, which is the whole point.
    /// </summary>
    private static BulkPlan OneOfEverything() => new()
    {
        Action = new BulkAction(ActionKind.Stop, ["Spooler"]),
        Plans =
        [
            new OperationPlan
            {
                Action = new ServiceAction(ActionKind.Stop, "Spooler"),
                Steps =
                [
                    new PlanStep("W32Time", "Windows Time", StepOperation.Stop, StepReason.Cascade),
                    new PlanStep("Spooler", "Print Spooler", StepOperation.Stop, StepReason.Requested)
                ],
                Warnings =
                [
                    new PlanWarning(PlanWarningKind.Cascade, "Spooler", ["W32Time"]),
                    new PlanWarning(PlanWarningKind.DependentsInTheWay, "Spooler", ["W32Time"]),
                    new PlanWarning(PlanWarningKind.SharedProcess, "Spooler", ["W32Time"])
                ],
                Problems = []
            }
        ],
        Problems =
        [
            new PlanProblem(PlanProblemKind.CascadeNotOperable, "Dnscache", ["Beep"]),
            new PlanProblem(PlanProblemKind.CannotComeBack, "Dnscache", ["W32Time"])
        ]
    };

    private static BulkPlan Selection(params string[] names) => new()
    {
        Action = new BulkAction(ActionKind.Stop, names),
        Plans =
        [
            .. names.Select(name => new OperationPlan
            {
                Action = new ServiceAction(ActionKind.Stop, name),
                Steps = [new PlanStep(name, name, StepOperation.Stop, StepReason.Requested)],
                Warnings = [],
                Problems = []
            })
        ],
        Problems = []
    };

    private static BulkRun Arrived(Planned panel) => Ran(panel, refused: false);

    private static BulkRun Refused(Planned panel) => Ran(panel, refused: true);

    private static BulkRun Ran(Planned panel, bool refused)
    {
        var plan = panel.Plan!;

        return new BulkRun
        {
            Plan = plan,
            Runs =
            [
                .. plan.Plans.Select(one => new PlanRun
                {
                    Plan = one,
                    Results =
                    [
                        .. one.Steps.Select(step => new StepResult
                        {
                            Step = step,
                            Outcome = refused ? StepOutcome.Failed : StepOutcome.Succeeded,
                            SkippedBecause = null,
                            Status = refused ? EntryStatus.Running : EntryStatus.Stopped,
                            ProcessId = refused
                                ? Reading<int>.Present(4812)
                                : Reading<int>.Absent(),
                            ErrorCode = refused ? 5 : 0,
                            Error = refused ? "Access is denied." : null,
                            Milliseconds = 10
                        })
                    ],
                    Cancelled = false,
                    Ceiling = TimeSpan.FromMinutes(1)
                })
            ]
        };
    }

    /// <summary>Every key the language file declares, read out of the file rather than the loader.</summary>
    private static HashSet<string> Declared()
    {
        var path = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Resources", "gui.en.json");

        return Regex
            .Matches(File.ReadAllText(path), @"""(gui\.[^""]+)""\s*:", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }
}
