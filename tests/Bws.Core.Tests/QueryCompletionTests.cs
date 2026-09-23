using Bws.Core.Querying;
using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// What the language offers to write where the caret stands - the half of the list under the
/// search box that can be checked without a window.
///
/// <b>Every row of the state table in `docs/PROJEKT-PODPOWIEDZI-20260915.md` section 4.7 is a
/// test here</b>, and two of its cells were corrected by these tests rather than copied: the
/// table said Down on <c>status:running</c> offers four rows and on <c>status:running,st</c>
/// twelve, and no single rule produces those two cells and the other seventeen. The rule these
/// hold is the one that does: asking on purpose differs from typing in exactly two places, an
/// empty field prefix and a word already written in full.
///
/// <b>What these assert is the list, in order, and the edit each row would make</b> - not "some
/// completion came back". The order is the language's declaration order, which groups related
/// fields, and the edit is what a person sees in the box.
/// </summary>
public sealed class QueryCompletionTests
{
    private static IEnumerable<string> Typing(string text) =>
        QueryCompletions.WhileTyping(text, text.Length).Select(offered => offered.Word);

    private static IEnumerable<string> Asking(string text) =>
        QueryCompletions.OnRequest(text, text.Length).Select(offered => offered.Word);

    private static readonly string[] StatusValues =
    [
        "running", "stopped", "paused", "pending", "startPending", "stopPending",
        "pausePending", "continuePending", "unknown", "any", "none", "?"
    ];

    [Fact]
    public void An_empty_box_offers_nothing_while_typing_and_every_field_on_request()
    {
        Assert.Empty(Typing(string.Empty));
        Assert.Equal(QueryFields.Names, Asking(string.Empty));

        // Twenty-one, and the number is asserted so that a field added to the language without a
        // sentence for it is noticed here as well as by the window's guard.
        Assert.Equal(21, QueryFields.Names.Count);
    }

    [Fact]
    public void The_start_of_a_field_name_offers_the_fields_beginning_with_it_in_declaration_order()
    {
        // status is declared before start, so it comes first - the table in the design wrote
        // them the other way round and the language's order wins.
        Assert.Equal(["status", "start"], Typing("sta"));
        Assert.Equal(["status", "start"], Asking("sta"));

        Assert.Equal(["status:", "start:"], QueryCompletions.WhileTyping("sta", 3).Select(offered => offered.Written));
    }

    [Fact]
    public void Case_is_folded_and_the_word_is_written_the_way_the_language_spells_it()
    {
        var offered = QueryCompletions.WhileTyping("STA", 3);

        Assert.Equal(["status:", "start:"], offered.Select(completion => completion.Written));
        Assert.Equal("start:", offered[1].Apply("STA").Text);
    }

    [Fact]
    public void A_leading_exclamation_mark_is_skipped_and_stays_where_it_was()
    {
        var offered = QueryCompletions.WhileTyping("!sta", 4);

        Assert.Equal(["status", "start"], offered.Select(completion => completion.Word));
        Assert.Equal(new QueryEdit("!start:", 7), offered[1].Apply("!sta"));
    }

    [Fact]
    public void A_field_name_is_matched_by_its_prefix_and_never_by_containment()
    {
        // `on` sits inside dependson and description. A word somebody is searching for must not
        // grow a list of fields under it.
        Assert.Empty(Typing("on"));
        Assert.Empty(Asking("on"));
    }

    [Fact]
    public void A_colon_offers_every_value_of_the_field_and_then_the_three_words_every_field_takes()
    {
        Assert.Equal(StatusValues, Typing("status:"));
        Assert.Equal(StatusValues, Asking("status:"));

        var kinds = QueryCompletions.WhileTyping("status:", 7).Select(offered => offered.Kind).Distinct();

        Assert.Equal([QueryCompletionKind.Value, QueryCompletionKind.Word], kinds);
        Assert.All(QueryCompletions.WhileTyping("status:", 7), offered => Assert.Equal("status", offered.Field));
    }

    [Fact]
    public void The_start_of_a_value_narrows_the_list_to_the_values_beginning_with_it()
    {
        Assert.Equal(["running"], Typing("status:r"));
        Assert.Equal(["running"], Asking("status:r"));
        Assert.Equal(["automatic", "auto", "any"], Typing("start:a"));
    }

    /// <summary>
    /// Decision 10 of the design: <c>pend</c> finds <c>pending</c> and the four that end in it.
    /// Value names are compound where field names are not, and somebody after the colon is
    /// choosing from a closed list rather than searching.
    /// </summary>
    [Fact]
    public void A_later_part_of_a_compound_value_matches_too_and_comes_after_the_whole_word_matches()
    {
        Assert.Equal(
            ["pending", "startPending", "stopPending", "pausePending", "continuePending"],
            Typing("status:pend"));

        Assert.Equal(["fileSystemDriver"], Typing("type:system"));
        Assert.Equal(["ownProcess", "sharedProcess"], Typing("type:pro"));
    }

    /// <summary>
    /// The reason the second match is on the start of a part rather than on containment: one
    /// letter is contained in most words, and the list would stop meaning anything.
    /// </summary>
    [Fact]
    public void A_letter_contained_in_the_middle_of_a_value_does_not_match_it()
    {
        // startPending contains an r and is not offered under status:r - manual and disabled
        // contain an a and are not offered under start:a.
        Assert.DoesNotContain("startPending", Typing("status:r"));
        Assert.DoesNotContain("manual", Typing("start:a"));
        Assert.DoesNotContain("disabled", Typing("start:a"));
    }

    [Fact]
    public void A_value_written_in_full_is_hidden_while_typing_and_offered_on_request()
    {
        // `driver` is written in full, and the two that end in it are still worth offering.
        Assert.Equal(["kernelDriver", "fileSystemDriver"], Typing("type:driver"));
        Assert.Equal(["driver", "kernelDriver", "fileSystemDriver"], Asking("type:driver"));

        // Nothing else fits, so nothing at all - otherwise accepting running would reopen the
        // list with running alone.
        Assert.Empty(Typing("status:running"));
        Assert.Equal(["running"], Asking("status:running"));

        // Spelled differently and still written in full: the language accepts it, so it is.
        Assert.Empty(Typing("status:start-pending"));
    }

    [Fact]
    public void The_value_under_the_caret_is_the_one_between_the_commas_around_it()
    {
        Assert.Equal(["stopped", "startPending", "stopPending"], Typing("status:running,st"));
        Assert.Equal(["stopped", "startPending", "stopPending"], Asking("status:running,st"));

        var offered = QueryCompletions.WhileTyping("status:running,st", 17);

        Assert.Equal(new QueryEdit("status:running,stopped ", 23), offered[0].Apply("status:running,st"));

        // An empty value between two commas takes every value, and replaces nothing.
        Assert.Equal(StatusValues, QueryCompletions.WhileTyping("status:,,", 9).Select(completion => completion.Word));
        Assert.Equal(9..9, QueryCompletions.WhileTyping("status:,,", 9)[0].Replaces);
    }

    [Fact]
    public void A_text_field_offers_only_the_three_reserved_words()
    {
        Assert.Equal(["any", "none", "?"], Typing("name:"));
        Assert.Equal(["any", "none", "?"], Typing("pid:"));
        Assert.Empty(Typing("account:lo"));

        // An alias of a field name is accepted by the parser and resolved here the same way.
        Assert.Equal(["any", "none", "?"], Typing("dependents:"));
    }

    [Fact]
    public void An_unknown_field_offers_nothing_because_the_error_line_is_already_saying_so()
    {
        Assert.Empty(Typing("foo:"));
        Assert.Empty(Asking("foo:"));
    }

    [Fact]
    public void The_caret_in_whitespace_is_an_empty_member()
    {
        Assert.Empty(Typing("status:running "));
        Assert.Equal(QueryFields.Names, Asking("status:running "));

        var offered = QueryCompletions.OnRequest("status:running ", 15);

        Assert.Equal(15..15, offered[0].Replaces);
        Assert.Equal(new QueryEdit("status:running name:", 20), offered[0].Apply("status:running "));

        // A tab is whitespace to the scanner and to this.
        Assert.Equal(QueryFields.Names, Asking("status:running\t"));
    }

    [Fact]
    public void Nothing_is_offered_inside_quotes_or_after_a_quote_that_never_closes()
    {
        Assert.Empty(Typing("display:\"Print"));
        Assert.Empty(Asking("display:\"Print"));
        Assert.Empty(Typing("\"status:running\""));
        Assert.Empty(Asking("\"status:running\""));

        // An empty pair of quotes is bare by the scanner's definition and is still not a member
        // anybody can complete: what was written is not what was read.
        Assert.Empty(Asking("\"\""));

        // THE CASES THAT PROVE THE CHECK IS THERE - found by the mutation registry, not by
        // thought. With a quote or an escape AFTER the caret the prefix is clean, so without the
        // check `sta` would offer two fields and write them over a range holding the quotes.
        Assert.Empty(QueryCompletions.WhileTyping("sta\"tus\"", 3));
        Assert.Empty(QueryCompletions.OnRequest("status:r\"unning\"", 8));
        Assert.Empty(QueryCompletions.WhileTyping("sta\\ tus", 3));
    }

    [Fact]
    public void A_regular_expression_and_a_wildcard_match_no_value_and_get_nothing()
    {
        Assert.Empty(Typing("name:/^win/"));
        Assert.Empty(Typing("name:win*"));
    }

    [Fact]
    public void The_caret_in_the_middle_of_a_word_completes_the_whole_word()
    {
        var offered = QueryCompletions.WhileTyping("status:running", 3);

        Assert.Equal(["status", "start"], offered.Select(completion => completion.Word));

        // Written without a colon, because the member already has one.
        Assert.Equal("start", offered[1].Written);
        Assert.Equal(new QueryEdit("start:running", 5), offered[1].Apply("status:running"));

        // And the same for a value: the whole value goes, not the part before the caret.
        var value = QueryCompletions.WhileTyping("status:running", 9);

        Assert.Equal(new QueryEdit("status:running ", 15), value[0].Apply("status:running"));
    }

    [Fact]
    public void A_value_that_ends_the_line_gets_a_space_after_it_and_one_that_does_not_gets_nothing()
    {
        Assert.Equal("running ", QueryCompletions.WhileTyping("status:run", 10)[0].Written);
        Assert.Equal("running", QueryCompletions.WhileTyping("status:run stopped", 10)[0].Written);
        Assert.Equal("running", QueryCompletions.WhileTyping("status:run,stopped", 10)[0].Written);
    }

    [Fact]
    public void The_rest_of_the_line_comes_back_untouched()
    {
        const string typed = "spool  sta\tstatus:stopped";
        var offered = QueryCompletions.WhileTyping(typed, 10);

        Assert.Equal(new QueryEdit("spool  start:\tstatus:stopped", 13), offered[1].Apply(typed));
    }

    [Fact]
    public void A_lone_exclamation_mark_is_an_empty_member_the_mark_stays_in_front_of()
    {
        Assert.Empty(Typing("!"));

        var offered = QueryCompletions.OnRequest("!", 1);

        Assert.Equal(QueryFields.Names, offered.Select(completion => completion.Word));
        Assert.Equal(new QueryEdit("!name:", 6), offered[0].Apply("!"));
    }

    [Fact]
    public void An_alias_is_accepted_but_never_taught()
    {
        // dependents: is an alias of requiredby, and no NAME begins with depende. That is the
        // price of offering one spelling per field, and it is paid on purpose.
        Assert.Empty(Typing("depende"));
    }

    [Fact]
    public void A_caret_outside_the_text_is_moved_to_its_nearest_end_and_a_null_text_is_empty()
    {
        Assert.Equal(Typing("status:r"), QueryCompletions.WhileTyping("status:r", 99).Select(offered => offered.Word));

        // A caret before the first character stands in the member, with nothing typed before it.
        Assert.Equal(QueryFields.Names, QueryCompletions.OnRequest("status:r", -4).Select(offered => offered.Word));
        Assert.Equal(0..6, QueryCompletions.OnRequest("status:r", -4)[0].Replaces);

        Assert.Empty(QueryCompletions.WhileTyping(null, 3));
        Assert.Equal(QueryFields.Names, QueryCompletions.OnRequest(null, 3).Select(offered => offered.Word));
    }

    [Fact]
    public void The_reserved_words_are_one_list_in_the_order_they_are_offered()
    {
        Assert.Equal([QueryFields.Any, QueryFields.None, QueryFields.Unreadable], QueryFields.ReservedWords);
    }

    /// <summary>
    /// Text made of the characters that mean something in this language, plus enough letters
    /// for a field name or a value to appear by chance - the same generator the parser's own
    /// totality property uses, for the same reason.
    /// </summary>
    private static readonly Gen<(string Text, int Caret)> AwkwardWithCaret =
        Gen.String[
            Gen.OneOfConst(
                '"', '\\', ',', ':', '!', '*', '?', '/', '=', ' ', '\t', '-', '_',
                'a', 'd', 'e', 'g', 'i', 'n', 'o', 'p', 'r', 's', 't', 'u', 'y', 'P', 'S'),
            0,
            40]
        .SelectMany(text => Gen.Int[-3, text.Length + 3].Select(caret => (text, caret)));

    /// <summary>
    /// Any text and any caret come back as a list, never as an exception - and every row of that
    /// list points into the text it was asked about.
    ///
    /// This runs on every keystroke and every caret movement in a search box, and a stack trace
    /// out of moving the caret is the one failure a person cannot work around.
    /// </summary>
#pragma warning disable CA1031
    // Catching everything is the assertion: the property is that nothing of any kind escapes.
    [Fact]
    public void Asking_at_any_caret_in_any_text_never_throws_and_every_row_points_into_the_text()
    {
        AwkwardWithCaret.Sample(
            pair =>
            {
                var (text, caret) = pair;

                try
                {
                    foreach (var offered in QueryCompletions.WhileTyping(text, caret).Concat(QueryCompletions.OnRequest(text, caret)))
                    {
                        var (start, length) = offered.Replaces.GetOffsetAndLength(text.Length);

                        if (start < 0 || start + length > text.Length || offered.Written.Length == 0)
                        {
                            return false;
                        }

                        var edited = offered.Apply(text);

                        if (edited.Caret != start + offered.Written.Length
                            || !edited.Text.AsSpan(start).StartsWith(offered.Written, StringComparison.Ordinal))
                        {
                            return false;
                        }
                    }
                }
                catch (Exception failure)
                {
                    throw new InvalidOperationException(
                        $"Completing '{text}' at {caret} threw {failure.GetType().Name}: {failure.Message}", failure);
                }

                return true;
            },
            iter: 20_000,
            print: pair => $"a row pointed outside the text: <{pair.Text}> at {pair.Caret}");
    }
#pragma warning restore CA1031

    /// <summary>
    /// No completion offers itself again the moment it has been accepted.
    ///
    /// The rule "nothing when the only candidate is written in full" exists for this, and a
    /// property is the only way to hold it over every field, every value and every caret rather
    /// than over the two examples the table happens to name.
    /// </summary>
    [Fact]
    public void Nothing_offers_itself_again_once_it_has_been_written()
    {
        AwkwardWithCaret.Sample(
            pair =>
            {
                var (text, caret) = pair;

                foreach (var offered in QueryCompletions.OnRequest(text, caret))
                {
                    var edited = offered.Apply(text);

                    if (QueryCompletions.WhileTyping(edited.Text, edited.Caret)
                        .Any(again => again.Kind == offered.Kind && again.Word == offered.Word))
                    {
                        return false;
                    }
                }

                return true;
            },
            iter: 20_000,
            print: pair => $"accepting a row reopened the list with the same row: <{pair.Text}> at {pair.Caret}");
    }
}
