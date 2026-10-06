using System.IO;
using System.Text;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A session with administrator rights does not read a language file beside the program, and says
/// so - security report S-9, owner's decision of 2026-10-06.
///
/// <b>A real file beside the test host</b>, under a language code no Windows has, so the lookup the
/// product uses is the one exercised - not a stand-in handed to <c>Texts.Assemble</c>. The control
/// reads the same file through the lookup a session without rights gets.
/// </summary>
public sealed class BesideLanguageGuards : IDisposable
{
    private const string Code = "qx";

    private readonly string _file = Path.Combine(AppContext.BaseDirectory, "languages", "gui." + Code + ".json");

    public BesideLanguageGuards()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, @"{""a"": ""Beside""}");
    }

    [Fact]
    public void An_administrator_session_does_not_read_the_file_beside_the_program()
    {
        var english = Texts.Assemble(Code, English, Texts.BesideFor(elevated: true));
        var beside = Texts.Assemble(Code, English, Texts.BesideFor(elevated: false));

        Assert.Equal("English", english["a"]);
        Assert.Equal("Beside", beside["a"]);
    }

    [Fact]
    public void The_file_not_read_is_named_only_when_rights_kept_it_out()
    {
        Assert.Equal(_file, Texts.BesideNotRead(elevated: true, Code, None, File.Exists));

        Assert.Null(Texts.BesideNotRead(elevated: false, Code, None, File.Exists));
        Assert.Null(Texts.BesideNotRead(elevated: true, "en", None, File.Exists));
        Assert.Null(Texts.BesideNotRead(elevated: true, Code, BuiltIn, File.Exists));
        Assert.Null(Texts.BesideNotRead(elevated: true, Code, None, _ => false));
    }

    [Fact]
    public async Task An_administrator_window_says_which_translation_it_left_out()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock())
        {
            Says = new Says { Elevated = true, RuntimeSettings = [], BesideNotRead = _file }
        };

        await model.LoadAsync();

        Assert.Equal(Texts.Of("gui.status.besideNotRead", _file), model.Says.Notice);
    }

    [Fact]
    public void Both_facts_about_an_administrator_session_stand_in_one_line()
    {
        Assert.Equal(
            Texts.Of("gui.status.runtimeSettings", "CORECLR_ENABLE_PROFILING")
                + " " + Texts.Of("gui.status.besideNotRead", _file),
            Sentences.Rights(elevated: true, ["CORECLR_ENABLE_PROFILING"], _file));

        Assert.Equal(Texts.Of("gui.status.notElevated"), Sentences.Rights(elevated: false, [], _file));
    }

    /// <summary>English built in, and nothing else - what this build ships.</summary>
    private static Stream? English(string code) =>
        code == "en" ? new MemoryStream(Encoding.UTF8.GetBytes(@"{""a"": ""English""}")) : null;

    /// <summary>A translation built in for every code - the case where the file beside would lose anyway.</summary>
    private static Func<string, Stream?> BuiltIn => _ => new MemoryStream(Encoding.UTF8.GetBytes(@"{""a"": ""Built in""}"));

    private static Func<string, Stream?> None => _ => null;

    public void Dispose() => File.Delete(_file);
}
