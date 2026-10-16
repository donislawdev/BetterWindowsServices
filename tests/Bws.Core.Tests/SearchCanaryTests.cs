using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// The weekly search's own canary: a property that fails when, and only when, the runner asks.
///
/// tests/search/search.ps1 runs every listed property for minutes and turns what it saw into a
/// verdict. A runner that read a red property as green, or lost the seed that reproduces it, would
/// report a quiet Sunday forever - so before it trusts its own reading it runs this test by name with
/// BWS_SEARCH_CANARY set, and expects red with a seed in what the test said. The lesson is from the
/// mutation runner's canary, and further back from rule 15: an instrument that judges somebody
/// else's run has to show that it can see a failure, on a failure it planted.
///
/// <b>On every other run it checks the door itself</b> - that tests/Searching.cs counted the calls it
/// was told to make. A door that stopped counting would make every weekly run NOT SEARCHED, which is
/// red but says nothing about why.
///
/// Not in the weekly list (SearchGuards.NotASearch says why), so a Sunday never spends five minutes
/// on a property that cannot fail.
/// </summary>
public sealed class SearchCanaryTests
{
    [Fact]
    public void A_property_that_fails_is_red_and_says_how_to_reproduce_it()
    {
        var asked = Environment.GetEnvironmentVariable("BWS_SEARCH_CANARY") == "1";

        var calls = Gen.Int[0, 1_000_000].Searched(
            number => !asked || number < 1_000,
            new(1_000) { Print = number => $"the canary drew {number}, and any number from 1000 up was planted to fail" });

        Assert.True(
            calls >= 1_000,
            $"The door counted {calls} calls for a property told to draw 1000. The weekly search reads " +
            "this count to tell a property that searched from one that did not.");
    }
}
