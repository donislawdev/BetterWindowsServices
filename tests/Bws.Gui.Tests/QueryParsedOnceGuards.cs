using System.Runtime.ExceptionServices;
using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// One keystroke parses the query once - G-4 of the external performance report of 2026-09-28,
/// which counted eighteen parses of the same text: one to narrow the list, one for the sentence
/// about the scope and one for each of the sixteen filter chips.
///
/// <b>Counted through the one thing a parse leaves behind that can be seen from outside</b>, and
/// without a counter written into the product for a test. A pattern with a backreference is valid
/// and refused by the linear engine, so building it throws and catches (QueryPatterns.TryPattern) -
/// S-10 of the same report - and every parse of the line throws the same number of times. The
/// exceptions are counted only in this test's own flow of work (an AsyncLocal), because the event
/// is heard by the whole process and other tests run beside this one.
/// </summary>
public sealed class QueryParsedOnceGuards
{
    private const string Refused = "/(s)\\1/";

    [Fact]
    public async Task One_keystroke_parses_the_query_once_and_a_tick_does_not_parse_it_again()
    {
        var once = await Thrown(() =>
        {
            QueryAsTyped.Of(Refused);

            return Task.CompletedTask;
        });

        // A GUARD SATISFIED BY ABSENCE: if the engine ever supports backreferences, nothing throws,
        // both counts are zero and equal, and this would pass while counting nothing.
        Assert.True(once > 0, "The pattern is no longer refused by the linear engine, so nothing here is counted - pick a construct it refuses.");

        var machine = new LiveMachine(Rows.Entry("Spooler", "Print Spooler"), Rows.Entry("W32Time", "Windows Time"));
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        // The chips are read the way their bindings read them after the text changed.
        var typed = await Thrown(() =>
        {
            model.QueryText = Refused;
            Lit(model);

            return Task.CompletedTask;
        });

        Assert.Equal(once, typed);

        // A tick that finds a service moved re-runs the query over the list and asks the chips
        // again - over the same text, so over the same parse.
        machine.Stop("Spooler");

        var ticked = await Thrown(async () =>
        {
            await model.RefreshAsync();
            Lit(model);
        });

        Assert.Equal(0, ticked);
    }

    private static void Lit(MainViewModel model)
    {
        foreach (var chip in model.Filters)
        {
            _ = chip.IsOn;
        }
    }

    /// <summary>
    /// How many times the linear engine refused a pattern while this work ran, in this flow only.
    /// </summary>
    private static async Task<int> Thrown(Func<Task> work)
    {
        var mine = new AsyncLocal<bool> { Value = true };
        var count = 0;

        void Seen(object? sender, FirstChanceExceptionEventArgs thrown)
        {
            if (mine.Value && thrown.Exception is NotSupportedException)
            {
                Interlocked.Increment(ref count);
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += Seen;

        try
        {
            await work();
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= Seen;
        }

        return count;
    }
}
