using Bws.Core;

namespace Bws.Integration.Tests;

/// <summary>
/// The recovery list the plan that ends a process reads, put to the real manager and held against sc.exe.
///
/// <b>RpcSs is the specimen</b>, because every Windows since Vista ships it with "restart the computer" as its
/// recovery - measured on the owner's machine on 2026-09-30, and it is the item the plan refuses on. The
/// comparison is on what sc.exe can print: it prints no line for an item that does nothing, nor for the
/// undocumented type the same measurement found on Schedule, so those two are left out of both sides.
///
/// <b>Critical processes are not a specimen here, and that is measured rather than skipped</b>: under a
/// restricted token wininit.exe refuses the question with 5, so a test built on it would fail wherever the
/// suite runs without elevation. The canary in tools/scm-probe/recovery-probe.ps1 carries that half.
///
/// Read-only. Nothing here opens anything with a right that could change it.
/// </summary>
public sealed class RecoveryContractTests
{
    [Fact]
    public void The_recovery_list_of_RpcSs_reads_as_sc_exe_prints_it()
    {
        var ours = new WindowsEndingFactsReader().ReadRecovery("RpcSs");

        Assert.Equal(ReadOutcome.Present, ours.Outcome);

        // By tokens rather than by labels: an action is "WORDS -- Delay = N", and the first of them shares
        // its line with the FAILURE_ACTIONS label and a colon. The delay is compared as well since
        // 2026-09-30 (backlog 501), when the plan started saying it - the number is the first run of digits
        // after the equals sign, in milliseconds, whatever language the unit word is printed in.
        var theirs = CommandLineTool.ServiceControl("qfailure", "RpcSs", "5000").StandardOutput
            .Split('\n')
            .Where(line => line.Contains("-- Delay", StringComparison.Ordinal))
            .Select(line => Kind(line) + " " + Delay(line))
            .ToArray();

        string[] spoken =
        [
            .. ours.Value!.Items
                .Where(item => item.Action is not (RecoveryAction.Nothing or RecoveryAction.Unnamed))
                .Select(item => (item.Action switch
                {
                    RecoveryAction.RestartService => "RESTART",
                    RecoveryAction.RunProgram => "RUN PROCESS",
                    _ => "REBOOT"
                }) + " " + (long)item.Delay.TotalMilliseconds)
        ];

        Assert.NotEmpty(theirs);
        Assert.Equal(theirs, spoken);
    }

    /// <summary>
    /// The reset period, read since 2026-10-07 (backlog 541), against the number sc.exe prints for it. By
    /// tokens again: the first line ending in a colon and a number - or the word INFINITE, sc.exe's own for a
    /// count that never starts again - is the period, in seconds, whatever language the label is in.
    /// </summary>
    [Fact]
    public void The_reset_period_of_RpcSs_reads_as_sc_exe_prints_it()
    {
        var ours = new WindowsEndingFactsReader().ReadRecovery("RpcSs");

        var theirs = CommandLineTool.ServiceControl("qfailure", "RpcSs", "5000").StandardOutput
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Select(line => line[(line.LastIndexOf(':') + 1)..].Trim())
            .First(value => value == "INFINITE" || (value.Length > 0 && value.All(char.IsAsciiDigit)));

        Assert.Equal(ReadOutcome.Present, ours.Outcome);
        Assert.Equal(
            theirs,
            ours.Value!.ResetPeriod == Timeout.InfiniteTimeSpan
                ? "INFINITE"
                : ((long)ours.Value.ResetPeriod.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string Kind(string line)
    {
        var before = line[..line.IndexOf("--", StringComparison.Ordinal)];
        return before[(before.LastIndexOf(':') + 1)..].Trim();
    }

    private static string Delay(string line)
    {
        var after = line[(line.IndexOf('=', StringComparison.Ordinal) + 1)..].TrimStart();
        return new string([.. after.TakeWhile(char.IsAsciiDigit)]);
    }

    [Fact]
    public void A_service_that_is_not_there_has_no_recovery_rather_than_a_refused_one()
    {
        var ours = new WindowsEndingFactsReader().ReadRecovery("BwsNoSuchService-" + Guid.NewGuid().ToString("N"));

        Assert.Equal(ReadOutcome.Absent, ours.Outcome);
        Assert.Equal(0, ours.ErrorCode);
    }
}
