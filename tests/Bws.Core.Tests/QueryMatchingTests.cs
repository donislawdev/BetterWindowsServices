using System.Diagnostics;
using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// What a query decides about an entry.
///
/// Every rule checked here is written down in the query language document, and most of
/// them exist because the obvious alternative produces an empty list where a person
/// expected an answer. An empty list is the dangerous failure in this tool: it reads as
/// "there are none" when it means "you and I disagree about what you asked".
/// </summary>
public sealed class QueryMatchingTests
{
    [Theory]
    [InlineData("spool", true)]
    [InlineData("SPOOL", true)]
    [InlineData("dnscache", false)]
    public void Text_contains_by_default_and_ignores_case(string value, bool expected)
    {
        // Case folding is not a convenience. Windows treats service names that way, and the
        // same service is spelled differently in different places, so honouring case would
        // make results depend on somebody else's typing.
        Assert.Equal(expected, Matches($"name:{value}"));
    }

    [Theory]
    [InlineData("name:=Spooler", true)]
    [InlineData("name:=spooler", true)]
    [InlineData("name:=Spool", false)]
    public void An_equals_sign_asks_for_the_whole_value(string text, bool expected)
    {
        Assert.Equal(expected, Matches(text));
    }

    [Theory]
    [InlineData("name:Spool*", true)]
    [InlineData("name:*ooler", true)]
    [InlineData("name:Spool?r", true)]
    [InlineData("name:Spoole?", true)]
    [InlineData("name:Spool?", false)]
    [InlineData("name:*pool*", true)]
    [InlineData("name:ooler", true)]
    [InlineData("name:*ooler*x", false)]
    public void A_wildcard_describes_the_whole_value(string text, bool expected)
    {
        // Anchored at both ends, unlike the plain form. "Spool*" would be pointless if it
        // meant "contains", because the plain word already does.
        Assert.Equal(expected, Matches(text));
    }

    [Theory]
    [InlineData("name:/^Spool/", true)]
    [InlineData("name:/^pool/", false)]
    [InlineData("display:/print\\s+spooler/", true)]
    public void Slashes_ask_for_an_expression(string text, bool expected)
    {
        Assert.Equal(expected, Matches(text));
    }

    [Fact]
    public void An_expression_using_lookaround_still_works()
    {
        // The linear engine cannot take this one, so it falls back to the ordinary engine
        // with a time limit. If that fallback ever disappears, this goes red rather than
        // the failure showing up as an unexplained parse error in somebody's saved query.
        Assert.True(Matches("name:/Spool(?=er)/"));
    }

    [Fact]
    public void An_expression_that_would_run_away_on_a_backtracking_engine_finishes_anyway()
    {
        // The classic explosion. Expressions come from whoever is at the keyboard and run
        // over the whole listing on every keystroke, so this must not be survivable only
        // by luck. The linear engine takes this one, and the assertion is on the clock
        // rather than the answer: the wrong engine does not return a wrong result here,
        // it stops returning at all.
        var runOfAs = new string('a', 40);
        var query = QueryParserTests.Valid("name:/(a+)+b/");

        var stopwatch = Stopwatch.StartNew();
        var match = query.Match(Entries.Named(runOfAs, runOfAs));
        stopwatch.Stop();

        Assert.False(match.Matched);
        Assert.False(match.TooCostly);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 500,
            $"Took {stopwatch.ElapsedMilliseconds} ms, which means it backtracked.");
    }

    [Fact]
    public void An_expression_the_linear_engine_refuses_and_that_runs_away_is_reported_rather_than_thrown()
    {
        // A backreference is a construct the linear engine will not take, so this one falls
        // back to the backtracking engine where the time limit is the only net. Measured on
        // 2026-08-01 across subjects of 24, 32 and 40 characters: this pattern ran out of
        // time every time, while the plain (a+)+b above was accepted by the linear engine
        // and never did.
        //
        // Running out of time has to arrive as an answer the caller can report. Letting it
        // escape would end a run with a stack trace, and swallowing it into a plain no
        // would hide the fact that nothing was ever checked.
        var runOfAs = new string('a', 32);
        var query = QueryParserTests.Valid(@"name:/(a+)+\1b/");

        var match = query.Match(Entries.Named(runOfAs, runOfAs));

        Assert.False(match.Matched);
        Assert.True(match.TooCostly);
    }

    [Theory]
    [InlineData("status:running", true)]
    [InlineData("status:stopped", false)]
    [InlineData("type:ownProcess", true)]
    [InlineData("type:own-process", true)]
    [InlineData("type:own_process", true)]
    [InlineData("type:OWNPROCESS", true)]
    public void An_enumeration_takes_exact_values_in_any_spelling(string text, bool expected)
    {
        Assert.Equal(expected, Matches(text));
    }

    [Theory]
    [InlineData(EntryType.KernelDriver, true)]
    [InlineData(EntryType.FileSystemDriver, true)]
    [InlineData(EntryType.OwnProcess, false)]
    [InlineData(EntryType.SharedProcess, false)]
    public void Driver_covers_both_kinds_of_driver(EntryType type, bool expected)
    {
        // The document's own example uses this, and there is no single value behind it:
        // Windows has two driver kinds and no word for both. Losing the grouping would
        // turn the most ordinary question in the tool into type:kernelDriver,fileSystemDriver.
        Assert.Equal(expected, Matches("type:driver", Entries.Any with { EntryType = type }));
    }

    [Theory]
    [InlineData("pid:1234", true)]
    [InlineData("pid:1235", false)]
    [InlineData("pid:>1000", true)]
    [InlineData("pid:>1234", false)]
    [InlineData("pid:>=1234", true)]
    [InlineData("pid:<2000", true)]
    [InlineData("pid:<=1234", true)]
    [InlineData("pid:1000-2000", true)]
    [InlineData("pid:1234-1234", true)]
    [InlineData("pid:1-100", false)]
    public void Numbers_compare_and_ranges_include_both_ends(string text, bool expected)
    {
        Assert.Equal(expected, Matches(text));
    }

    [Fact]
    public void A_member_that_cannot_be_judged_is_false_and_never_an_error()
    {
        // A stopped service has no process. Asking about its process id is a question with
        // the answer no, not a query that fell over.
        var stopped = Entries.Any with
        {
            Status = EntryStatus.Stopped,
            ProcessId = Reading<int>.Absent()
        };

        Assert.False(Matches("pid:>1000", stopped));
        Assert.False(QueryParser.Parse("pid:>1000").Query!.Match(stopped).Unreadable);
    }

    // -- alternatives, negation and order ------------------------------------------------

    [Theory]
    [InlineData("status:running,paused", true)]
    [InlineData("status:stopped,paused", false)]
    public void A_comma_inside_one_member_means_any_of_these(string text, bool expected)
    {
        Assert.Equal(expected, Matches(text));
    }

    [Fact]
    public void The_same_field_twice_means_either_and_not_both()
    {
        // Without this, ticking two status boxes in the interface would build a query that
        // can never match anything, and ticking two boxes is the most ordinary thing a
        // person does. The rule is forced by the promise that clicking filters writes a
        // query, not chosen for elegance.
        Assert.True(Matches("status:running status:stopped"));
        Assert.True(Matches("pid:1234 pid:9999"));
    }

    [Fact]
    public void Two_different_fields_mean_both_at_once()
    {
        Assert.True(Matches("status:running type:ownProcess"));
        Assert.False(Matches("status:running type:driver"));
    }

    [Fact]
    public void An_exclusion_wins_over_anything_that_let_the_entry_through()
    {
        Assert.False(Matches("status:running !name:spool"));
        Assert.False(Matches("!name:spool status:running"));
    }

    [Fact]
    public void A_query_of_nothing_but_exclusions_means_everything_else()
    {
        // The alternative reading, that exclusions select from nothing, would make a query
        // built only of exclusions return an empty list, which is useless.
        Assert.True(Matches("!status:stopped"));
        Assert.False(Matches("!status:running"));
    }

    [Theory]
    [InlineData("status:running type:ownProcess !name:dnscache")]
    [InlineData("!name:dnscache status:running type:ownProcess")]
    [InlineData("type:ownProcess !name:dnscache status:running")]
    public void The_order_members_were_typed_in_never_changes_the_answer(string text)
    {
        // Worth its own test because breaking it shows up as results that depend on the
        // order somebody clicked filters in, which nobody would ever think to check.
        Assert.True(Matches(text));
    }

    // -- bare words ---------------------------------------------------------------------

    [Theory]
    [InlineData("spooler", true)]
    [InlineData("print", true)]
    [InlineData("localsystem", true)]
    [InlineData("dnscache", false)]
    public void A_bare_word_searches_name_display_name_and_account(string text, bool expected)
    {
        Assert.Equal(expected, Matches(text));
    }

    [Fact]
    public void A_bare_word_does_not_reach_fields_outside_the_free_search_set()
    {
        // Widening this silently is the thing the syntax version exists to prevent: the
        // same saved query would quietly start returning different entries.
        Assert.False(Matches("ownprocess"));
        Assert.False(Matches("running"));
    }

    [Fact]
    public void Two_bare_words_mean_both()
    {
        Assert.True(Matches("spooler print"));
        Assert.False(Matches("spooler dnscache"));
    }

    // -- delayed automatic start --------------------------------------------------------

    [Fact]
    public void Automatic_covers_delayed_because_an_alias_means_what_the_word_means()
    {
        Assert.True(Matches("start:auto", Delayed));
        Assert.True(Matches("start:automatic", Delayed));
        Assert.True(Matches("start:auto", PlainAutomatic));
        Assert.True(Matches("start:automatic", PlainAutomatic));
    }

    [Fact]
    public void Delayed_narrows_to_the_ones_that_actually_start_late()
    {
        Assert.True(Matches("start:delayed", Delayed));
        Assert.False(Matches("start:delayed", PlainAutomatic));
    }

    [Fact]
    public void Automatic_without_delayed_is_written_as_an_exclusion()
    {
        // The escape hatch for anybody who needs the distinction, built out of rules that
        // already exist rather than a third value that would need explaining.
        Assert.True(Matches("start:auto !start:delayed", PlainAutomatic));
        Assert.False(Matches("start:auto !start:delayed", Delayed));
    }

    [Fact]
    public void The_delay_flag_is_absent_where_the_idea_does_not_apply()
    {
        var manual = Entries.Any with
        {
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent()
        };

        Assert.False(Matches("start:delayed", manual));
        Assert.True(Matches("start:manual", manual));
    }

    // -- the four states a field can be in ----------------------------------------------

    [Fact]
    public void None_asks_for_a_value_that_is_genuinely_missing()
    {
        var withoutAccount = Entries.Any with { Account = Reading<string>.Absent() };

        Assert.True(Matches("account:none", withoutAccount));
        Assert.False(Matches("account:none", Entries.Any));
    }

    [Fact]
    public void Any_asks_whether_there_is_a_value_at_all()
    {
        Assert.True(Matches("account:any", Entries.Any));
        Assert.False(Matches("account:any", Entries.Any with { Account = Reading<string>.Absent() }));
    }

    [Fact]
    public void A_question_mark_asks_for_what_could_not_be_read()
    {
        // Odd-looking and necessary. In an audit tool "show me what you failed to read" is
        // a question in its own right, and without it somebody can see that a result is
        // partial with no way to ask what is missing.
        Assert.True(Matches("account:?", Refused));
        Assert.False(Matches("account:?", Entries.Any));
    }

    [Fact]
    public void Quoting_takes_the_reserved_meaning_away()
    {
        var named = Entries.Named("none", "The none service");

        Assert.True(Matches("""name:"none" """, named));
        Assert.False(Matches("name:none", named));
    }

    [Fact]
    public void A_member_about_an_unreadable_field_does_not_match_and_says_the_result_is_partial()
    {
        // The most important rule in the language. Without it, a query on a machine without
        // the right permissions returns a short list that looks like a complete answer, and
        // an answer that is believable and untrue at once is the worst thing this tool can
        // produce.
        var match = QueryParser.Parse("account:localsystem").Query!.Match(Refused);

        Assert.False(match.Matched);
        Assert.True(match.Unreadable);
    }

    [Fact]
    public void An_exclusion_about_an_unreadable_field_does_not_exclude_and_still_admits_the_gap()
    {
        // We cannot tell whether the exclusion applies, so the entry stays. Dropping it
        // would be a decision made on information nobody has.
        var match = QueryParser.Parse("!account:localsystem").Query!.Match(Refused);

        Assert.True(match.Matched);
        Assert.True(match.Unreadable);
    }

    [Fact]
    public void An_unreadable_delay_flag_leaves_the_start_type_answerable_and_the_delay_not()
    {
        // The reason the delay is a field of its own rather than a sixth start type. One
        // enumeration would have to call this plain automatic, which is a confident answer
        // about something nobody read.
        var unknownDelay = Entries.Any with { DelayedAuto = Reading<bool>.Denied(Entries.AccessDenied, "access denied") };

        Assert.True(Matches("start:auto", unknownDelay));

        var delayed = QueryParser.Parse("start:delayed").Query!.Match(unknownDelay);

        Assert.False(delayed.Matched);
        Assert.True(delayed.Unreadable);
    }

    [Fact]
    public void A_query_that_touches_nothing_unreadable_reports_a_complete_result()
    {
        var result = QueryParser.Parse("status:running").Query!.Filter([Entries.Any, Refused]);

        Assert.Equal(0, result.Unreadable);
        Assert.Equal(2, result.Entries.Count);
    }

    [Fact]
    public void Filtering_counts_every_entry_whose_answer_rested_on_something_unreadable()
    {
        var result = QueryParser.Parse("account:localsystem").Query!.Filter([Entries.Any, Refused, Refused]);

        Assert.Equal(2, result.Unreadable);
        Assert.Single(result.Entries);
    }

    // -- quoting and escaping -----------------------------------------------------------

    [Fact]
    public void Quotes_protect_a_value_with_a_space_in_it()
    {
        var spaced = Entries.Named("ContosoVPN Tunnel", "ContosoVPN Tunnel");

        Assert.True(Matches("""name:"ContosoVPN Tunnel" """, spaced));
    }

    [Fact]
    public void Quotes_protect_a_comma_from_splitting_the_value()
    {
        var comma = Entries.Named("Backup", "Kowalski, Jan");

        Assert.True(Matches("""display:"Kowalski, Jan" """, comma));
    }

    [Fact]
    public void Only_the_first_colon_separates_so_a_path_shaped_value_survives()
    {
        // Without this every path would be a syntax error, and paths are what this domain
        // is full of.
        var path = Entries.Named("Fabrikam", @"C:\Program Files (x86)\Fabrikam");

        Assert.True(Matches(@"display:C:\Program", path));
    }

    [Fact]
    public void A_backslash_in_front_of_an_ordinary_character_stays_a_backslash()
    {
        var account = Entries.Any with { Account = Reading<string>.Present(@"NT SERVICE\McmSvc") };

        Assert.True(Matches(@"account:SERVICE\McmSvc", account));
    }

    [Theory]
    [InlineData(@"name:\*", true)]
    [InlineData("""name:"*" """, true)]
    [InlineData("name:*", true)]
    public void An_escaped_or_quoted_star_is_a_literal_star(string text, bool expected)
    {
        // The last case is the control: a bare star is still a wildcard, so it matches the
        // entry whatever its name is. Without it this test would pass even if escaping did
        // nothing at all.
        Assert.Equal(expected, Matches(text, Entries.Named("*", "A literal star")));
    }

    [Fact]
    public void An_escaped_star_does_not_match_a_name_that_merely_has_one_somewhere()
    {
        Assert.False(Matches(@"name:\*", Entries.Named("Spooler", "Print Spooler")));
    }

    [Fact]
    public void An_escaped_exclamation_mark_is_part_of_the_value_rather_than_a_negation()
    {
        var shouty = Entries.Named("!raz", "Loud service");

        Assert.True(Matches(@"name:\!raz", shouty));
        Assert.True(Matches("""name:"!raz" """, shouty));
    }

    [Fact]
    public void A_quote_can_be_put_inside_a_quoted_value()
    {
        var odd = Entries.Named("on\"off", "Odd name");

        Assert.True(Matches("""name:"on\"off" """, odd));
    }

    private static ScmEntry Delayed => Entries.Any with
    {
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(true)
    };

    private static ScmEntry PlainAutomatic => Entries.Any with
    {
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false)
    };

    private static ScmEntry Refused => Entries.Any with
    {
        Account = Reading<string>.Denied(Entries.AccessDenied, "access denied")
    };

    private static bool Matches(string text) => Matches(text, Entries.Any);

    private static bool Matches(string text, ScmEntry entry) =>
        QueryParserTests.Valid(text).Match(entry).Matched;
}
