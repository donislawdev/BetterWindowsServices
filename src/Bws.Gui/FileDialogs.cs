using Microsoft.Win32;

namespace Bws.Gui;

/// <summary>
/// The window's file dialogs - the two that save (a snapshot, an export) and the one that opens a
/// snapshot to compare - built in one place, so that where they start is decided once (GUI rule 5).
///
/// <b>WHY THIS EXISTS, backlog 562, seen 2026-10-14 on a live window.</b> None of the three said
/// where to start, so a dialog with no folder remembered for this program opened in the process's
/// working directory - and Explorer starts programs in System32. A window without rights there
/// refuses the write, and an elevated window would put the file into a system folder.
///
/// <b>A DEFAULT, NOT AN INITIAL FOLDER - owner's decision of 2026-10-14.</b> Windows remembers the
/// last folder a person used in a dialog of this program, and that memory is worth more than any
/// guess of ours: an administrator who keeps snapshots in one folder finds the dialog there.
/// <c>DefaultDirectory</c> is what the dialog shows only while nothing is remembered, and
/// <c>InitialDirectory</c> would override the memory every time - which is why a guard holds the
/// second one empty. Both are on <c>CommonItemDialog</c> since .NET 8 (read on learn.microsoft.com
/// the same day, rule 14).
///
/// <b>Documents, from the shell rather than from a path we build.</b> The folder may be redirected,
/// and the answer is the person's own. When the shell has none, the answer is empty and the dialog
/// behaves as it did before this file existed - no worse than then, and said rather than hidden.
/// The S-7 caution about folders that follow the process's environment does not apply here: this
/// is a folder offered to a person, who sees it and chooses, not one this program writes to alone.
///
/// <b>The dialogs themselves are never opened by a test</b> - a dialog opened from a session in the
/// background never appears, measured 2026-08-25. What is checked is how they are built, and
/// <c>FileDialogGuards</c> holds that nothing else in the window builds one.
/// </summary>
internal static class FileDialogs
{
    /// <summary>A dialog asking where a new file should go.</summary>
    /// <param name="fileName">The name offered, which the person may change.</param>
    /// <param name="extension">With its leading dot, as the dialog wants it.</param>
    /// <param name="kind">What the files are called in the dialog's filter, already in words.</param>
    internal static SaveFileDialog Save(string fileName, string extension, string kind) =>
        Started(new SaveFileDialog
        {
            FileName = fileName,
            DefaultExt = extension,
            Filter = Filter(extension, kind),
            AddExtension = true
        });

    /// <summary>A dialog asking which existing file to read.</summary>
    /// <param name="extension">With its leading dot, as the dialog wants it.</param>
    /// <param name="kind">What the files are called in the dialog's filter, already in words.</param>
    /// <param name="title">
    /// What the dialog is choosing for, in its title bar - since 2026-10-15, when comparing two files
    /// put two of these on screen one after the other and "Open" said nothing about which was which.
    /// </param>
    internal static OpenFileDialog Open(string extension, string kind, string title) =>
        Started(new OpenFileDialog
        {
            DefaultExt = extension,
            Filter = Filter(extension, kind),
            CheckFileExists = true,
            Title = title
        });

    /// <summary>
    /// The folder a dialog starts in when Windows remembers none for this program: the person's
    /// Documents, or empty when the shell has no such folder for them.
    /// </summary>
    internal static string StartingFolder() => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private static T Started<T>(T dialog)
        where T : FileDialog
    {
        dialog.DefaultDirectory = StartingFolder();

        return dialog;
    }

    private static string Filter(string extension, string kind) => kind + " (*" + extension + ")|*" + extension;
}
