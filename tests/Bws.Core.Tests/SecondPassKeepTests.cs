using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// File answers kept from an earlier reading, and an inspector that remembers nothing between passes.
///
/// <b>Written 2026-09-29, W4 of the performance series.</b> Two changes that belong together because
/// both are about how OLD an answer may be. <see cref="SecondPass.Keep"/> carries answers forward
/// on purpose - the owner's S-1 decision in `ADR-13` - and <see cref="IBinaryInspector.ForOnePass"/>
/// makes sure nothing else does: the publisher memory of the Windows inspector used to outlive every
/// F5 the window was ever given (backlog 468).
/// </summary>
public sealed class SecondPassKeepTests
{
    private const string Spooler = @"C:\Windows\System32\spoolsv.exe";
    private const string Host = @"C:\Windows\System32\svchost.exe";

    [Fact]
    public void A_file_already_answered_is_kept_and_not_asked_about_again()
    {
        var earlier = SecondPass.Fill(Listing(), new FakeBinaryInspector());
        var inspector = new FakeBinaryInspector();

        var filled = SecondPass.Fill(SecondPass.Keep(Listing(), earlier), inspector);

        Assert.Empty(inspector.SignaturesAsked);
        Assert.Equal(earlier.Select(entry => entry.Signature.Value), filled.Select(entry => entry.Signature.Value));
        Assert.Equal(earlier.Select(entry => entry.BinaryHash.Value), filled.Select(entry => entry.BinaryHash.Value));
    }

    [Fact]
    public void A_new_path_and_a_changed_path_are_verified_and_nothing_else_is()
    {
        // Keyed by the file, so the new service on an already verified svchost has its answer, and
        // the spooler pointing somewhere else is a different key and is asked about.
        const string Moved = @"D:\Spool\spoolsv.exe";
        const string New = @"C:\Program Files\Vendor\agent.exe";

        var earlier = SecondPass.Fill(Listing(), new FakeBinaryInspector());

        var fresh = new[]
        {
            At("Spooler", Moved),
            At("Dhcp", Host),
            At("RpcSs", Host),
            At("VendorAgent", New)
        };

        var inspector = new FakeBinaryInspector();
        var filled = SecondPass.Fill(SecondPass.Keep(fresh, earlier), inspector);

        Assert.Equal([New, Moved], inspector.SignaturesAsked.Order(StringComparer.Ordinal));
        Assert.All(filled, entry => Assert.Equal(ReadOutcome.Present, entry.Signature.Outcome));
    }

    [Fact]
    public void A_file_nobody_read_is_asked_about_again()
    {
        // "Not read" is what a skipped file on another machine answers, and it is not an answer.
        var skipping = new FakeBinaryInspector(new Dictionary<string, Reading<BinarySignature>>(StringComparer.OrdinalIgnoreCase)
        {
            [Spooler] = Reading<BinarySignature>.NotRead()
        });

        var earlier = SecondPass.Fill(Listing(), skipping);
        var inspector = new FakeBinaryInspector();

        SecondPass.Fill(SecondPass.Keep(Listing(), earlier), inspector);

        Assert.Equal([Spooler], inspector.SignaturesAsked);
    }

    [Fact]
    public void Every_pass_asks_through_an_inspector_that_remembers_nothing_from_the_last()
    {
        // The window's own inspector refuses to be asked anything, so the pass only works if it
        // asks through the one ForOnePass hands it.
        var windowLong = new OnlyThroughAPass();

        var filled = SecondPass.Fill(Listing(), windowLong);

        Assert.Equal(1, windowLong.Passes);
        Assert.All(filled, entry => Assert.Equal(ReadOutcome.Present, entry.Signature.Outcome));
    }

    [Fact]
    public void The_windows_inspector_starts_every_pass_empty()
    {
        // The only thing a fresh instance can promise without a real catalogue behind it: it IS a
        // fresh instance, so the publisher memory of the one before it does not come along.
        var inspector = new WindowsBinaryInspector();

        Assert.NotSame(inspector, inspector.ForOnePass());
    }

    private static ScmEntry[] Listing() =>
    [
        At("Spooler", Spooler),
        At("Dhcp", Host),
        At("RpcSs", Host)
    ];

    private static ScmEntry At(string name, string file) =>
        Entries.Any with { ServiceName = name, DisplayName = name, BinaryFile = Reading<string>.Present(file) };

    /// <summary>An inspector that answers nothing itself and counts how many passes asked it for one.</summary>
    private sealed class OnlyThroughAPass : IBinaryInspector
    {
        internal int Passes { get; private set; }

        public Reading<BinarySignature> ReadSignature(string file) =>
            throw new InvalidOperationException("Asked the window's own inspector rather than one for this pass.");

        public Reading<string> ReadFileVersion(string file) =>
            throw new InvalidOperationException("Asked the window's own inspector rather than one for this pass.");

        public Reading<string> ReadHash(string file) =>
            throw new InvalidOperationException("Asked the window's own inspector rather than one for this pass.");

        public IBinaryInspector ForOnePass()
        {
            Passes++;

            return new FakeBinaryInspector();
        }
    }
}
