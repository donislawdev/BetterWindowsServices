using System.ComponentModel;
using Windows.Win32.Foundation;
using Windows.Win32.System.Services;

namespace Bws.Core;

/// <summary>
/// The manager's own vocabulary, turned into ours.
///
/// One place because two copies drift, and both halves of the manager - the one that reads
/// and the one that writes - need the same two answers. A listing that called a state
/// "Stopped" while a step result called the same state something else would be two truths
/// about one machine.
///
/// Kept internal: no system type crosses this seam, which is rule 4 of part 2 of
/// 06-STRUKTURA-I-KONWENCJE.
/// </summary>
internal static class ManagerTerms
{
    /// <summary>
    /// A refusal, in words. The system's own, in the system language, which is why it is
    /// not a translation key - there is nothing of ours to translate.
    ///
    /// Decoding these into something a person can act on - 1069 meaning an account without
    /// the log on as a service right, and the rest of that family - is promised by the
    /// specification and is a slice of its own. This is the honest interim: the number, and
    /// whatever Windows says about it.
    /// </summary>
    internal static string Describe(int code) =>
        code == (int)WIN32_ERROR.ERROR_ACCESS_DENIED
            ? "access denied"
            : new Win32Exception(code).Message;

    internal static EntryStatus Status(SERVICE_STATUS_CURRENT_STATE state) => state switch
    {
        SERVICE_STATUS_CURRENT_STATE.SERVICE_STOPPED => EntryStatus.Stopped,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_START_PENDING => EntryStatus.StartPending,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_STOP_PENDING => EntryStatus.StopPending,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_RUNNING => EntryStatus.Running,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_CONTINUE_PENDING => EntryStatus.ContinuePending,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_PAUSE_PENDING => EntryStatus.PausePending,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_PAUSED => EntryStatus.Paused,
        _ => EntryStatus.Unknown
    };
}
