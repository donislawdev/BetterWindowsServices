using System.Security.Cryptography;
using Bws.Core;

namespace Bws.Integration.Tests;

/// <summary>
/// Asking several files at once must not change what the machine says about any of them.
///
/// This is the guard slice S5a1 was built around. Verifying signatures went from 4376 ms to
/// 1031 ms over 544 distinct files by asking about sixteen at a time, and the entire risk in
/// that trade is a verdict that comes out differently under load. It would not look like a
/// crash. It would look like a snapshot showing that a binary changed publisher overnight,
/// on a machine where nothing happened - which for an audit tool is the worst failure there
/// is, because it is believed.
///
/// A fake cannot check this. The question is about WinVerifyTrust and the catalogue
/// providers, so the files have to be the machine's own.
///
/// It costs roughly five seconds, and that is the point rather than an accident: the
/// single-threaded run it compares against is the expensive one. A cheaper version sampling
/// forty files would pass on a machine where the other five hundred disagreed.
///
/// Read-only. Asking who signed a file changes nothing.
/// </summary>
public sealed class SecondPassConcurrencyTests
{
    [Fact]
    public void Not_one_verdict_changes_when_several_files_are_asked_about_at_once()
    {
        var entries = new WindowsScmCatalog().ReadAll();

        var alone = SecondPass.Fill(entries, new WindowsBinaryInspector(), degreeOfParallelism: 1);
        var together = SecondPass.Fill(entries, new WindowsBinaryInspector(), SecondPass.DefaultDegreeOfParallelism);

        Assert.Equal(alone.Count, together.Count);

        var compared = 0;

        for (var index = 0; index < alone.Count; index++)
        {
            var one = alone[index];
            var other = together[index];

            // Same entry in the same place - meaning the two runs agree on position, which
            // is not the same as either of them being in the order it was asked. Both runs
            // reordered the same way would still pass this, and that is not a hole to fix
            // here: it was found by reordering the result on purpose, which left this whole
            // class green and reddened the unit test written for it. Comparing runs finds
            // what wanders between runs. Input order is held elsewhere.
            Assert.Equal(one.ServiceName, other.ServiceName);

            Assert.Equal(Describe(one.Signature), Describe(other.Signature));
            Assert.Equal(Describe(one.FileVersion), Describe(other.FileVersion));
            Assert.Equal(Describe(one.BinaryHash), Describe(other.BinaryHash));

            if (one.Signature.IsPresent)
            {
                compared++;
            }
        }

        // The comparison above passes trivially if nothing was verified at all - two runs
        // that both answered "not read" everywhere agree perfectly. On a real machine most
        // entries name a file that exists, so a small number here is the shape of a pass
        // that gave up rather than one that agreed.
        Assert.True(
            compared > alone.Count / 2,
            $"Only {compared} of {alone.Count} entries came back with a signature at all, " +
            "so the two runs agreeing says nothing about verifying anything.");
    }

    [Fact]
    public void Every_answer_belongs_to_the_file_it_is_attached_to()
    {
        // Written because the test above cannot see this, and that was established by
        // breaking it rather than by thinking about it. Attaching every answer to the next
        // file along left all 289 unit tests and both halves of the comparison above green:
        // two runs of the same wrong code agree perfectly. Comparing runs finds answers that
        // wander between runs, and nothing else.
        //
        // So this asks a question with an answer of its own. The hash of a file is something
        // this test can work out for itself, and an answer hanging on the wrong file gets
        // caught by the only oracle that does not come from the code under test.
        //
        // Every file rather than a sample. Hashing the machine's 544 distinct binaries costs
        // about half a second, and the failure being guarded against is one file out of
        // place - which a sample is exactly the wrong instrument for.
        var entries = SecondPass.Fill(
            new WindowsScmCatalog().ReadAll(),
            new WindowsBinaryInspector(),
            SecondPass.DefaultDegreeOfParallelism);

        var examined = 0;

        foreach (var entry in entries.Where(entry => entry.BinaryHash.IsPresent))
        {
            using var stream = File.OpenRead(entry.BinaryFile.Value!);
            var independently = Convert.ToHexStringLower(SHA256.HashData(stream));

            Assert.Equal(independently, entry.BinaryHash.Value);
            examined++;
        }

        Assert.True(
            examined > entries.Count / 2,
            $"Only {examined} of {entries.Count} entries had a hash to check, so agreeing about them proves little.");
    }

    /// <summary>
    /// Everything a reading carries, flattened, so a difference in any part of it fails.
    ///
    /// The verdict, the number behind it and the publisher are compared together on purpose.
    /// A run that got the status right and the name wrong would be the same false difference
    /// in a snapshot as one that got both wrong.
    /// </summary>
    private static string Describe<T>(Reading<T> reading) where T : class =>
        $"{reading.Outcome}/{reading.ErrorCode}/{Value(reading.Value)}";

    private static string Value<T>(T? value) where T : class => value switch
    {
        null => "-",
        BinarySignature signature => $"{signature.Status}/{signature.ResultCode}/{signature.Publisher}",
        _ => value.ToString() ?? "-"
    };
}
