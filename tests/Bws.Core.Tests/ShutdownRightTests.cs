using System.Diagnostics;

namespace Bws.Core.Tests;

/// <summary>
/// The privilege to shut the system down, switched on the token of THIS process and put back - the one piece of
/// the product that touches its own token (<see cref="ShutdownRight"/>, backlog 555 part a2). Nothing is shut
/// down: the privilege only lets a later call do it, and the only call that would is never made here.
///
/// <b>Read back through <c>whoami /priv</c>, a child of this process</b>, because a child starts with a copy of
/// its parent's token as it stands, privileges switched on included - so what the child lists is what this
/// process holds at that moment, read by a tool that is not the one under test. The names of the privileges are
/// the same in every language and the word for their state is not, so the state is compared with the state of
/// <c>SeChangeNotifyPrivilege</c>, which every token holds switched on.
/// </summary>
public sealed class ShutdownRightTests
{
    [Fact]
    public void The_privilege_is_on_while_held_and_as_it_was_once_let_go()
    {
        var on = State("SeChangeNotifyPrivilege");
        var before = State("SeShutdownPrivilege");

        Assert.NotNull(on);

        using (var right = ShutdownRight.TurnOn(out var refusal))
        {
            if (right is null)
            {
                // A token without it is refused with Windows' number for a privilege not held - never a 5.
                Assert.Null(before);
                Assert.Equal(1314, refusal!.ErrorCode);
                return;
            }

            Assert.Null(refusal);
            Assert.Equal(on, State("SeShutdownPrivilege"));

            // A second hold changes nothing, so letting it go puts nothing back - the first still holds it on.
            using (var again = ShutdownRight.TurnOn(out var none))
            {
                Assert.NotNull(again);
                Assert.Null(none);
            }

            Assert.Equal(on, State("SeShutdownPrivilege"));
        }

        Assert.Equal(before, State("SeShutdownPrivilege"));
    }

    /// <summary>The state word <c>whoami</c> gives one privilege of this process, or nothing when the token does not hold it.</summary>
    private static string? State(string privilege)
    {
        using var whoami = Process.Start(new ProcessStartInfo("whoami.exe", "/priv /fo csv /nh")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;

        var lines = whoami.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        whoami.WaitForExit();
        Assert.Equal(0, whoami.ExitCode);

        var row = lines
            .Select(line => line.Trim().Trim('"').Split("\",\""))
            .SingleOrDefault(fields => fields.Length == 3 && fields[0] == privilege);

        return row?[2];
    }
}
