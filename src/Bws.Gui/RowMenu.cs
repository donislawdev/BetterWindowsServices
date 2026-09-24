using System.Windows;
using System.Windows.Controls;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// One item of the menu on a row: what it is called, which key does the same thing, and what it
/// does.
///
/// <b>Public because the menu item binds to it</b>, like <see cref="DetailLine"/> and for the same
/// reason - a binding cannot see a property on an internal type from outside this assembly, and
/// finding that out costs a menu of blank lines with nothing in the build to say so. The
/// constructor is internal: only the window composes these.
///
/// <b>The key is a translation key of its own rather than a literal "Enter"</b>, because it is a
/// word a person reads - and rule 1 of the GUI rules in the project notes allows no string written
/// into a view. It travels as text rather than as a KeyGesture on purpose: the press itself is
/// decided in <see cref="Shortcuts"/>, and a gesture declared here as well would be the same key
/// written in two places, free to disagree.
/// </summary>
public sealed class RowMenuEntry
{
    private readonly string _gestureKey;
    private readonly Func<string>? _filling;

    /// <param name="filling">
    /// The one value a label cannot carry in the language file - the help menu's version number,
    /// since 2026-09-24. Asked each time the label is read, so it is never a copy taken earlier.
    /// </param>
    internal RowMenuEntry(string labelKey, string? gestureKey, Func<Task> act, Func<string>? filling = null)
    {
        LabelKey = labelKey;
        _gestureKey = gestureKey ?? string.Empty;
        Act = act;
        _filling = filling;
    }

    /// <summary>The key of its label, which is how a test tells the items apart without reading words.</summary>
    internal string LabelKey { get; }

    /// <summary>What the item says, in the language of whoever is reading it.</summary>
    public string Label => _filling is null ? Texts.Of(LabelKey) : Texts.Of(LabelKey, _filling());

    /// <summary>The key that does the same thing, written beside the label - or nothing.</summary>
    public string Gesture => _gestureKey.Length == 0 ? string.Empty : Texts.Of(_gestureKey);

    /// <summary>What pressing it does. Already finished for the items that do their work on the spot.</summary>
    internal Func<Task> Act { get; }
}

/// <summary>
/// The menu on a row, built from a list rather than written out.
///
/// <b>IT LEFT MainWindow.xaml ON 2026-09-15, AND THE REASON WAS A CEILING RATHER THAN A PRINCIPLE -
/// though the principle was already written.</b> The markup stood at the size ratchet's 376 lines
/// with three files on it, and "Show details" needed two more. GUI rule 8 in the project notes had
/// said since before that day that repeated things are rendered from a collection - "a set of
/// buttons, fields or tabs differing only by value is a list of data rendered in a loop, already at
/// three" - and the heading menu had been built this way since 2026-09-05. This is the same shape:
/// the items are data, the loop below draws them, and a ninth item is one line in a list.
///
/// <b>Groups rather than a flat list with separator markers</b>, because the separator is what a
/// group IS on screen, and a marker object in the list would be a thing that is not an item
/// pretending to be one - every loop over the entries would have to know to skip it.
///
/// <b>Items added to the menu one by one rather than handed over as ItemsSource</b>, which is the
/// heading menu's arrangement and the one the guards can see: <c>Items.OfType&lt;MenuItem&gt;()</c>
/// answers with the real items whether or not the menu has ever opened, where containers generated
/// from an ItemsSource exist only after a layout that a window nobody has shown never runs -
/// `docs/10` trap 19.
///
/// <b>The handlers are wired here in code rather than in a style</b>, for the reason the heading
/// menu gives: a resource dictionary with no code behind has nowhere to put an EventSetter's
/// handler.
/// </summary>
internal static class RowMenu
{
    /// <summary>
    /// What the menu offers, in the order it is offered: the one thing Enter does, then the four
    /// copies, then the five previews.
    ///
    /// <b>"Show details" first, because it is the item the default gesture stands for.</b> Windows
    /// puts the action a double click performs at the top of a list's menu, and this menu's double
    /// click and Enter both open the panel - so the item that names them is where a person looks
    /// for it first.
    ///
    /// <b>The previews still open a plan and nothing else</b> - the reasoning that stood beside
    /// them in the markup, worded as questions rather than verbs since 2026-08-19: an item called
    /// "Stop" would be a lie to somebody's hand, because what changed that day is that the plan has
    /// a button under it, not that the menu acts.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<RowMenuEntry>> GroupsFor(MainWindow window) =>
    [
        [
            new RowMenuEntry("gui.menu.details", "gui.menu.details.gesture", () =>
                Task.FromResult(window.OpenDetailsOfPointed()))
        ],
        [
            new RowMenuEntry("gui.menu.copyName", null, () => Task.FromResult(window.Copy(Copying.Name))),
            new RowMenuEntry("gui.menu.copyDisplayName", null, () => Task.FromResult(window.Copy(Copying.DisplayName))),
            new RowMenuEntry("gui.menu.copyDescription", null, () => Task.FromResult(window.Copy(Copying.Description))),

            // THE WHOLE ENTRY, AND IT IS WHAT Ctrl+C DOES TOO - owner's request, 2026-08-13.
            // Everything the catalogue knows rather than the columns that happen to be on, because
            // somebody copying an entry into a ticket wants what there is to know, and the columns
            // that are off by default are exactly the ones too long to have kept on screen.
            //
            // AND ITS KEY IS WRITTEN BESIDE IT SINCE 2026-09-24 - UX-GUI-010 found Ctrl+C named in no
            // text of the window. The press is still decided in Shortcuts.cs, this is only its name.
            new RowMenuEntry("gui.menu.copyAll", "gui.menu.copyAll.gesture", () => Task.FromResult(window.Copy(Copying.Everything)))
        ],
        [
            new RowMenuEntry("gui.menu.previewStop", null, () => window.Preview(ActionKind.Stop)),
            new RowMenuEntry("gui.menu.previewStart", null, () => window.Preview(ActionKind.Start)),
            new RowMenuEntry("gui.menu.previewRestart", null, () => window.Preview(ActionKind.Restart)),

            // THE TWO PREVIEWS THAT CAN END A PROCESS, 2026-09-16 - backlog 374, the same order
            // as the action bar. The same question as the three above them, about the plan
            // `bws kill` builds. They stay live over any selection, because an item has no
            // tooltip to carry a reason - the refusal for more than one entry lives in Preview
            // and reaches the status line, which is how every item here reports doing nothing.
            new RowMenuEntry("gui.menu.previewForceStop", null, () => window.Preview(ActionKind.ForceStop)),
            new RowMenuEntry("gui.menu.previewForceRestart", null, () => window.Preview(ActionKind.ForceRestart))
        ]
    ];

    /// <summary>
    /// The menu itself: one item per entry, a rule between groups, each item wearing the style
    /// handed in and carrying its entry as its data context so the style can read the words.
    /// </summary>
    internal static ContextMenu Build(IReadOnlyList<IReadOnlyList<RowMenuEntry>> groups, Style item)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var menu = new ContextMenu();

        foreach (var group in groups)
        {
            // Between groups only. A rule at the top of a menu is a line that means nothing,
            // which is what the heading menu says about its own.
            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new Separator());
            }

            foreach (var entry in group)
            {
                var made = new MenuItem { DataContext = entry, Style = item };

                // The result is not read: what an item did is on the screen or on the clipboard,
                // and a menu item has nowhere to say that it did nothing. The window says it
                // through its own status line, as it does for a key press.
                made.Click += async (_, _) => await entry.Act().ConfigureAwait(true);

                menu.Items.Add(made);
            }
        }

        return menu;
    }
}
