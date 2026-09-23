using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// The two places that hold how long the window waits after a keystroke agree with each other.
///
/// <b>The comment beside one of them has said they have to agree since the day it was written, and
/// nothing was checking.</b> A number in prose has nothing counting it - the same rule this project
/// states about comments, met here in a shape a compiler cannot see at all: one number is a
/// TimeSpan in C# and the other is an attribute in markup, and no build has an opinion about
/// whether they match.
///
/// <b>What a disagreement costs is not an error, which is why it is worth a guard.</b> It brings
/// back a problem that shipped once and was measured: the query was pushed into the model from a
/// timer, and pushing a value into a two way binding makes the binding write the source back to the
/// target - overwriting characters typed in the meantime. Three keystrokes out of 248 never reached
/// the box, and the row counts beside them made no sense. Binding.Delay is the framework's own
/// answer and has none of that - the timer that is left answers the one question a binding cannot,
/// which is whether somebody is typing right now. Those two only line up while the numbers do.
///
/// <b>Read out of the files rather than out of the code</b>, because the markup number is an
/// attribute value with no name a test could reference, and making the C# one public to buy a test
/// would be the tail wagging the dog. Both patterns are anchored on the names beside them, so a
/// four hundred somewhere else in either file is not what this reads.
/// </summary>
public sealed class TypingDelayGuards
{
    private static readonly Regex InCode = new(
        @"AfterTyping\s*=\s*TimeSpan\.FromMilliseconds\((\d+)\)",
        RegexOptions.Compiled,
        Sources.Ceiling);

    private static readonly Regex InMarkup = new(
        @"x:Name=""QueryBox""[\s\S]*?Delay=(\d+)",
        RegexOptions.Compiled,
        Sources.Ceiling);

    [Fact]
    public void The_wait_after_a_keystroke_is_the_same_number_in_the_code_and_in_the_markup()
    {
        var code = Only(InCode, Sources.Shipped(), "AfterTyping in the window's code behind");
        var markup = Only(InMarkup, Sources.ShippedMarkup(), "Delay on the query box in the markup");

        Assert.True(
            string.Equals(code, markup, StringComparison.Ordinal),
            $"The window waits {code} ms after a keystroke according to its code and {markup} ms "
            + "according to its markup. They have to be the same number: the binding defers the "
            + "source update and the timer answers whether somebody is still typing, and a tick "
            + "landing between the two is the problem both of them exist to keep out.");
    }

    /// <summary>
    /// The one match, or a failure naming what was not found.
    ///
    /// <b>A guard that reads nothing passes</b>, which is the way this whole family of tests goes
    /// quietly wrong - so finding no match, or more than one, is a red test rather than a value
    /// nobody looked at.
    /// </summary>
    private static string Only(Regex pattern, IEnumerable<string> files, string what)
    {
        var found = files
            .SelectMany(file => pattern.Matches(File.ReadAllText(file)).Cast<Match>())
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.True(
            found.Length == 1,
            $"Expected exactly one place to name {what}, and found {found.Length}. Either it moved "
            + "and this guard is now reading nothing, or there are two of them - and two numbers "
            + "for one behaviour is the thing this guard is about.");

        return found[0];
    }
}
