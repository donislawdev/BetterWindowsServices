namespace Bws.Core;

/// <summary>
/// One entry in the service control manager: a service or a driver.
///
/// Field names follow the binding "w kodzie" column of the glossary. They are a
/// public contract because they surface in the CLI JSON output and later in the
/// snapshot format, so renaming one is a breaking change, not a tidy-up.
///
/// Everything here is cheap to read: it comes from enumerating the manager plus one
/// configuration query. Expensive data (signatures, permissions, triggers, memory)
/// belongs to a second pass and is not modelled yet.
/// </summary>
public sealed record ScmEntry
{
    /// <summary>The immutable identity. Case-insensitive to compare, case-preserving to show.</summary>
    public required string ServiceName { get; init; }

    /// <summary>Label for a human. Translated to the system language, never an identity.</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// What the entry says it is for, in the words its author wrote for a person to read.
    ///
    /// <b>The second column of services.msc, and the only one that answers "what even is
    /// this".</b> It arrived on 2026-08-12, delegated by the owner and settled by measurement
    /// rather than by preference - see <see cref="ScmDetailReader.ReadDescription"/> for why it
    /// must come from the manager and never from the registry, and for the cost that puts it in
    /// the cheap pass.
    ///
    /// <b>A reading rather than a string, and all four states really happen here.</b> Over 819
    /// entries: 384 have none at all, which is nearly every driver and is a fact about the entry
    /// rather than a failure. Ten have one the manager could not resolve into words, which is a
    /// failure and not an absence.
    ///
    /// <b>Translated, so never an identity</b> - `ADR-14`, the same rule as
    /// <see cref="DisplayName"/>. Nothing may match, key or compare entries on this text.
    ///
    /// <b>It can be long and it can contain a newline</b>, which is a fact whatever displays it
    /// has to hold: the longest measured is 1251 characters and two carry a line break. A cell
    /// showing it in one line is backlog 167.
    /// </summary>
    public required Reading<string> Description { get; init; }

    public required EntryType EntryType { get; init; }

    /// <summary>
    /// Which side of the per-user family this entry is on, or None for the rest of the machine.
    ///
    /// Required like the rest: it is read from the same type bits as <see cref="EntryType"/>,
    /// on the same call, so an entry without an answer here would mean the enumeration itself
    /// failed - and that is not a state a single field gets to represent.
    /// </summary>
    public required PerUserRole PerUserRole { get; init; }

    public required EntryStatus Status { get; init; }

    /// <summary>
    /// Process holding the entry, when one is running. Absent rather than zero when
    /// the entry is stopped, because zero is a value and "not running" is not.
    /// </summary>
    public required Reading<int> ProcessId { get; init; }

    /// <summary>Comes from the configuration query, which can be refused.</summary>
    public required Reading<StartType> StartType { get; init; }

    /// <summary>
    /// Whether an automatic entry starts late, after the boot rush.
    ///
    /// A separate field rather than a sixth <see cref="Core.StartType"/> value, and the
    /// reason is the refusal case. The manager answers the start type and the delay flag
    /// through two different calls, so "automatic, and I could not find out whether it is
    /// delayed" is a state that happens. One enumeration holds one value and would have to
    /// report that as plain automatic, which is a confident answer about something nobody
    /// read. Measured on a real machine on 2026-08-01: 13 of 78 automatic services carry
    /// the flag, and sc.exe reports both kinds as start type 2.
    ///
    /// <b>READ FOR EVERY ENTRY THAT CAN CARRY ONE, AND THIS PARAGRAPH SAID THE OPPOSITE UNTIL
    /// 2026-08-25.</b> It read "absent on anything that is not automatic" - true of an earlier
    /// reader, false since ReadDelayedAuto stopped asking the start type first, and load bearing:
    /// the way back for a start type change asks this field whether it may name a previous type,
    /// which under that sentence was a question about something always absent.
    ///
    /// Absent on a driver, which has no such setting. Absent is not false: false would claim
    /// the question was asked and answered.
    /// </summary>
    public required Reading<bool> DelayedAuto { get; init; }

    /// <summary>
    /// Account the entry runs as, as text. Translated on non-English systems, so it is
    /// a label and never an identity (ADR-14). The account SID belongs to the expensive
    /// pass and is not read here.
    /// </summary>
    public required Reading<string> Account { get; init; }

    /// <summary>
    /// What this entry declares it needs before it can run.
    ///
    /// Free: it arrives in the same configuration structure the start type and the account
    /// come from, so reading it costs no extra call. Measured on a real machine on
    /// 2026-08-01: 210 of 339 services declare at least one, the longest list has five.
    ///
    /// Names are kept exactly as the manager returns them, including the leading plus that
    /// marks a load order group rather than a service. RemoteAccess on that machine
    /// declares "+NetBIOSGroup" alongside four ordinary services. Stripping the marker
    /// would turn a group into a service that does not exist.
    ///
    /// This is what the entry declares, which is not the same question as what would break
    /// if it stopped. The manager answers that one itself, and the cascade will ask it
    /// rather than inverting this list, because the manager also knows which services are
    /// in which group and this list does not.
    /// </summary>
    public required Reading<IReadOnlyList<string>> DependsOn { get; init; }

    /// <summary>
    /// What breaks if this entry stops - the other direction of the field above, and the one
    /// somebody actually asks before touching a machine.
    ///
    /// <b>NOT free, which is the whole reason it is a family of its own.</b> The declaration above
    /// arrives in the configuration structure the start type comes from and costs nothing. This
    /// takes a call PER ENTRY: the manager is asked, one service at a time, who is standing on it.
    /// Measured on this machine on 2026-09-05 through the same Win32 call reached from .NET,
    /// five runs with the first discarded: <b>236-259 ms over 313 services, 784 dependents found,
    /// nothing refused</b>. That is the size of the whole listing again - 423-500 ms over 810
    /// entries - so paying it on every F5 for a column that is off by default is exactly the trade
    /// `ADR-13` refuses. It is read when somebody asks, and <see cref="ExtraRead.RequiredBy"/> is
    /// how they ask.
    ///
    /// <b>ASKED OF THE MANAGER RATHER THAN INVERTED FROM THE DECLARATIONS, and that is not
    /// convenience.</b> An entry may declare a load order GROUP instead of a service -
    /// RemoteAccess declares "+NetBIOSGroup" on this machine - and the declaration says nothing
    /// about who is in that group. The manager knows and we would be guessing. The same argument
    /// is written at <c>IScmCatalog.ReadDependents</c>, which is what this reads through, and it
    /// is the reason the plan's cascade has always asked rather than inverted.
    ///
    /// <b>The first hop only.</b> What the manager returns is who depends on this entry directly,
    /// not the closure - a service standing on a service standing on this one is not in this list.
    /// `05-PRZYPADKI-BRZEGOWE` records that measurement because it is what decides whether a
    /// cascade has to recurse, and a column showing one hop must not be read as showing all of
    /// them.
    /// </summary>
    public required Reading<IReadOnlyList<string>> RequiredBy { get; init; }

    /// <summary>
    /// The conditions under which the manager starts or stops this entry by itself.
    ///
    /// The first of the four families S4 was cut into, and the family that decided the
    /// shape of the other three: it looked like a candidate for deferring under ADR-13 and
    /// measured at 45-75 ms across 810 entries, which is nothing. So it is read on every
    /// listing and its ordinary state is <see cref="ReadOutcome.Present"/> or
    /// <see cref="ReadOutcome.Absent"/>, never NotRead.
    ///
    /// This comment said it was "the first field whose ordinary state is NotRead" and that
    /// stopped being true the moment the measurement came back - while the same sentence
    /// went on to appear, correctly, on the signature. Two fields cannot both be the first,
    /// and nothing in a build notices a comment contradicting another comment.
    ///
    /// What does still hold is why the state exists at all: a listing has to be useful
    /// before everything is known, so "nobody asked for this yet" must never render like
    /// "there is nothing here". A stopped service with no triggers is broken. A stopped
    /// service waiting for a trigger is working.
    ///
    /// Absent, not empty, when the entry has none - measured on a real machine on
    /// 2026-08-01: 122 entries of 810 have at least one, so having none is the ordinary
    /// case and is a fact about the service rather than a gap in what we read.
    /// </summary>
    public required Reading<IReadOnlyList<ServiceTrigger>> Triggers { get; init; }

    /// <summary>
    /// The whole command the manager runs, executable and arguments together, exactly as
    /// it comes back.
    ///
    /// Kept verbatim rather than tidied, because this is the value a snapshot has to
    /// preserve and a diff has to compare. The arguments are part of what somebody changed
    /// when they changed it, and a normalised version would hide that.
    ///
    /// Free: it arrives in the same configuration structure the start type, the account and
    /// the dependencies come from, so reading it costs no extra call.
    /// </summary>
    public required Reading<string> BinaryPath { get; init; }

    /// <summary>
    /// The file that command actually runs, as an absolute path.
    ///
    /// Its own field rather than something a reader is left to work out, because working it
    /// out is the hard half: arguments have to come off, <c>\SystemRoot\</c> and relative
    /// paths have to be resolved, and an unquoted path with spaces is genuinely ambiguous.
    /// See <see cref="BinaryPathResolver"/> for the rules and the measurements behind them.
    ///
    /// Absent when the entry names no file at all and no default applies.
    /// </summary>
    public required Reading<string> BinaryFile { get; init; }

    /// <summary>
    /// Whether <see cref="BinaryFile"/> is on disk.
    ///
    /// An entry set to start automatically whose file is gone is an orphan in the glossary's
    /// sense, and C13 of the specification is about finding them. Measured on a real machine
    /// on 2026-08-01: five entries of 810 name a file that is not there, none of them
    /// automatic, which is why this is reported as its own fact rather than folded into a
    /// single "orphan" answer that would have been empty.
    ///
    /// Cheap enough to read on every listing: 34-39 ms across 810 entries, against a budget
    /// of a second, and against 45-75 ms for the triggers already read the same way.
    /// </summary>
    public required Reading<bool> BinaryOnDisk { get; init; }

    /// <summary>
    /// Who signed <see cref="BinaryFile"/> and whether the system trusts the signature.
    ///
    /// The first field whose ordinary state in a plain listing is
    /// <see cref="ReadOutcome.NotRead"/>, and the first one that earns it. Measured on a
    /// real machine on 2026-08-01, seven runs with the first discarded as cold: verifying
    /// 544 distinct files costs 4620-7656 ms, median 4882, against 476-551 ms for everything
    /// the listing does otherwise. Reading it every time would put the listing five to eight
    /// times over its one second budget - a range rather than a figure, because the spread
    /// of this one operation is wider than the whole of the rest of the listing.
    ///
    /// That is what ADR-13 was written for. Triggers and launch paths both looked like they
    /// would need it and both turned out cheap enough not to - this is the family where the
    /// deferral is paid for by a measurement rather than by an expectation.
    /// </summary>
    public required Reading<BinarySignature> Signature { get; init; }

    /// <summary>
    /// How hard the system takes it when this entry fails during boot.
    ///
    /// Free: it arrives in the same configuration structure as the start type, and it has
    /// been sitting in that buffer unread since the first slice. `D1` names it among the
    /// things a snapshot holds, which is what finally gave it a reader.
    /// </summary>
    public required Reading<ErrorControl> ErrorControl { get; init; }

    /// <summary>
    /// The load order group this entry belongs to, if any.
    ///
    /// Also free and also from that buffer. It matters for the same reason a dependency
    /// does: an entry can declare a dependency on a whole group rather than on a service,
    /// and this is the other end of that relationship. Absent for most entries.
    /// </summary>
    public required Reading<string> LoadOrderGroup { get; init; }

    /// <summary>
    /// The version <see cref="BinaryFile"/> claims for itself.
    ///
    /// Cheap on its own - 0.27 s across the same 544 files - and read in the same pass
    /// anyway, because it comes from a file that has just been opened for the signature.
    /// Splitting it out would mean walking every binary on the machine twice to save a
    /// quarter of a second on a step that already costs three.
    ///
    /// Absent for a file with no version resource, which is ordinary rather than missing.
    /// </summary>
    public required Reading<string> FileVersion { get; init; }

    /// <summary>
    /// SHA-256 of <see cref="BinaryFile"/>, lower case hexadecimal.
    ///
    /// The thing a snapshot needs that a signature cannot give: a signature says who vouched
    /// for the file, and this says whether it is byte for byte the same file. A binary
    /// swapped for another one signed by the same publisher changes this and nothing else.
    ///
    /// Read alongside the signature rather than on its own, because both open the same file
    /// and walking every binary on the machine twice would cost more than the hash does.
    /// Measured on 2026-08-01: 0.52 s across 544 distinct files totalling 368 MB, against
    /// 4620-7656 ms for the signatures in the same pass.
    /// </summary>
    public required Reading<string> BinaryHash { get; init; }

    /// <summary>
    /// The privileges the entry asks the manager to leave in its token, by name.
    ///
    /// What it asks for, which is not what it gets and not what it could have. A service
    /// declaring none is not a service without privileges - the manager then leaves the
    /// account's whole set in place, so declaring nothing is the permissive case and
    /// declaring a short list is the careful one. Reading this the other way round would
    /// invert every finding built on it.
    ///
    /// Names are kept exactly as the manager returns them, including the casing, which
    /// varies between services on one machine: Schedule declares SeSystemTimePrivilege and
    /// Sense declares SeSystemtimePrivilege, and they are the same privilege. Comparison is
    /// therefore case-insensitive everywhere, and a diff that compared these as plain text
    /// would report a change between two machines that had none.
    ///
    /// Absent, not empty, for an entry declaring nothing - measured on a real machine on
    /// 2026-08-01: 215 of 339 services declare at least one, the longest list has 28, and
    /// all 471 drivers declare none because a driver has no token to trim.
    ///
    /// Cheap: 43-50 ms across 810 entries, on the configuration handle the listing already
    /// holds, against a budget of a second.
    /// </summary>
    public required Reading<IReadOnlyList<string>> RequiredPrivileges { get; init; }

    /// <summary>
    /// Whether the entry has an identity of its own. See <see cref="Core.ServiceSidType"/>
    /// for why "none" lives in the outcome rather than in the enumeration.
    ///
    /// Asked of every entry including drivers, although every driver measured answers none.
    /// Skipping them would save about ten milliseconds and would turn a measurement of one
    /// machine into a claim about all of them.
    /// </summary>
    public required Reading<ServiceSidType> SidType { get; init; }

    /// <summary>
    /// Who may do what to this entry, as the text form the system reads and writes.
    ///
    /// The text form rather than a decoded list, and that is a deliberate split the glossary
    /// makes in pitfall P9: the decoded list is what a person is shown, the text form is what
    /// a snapshot keeps, because only the text form is faithful and comparable. The window
    /// will decode it when there is a panel to decode it into.
    ///
    /// Owner, group and permissions. The audit list - the SACL - is not here: asking for it
    /// fails the whole read with error 5 unless SeSecurityPrivilege is enabled, which it is
    /// not even in an elevated session, so including it would cost the other three parts and
    /// buy nothing. This is a deliberate difference from <c>sc sdshow</c>, which shows the
    /// audit list and does not show owner or group.
    ///
    /// The one field here read through a handle of its own, because READ_CONTROL is a
    /// different right from the one the listing already has. Measured under a restricted
    /// token on 2026-08-01: five entries of 810 open for configuration and refuse when
    /// READ_CONTROL is added, so asking for both together would have cost them the fields
    /// they report today. 29-33 ms plus about 30 for the extra handle, across 810 entries.
    /// </summary>
    public required Reading<string> SecurityDescriptor { get; init; }

    /// <summary>
    /// What the process behind this entry is using, when one is running.
    ///
    /// The last of the four families S4 was cut into, and the only one that is a
    /// measurement rather than a description of how the machine is set up. Everything else
    /// on this record reads the same twice in a row - this does not, which is why it is not
    /// part of a snapshot and why a plain listing does not ask for it.
    ///
    /// Absent for the great majority: 691 entries of 810 are not running, and an entry with
    /// no process has no memory in the same way a stopped entry has no process id. Not read
    /// until somebody asks with <c>--memory</c> or with a query about it.
    ///
    /// Cheap, and deliberately not read anyway. Measured on 2026-08-01: asking all 110
    /// processes takes under a millisecond. See <see cref="MemoryPass"/> for why that is not
    /// the argument it looks like.
    /// </summary>
    public required Reading<ProcessMemory> Memory { get; init; }

    /// <summary>
    /// True for a name in <see cref="DependsOn"/> that names a load order group rather
    /// than a service. Stopping one member of a group does not necessarily break anything
    /// that depends on the group, so the two cannot be treated alike when planning.
    /// </summary>
    public static bool IsGroup(string dependency) =>
        dependency.StartsWith('+');

    public bool IsDriver => EntryType is EntryType.KernelDriver or EntryType.FileSystemDriver;

    /// <summary>
    /// The single most useful derived fact in the whole tool: the entry is supposed to
    /// be running and is not.
    ///
    /// Unknown when the start type could not be read, because "I could not check"
    /// must never render as "everything is fine".
    /// </summary>
    public Reading<bool> RunsAgainstItsStartType =>
        StartType.Outcome switch
        {
            ReadOutcome.Present => Judge(),

            // Passes the refusal on whole, number and sentence together. A refusal always
            // carries both, so there is nothing here to invent - and inventing a sentence
            // was what the old fallback did, in English, in code.
            ReadOutcome.Denied => Reading<bool>.Denied(StartType.ErrorCode, StartType.Reason!),
            _ => Reading<bool>.NotRead()
        };

    /// <summary>
    /// The other direction: the entry is switched off and is running anyway.
    ///
    /// <b>ITS OWN FACT RATHER THAN A WIDER <see cref="RunsAgainstItsStartType"/>, and that was the
    /// decision - backlog 170, 2026-08-18.</b> One boolean answering two opposite questions destroys
    /// the part of the answer somebody needs in order to act: a true would stop saying WHICH of the
    /// two situations they have, and the remedies are opposite. A stopped automatic entry gets
    /// started or investigated; this one is a decision about whether to stop it or to re-enable it.
    /// The same discipline the four states of a <see cref="Reading{T}"/> already keep.
    ///
    /// <b>What it means on a real machine, because it is not a corner case.</b> The manager could
    /// not have started this at boot, and it is up - so either somebody disabled it while it was
    /// already running, where the change takes effect at the next start, or something started it
    /// out of band. The owner's screenshot of services.msc from 2026-08-12 carries one:
    /// two columns side by side saying opposite things, and nothing in that window names it.
    ///
    /// Unknown when the start type could not be read, for the reason its sibling gives: "I could
    /// not check" must never render as "everything is fine". The status needs no such care - it is
    /// not a <see cref="Reading{T}"/>, because the manager hands it over with the listing or hands
    /// over nothing at all.
    /// </summary>
    public Reading<bool> RunsWhileDisabled =>
        StartType.Outcome switch
        {
            ReadOutcome.Present => Reading<bool>.Present(
                StartType.Value == Core.StartType.Disabled && Status == EntryStatus.Running),

            ReadOutcome.Denied => Reading<bool>.Denied(StartType.ErrorCode, StartType.Reason!),
            _ => Reading<bool>.NotRead()
        };

    /// <summary>
    /// Whether the entry not running is a failure, once the triggers are taken into account.
    ///
    /// An automatic entry that is stopped used to be the whole answer. It is not: an entry
    /// with a trigger that starts it is doing exactly what it was configured to do, and the
    /// system will bring it up when the condition arrives. Measured on a real machine on
    /// 2026-08-01, four of the ten entries this reported were waiting rather than broken -
    /// and a signal that is wrong four times out of ten is one people learn to ignore.
    ///
    /// The cost of that is here, in the last branch: the answer now depends on a field that
    /// can be unread, so an entry that looks stopped and whose triggers nobody read gets
    /// "I do not know" rather than an accusation. That only applies to entries that would
    /// otherwise be reported - everything else is answered without the triggers being needed
    /// at all, so an unread listing does not turn into 810 shrugs.
    /// </summary>
    private Reading<bool> Judge()
    {
        // A PER-USER TEMPLATE HAS NO ANSWER TO THIS, AND SAYING "yes" WAS THE SAME FALSE ALARM
        // `peruser:` HAD JUST REMOVED FROM THE QUERY. Owner's decision 2026-08-26, backlog 235.
        //
        // A template is automatic and never runs, because running is not what it is for - a
        // session's copy runs beside it. Judged by the rule below it looks exactly like a service
        // that failed to start, and on this machine that was 23 entries of 798 wearing an
        // accusation. Measured 2026-08-25 on `bws show CDPUserSvc --full`: "Runs against its start
        // type: yes", with CDPUserSvc_7b537 running next to it.
        //
        // ABSENT RATHER THAN A PRESENT FALSE, and the difference is the one this type exists for.
        // False would claim somebody asked and the answer was no. Absent says the question does not
        // apply, which is what lets `bws show` drop the line entirely, the cell go blank, and
        // `mismatch:none` count it as agreeing with itself rather than as unreadable.
        //
        // ITS SIBLING RunsWhileDisabled IS DELIBERATELY UNTOUCHED. Disabled-and-running is a real
        // false for a template rather than a question without meaning - it genuinely is not running.
        if (PerUserRole == PerUserRole.Template)
        {
            return Reading<bool>.Absent();
        }

        if (StartType.Value != Core.StartType.Automatic || Status == EntryStatus.Running)
        {
            return Reading<bool>.Present(false);
        }

        return Triggers.Outcome switch
        {
            // Something starts it by itself. Stopped is where it is supposed to sit.
            ReadOutcome.Present when Triggers.Value!.Any(trigger => trigger.Action == TriggerAction.Start) =>
                Reading<bool>.Present(false),

            ReadOutcome.Present or ReadOutcome.Absent => Reading<bool>.Present(true),
            ReadOutcome.Denied => Reading<bool>.Denied(Triggers.ErrorCode, Triggers.Reason!),
            _ => Reading<bool>.NotRead()
        };
    }
}
