namespace Bws.Core.History;

/// <summary>
/// Reads the change history of this machine. One implementation talks to Windows, and a test can
/// hand in a history it built itself.
/// </summary>
public interface IChangeHistoryReader
{
    ChangeHistory Read();
}

/// <summary>
/// Who cleared the log and when - read from the event Windows writes as the first record of a log it
/// has just emptied.
/// </summary>
public sealed record Clearing(DateTimeOffset? At, Actor? By);

/// <summary>
/// How much of the past the log can still speak for. Said before any change is listed, because a
/// history that reaches back three days reads exactly like three quiet days unless somebody says so.
/// </summary>
public sealed record LogCoverage
{
    /// <summary>The channel, which is <c>System</c> for everything this reads today.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// When the oldest record in the log was written. Null for an empty log.
    ///
    /// <b>Not a promise that nothing is missing since</b> - logic report H02, 2026-10-27. Nothing about
    /// a log says it was written to without a gap: the log service can have been stopped, a log set to
    /// keep what it has can have stood full for a week before somebody made it larger, and an archived
    /// log keeps its past in other files.
    /// </summary>
    public required DateTimeOffset? ReachesBack { get; init; }

    /// <summary>
    /// The log has no room left and Windows makes room by throwing its oldest events away, so the
    /// past it covers moves forward as new events arrive. Only ever true for <see cref="LogMode.Circular"/>.
    /// </summary>
    public required bool Overwritten { get; init; }

    /// <summary>
    /// Windows says the log is full. With a log set never to overwrite, that means NEW events are not
    /// being written at all - the recent end of the history is the one missing. Windows documents it as
    /// always false for the two other modes, so it only speaks for <see cref="LogMode.Retain"/>.
    /// </summary>
    public required bool Full { get; init; }

    /// <summary>Set when the oldest record says the log was cleared, and by whom.</summary>
    public Clearing? Cleared { get; init; }

    /// <summary>What Windows does when the log reaches its size limit - read, not inferred (H02).</summary>
    public required LogMode Mode { get; init; }
}

/// <summary>
/// What Windows does when a log reaches its size limit - since 2026-10-27, logic report H02. Named as
/// <c>Get-WinEvent -ListLog</c> names it in its LogMode column, the words an administrator already reads.
///
/// <b>Read from the channel's configuration, not inferred from its state.</b> Until that day the size and
/// <c>EvtLogFull</c> stood in for it, and Windows documents that flag as always false for two of these
/// three - so an archived log at its limit was said to be overwriting its oldest events.
/// </summary>
public enum LogMode
{
    /// <summary>The oldest events are overwritten - retention off, the default for the System log.</summary>
    Circular,

    /// <summary>The full file is put aside as Archive-System-*.evtx and a new one begun - nothing is overwritten.</summary>
    AutoBackup,

    /// <summary>Nothing is overwritten and nothing is put aside, so a full log takes no new events.</summary>
    Retain
}

/// <summary>
/// What Windows says about the log file itself, before any event in it is read.
/// </summary>
/// <param name="FileSize">The size of the log file now, in bytes.</param>
/// <param name="MaximumSize">The size the channel is configured never to grow beyond, in bytes.</param>
/// <param name="Full">Whether Windows reports the log as full.</param>
/// <param name="Mode">What the channel is configured to do at its size limit.</param>
public sealed record LogFacts(ulong FileSize, ulong MaximumSize, bool Full, LogMode Mode)
{
    /// <summary>
    /// How much a log file grows at a time. The file is a header followed by chunks of this size, and
    /// a log stops growing - and starts overwriting, if it may - when one more chunk would not fit.
    ///
    /// <b>The size is the evtx format's, written down by people who read the format rather than by
    /// Microsoft, so it is an outside source and is named as one.</b> What was measured here, 2026-10-15:
    /// the one log on the owner's machine that was overwriting (Security) was exactly a 4096 byte header
    /// plus 320 chunks of this size, at a limit of 320 chunks, and the four that were not sat at 5 to
    /// 34 per cent of their limit.
    /// </summary>
    public const ulong Chunk = 64 * 1024;

    /// <summary>
    /// Whether the file has reached the point where it cannot grow by another chunk.
    ///
    /// <b>FROM THE SIZE, AND NOT FROM THE OLDEST RECORD NUMBER, WHICH LOOKED LIKE THE OBVIOUS SIGNAL AND
    /// IS NOT ONE.</b> Windows documents that number as the oldest record in the log, and on the owner's
    /// machine it answered 1 for a System log whose oldest record is number 518 - a clear does not
    /// restart the numbering, and whatever the number tracks, it is not what the documentation says.
    /// What IS documented is the rule a circular log follows: once it reaches its maximum size it
    /// overwrites its oldest events. So the question asked here is the one that rule is about.
    /// </summary>
    public bool AtSizeLimit => FileSize + Chunk > MaximumSize;
}

/// <summary>
/// The change history of this machine: how far back the log reaches, and every change in it, newest
/// first - the order Event Viewer and Get-WinEvent show.
/// </summary>
public sealed record ChangeHistory(LogCoverage Log, IReadOnlyList<ServiceChange> Changes)
{
    /// <summary>
    /// The changes tied to one entry BY IDENTITY - its internal name, compared the way the service
    /// manager compares names, without regard to case.
    ///
    /// <b>Installations are never among them</b>, because event 7045 carries no internal name, and
    /// matching one by display name would be the guess rule 3 forbids - a display name can change after
    /// the install, comes translated and need not be unique. Owner's decision J1-b, 2026-10-15.
    /// </summary>
    public IReadOnlyList<ServiceChange> Of(string serviceName) =>
        [.. Changes.Where(change => string.Equals(change.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Start type changes that do not say which entry they are about, so that a view of one entry can
    /// say how many it could not consider instead of leaving them out without a word.
    /// </summary>
    public int Unnamed => Changes.Count(change => change.Kind == ChangeKind.StartTypeChanged && change.ServiceName is null);

    /// <summary>Whether the log holds any installation, for the sentence a view of one entry owes.</summary>
    public bool HasInstallations => Changes.Any(change => change.Kind == ChangeKind.Installed);

    /// <summary>
    /// How far back a log reaches, from the facts about its file and its oldest record.
    ///
    /// <b>Overwritten and full exclude each other</b>: a full log is one that will not overwrite, and a
    /// log that overwrites is never reported full - on the owner's machine the Security log sat at its
    /// limit, overwriting, and reported not full.
    ///
    /// <b>Overwritten only for a circular log, since 2026-10-27 (H02).</b> It was "not full and at the
    /// limit", which also fits an archived log at its limit - Windows puts that file aside rather than
    /// overwriting it, and reports it never full.
    /// </summary>
    public static LogCoverage Cover(string name, LogFacts facts, DateTimeOffset? oldest, Clearing? cleared)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return new LogCoverage
        {
            Name = name,
            ReachesBack = oldest,
            Overwritten = facts.Mode == LogMode.Circular && facts.AtSizeLimit,
            Full = facts.Full,
            Cleared = cleared,
            Mode = facts.Mode
        };
    }
}
