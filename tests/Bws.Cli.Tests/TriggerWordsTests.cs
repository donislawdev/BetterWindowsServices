using Bws.Core;

namespace Bws.Cli.Tests;

/// <summary>
/// Every trigger kind the core names has a word of its own in the terminal.
///
/// <b>Written 2026-10-28 for backlog 627</b>, when the core named a tenth kind, Aggregate. The words are a
/// switch with a fallback, so a kind added to the core and forgotten here prints "Unknown" - the very word
/// the new name was added to replace - and nothing else would say so. CellFaceTests asks the same of the
/// window.
/// </summary>
public sealed class TriggerWordsTests
{
    public static TheoryData<TriggerKind> Named()
    {
        var named = new TheoryData<TriggerKind>();

        foreach (var kind in Enum.GetValues<TriggerKind>().Where(kind => kind != TriggerKind.Unknown))
        {
            named.Add(kind);
        }

        return named;
    }

    [Theory]
    [MemberData(nameof(Named))]
    public void Every_kind_the_core_names_has_a_word_of_its_own(TriggerKind kind)
    {
        Assert.NotEqual(Texts.Of("cli.cell.unknown"), TriggerWords.Of(kind));
    }
}
