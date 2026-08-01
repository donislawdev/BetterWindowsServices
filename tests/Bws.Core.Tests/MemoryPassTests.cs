using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Filling in what each entry's process is using.
///
/// Most of what can go wrong here is arithmetic about which entries share a process, and
/// all of it is quiet: every number stays plausible and only its meaning changes. That is
/// what these tests are about rather than the reading itself, which the fake stands in for.
/// </summary>
public sealed class MemoryPassTests
{
    [Fact]
    public void A_process_running_five_entries_says_so_in_all_five()
    {
        var filled = MemoryPass.Fill(Specimens.All, new FakeProcessMemoryReader());

        // DcomLaunch and PlugPlay were read off a real machine sharing process 1900. Every
        // entry in a shared process has to carry the count, not just the first one found.
        foreach (var name in new[] { "DcomLaunch", "PlugPlay" })
        {
            var entry = filled.Single(candidate => candidate.ServiceName == name);

            Assert.True(entry.Memory.IsPresent);
            Assert.Equal(2, entry.Memory.Value!.SharedBy);
            Assert.True(entry.Memory.Value.IsShared);
        }
    }

    [Fact]
    public void An_entry_with_the_process_to_itself_is_not_reported_as_sharing()
    {
        var filled = MemoryPass.Fill(Specimens.All, new FakeProcessMemoryReader());
        var spooler = filled.Single(entry => entry.ServiceName == "Spooler");

        Assert.Equal(1, spooler.Memory.Value!.SharedBy);
        Assert.False(spooler.Memory.Value.IsShared);
    }

    [Fact]
    public void One_question_per_process_rather_than_one_per_entry()
    {
        var reader = new FakeProcessMemoryReader();
        MemoryPass.Fill(Specimens.All, reader);

        // The two sharing a process are asked about once between them, and nothing is asked
        // about twice. Getting this wrong leaves every answer right and only the cost wrong,
        // which is exactly the kind of mistake no assertion about values would find.
        Assert.Equal(reader.Asked.Distinct().Count(), reader.Asked.Count);
        Assert.Single(reader.Asked, id => id == 1900);
    }

    [Fact]
    public void An_entry_that_is_not_running_has_no_memory_rather_than_an_unknown_one()
    {
        var filled = MemoryPass.Fill(Specimens.All, new FakeProcessMemoryReader());
        var stopped = filled.Single(entry => entry.ServiceName == "AsusUpdateCheck");

        // Absent, not refused and not zero. There is genuinely no process, which is a fact
        // about the entry - and it is the ordinary case, 691 entries of 810 on a real
        // machine. Zero would be a measurement nobody took.
        Assert.Equal(ReadOutcome.Absent, stopped.Memory.Outcome);
    }

    [Fact]
    public void A_process_id_is_never_a_refusal_which_is_why_the_pass_has_no_branch_for_one()
    {
        // The assumption the pass rests on, pinned so that changing it breaks a test rather
        // than producing a quiet wrong answer. The id arrives in the enumeration buffer
        // alongside the name and the status, so an entry in the listing at all has one:
        // present while something runs, absent while nothing does. Never refused.
        //
        // Locked is the catalogue's entry with everything else refused, and even it has an
        // ordinary absent process id.
        Assert.All(Specimens.All, entry =>
            Assert.True(entry.ProcessId.Outcome is ReadOutcome.Present or ReadOutcome.Absent));

        var locked = MemoryPass.Fill(Specimens.All, new FakeProcessMemoryReader())
            .Single(entry => entry.ServiceName == "Locked");

        Assert.Equal(ReadOutcome.Absent, locked.Memory.Outcome);
    }

    [Fact]
    public void A_process_that_refuses_costs_that_entry_and_no_other()
    {
        var reader = new FakeProcessMemoryReader(new Dictionary<int, Reading<ProcessMemory>>
        {
            // Access denied, which on a real machine is a protected process - Defender and
            // lsass among them. Measured on 2026-08-01: none of them refuse the right this
            // tool asks for, but seven refuse the wider one somebody might reach for.
            [1900] = Reading<ProcessMemory>.Denied(Entries.AccessDenied, "access denied")
        });

        var filled = MemoryPass.Fill(Specimens.All, reader);

        Assert.Equal(ReadOutcome.Denied, filled.Single(e => e.ServiceName == "DcomLaunch").Memory.Outcome);
        Assert.True(filled.Single(e => e.ServiceName == "Spooler").Memory.IsPresent);
    }

    [Fact]
    public void Counting_over_a_filtered_list_would_be_the_one_way_to_get_this_wrong()
    {
        // Pinned as a test because it is the mistake the pass is arranged to prevent and
        // nothing in the type system stops it. Handed only the entries that survived a
        // filter, the count is of those entries - so a service in a shared process would be
        // reported as having it to itself, which is a wrong number that looks right.
        var oneOfTheTwo = Specimens.All.Where(entry => entry.ServiceName == "DcomLaunch").ToArray();
        var filtered = MemoryPass.Fill(oneOfTheTwo, new FakeProcessMemoryReader());

        Assert.Equal(1, filtered[0].Memory.Value!.SharedBy);

        // Over the whole listing, the same entry knows better. That difference is why the
        // command line runs this before filtering and says so.
        var whole = MemoryPass.Fill(Specimens.All, new FakeProcessMemoryReader());

        Assert.Equal(2, whole.Single(entry => entry.ServiceName == "DcomLaunch").Memory.Value!.SharedBy);
    }
}
