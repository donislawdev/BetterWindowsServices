namespace Bws.Core;

/// <summary>
/// How much memory the process behind an entry is using, and how many entries have to
/// share that answer.
///
/// The first thing this tool reads that is a measurement rather than a configuration.
/// Everything else here describes how the machine is set up and reads the same twice in a
/// row - these numbers are different a second later, which is why they are never written
/// into a snapshot. <c>D1</c> lists what a snapshot holds and memory is deliberately not on
/// it.
///
/// Two numbers because they answer two questions and cost one call between them. The
/// working set is what the process is holding in physical memory now, which is what a
/// person means by "how much is this using". The commit is what it has asked the system to
/// back, which is what it costs whether or not it is resident. Neither is the number Task
/// Manager shows in its Memory column - that one is the private working set, and it cannot
/// be had without a right that seven processes of 110 refuse.
/// </summary>
/// <param name="WorkingSet">
/// Physical memory the process holds, shared pages included. Comparable with
/// <c>Get-Process WorkingSet64</c>.
///
/// Shared pages are the catch: a page of a shared library is counted in every process
/// holding it, so adding this column up gives a number larger than the memory in the
/// machine. That is a property of the measurement and not of our reading of it.
/// </param>
/// <param name="Commit">
/// Memory the process has asked the system to back, resident or not. Comparable with
/// <c>Get-Process PrivateMemorySize64</c> and with what Task Manager calls commit size.
///
/// The manager reports this same figure twice, as <c>PrivateUsage</c> and as
/// <c>PagefileUsage</c> - measured identical on all 110 processes on the machine this was
/// written on, so carrying both would be carrying one number twice.
/// </param>
/// <param name="SharedBy">
/// How many entries this one process is running. One means the entry has it to itself.
///
/// Here rather than alongside, because a number that belongs to five services and does not
/// say so is the failure this whole field would otherwise introduce: five rows would each
/// claim the same 36 MB and a person adding them up would get five times the truth. Kept
/// with the numbers so the two cannot be separated by any caller.
///
/// Measured on a real machine on 2026-08-01: 119 entries run in 110 processes, and 105 of
/// those processes host exactly one entry. The big svchost groups from the configuration -
/// netsvcs holds 48 services - do not appear at runtime, because Windows splits svchost
/// into a process per service when the machine has enough memory.
/// </param>
public sealed record ProcessMemory(long WorkingSet, long Commit, int SharedBy)
{
    /// <summary>True when this answer belongs to more than one entry.</summary>
    public bool IsShared => SharedBy > 1;
}

/// <summary>
/// Reads what a process is using. The seam where a test double stands in for the real
/// system, kept as narrow as the one over the service control manager (ADR-10).
///
/// Takes a process id rather than an entry, because that is the whole of what it needs and
/// because several entries can share one - the caller asks once per process, not once per
/// entry.
/// </summary>
public interface IProcessMemoryReader
{
    /// <summary>
    /// What the process is using, or why we could not find out.
    ///
    /// Refusal is a real answer here in a way it is not for the service manager. A process
    /// can be protected and refuse an administrator, and the process can also simply have
    /// ended between the listing and this call - a service that stopped a moment ago is
    /// gone, not forbidden, and both come back as a refusal carrying the system's own
    /// number so the two can be told apart by whoever reads it.
    /// </summary>
    Reading<ProcessMemory> Read(int processId);
}
