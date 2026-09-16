using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The questions offered under the search box, checked against the list they are offered on.
///
/// <b>Backlog 358: three of the six examples selected nothing on Drivers</b> - a driver has no
/// account and no trigger, and "services only" asked of the drivers list is a contradiction. They
/// sat in a tooltip nobody pointed at from 2026-08-13, and became one click away when the list
/// under the box was built. An example that selects nothing teaches the language wrong and blames
/// the person for it - QueryExamples.cs says so in its own header.
///
/// <b>THE CRITERION IS "NARROWS", NOT "MATCHES", AND THAT IS WHAT KEEPS THIS FROM BEING
/// CIRCULAR.</b> A test that only asked "does each example select something" would pass for any
/// fixture built to contain a match. This asks two things of every example on every scope: one
/// shown there selects SOME of the list and not ALL of it, and one hidden there selects none or
/// the whole - because <c>!type:driver</c> on Services selects every row, which teaches exactly as
/// much as selecting none. Whether the declarations are right is a question about real machines,
/// measured with <c>bws list --query</c> on 2026-09-16 and written beside each example.
///
/// <b>The fixture has the shapes a real machine has, not the shapes Rows.cs hands out.</b> Rows.Driver
/// inherits an account of LocalSystem from Rows.Entry, which 461 of 464 drivers on this machine do
/// not have - a test on that fixture would show the account example selecting a driver and never
/// find out that it cannot. So the drivers here carry no account and no trigger, and no process.
/// </summary>
public sealed class QueryExampleScopeTests
{
    [Fact]
    public async Task Every_example_shown_on_a_list_narrows_it_and_every_example_hidden_from_it_would_not()
    {
        var model = await Loaded();

        foreach (var scope in Enum.GetValues<EntryScope>())
        {
            model.ClearQuery();
            model.Scope = scope;

            var whole = model.Rows.Count;
            var shown = QueryExamples.For(scope);

            Assert.NotEmpty(shown);

            foreach (var example in QueryExamples.All)
            {
                model.QueryText = example.Query;

                var selected = model.Rows.Count;

                if (shown.Contains(example))
                {
                    Assert.True(
                        selected > 0 && selected < whole,
                        $"`{example.Query}` is shown on {scope} and selects {selected} of {whole} there - " +
                        "an example that selects nothing, or everything, teaches nothing.");
                }
                else
                {
                    Assert.True(
                        selected == 0 || selected == whole,
                        $"`{example.Query}` is hidden on {scope} but selects {selected} of {whole} there - " +
                        "if it narrows the list, it should be offered.");
                }
            }
        }
    }

    [Fact]
    public async Task The_tooltip_and_the_list_under_the_box_offer_the_same_questions_as_the_scope()
    {
        var model = await Loaded();

        foreach (var scope in Enum.GetValues<EntryScope>())
        {
            model.ClearQuery();
            model.Scope = scope;

            var expected = QueryExamples.For(scope);

            Assert.Equal(expected.Select(example => example.Label), model.Examples.Select(example => example.Label));

            // The tooltip carries every shown question and none of the hidden ones. Labels rather
            // than queries, because two queries could be substrings of each other.
            foreach (var example in QueryExamples.All)
            {
                Assert.Equal(expected.Contains(example), model.SearchTip.Contains(example.Label, StringComparison.Ordinal));
            }

            // The list under the box reads the same cut at the moment it opens, not at
            // construction - which is what lets it follow the scope switch.
            model.Suggesting.Keyboard(present: true);
            model.Suggesting.Close();

            Assert.True(model.Suggesting.Ask(string.Empty, 0, 0));
            Assert.Equal(expected.Select(example => example.Query), model.Suggesting.Offered.Select(row => row.Word));

            model.Suggesting.Close();
        }
    }

    [Fact]
    public async Task Moving_the_scope_tells_the_box_that_its_questions_and_its_tooltip_changed()
    {
        var model = await Loaded();
        var announced = new List<string>();

        model.Scope = EntryScope.Services;
        model.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        model.Scope = EntryScope.Drivers;

        // Both, because they are two bindings: the tooltip is one string bound on the box, the
        // examples are a list. A tooltip left behind would show the account example on Drivers
        // for as long as the window stayed open - the shape backlog 358 found.
        Assert.Contains(nameof(MainViewModel.Examples), announced);
        Assert.Contains(nameof(MainViewModel.SearchTip), announced);
    }

    [Fact]
    public void Every_scope_keeps_at_least_the_examples_a_driver_can_answer()
    {
        // The three every kind of entry has an answer to: a start type against a status, a start
        // type against a status the other way round, and a file on disk. Pinned so that a scope
        // added later, or a declaration edited by hand, cannot leave a list with an empty tooltip.
        foreach (var scope in Enum.GetValues<EntryScope>())
        {
            Assert.True(QueryExamples.For(scope).Count >= 3, $"{scope} offers fewer than three questions.");
        }
    }

    private static async Task<MainViewModel> Loaded()
    {
        var model = new MainViewModel(new LiveMachine(Machine()), new SteppedClock());

        await model.LoadAsync();

        return model;
    }

    /// <summary>
    /// A small machine with one entry for each question and the ordinary ones beside them.
    /// </summary>
    private static ScmEntry[] Machine() =>
    [
        // Services. Every one has an account, because every service does.
        Service("Spooler", StartType.Automatic, EntryStatus.Running, "LocalSystem"),
        Service("wuauserv", StartType.Automatic, EntryStatus.Stopped, "LocalSystem") with
        {
            ProcessId = Reading<int>.Absent(),
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present([new ServiceTrigger(TriggerKind.IpAddress, TriggerAction.Start)])
        },
        Service("Fax", StartType.Disabled, EntryStatus.Running, @"NT AUTHORITY\NetworkService"),
        Service("Ghost", StartType.Manual, EntryStatus.Stopped, @"NT AUTHORITY\LocalService") with
        {
            ProcessId = Reading<int>.Absent(),
            BinaryOnDisk = Reading<bool>.Present(false)
        },

        // Drivers. No account, no trigger, no process - the manager reads none of those for a
        // driver, and the query has to meet that rather than a fixture that says otherwise.
        Driver("disk", EntryType.KernelDriver, StartType.Boot, EntryStatus.Running),
        Driver("cdrom", EntryType.KernelDriver, StartType.Manual, EntryStatus.Running),
        Driver("NTFS", EntryType.FileSystemDriver, StartType.Boot, EntryStatus.Running),
        Driver("npf", EntryType.KernelDriver, StartType.Automatic, EntryStatus.Stopped),
        Driver("vgapnp", EntryType.KernelDriver, StartType.Disabled, EntryStatus.Running),
        Driver("sermouse", EntryType.KernelDriver, StartType.Manual, EntryStatus.Stopped) with
        {
            BinaryOnDisk = Reading<bool>.Present(false)
        }
    ];

    private static ScmEntry Service(string name, StartType start, EntryStatus status, string account) =>
        Rows.Entry(name) with
        {
            StartType = Reading<StartType>.Present(start),
            Status = status,
            Account = Reading<string>.Present(account),
            BinaryPath = Reading<string>.Present(@"C:\Windows\System32\svchost.exe -k netsvcs"),
            BinaryFile = Reading<string>.Present(@"C:\Windows\System32\svchost.exe"),
            BinaryOnDisk = Reading<bool>.Present(true)
        };

    private static ScmEntry Driver(string name, EntryType type, StartType start, EntryStatus status) =>
        Rows.Entry(name) with
        {
            EntryType = type,
            StartType = Reading<StartType>.Present(start),
            Status = status,
            ProcessId = Reading<int>.Absent(),
            AcceptsStop = Reading<bool>.Absent(),
            Account = Reading<string>.Absent(),
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
            BinaryPath = Reading<string>.Present(@"C:\Windows\System32\drivers\" + name + ".sys"),
            BinaryFile = Reading<string>.Present(@"C:\Windows\System32\drivers\" + name + ".sys"),
            BinaryOnDisk = Reading<bool>.Present(true)
        };
}
