using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Filling in what a listing cannot afford to know up front.
///
/// ADR-13 in code. Two families of expensive data came before this one and both turned out
/// cheap enough not to need it - triggers at 45-75 ms and launch paths at 45-60 ms, against
/// a listing budget of a second. This one measured at roughly five seconds over 810 entries
/// and 544 distinct files on the machine it was written against, which is the first time
/// the deferral was paid for by a measurement rather than expected.
/// </summary>
public sealed class SecondPassTests
{
    [Fact]
    public void One_question_per_distinct_file_rather_than_one_per_entry()
    {
        // The reason this matters is measured rather than assumed: 810 entries on a real
        // machine point at 544 distinct files, because every service sharing a host process
        // names the same svchost. Asking per entry would repeat a third of the work, and
        // this is the family where the work is expensive.
        var shared = @"C:\WINDOWS\system32\svchost.exe";

        var entries = new[]
        {
            Entry("DcomLaunch", shared),
            Entry("PlugPlay", shared),
            Entry("Spooler", @"C:\WINDOWS\System32\spoolsv.exe")
        };

        var inspector = new FakeBinaryInspector();

        SecondPass.Fill(entries, inspector);

        Assert.Equal(2, inspector.SignaturesAsked.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(2, inspector.SignaturesAsked.Count);
        Assert.Equal(2, inspector.VersionsAsked.Count);
    }

    [Fact]
    public void Two_entries_sharing_a_file_get_the_same_answer()
    {
        // The other half of caching, and the half that would break silently: saving the
        // question is only correct if the answer reaches everybody who would have asked it.
        var shared = @"C:\WINDOWS\system32\svchost.exe";
        var filled = SecondPass.Fill([Entry("DcomLaunch", shared), Entry("PlugPlay", shared)], new FakeBinaryInspector());

        Assert.Equal(
            filled[0].Signature.Value!.Publisher,
            filled[1].Signature.Value!.Publisher);

        Assert.All(filled, entry => Assert.True(entry.Signature.IsPresent));
    }

    [Fact]
    public void An_entry_with_nothing_to_run_is_answered_absent_rather_than_asked_about()
    {
        // No file means no signature to be on it. That is a fact about the entry, so it
        // never reaches the inspector at all - and it must not come back as "not read",
        // which would say the second pass had not got here yet.
        var inspector = new FakeBinaryInspector();

        var filled = SecondPass.Fill(
            [Entries.Any with { BinaryFile = Reading<string>.Absent() }],
            inspector);

        Assert.Equal(ReadOutcome.Absent, filled[0].Signature.Outcome);
        Assert.Equal(ReadOutcome.Absent, filled[0].FileVersion.Outcome);
        Assert.Empty(inspector.SignaturesAsked);
    }

    [Fact]
    public void A_refusal_further_up_is_passed_on_rather_than_turned_into_an_answer()
    {
        // The configuration was refused, so nobody ever learned which file to look at.
        // Reporting that as "no signature" would turn a fact about our permissions into an
        // accusation about the machine, which is rule 8 in one line.
        var inspector = new FakeBinaryInspector();

        var filled = SecondPass.Fill(
            [Entries.Any with { BinaryFile = Reading<string>.Denied(Entries.AccessDenied, "access denied") }],
            inspector);

        Assert.Equal(ReadOutcome.Denied, filled[0].Signature.Outcome);
        Assert.Equal(Entries.AccessDenied, filled[0].Signature.ErrorCode);
        Assert.Equal("access denied", filled[0].Signature.Reason);
        Assert.Empty(inspector.SignaturesAsked);
    }

    [Fact]
    public void Entries_come_back_as_new_records_and_the_originals_are_untouched()
    {
        // A snapshot and a plan are evidence, and evidence does not change after it has
        // been shown. An entry that could quietly gain fields after a caller was handed it
        // is the same problem one level down.
        var original = Entry("Spooler", @"C:\WINDOWS\System32\spoolsv.exe");
        var filled = SecondPass.Fill([original], new FakeBinaryInspector());

        Assert.Equal(ReadOutcome.NotRead, original.Signature.Outcome);
        Assert.Equal(ReadOutcome.Present, filled[0].Signature.Outcome);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(64)]
    public void One_question_per_distinct_file_however_many_are_asked_at_once(int degree)
    {
        // The property that makes this affordable, held at every degree rather than only at
        // one. The shape it rules out is the obvious parallel version: go wide over the
        // entries and let a concurrent dictionary sort out the repeats. That dictionary is
        // documented as free to run its factory more than once for a key, so the repeats it
        // was supposed to save would come back as extra verifications at about eight
        // milliseconds each.
        //
        // This checks the structure, not the timing. Whether real concurrency can corrupt a
        // real verdict was answered elsewhere and by measurement - 29 376 comparisons of a
        // live machine's files against the single-threaded answer, none of them different.
        var files = Enumerable.Range(0, 40).Select(number => $@"C:\WINDOWS\system32\host{number}.exe").ToArray();

        var entries = Enumerable.Range(0, 200)
            .Select(number => Entry($"Service{number}", files[number % files.Length]))
            .ToArray();

        var inspector = new FakeBinaryInspector();

        SecondPass.Fill(entries, inspector, degree);

        Assert.Equal(files.Length, inspector.SignaturesAsked.Count);
        Assert.Equal(files.Length, inspector.VersionsAsked.Count);
        Assert.Equal(files.Length, inspector.HashesAsked.Count);
        Assert.Equal(files.Length, inspector.SignaturesAsked.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void The_order_of_the_result_is_the_order_of_the_input_whoever_answered_first()
    {
        // Answers arrive in whatever order the machine finishes them, and the list must not.
        // A snapshot is sorted before it is written, so this would not show there - it would
        // show in a listing, as rows that move between runs of a command nobody changed.
        var entries = Enumerable.Range(0, 120)
            .Select(number => Entry($"Service{number}", $@"C:\WINDOWS\system32\host{number}.exe"))
            .ToArray();

        var filled = SecondPass.Fill(entries, new FakeBinaryInspector(), degreeOfParallelism: 32);

        Assert.Equal(
            entries.Select(entry => entry.ServiceName),
            filled.Select(entry => entry.ServiceName));
    }

    [Fact]
    public void Every_degree_gives_the_same_answers_as_asking_one_at_a_time()
    {
        // The guard the slice exists for, in miniature. The real one runs against the
        // machine's own files, because a fake cannot be wrong about a signature - but this
        // one runs in milliseconds and catches an answer attached to the wrong file, which
        // is the way this shape breaks when somebody rearranges the phases.
        var entries = Enumerable.Range(0, 120)
            .Select(number => Entry($"Service{number}", $@"C:\WINDOWS\system32\host{number % 37}.exe"))
            .ToArray();

        var alone = SecondPass.Fill(entries, new FakeBinaryInspector(), degreeOfParallelism: 1);

        foreach (var degree in new[] { 2, 8, 32 })
        {
            var together = SecondPass.Fill(entries, new FakeBinaryInspector(), degree);

            Assert.Equal(
                alone.Select(entry => (entry.ServiceName, entry.Signature.Value?.Publisher, entry.FileVersion.Value, entry.BinaryHash.Value)),
                together.Select(entry => (entry.ServiceName, entry.Signature.Value?.Publisher, entry.FileVersion.Value, entry.BinaryHash.Value)));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Asking_fewer_than_one_at_a_time_is_refused(int degree)
    {
        // Minus one is the platform's own word for "no limit", so a degree that came out
        // negative from somebody's arithmetic would not be an error down there - it would
        // be the widest run the machine can manage, silently. Refusing both here means the
        // complaint names the argument instead of surfacing from inside a parallel loop.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SecondPass.Fill([Entries.Any], new FakeBinaryInspector(), degree));
    }

    private static ScmEntry Entry(string name, string file) =>
        Entries.Any with
        {
            ServiceName = name,
            BinaryFile = Reading<string>.Present(file),
            BinaryOnDisk = Reading<bool>.Present(true)
        };
}
