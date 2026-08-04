namespace Bws.Cli;

/// <summary>
/// Exit codes are a public contract: monitoring and scripts depend on them, so a new
/// way of ending means a new constant here, never reusing a near-enough one.
///
/// <b>Its own file since 2026-08-04, and the size ratchet is what asked.</b> It sat at the
/// bottom of Program.cs, which reached the ceiling that may only ever go down, and the ratchet
/// exists so that adding to the longest file starts with looking for a seam. This is a good one
/// twice over: the table is the most frozen thing in the command line tool and had the least
/// findable home, five hundred lines below the program it belongs to - and it shares nothing
/// with what that file does, which is read arguments, choose a verb and produce a document.
///
/// <c>tools/audit/audit.ps1</c> reads these constants and holds them against the table in
/// docs/02, so it was pointed here in the same change. A bridge between a document and code
/// that quietly stops finding the code would go green while checking nothing.
/// </summary>
internal static class ExitCode
{
    internal const int Ok = 0;
    internal const int Runtime = 1;
    internal const int Usage = 2;

    /// <summary>
    /// The plan was good, it ran, and something in it did not get where it was going.
    ///
    /// Its own code rather than the runtime one. A monitor that cannot tell "the manager
    /// refused to stop that service" from "bws could not start at all" has to treat both
    /// as the same night-time page, and only one of them is about the tool.
    /// </summary>
    internal const int Incomplete = 3;

    /// <summary>
    /// Somebody stopped the run by hand.
    ///
    /// Takes precedence over <see cref="Incomplete"/> when both apply, because it is the
    /// cause and the other is the effect - a person reading one number wants to know that
    /// the run was stopped, not that stopping it left work undone.
    ///
    /// Non-zero even when every step still arrived, which happens often now that the steps
    /// putting things back are carried out anyway. A wrapper script must not treat a run
    /// somebody stopped as a clean success. Ansible reserves a code for this too and the
    /// reasoning is the same.
    ///
    /// Deliberately not 130, the shell convention of 128 plus the signal number. That
    /// convention belongs to POSIX shells, means nothing on Windows, and mixing it into a
    /// table of small numbers would make the table harder to read rather than easier.
    /// </summary>
    internal const int Interrupted = 4;

    /// <summary>
    /// A comparison ran and found differences. Only ever returned when asked for.
    ///
    /// Not a failure, and that is why it needed a number of its own rather than borrowing
    /// <see cref="Incomplete"/>. Drift is what this tool is for finding - reporting it as
    /// the same thing as a refused operation would tell a monitor that something went wrong
    /// when what happened is that something was discovered.
    ///
    /// Behind --exit-code rather than automatic. The rest of this table answers "did the
    /// tool work", never "what did it find", and a diff that ended non-zero by default would
    /// be the one command breaking that rule - and would break every script that only wanted
    /// the differences printed. Owner's decision, 2026-08-01, recorded as open question 11
    /// in the product specification.
    /// </summary>
    internal const int Differences = 5;
}
