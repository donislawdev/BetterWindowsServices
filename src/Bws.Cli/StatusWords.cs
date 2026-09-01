using Bws.Core;

namespace Bws.Cli;

/// <summary>
/// The word for a running state, in sentence case rather than the manager's own spelling.
///
/// <b>WRITTEN 2026-09-01 FOR BACKLOG 124, AND THE FAULT IT REPAIRS IS A DISAGREEMENT BETWEEN THE
/// TWO INTERFACES RATHER THAN A WRONG WORD.</b> The window has said <c>Start pending</c> since
/// `docs/11` 3.7 asked for sentence case, and this tool went on printing <c>StartPending</c> - the
/// name of an enumeration value, which is a shape from the code rather than a word for a person.
/// One machine, one state, two spellings depending on which interface somebody happened to open.
///
/// <b>THE MACHINE READABLE OUTPUT IS NOT TOUCHED AND MUST NOT BE.</b> <c>bws list --json</c> and
/// the snapshot both carry <c>Running</c> and <c>StartPending</c> as field values, and those are a
/// frozen contract - `docs/02`. Every tool in <c>tools/</c> that compares this product against
/// <c>sc.exe</c> reads the JSON for exactly that reason, so this change reaches the table and the
/// report and stops there. That division is the whole reason this class renders text and nothing
/// else.
///
/// <b>Keys written out one by one rather than built from the value's name</b>, which is the reason
/// <c>CellFaces.StatusLabel</c> gives for the same switch in the window: a key assembled at run
/// time cannot be searched for, and a missing one would print as itself with nothing anywhere
/// saying why.
///
/// <b>The words are this tool's own copy rather than the window's</b>, and that is deliberate for
/// the reason <c>StartCell</c> already states about the start type: what a person reads comes out
/// of the language file of the interface they are looking at. The two files agree today because
/// somebody made them agree, and <c>QueryParityContractTests</c> holds the entries in common
/// rather than the wording.
/// </summary>
internal static class StatusWords
{
    internal static string Of(EntryStatus status) => status switch
    {
        EntryStatus.Running => Texts.Of("cli.cell.status.running"),
        EntryStatus.Stopped => Texts.Of("cli.cell.status.stopped"),
        EntryStatus.Paused => Texts.Of("cli.cell.status.paused"),
        EntryStatus.StartPending => Texts.Of("cli.cell.status.startPending"),
        EntryStatus.StopPending => Texts.Of("cli.cell.status.stopPending"),
        EntryStatus.ContinuePending => Texts.Of("cli.cell.status.continuePending"),
        EntryStatus.PausePending => Texts.Of("cli.cell.status.pausePending"),
        _ => Texts.Of("cli.cell.status.unknown")
    };
}
