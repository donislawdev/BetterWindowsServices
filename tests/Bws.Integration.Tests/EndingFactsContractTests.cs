using System.Globalization;
using Bws.Core;

namespace Bws.Integration.Tests;

/// <summary>
/// The two questions a plan asks a process before ending it, put to real processes.
///
/// <see cref="WindowsEndingFactsReader"/> opens a handle carrying the right to end a process and
/// closes it, then reads when the process started. Both answers are checked here against things
/// this code did not produce: PowerShell's <c>Get-Process</c> for the start time, and Windows'
/// own documented refusals for the other two.
///
/// <b>Three specimens, and each was measured before it was written down (2026-09-16):</b>
/// <list type="bullet">
/// <item><description><b>This process</b> - a process can always open itself for ending, and
/// its start time is one PowerShell can also read.</description></item>
/// <item><description><b>The System process, PID 4</b> - refuses to be opened for ending with
/// access denied, even from an elevated session with <c>SeDebugPrivilege</c> enabled, and still
/// answers a limited query. It is the one process on every Windows since XP that carries that
/// number, so the refusal path has a specimen that does not depend on what else is running or
/// on who is asking.</description></item>
/// <item><description><b>PID 0</b> - the one number Windows documents as impossible to open:
/// <c>OpenProcess</c> fails for the System Idle Process with <c>ERROR_INVALID_PARAMETER</c>, which
/// is the code the reader turns into "no such process".</description></item>
/// </list>
///
/// <b>Why the absent specimen is not "a number no process has", measured rather than assumed:</b>
/// the kernel ignores the two low bits of a process number, so a number one past a real process
/// opens THAT process - measured on 2026-09-16, own PID plus one, two and three all opened for
/// both rights. And a process this test started and let finish is no good either: a process
/// whose handle is still open can be opened after it has ended, and its number goes back into the
/// pool the moment it cannot. PID 0 is documented, and it is the only such number.
///
/// Read-only. Opening a handle that carries the right to end a process does not end it, and
/// nothing here calls anything that could.
/// </summary>
public sealed class EndingFactsContractTests
{
    [Fact]
    public void This_process_can_be_ended_and_started_when_PowerShell_says_it_did()
    {
        var facts = new WindowsEndingFactsReader().Read(Environment.ProcessId);

        Assert.Equal(ReadOutcome.Present, facts.CanBeEnded.Outcome);
        Assert.True(facts.CanBeEnded.Value);

        Assert.Equal(ReadOutcome.Present, facts.Created.Outcome);

        // The same instant, read by somebody else. Get-Process converts the kernel's file time
        // to a local DateTime and ToFileTime converts it back inside the same process, which
        // keeps the daylight-saving flag and round-trips exactly - so this is an equality, not
        // a tolerance. A sign-extended low half or swapped halves would be off by decades.
        var theirs = long.Parse(
            CommandLineTool.PowerShell($"(Get-Process -Id {Environment.ProcessId}).StartTime.ToFileTime()"),
            CultureInfo.InvariantCulture);

        Assert.Equal(theirs, facts.Created.Value);
    }

    [Fact]
    public void The_System_process_refuses_to_be_ended_and_still_says_when_it_started()
    {
        const int system = 4;

        var facts = new WindowsEndingFactsReader().Read(system);

        // Access denied, carrying the system's number and its own words. The number is what a
        // script keys on and it is the same on every install - the words are in the language
        // of the machine, so only their presence is checked.
        Assert.Equal(ReadOutcome.Denied, facts.CanBeEnded.Outcome);
        Assert.Equal(5, facts.CanBeEnded.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(facts.CanBeEnded.Reason));

        // The limited query is a different right and is granted where ending is not - which is
        // the whole reason the reader opens two handles rather than one. Against Get-Process,
        // which reads the same process's start time through the same limited right.
        Assert.Equal(ReadOutcome.Present, facts.Created.Outcome);

        var theirs = long.Parse(
            CommandLineTool.PowerShell($"(Get-Process -Id {system}).StartTime.ToFileTime()"),
            CultureInfo.InvariantCulture);

        Assert.Equal(theirs, facts.Created.Value);
    }

    [Fact]
    public void The_idle_process_number_reads_as_no_such_process_and_not_as_a_refusal()
    {
        var facts = new WindowsEndingFactsReader().Read(0);

        // Both questions come back absent, and neither carries a code: "there is nothing to
        // end" is a fact about the machine, not a sentence about permissions. The layer above
        // answers the two differently, and reporting this one as a refusal would tell a person
        // that Windows will not let the tool end something that is not there.
        Assert.Equal(ReadOutcome.Absent, facts.CanBeEnded.Outcome);
        Assert.Equal(0, facts.CanBeEnded.ErrorCode);

        Assert.Equal(ReadOutcome.Absent, facts.Created.Outcome);
        Assert.Equal(0, facts.Created.ErrorCode);
    }
}
