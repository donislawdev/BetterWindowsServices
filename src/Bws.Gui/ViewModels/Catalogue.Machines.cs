using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The four machines the sheet's views are drawn over - one per state a view with data can be in.
///
/// <b>Four states of GUI rule 3, four catalogs, and every one of them is the REAL model in that
/// state rather than a view told to look like it.</b> A view over a machine that answers is the
/// data state. One over a machine that has nothing is the empty state. One over a machine that
/// refuses is the error state, reached the way the product reaches it - <c>Readings.LoadEverything</c>
/// catches what <c>ReadAll</c> throws and says so under the list. And one over a machine that
/// never answers is the loading state, which is what the model shows between asking and hearing
/// back, for as long as that takes.
///
/// <b>The stalled machine blocks a thread, and the sheet owns that thread.</b> The model reads
/// through <c>Task.Run</c>, so a read that never returns is a pool thread that never comes back -
/// one per loading sample. The gate is opened by <see cref="Stalling.Release"/>, which the
/// catalogue window calls when it closes and a test calls when it is done, so nothing is left
/// waiting after the sheet is gone. Section 7 of docs/PROJEKT-KATALOG-20260916.md counts them.
///
/// Nothing here reads this machine. Rule 10 of the project notes - no outward calls - and the
/// same argument as every fake in the tests: a sheet that read the real manager would be a sheet
/// of a different picture on every machine it opened on.
/// </summary>
public static partial class Catalogue
{
    /// <summary>A machine that holds these entries and nothing else, and answers at once.</summary>
    private sealed class Frozen(IReadOnlyList<ScmEntry> entries) : IScmCatalog
    {
        public IReadOnlyList<ScmEntry> ReadAll() => entries;

        public IReadOnlyList<ScmStatus> ReadStatuses() =>
            [.. entries.Select(entry => new ScmStatus(entry.ServiceName, entry.Status, entry.ProcessId))];

        // Nothing depends on anything here: a plan over a specimen names the specimen alone,
        // which is the plan that is easiest to read on a sheet.
        public Reading<IReadOnlyList<string>> ReadDependents(string serviceName) =>
            Reading<IReadOnlyList<string>>.Present([]);
    }

    /// <summary>
    /// A machine that refuses to be read, with the refusal the manager gives when it is not
    /// allowed to - error 5, in the words Windows uses for it on this machine.
    /// </summary>
    private sealed class Refusing : IScmCatalog
    {
        private const int AccessDenied = 5;

        public IReadOnlyList<ScmEntry> ReadAll() => throw new System.ComponentModel.Win32Exception(AccessDenied);

        public IReadOnlyList<ScmStatus> ReadStatuses() => throw new System.ComponentModel.Win32Exception(AccessDenied);

        public Reading<IReadOnlyList<string>> ReadDependents(string serviceName) =>
            Reading<IReadOnlyList<string>>.Denied(AccessDenied, new System.ComponentModel.Win32Exception(AccessDenied).Message);
    }

    /// <summary>
    /// A machine that does not answer until it is released - the loading state, held for as long
    /// as the sheet is open.
    /// </summary>
    private sealed class Stalled(Stalling gate) : IScmCatalog
    {
        public IReadOnlyList<ScmEntry> ReadAll()
        {
            gate.Wait();

            return [];
        }

        public IReadOnlyList<ScmStatus> ReadStatuses()
        {
            gate.Wait();

            return [];
        }

        public Reading<IReadOnlyList<string>> ReadDependents(string serviceName) =>
            Reading<IReadOnlyList<string>>.NotRead();
    }

    /// <summary>
    /// The gate every stalled read of one sheet waits on. Opened once, when the sheet is done
    /// with its loading samples - by the window closing, or by a test finishing - so that the
    /// threads those reads hold are given back.
    /// </summary>
    public sealed class Stalling : IDisposable
    {
        private readonly ManualResetEventSlim _gate = new(initialState: false);
        private int _waiting;

        /// <summary>How many reads have waited on this gate - one per loading sample, and a
        /// second from the same model would be a tick the sheet is not supposed to have.</summary>
        public int Waiting => _waiting;

        internal void Wait()
        {
            Interlocked.Increment(ref _waiting);

            _gate.Wait();
        }

        /// <summary>Lets every stalled read return, empty. Safe to call more than once.</summary>
        public void Release() => _gate.Set();

        // Release and nothing more. Disposing the event while a read is still on its way out of
        // Wait is the one race this class could have, and an event left open costs one handle
        // per sheet in a process that is about to end.
        public void Dispose() => Release();
    }
}
