using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// Whole entries, generated, with every field the manager hands over varied in all four of its states -
/// for the property that writes a machine to disk and reads it back.
///
/// <b>Its own file rather than more of <see cref="Machines"/></b>, and the reason is the same one that
/// file gives for being apart from the diff properties: Machines varies only what a comparison can see,
/// on purpose, so that a property about the engine checks the engine. A round trip through a file has
/// the opposite need - every field, because the field nobody varied is the one a writer and a reader
/// can disagree about for a year without anybody noticing.
///
/// <b>Text is what a registry string can hold</b>: quotes, backslashes, control characters, characters
/// past the first plane as whole pairs, a byte order mark, a line separator. Never a lone surrogate -
/// backlog 590 records that one comes back as U+FFFD and why that is left as it is.
///
/// <b>Beside each entry, what the two passes will be told about it</b>: the signature, version and hash
/// the inspector answers for its file, and who the manager says depends on it. The passes then fill
/// those fields the way they fill them on a real machine, rather than the generator writing an answer
/// no pass would ever produce.
/// </summary>
internal static class WholeEntries
{
    /// <summary>Names a manager can hold, none two that differ only in case.</summary>
    private static readonly string[] Names =
    [
        "Spooler", "BFE", "W32Time", "a b", "x$y_z.1", "\u0141\u00f3d\u017a", "Twin",
        "AVeryLongServiceNameThatGoesOnLongerThanAnyColumnIsWideAndThenSomeMoreToBeSure"
    ];

    private static readonly Gen<string> Text = Gen.Frequency(
        (4, Gen.String[Gen.Char['a', 'z'], 0, 12]),
        (3, Gen.String[Gen.OneOfConst(
            '"', '\\', '/', '\t', '\n', '\r', '\0', '\u001f', '\u007f', '\u0105', '\u0141', '\u00df',
            '\u200b', '\ufeff', '<', '&', ' ', '\u2028', ',', ':', '{', '}', '[', ']'), 1, 10]),
        (1, Gen.OneOfConst(
            "\ud83d\ude00", "C:\\Program Files\\Some Vendor\\x.exe", "%SystemRoot%\\system32\\svchost.exe -k netsvcs -p",
            "@%SystemRoot%\\system32\\x.dll,-100", "\\??\\C:\\Windows\\x.sys", "", " ", "null", "\"quoted\"")));

    private static readonly Gen<IReadOnlyList<string>> Words =
        Gen.OneOf(Text, Gen.OneOfConst("+NetBIOSGroup", "RpcSs", "rpcss")).List[0, 4].Select(list => (IReadOnlyList<string>)list);

    private static Gen<Reading<T>> Read<T>(Gen<T> value) => Gen.Frequency(
        (5, value.Select(Reading<T>.Present)),
        (1, Gen.Const(Reading<T>.Absent())),
        (1, Gen.Const(Reading<T>.NotRead())),
        (1, Gen.Select(Gen.Int[1, 2000], Text).Select(refused => Reading<T>.Denied(refused.Item1, refused.Item2))));

    private static Gen<T> AnyOf<T>() where T : struct, Enum => Gen.OneOfConst(Enum.GetValues<T>());

    private static readonly Gen<IReadOnlyList<ServiceTrigger>> Triggers =
        Gen.Select(AnyOf<TriggerKind>(), AnyOf<TriggerAction>())
            .Select(pair => new ServiceTrigger(pair.Item1, pair.Item2))
            .List[0, 3]
            .Select(list => (IReadOnlyList<ServiceTrigger>)list);

    private static readonly Gen<ProcessMemory> Memory =
        Gen.Select(Gen.Long[0, long.MaxValue], Gen.Long[0, long.MaxValue], Gen.Int[1, 60])
            .Select(drawn => new ProcessMemory(drawn.Item1, drawn.Item2, drawn.Item3));

    private static readonly Gen<BinarySignature> Signature =
        Gen.Select(AnyOf<SignatureStatus>(), Gen.Int, Text.Select(string? (text) => text.Length == 0 ? null : text))
            .Select(drawn => new BinarySignature(drawn.Item1, drawn.Item2, drawn.Item3));

    /// <summary>The fields the listing and the detail reading fill, everything but the name.</summary>
    private static readonly Gen<Func<string, ScmEntry>> Shape =
        Gen.Select(
                Gen.Select(Text, Read(Text), AnyOf<EntryType>(), AnyOf<PerUserRole>(), AnyOf<EntryStatus>(), Read(Gen.Int), Gen.Bool),
                Gen.Select(Read(Gen.Bool), Read(AnyOf<StartType>()), Read(Gen.Bool), Read(Text), Read(Words), Read(Triggers)),
                Gen.Select(Read(Text), Read(Text), Read(Gen.Bool), Read(AnyOf<ErrorControl>()), Read(Text)),
                Gen.Select(Read(Words), Read(AnyOf<ServiceSidType>()), Read(Text), Read(Memory)))
            .Select(drawn => (Func<string, ScmEntry>)(name =>
                Secured(Filed(Set(Headed(name, drawn.Item1), drawn.Item2), drawn.Item3), drawn.Item4)));

    /// <summary>
    /// The name and what the listing hands over, on top of the fixture. The three fields the passes fill
    /// start never asked, as they do on a real machine before the passes run.
    /// </summary>
    private static ScmEntry Headed(
        string name,
        (string Display, Reading<string> Description, EntryType Type, PerUserRole Role, EntryStatus Status, Reading<int> Process, bool Recognizer) head) =>
        Entries.Any with
        {
            ServiceName = name,
            DisplayName = head.Display,
            Description = head.Description,
            EntryType = head.Type,
            RecognizerDriver = head.Recognizer,
            PerUserRole = head.Role,
            Status = head.Status,
            ProcessId = head.Process,
            RequiredBy = Reading<IReadOnlyList<string>>.NotRead(),
            Signature = Reading<BinarySignature>.NotRead(),
            FileVersion = Reading<string>.NotRead(),
            BinaryHash = Reading<string>.NotRead()
        };

    private static ScmEntry Set(
        ScmEntry entry,
        (Reading<bool> Accepts, Reading<StartType> Start, Reading<bool> Delayed, Reading<string> Account, Reading<IReadOnlyList<string>> DependsOn, Reading<IReadOnlyList<ServiceTrigger>> Triggers) settings) =>
        entry with
        {
            AcceptsStop = settings.Accepts,
            StartType = settings.Start,
            DelayedAuto = settings.Delayed,
            Account = settings.Account,
            DependsOn = settings.DependsOn,
            Triggers = settings.Triggers
        };

    private static ScmEntry Filed(
        ScmEntry entry,
        (Reading<string> Path, Reading<string> File, Reading<bool> OnDisk, Reading<ErrorControl> Errors, Reading<string> Group) files) =>
        entry with
        {
            BinaryPath = files.Path,
            BinaryFile = files.File,
            BinaryOnDisk = files.OnDisk,
            ErrorControl = files.Errors,
            LoadOrderGroup = files.Group
        };

    private static ScmEntry Secured(
        ScmEntry entry,
        (Reading<IReadOnlyList<string>> Privileges, Reading<ServiceSidType> Sid, Reading<string> Descriptor, Reading<ProcessMemory> Memory) security) =>
        entry with
        {
            RequiredPrivileges = security.Privileges,
            SidType = security.Sid,
            SecurityDescriptor = security.Descriptor,
            Memory = security.Memory
        };

    /// <summary>
    /// What the two passes will be told about one entry. The manager's answer about dependants has
    /// three states, not four: it answers, finds nobody, or refuses - "never asked" is not something it
    /// can say.
    /// </summary>
    private static readonly Gen<Told> Answers =
        Gen.Select(Read(Signature), Read(Text), Read(Text), Read(Gen.OneOfConst(Names).List[0, 3].Select(list => (IReadOnlyList<string>)list)))
            .Select(drawn => new Told(
                drawn.Item1,
                drawn.Item2,
                drawn.Item3,
                drawn.Item4.Outcome == ReadOutcome.NotRead ? Reading<IReadOnlyList<string>>.Absent() : drawn.Item4));

    /// <summary>One to eight entries, each with what the passes will be told about it.</summary>
    internal static readonly Gen<IReadOnlyList<Listed>> Any =
        Gen.Select(Shape, Answers).List[1, Names.Length]
            .Select(drawn => (IReadOnlyList<Listed>)[.. drawn.Select((one, at) => new Listed(one.Item1(Names[at]), one.Item2))]);
}

/// <summary>What the inspector says about an entry's file, and what the manager says depends on it.</summary>
internal sealed record Told(
    Reading<BinarySignature> Signature,
    Reading<string> Version,
    Reading<string> Hash,
    Reading<IReadOnlyList<string>> Dependents);

/// <summary>An entry as the listing gives it, with what the passes will be told about it.</summary>
internal sealed record Listed(ScmEntry Entry, Told Told)
{
    public override string ToString() => $"{Entry.ServiceName} ({Entry.DisplayName.Length} chars of display name)";
}
