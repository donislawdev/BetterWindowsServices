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
    /// A file inspector that vouches for every file at once - what the details panel's own reading
    /// meets on a machine where everything is signed. The product always has an inspector, so a
    /// panel specimen without one would show "not read" beside a signature, which since
    /// 2026-09-24 the product only shows for a file on another machine.
    /// </summary>
    private sealed class Vouching : IBinaryInspector
    {
        public Reading<BinarySignature> ReadSignature(string file) =>
            Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.Trusted, 0, "Microsoft Windows"));

        public Reading<string> ReadFileVersion(string file) => Reading<string>.Present("10.0.26100.1");

        public Reading<string> ReadHash(string file) =>
            Reading<string>.Present("5574acc33b33ab8fbd7e45b14b0d8425fd67df867a7c6d09417ea4ea096d0d57");
    }

    /// <summary>A memory reader that answers at once, for the same reason as <see cref="Vouching"/>.</summary>
    private sealed class Measuring : IProcessMemoryReader
    {
        public Reading<ProcessMemory> Read(int processId) =>
            Reading<ProcessMemory>.Present(new ProcessMemory(18 * 1024 * 1024, 9 * 1024 * 1024, 1));
    }

    /// <summary>
    /// The details panel's own reading, asked and never answered - the loading state of the panel,
    /// held for as long as the sheet is open. A pending task rather than a stalled thread, because
    /// nothing here needs a thread to wait: the panel awaits the task, and an answer that never
    /// comes is all the loading state is. The machine's own loading sample keeps the one thread
    /// the gate below counts.
    /// </summary>
    private sealed class NeverAnswering : IEntryReads
    {
        public Bws.Core.Querying.ExtraRead Claim(EntryRow row) =>
            Bws.Core.Querying.ExtraRead.Signatures | Bws.Core.Querying.ExtraRead.Memory | Bws.Core.Querying.ExtraRead.RequiredBy;

        public Task ReadAsync(EntryRow row, Bws.Core.Querying.ExtraRead families) => new TaskCompletionSource().Task;
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
