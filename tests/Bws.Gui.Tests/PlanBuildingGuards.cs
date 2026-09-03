using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Where a plan for a whole selection is worked out, and what happens when two are asked for at
/// once. Backlog 301.
///
/// <b>THE COMMENT THAT DEFENDED THE OLD ARRANGEMENT WAS TRUE AND WAS ABOUT ANOTHER QUESTION.</b> It
/// carried a measurement of how DEEP a cascade is - 316-360 ms against 325-366 ms, a spread wider
/// than the difference - and stood where somebody would look for how MANY entries are selected.
/// That axis had never been measured. `tools/plan-probe` measured it on 2026-09-02 over 799
/// entries: a hundred selected and asked to stop cost 19-27 ms, four hundred 95-112, and the whole
/// listing 224-240. All of it is round trips to the manager, one per name, each opening the manager
/// and then the service.
///
/// <b>These are guards about a thread and about an order, not about a duration.</b> A test that
/// asserted milliseconds would be a test about whoever's machine ran it - the numbers live in the
/// probe and in the comment at <see cref="MainViewModel.PlanAsync"/>. What can be checked at a desk
/// is that the manager is not asked from the thread that draws, and that a preview overtaken by a
/// later one does not land on top of it.
/// </summary>
public sealed class PlanBuildingGuards
{
    /// <summary>
    /// The manager is asked who breaks on a background thread, never on the one drawing the window.
    ///
    /// <b>The first assertion is the probe checking that it touched the thing it is about.</b> This
    /// project spent a session on two probes that measured the wrong path and were given away by
    /// their numbers rather than by reasoning - so a guard about WHERE a question was asked starts
    /// by insisting a question was asked at all. Without it, a plan that stopped asking the manager
    /// anything would pass this silently.
    /// </summary>
    [Fact]
    public async Task The_manager_is_not_asked_about_a_cascade_on_the_thread_that_draws()
    {
        var machine = Machine();
        var window = await Showing(machine);

        var theWindowThread = WpfHost.On(() => Environment.CurrentManagedThreadId);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));

        Assert.NotEmpty(machine.CascadeAskedOn);
        Assert.DoesNotContain(theWindowThread, machine.CascadeAskedOn);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A preview overtaken by a later one is dropped rather than shown.
    ///
    /// <b>A race that did not exist until the work moved off the window's thread, which is why the
    /// counter arrives with it rather than before it.</b> While a plan was worked out in the
    /// handler, a second ask could not begin until the first had finished. A quarter of a second is
    /// long enough to ask for one thing, change your mind and ask for another - and two builds that
    /// finish in whatever order they please would put the first answer on screen over the second,
    /// under a heading somebody opened for the second.
    ///
    /// <b>Both are held inside the manager so that both are really in flight</b>, which is the only
    /// way to have two of these overlap from a test: the double is otherwise so fast that the first
    /// would be finished before the second began, and this would be a test about one preview.
    /// </summary>
    [Fact]
    public async Task A_preview_overtaken_by_a_later_one_does_not_land_on_top_of_it()
    {
        var machine = Machine();
        var window = await Showing(machine);

        machine.HoldCascade();

        var first = WpfHost.On(() => window.Preview(ActionKind.Stop));
        var second = WpfHost.On(() => window.Preview(ActionKind.Stop));

        machine.ReleaseCascade();

        Assert.False(await first);
        Assert.True(await second);

        WpfHost.On(window.Close);
    }

    private static LiveMachine Machine() => new LiveMachine(
            Rows.Entry("Spooler", "Print Spooler"),
            Rows.Entry("W32Time", "Windows Time"),
            Rows.Entry("Dnscache", "DNS Client"))
        .DependedOnBy("Spooler", "W32Time");

    /// <summary>
    /// A window looking at that machine, with two rows picked.
    ///
    /// The same arrangement <see cref="PlanFixture.Ready"/> makes, built here because these tests
    /// need to hold the machine itself - one to ask which threads reached it, one to hold it still.
    /// </summary>
    private static async Task<MainWindow> Showing(LiveMachine machine)
    {
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        WpfHost.On(() =>
        {
            window.Entries.ItemsSource = model.Rows;
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Spooler");
            window.Entries.SelectedItems.Add(model.Rows.First(row => row.ServiceName == "W32Time"));
        });

        WpfHost.Settled();

        return window;
    }
}
