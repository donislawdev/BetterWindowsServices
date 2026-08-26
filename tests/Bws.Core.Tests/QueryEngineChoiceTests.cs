using System.Text.RegularExpressions;
using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// Which engine a pattern is given to, asked directly.
///
/// <b>This file exists because the claim used to be made by a stopwatch.</b> Until 2026-08-26
/// every pattern in the language carried a fifty millisecond match timeout, including the ones
/// handed to the linear engine where the comment above the constant said it could not be
/// reached. The effect was that a wrong engine choice showed up as a timeout - so the guard
/// went red when the machine was busy, and the two tests that noticed both blamed the query.
/// Backlog 137 and 174.
///
/// The engine choice is the thing actually being defended, and it is a fact about the code
/// rather than about the machine. Asking for it costs one line and cannot be made flaky by
/// anything else running at the same time.
///
/// <b>What this does NOT say, rather than leaving it to be found:</b> nothing here claims the
/// linear engine is fast, or that the fallback is slow. Those are measurements and they live
/// where measurements live. This says only who was handed the work.
/// </summary>
public sealed class QueryEngineChoiceTests
{
    [Theory]
    // The classic explosion. On the backtracking engine this is roughly two to the fortieth
    // steps against a run of forty a characters - measured on 2026-08-26 as five timeouts out
    // of five at 59-66 ms, against 0.0-1.0 ms on the linear engine.
    [InlineData("(a+)+b")]
    // An ordinary expression somebody would type, and the one backlog 174 went red on.
    [InlineData(@"print\s+spooler")]
    [InlineData("^Spool")]
    [InlineData("Spool|Dnscache")]
    public void A_pattern_the_linear_engine_can_take_is_given_to_it(string pattern)
    {
        Assert.True(QueryPatterns.TryPattern(pattern, out var compiled, out var failure), failure);
        Assert.NotNull(compiled);

        Assert.True(
            compiled.Options.HasFlag(RegexOptions.NonBacktracking),
            $"'{pattern}' went to the backtracking engine. Patterns here come from whoever is "
            + "at the keyboard and run over every entry on every keystroke, so one that can be "
            + "made to run away freezes the window rather than answering it.");

        Assert.Equal(Regex.InfiniteMatchTimeout, compiled.MatchTimeout);
    }

    /// <summary>
    /// The other half, and without it the theory above passes on an implementation that sends
    /// everything to the linear engine and throws on the constructs it cannot express.
    ///
    /// A backreference is a thing somebody meant, so it is served rather than refused - by the
    /// ordinary engine, where the time limit is the only net there is.
    /// </summary>
    [Theory]
    [InlineData(@"(a+)+\1b")]
    [InlineData("Spool(?=er)")]
    public void A_pattern_the_linear_engine_refuses_falls_back_and_keeps_its_time_limit(string pattern)
    {
        Assert.True(QueryPatterns.TryPattern(pattern, out var compiled, out var failure), failure);
        Assert.NotNull(compiled);

        Assert.False(
            compiled.Options.HasFlag(RegexOptions.NonBacktracking),
            $"'{pattern}' claims to be on the linear engine, which cannot express it.");

        Assert.NotEqual(Regex.InfiniteMatchTimeout, compiled.MatchTimeout);
    }

    /// <summary>
    /// A wildcard is the language's own idea rather than something a person wrote in regular
    /// expression syntax, and it is compiled by its own call - so it needs its own claim.
    ///
    /// <b>This is the path where a wrong choice would be felt first.</b> A wildcard is typed
    /// into a search box and evaluated over every entry on every keystroke.
    /// </summary>
    [Theory]
    [InlineData("Spool*")]
    [InlineData("*ooler")]
    [InlineData("Spool?r")]
    public void A_wildcard_is_given_to_the_linear_engine_and_carries_no_time_limit(string wildcard)
    {
        // Nothing escaped, so every star and question mark still carries its special meaning -
        // which is the shape the scanner produces for a value nobody put a backslash in.
        var scanned = new ScannedText(wildcard, new bool[wildcard.Length]);

        Assert.True(QueryPatterns.TryWildcard(scanned, out var compiled, out var failure), failure);
        Assert.NotNull(compiled);

        Assert.True(compiled.Options.HasFlag(RegexOptions.NonBacktracking), wildcard);
        Assert.Equal(Regex.InfiniteMatchTimeout, compiled.MatchTimeout);
    }
}
