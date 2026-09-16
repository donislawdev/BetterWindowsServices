using Bws.Core.Planning;

namespace Bws.Core.Tests;

/// <summary>
/// The one rule for reading a number of seconds, which both interfaces ask a person for.
///
/// <b>Written 2026-09-15, the day the rule stopped being two.</b> The terminal read
/// <c>--timeout</c> in <c>Arguments.Seconds</c> and the window read its "Wait up to" box in a
/// setter, each with its own sixty and its own idea of what a number is, agreeing by copying. The
/// owner's decision for the window's box was "the same rule as the terminal, no ceiling", and the
/// way two interfaces keep one rule is one function - this one.
///
/// <b>Every case here is a way the old window setter or the old binding was silent.</b> Zero and a
/// minus were refused without a word, a word failed inside the binding, a fraction and a
/// thousands mark read differently on two machines, and a number past what an int holds failed
/// the same way as a word.
/// </summary>
public sealed class StepCeilingTests
{
    /// <summary>Sixty, which `E1` of the specification uses in its own example.</summary>
    [Fact]
    public void The_default_is_a_minute() =>
        Assert.Equal(TimeSpan.FromSeconds(60), StepCeiling.Default);

    [Theory]
    [InlineData("1", 1)]
    [InlineData("60", 60)]
    [InlineData("90", 90)]
    [InlineData("3600", 3600)]
    [InlineData("2147483647", int.MaxValue)]
    public void A_whole_number_of_seconds_at_least_one_is_read(string typed, int seconds) =>
        Assert.Equal(seconds, StepCeiling.Seconds(typed));

    /// <summary>
    /// <b>No ceiling, and that is the owner's decision rather than an omission.</b> A service somebody
    /// knows takes ten minutes is given ten minutes, and the report says where the entry was left
    /// if it takes longer. The largest number an int holds is the only edge, and it is not a rule.
    /// </summary>
    [Fact]
    public void There_is_no_ceiling_on_the_ceiling() =>
        Assert.Equal(86_400, StepCeiling.Seconds("86400"));

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-60")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 60")]
    [InlineData("60 ")]
    [InlineData("abc")]
    [InlineData("30s")]
    [InlineData("1.5")]
    [InlineData("1,5")]
    [InlineData("1 000")]
    [InlineData("+60")]
    [InlineData("2147483648")]
    [InlineData("99999999999")]
    public void Anything_else_names_no_number_of_seconds(string typed) =>
        Assert.Null(StepCeiling.Seconds(typed));

    /// <summary>
    /// Null in, null out - a binding can hand a null over, and a rule that threw on it would be a
    /// rule with a second answer.
    /// </summary>
    [Fact]
    public void Nothing_at_all_names_no_number_either() =>
        Assert.Null(StepCeiling.Seconds(null));

    /// <summary>
    /// <b>Whitespace is refused BY THE RULE, deliberately.</b> The rule has no leniency in it, so
    /// that a caller which forgives a typed space - the window's box does - has to say so where it
    /// does, and the terminal, which forgives nothing, reads the same function unchanged.
    /// </summary>
    [Fact]
    public void A_space_around_the_number_is_the_callers_to_forgive_and_not_the_rules() =>
        Assert.Null(StepCeiling.Seconds(" 60 "));
}
