using System.Windows;
using System.Windows.Controls;
using Bws.Core.Planning;

namespace Bws.Gui;

/// <summary>
/// The bar of things somebody can do to the rows they picked, which does as little as a part of a
/// window can.
///
/// <b>It asks and it does not answer.</b> What a plan would be is worked out in the core, what it
/// says is decided in the view models, and which rows are picked is read from the grid at the
/// moment somebody presses - so what is left here is that a button was pressed and how many rows
/// there were when it was.
///
/// <b>One event carrying which action rather than three events</b>, because the three buttons
/// differ in exactly one thing and the window answers all three the same way. Three would be three
/// subscriptions in the window that could drift apart.
///
/// <b>The kind travels in an EventArgs of its own rather than as the event's type argument</b>, and
/// an analyser is what decided that: a handler whose second parameter is not an EventArgs is
/// refused across this whole product. The type below is four lines and says what it carries.
/// </summary>
public partial class ActionBar : UserControl
{
    public ActionBar()
    {
        InitializeComponent();

        // NOTHING IS PICKED WHEN A WINDOW OPENS, so the bar starts in the state it will be in for
        // as long as somebody is reading the list rather than acting on it. Without this line the
        // three buttons are live over an empty selection until the first click anywhere.
        Picked(0);
    }

    /// <summary>Somebody asked to see what one of the three would do to the rows they picked.</summary>
    internal event EventHandler<PreviewAsked>? PreviewRequest;

    /// <summary>Somebody asked for the machine to be read again.</summary>
    internal event EventHandler? RefreshRequest;

    /// <summary>Somebody asked for what the list shows to be written to a file.</summary>
    internal event EventHandler? ExportRequest;

    /// <summary>Somebody asked to see what setting a start type would do.</summary>
    internal event EventHandler<StartTypeAsked>? StartTypeRequest;

    /// <summary>Somebody asked for the machine overview back - `G`.</summary>
    internal event EventHandler? OverviewRequest;

    /// <summary>
    /// How many rows are picked, which decides whether the three buttons mean anything.
    ///
    /// <b>Told rather than bound, and that is a repair this window has already paid for.</b>
    /// Nothing here binds to the grid's selection: a binding into a list that reconciles itself
    /// once a second is another party in the middle of `A10`, and SelectedItem bound two way is
    /// exactly what broke the window journey on 2026-08-18. An event that fires when a person
    /// changes the selection is a different animal from a binding that watches the list.
    ///
    /// <b>The reason a button is off is put ON the button</b>, because a control that refuses
    /// without saying why is the fault the plan panel had until this same packet. WPF will not
    /// show a tooltip on a disabled control unless the markup says so - the attribute is next to
    /// each button.
    /// </summary>
    internal void Picked(int count)
    {
        var anything = count > 0;

        // THE KEYS ARE WRITTEN OUT HERE RATHER THAN PASSED AS ONE, and a guard is what decided
        // that: TextKeyGuards checks that every declared sentence reaches a screen by looking for
        // its key in the source, so a key travelling as a variable is invisible to it and all three
        // of these read as orphans. Sentences.Admissions carries the same note for the same reason.
        Set(StopButton, anything, Texts.Of("gui.action.stop.hint"));
        Set(StartButton, anything, Texts.Of("gui.action.start.hint"));
        Set(RestartButton, anything, Texts.Of("gui.action.restart.hint"));
        Set(StartTypeButton, anything, Texts.Of("gui.action.startType.hint"));
    }

    /// <summary>Whether the buttons are live, for a test that would otherwise have to guess.</summary>
    internal bool Offering => StopButton.IsEnabled;

    /// <summary>
    /// The buttons themselves, so that a guard can press one rather than call what it would call.
    ///
    /// <b>The press is the part worth holding.</b> What happens afterwards is checked elsewhere at
    /// length, and none of it means anything if the click never arrives - which is the failure this
    /// window has met most often, a mechanism that works in every part and is wired to nothing.
    /// </summary>
    internal Button Stop => StopButton;

    /// <inheritdoc cref="Stop"/>
    internal Button Start => StartButton;

    /// <inheritdoc cref="Stop"/>
    internal Button Restart => RestartButton;

    /// <inheritdoc cref="Stop"/>
    internal Button Refresh => RefreshButton;

    /// <inheritdoc cref="Stop"/>
    internal Button Export => ExportButton;

    /// <inheritdoc cref="Stop"/>
    internal Button StartType => StartTypeButton;

    /// <inheritdoc cref="Stop"/>
    internal Button OverviewBack => OverviewButton;

    private static void Set(Button button, bool anything, string saying)
    {
        button.IsEnabled = anything;

        button.ToolTip = anything ? saying : Texts.Of("gui.action.needsPick");
    }

    private void StopAsked(object sender, RoutedEventArgs e) =>
        PreviewRequest?.Invoke(this, new PreviewAsked(ActionKind.Stop));

    private void StartAsked(object sender, RoutedEventArgs e) =>
        PreviewRequest?.Invoke(this, new PreviewAsked(ActionKind.Start));

    private void RestartAsked(object sender, RoutedEventArgs e) =>
        PreviewRequest?.Invoke(this, new PreviewAsked(ActionKind.Restart));

    private void RefreshAsked(object sender, RoutedEventArgs e) =>
        RefreshRequest?.Invoke(this, EventArgs.Empty);

    private void ExportAsked(object sender, RoutedEventArgs e) =>
        ExportRequest?.Invoke(this, EventArgs.Empty);

    private void OverviewAsked(object sender, RoutedEventArgs e) =>
        OverviewRequest?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Opens the three types under the button, the same way the column picker opens its list.
    ///
    /// <b>A left click on a context menu, which is unusual and is the point</b> - the menu is the
    /// surface the theme already dresses, and the button is the discoverability. ButtonMenu carries
    /// the whole argument.
    /// </summary>
    private void StartTypeAsked(object sender, RoutedEventArgs e) => ButtonMenu.OpenUnder(StartTypeButton);

    private void SetAutomatic(object sender, RoutedEventArgs e) => Wanted(Core.StartType.Automatic);

    private void SetManual(object sender, RoutedEventArgs e) => Wanted(Core.StartType.Manual);

    private void SetDisabled(object sender, RoutedEventArgs e) => Wanted(Core.StartType.Disabled);

    private void Wanted(Core.StartType type) =>
        StartTypeRequest?.Invoke(this, new StartTypeAsked(type));
}

/// <summary>Which of the three somebody asked to see the effects of.</summary>
internal sealed class PreviewAsked(ActionKind kind) : EventArgs
{
    /// <summary>The action, in the words a person used rather than the steps it becomes.</summary>
    internal ActionKind Kind { get; } = kind;
}

/// <summary>Which start type somebody asked to see the effects of setting.</summary>
internal sealed class StartTypeAsked(Core.StartType type) : EventArgs
{
    /// <summary>The type a plan would write, in the words of the catalogue rather than the manager.</summary>
    internal Core.StartType Type { get; } = type;
}
