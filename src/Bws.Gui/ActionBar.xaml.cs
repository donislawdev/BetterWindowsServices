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
        Picked(0, onlyDrivers: false);

        OfferTheSettings();
    }

    /// <summary>
    /// The startup settings under the button, one item per setting, in the order the command line
    /// offers the words.
    ///
    /// <b>A LIST SINCE 2026-09-24, WHEN THE FOURTH ARRIVED - "Automatic (delayed)", backlog 231.</b>
    /// Three items and three handlers were written out in the markup until then, which is GUI rule
    /// 8 of the project notes at its threshold: a set differing only by value is data rendered in a
    /// loop, already at three. The shape is RowMenu's - items added one by one, so a guard reading
    /// <c>Items.OfType&lt;MenuItem&gt;()</c> gets the real items before the menu has ever opened
    /// (`docs/10` trap 19), and each item a real MenuItem with its own automation peer rather than
    /// a container a grouped menu hides (trap 7). The handler is wired here because a style in a
    /// resource dictionary has nowhere to put one.
    ///
    /// <b>The words come from CellFaces.SettingLabel</b>, the one place the cell, this menu, the
    /// step and the button name a setting - so "Automatic (delayed)" here is the letters a delayed
    /// entry wears in the list.
    ///
    /// <b>Filled by <see cref="StartSettingChoice.Offer"/> since 2026-09-24</b>, which fills the
    /// same four under "Set startup type" on a row - UX-GUI-018.
    /// </summary>
    private void OfferTheSettings() =>
        StartSettingChoice.Offer(StartTypeMenu, (Style)FindResource("RowMenuItem"), setting =>
        {
            StartTypeRequest?.Invoke(this, new StartTypeAsked(setting));

            return Task.CompletedTask;
        });

    /// <summary>
    /// Whether carrying out what these buttons ask for needs administrator rights this session does
    /// not have - UX-GUI-004 (b). The window says so once, from the session's rights, and the six
    /// write buttons then wear a shield (RightsMark in Actions.xaml) and say it in their tooltips.
    ///
    /// <b>Before the plan rather than only under it.</b> A window without rights looked ready to
    /// act: every button live, every plan built in full, and the one sentence about rights waiting
    /// under the plan beside a grey button. A dependency property because the shield is decided by
    /// a trigger in the theme, and a trigger can only watch what tells it when it changes.
    /// </summary>
    public bool NeedsRights
    {
        get => (bool)GetValue(NeedsRightsProperty);
        set => SetValue(NeedsRightsProperty, value);
    }

    /// <summary>The property behind <see cref="NeedsRights"/>.</summary>
    public static readonly DependencyProperty NeedsRightsProperty =
        DependencyProperty.Register(nameof(NeedsRights), typeof(bool), typeof(ActionBar), new PropertyMetadata(false));

    /// <summary>Somebody asked to see what one of the five would do to the rows they picked.</summary>
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
    /// How many ENTRIES the picked rows stand for, which decides which buttons mean anything.
    ///
    /// <b>Entries rather than rows since 2026-09-16, because two of the buttons take exactly one.</b>
    /// A folded per-user family is one row standing for a template and every session's copy, and
    /// the window is the one thing that knows how many that is - it hands the count over from the
    /// same place its plans are built, so the bar and the plan can never disagree about what a row
    /// stands for. For the four buttons that take any number, "more than none" reads the same
    /// either way.
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
    ///
    /// <b>DRIVERS ALONE TURN THE BAR OFF, SINCE 2026-09-23 - UX-GUI-003.</b> The core refuses a
    /// driver every one of these asks (PlanBuilder, before it looks at the kind), and the bar used
    /// to offer all six over one anyway - measured on a window without rights, each opened a plan
    /// of nothing. The reason is the tool rather than the selection, so it gets its own sentence:
    /// "pick one or more entries" would send somebody to pick what they already picked. Drivers
    /// beside a service leave the bar on, and the plan says what happens to each.
    /// </summary>
    internal void Picked(int entries, bool onlyDrivers)
    {
        var anything = entries > 0 && !onlyDrivers;
        var off = onlyDrivers ? Texts.Of("gui.action.onlyDrivers") : Texts.Of("gui.action.needsPick");

        // THE KEYS ARE WRITTEN OUT HERE RATHER THAN PASSED AS ONE, and a guard is what decided
        // that: TextKeyGuards checks that every declared sentence reaches a screen by looking for
        // its key in the source, so a key travelling as a variable is invisible to it and all three
        // of these read as orphans. Sentences.Admissions carries the same note for the same reason.
        Set(StopButton, anything, anything ? Texts.Of("gui.action.stop.hint") : off);
        Set(StartButton, anything, anything ? Texts.Of("gui.action.start.hint") : off);
        Set(RestartButton, anything, anything ? Texts.Of("gui.action.restart.hint") : off);
        Set(StartTypeButton, anything, anything ? Texts.Of("gui.action.startType.hint") : off);

        // ONE ENTRY AND NOT "ANYTHING" - `docs/ANALIZA-FORCE` 15.6, and the paragraph over these
        // two buttons in the markup. Three answers rather than two, because "pick one" over an
        // empty selection and "pick ONE" over five are different instructions. The refusal itself
        // stands in the window's Preview, where the row menu arrives as well - this is the reason
        // put where a person is looking. Over drivers alone the driver sentence wins, because
        // picking one of them would still leave nothing to do.
        var one = entries == 1 && !onlyDrivers;
        var many = entries > 1 && !onlyDrivers;

        Set(ForceStopButton, one, one ? Texts.Of("gui.action.forceStop.hint") : many ? Texts.Of("gui.action.force.onlyOne") : off);
        Set(ForceRestartButton, one, one ? Texts.Of("gui.action.forceRestart.hint") : many ? Texts.Of("gui.action.force.onlyOne") : off);
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
    internal Button ForceStop => ForceStopButton;

    /// <inheritdoc cref="Stop"/>
    internal Button ForceRestart => ForceRestartButton;

    /// <inheritdoc cref="Stop"/>
    internal Button Refresh => RefreshButton;

    /// <inheritdoc cref="Stop"/>
    internal Button StartType => StartTypeButton;

    /// <inheritdoc cref="Stop"/>
    internal Button OverviewBack => OverviewButton;

    /// <summary>
    /// Turns a button on or off and puts on it what it would do, or why it will not.
    ///
    /// <b>One method for all six since 2026-09-23</b>, when the single-entry buttons lost their own
    /// SetOne. The choice of sentence moved to the caller, where the three reasons for "off" -
    /// nothing picked, drivers alone, more than one for a forcing ask - are decided side by side,
    /// and an empty selection still shares its sentence across the bar so the bar never disagrees
    /// with itself.
    /// </summary>
    private void Set(Button button, bool on, string saying)
    {
        button.IsEnabled = on;

        // The shield in words, on a button that would open a plan this session cannot carry out -
        // a screen reader gets no node for the shield, and the plan can still be shown. Never on
        // an off button: its sentence is why it is off, and rights are not that reason.
        button.ToolTip = on && NeedsRights ? saying + " " + Texts.Of("gui.action.needsRights") : saying;
    }

    private void StopAsked(object sender, RoutedEventArgs e) =>
        PreviewRequest?.Invoke(this, new PreviewAsked(ActionKind.Stop));

    private void StartAsked(object sender, RoutedEventArgs e) =>
        PreviewRequest?.Invoke(this, new PreviewAsked(ActionKind.Start));

    private void RestartAsked(object sender, RoutedEventArgs e) =>
        PreviewRequest?.Invoke(this, new PreviewAsked(ActionKind.Restart));

    private void ForceStopAsked(object sender, RoutedEventArgs e) =>
        PreviewRequest?.Invoke(this, new PreviewAsked(ActionKind.ForceStop));

    private void ForceRestartAsked(object sender, RoutedEventArgs e) =>
        PreviewRequest?.Invoke(this, new PreviewAsked(ActionKind.ForceRestart));

    private void RefreshAsked(object sender, RoutedEventArgs e) =>
        RefreshRequest?.Invoke(this, EventArgs.Empty);

    private void ExportAsked(object sender, RoutedEventArgs e) =>
        ExportRequest?.Invoke(this, EventArgs.Empty);

    private void OverviewAsked(object sender, RoutedEventArgs e) =>
        OverviewRequest?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Opens the four settings under the button, the same way the column picker opens its list.
    ///
    /// <b>A left click on a context menu, which is unusual and is the point</b> - the menu is the
    /// surface the theme already dresses, and the button is the discoverability. ButtonMenu carries
    /// the whole argument.
    /// </summary>
    private void StartTypeAsked(object sender, RoutedEventArgs e) => ButtonMenu.OpenUnder(StartTypeButton);
}

/// <summary>Which of the five somebody asked to see the effects of.</summary>
internal sealed class PreviewAsked(ActionKind kind) : EventArgs
{
    /// <summary>The action, in the words a person used rather than the steps it becomes.</summary>
    internal ActionKind Kind { get; } = kind;
}

/// <summary>Which startup setting somebody asked to see the effects of writing.</summary>
internal sealed class StartTypeAsked(StartSetting setting) : EventArgs
{
    /// <summary>The setting a plan would write.</summary>
    internal StartSetting Setting { get; } = setting;
}
