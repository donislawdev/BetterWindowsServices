namespace Bws.Gui.ViewModels;

/// <summary>
/// How a full reading of the machine treats what the window already knows about files.
///
/// <b>Two ways since 2026-09-29, the owner's S-1 decision recorded in `ADR-13`.</b> Until then every
/// full reading verified every signature again - about 12 s of processor over 797 entries, 4.3-5.0 s
/// of clock on two processors - including the one after a plan, which writes no file and no path
/// and so cannot have changed a single answer. The words for the two are in docs/03.
/// </summary>
internal enum Relisting
{
    /// <summary>
    /// Everything read and verified again: the first look and F5. The only way a verdict older than
    /// the reading goes away, and the reason F5 exists.
    /// </summary>
    Afresh,

    /// <summary>
    /// The machine read again, and the signature, version and hash of every file the window has
    /// already asked about kept rather than verified again: after a plan, after a change to what is
    /// installed, and when a question needs a family the window has not read. A file with a new or
    /// changed path is still verified. See <c>SecondPass.Keep</c>.
    /// </summary>
    Keeping
}
