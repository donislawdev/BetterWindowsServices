namespace Bws.Architecture.Tests;

/// <summary>
/// One thing a ceiling is held over - method length in src/, parameter count in tests/, and so on -
/// with the two numbers that hold it and what to do when either goes red.
///
/// <b>Both numbers are pinned to the measurement EXACTLY</b>, owner's decision 2026-09-23. The file
/// ratchet this replaces allowed a hundred lines of slack, and its own history records the slack
/// letting a loose ceiling through at least five times before a mutation run noticed. A ceiling
/// above the measurement grants room nobody decided to grant. So one test per number, and its
/// message says which way it went: over means split the thing, under means lower the number to
/// what it now is - the routine door, one line.
///
/// <b>The second number is the crowd</b>: how many stand close to the ceiling. The ceiling watches
/// the worst item and is blind to several climbing together, none of them a record - which is the
/// shape a codebase actually drifts into. Close means 70% of the ceiling for the axes with a wide
/// range, and a fixed number for depth and parameters, where the range is 0 to 9 and a percentage
/// would reshape the band every time the ceiling moved by one.
/// </summary>
internal sealed record ShapeAxis(
    string Name,
    string Unit,
    string Constant,
    int Ceiling,
    string CrowdConstant,
    int Crowd,
    Func<int, bool> Near,
    string Remedy,
    Func<IEnumerable<ShapeItem>> Measure)
{
    public override string ToString() => Name;

    /// <summary>Close means at least this share of the ceiling.</summary>
    internal static Func<int, bool> Share(int ceiling, double share) => value => value >= ceiling * share;

    /// <summary>Close means at least this many, whatever the ceiling is.</summary>
    internal static Func<int, bool> AtLeast(int floor) => value => value >= floor;

    /// <summary>Everything this axis measures, largest first, with the exemptions left out.</summary>
    internal IReadOnlyList<ShapeItem> Items() =>
        [.. Measure()
            .Where(item => !ShapeCeilings.Exemptions.Any(exemption => exemption.Axis == Name && exemption.Unit == item.Name))
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Where, StringComparer.Ordinal)];

    /// <summary>Null when the ceiling is the measurement, otherwise the sentence saying what to do.</summary>
    internal string? CeilingVerdict() => OverVerdict() ?? UnderVerdict();

    /// <summary>Null unless something grew past the ceiling.</summary>
    internal string? OverVerdict()
    {
        var items = Items();
        if (Largest(items).Value <= Ceiling)
        {
            return null;
        }

        // Ten at most: past that the list is a ceiling set wrongly, not code that grew.
        var over = items.TakeWhile(item => item.Value > Ceiling).Take(10).Select(item => $"  {item.Value,5}  {item.Where}");
        return $"{Name}: {items[0].Where} measures {items[0].Value} {Unit}, over the ceiling of {Ceiling}. " +
            $"{Remedy} - do not raise the number, raising it is the owner's decision. Over the ceiling:" +
            Environment.NewLine + string.Join(Environment.NewLine, over);
    }

    /// <summary>Null unless the ceiling has been left standing above the largest thing under it.</summary>
    internal string? UnderVerdict()
    {
        var largest = Largest(Items());

        return largest.Value < Ceiling
            ? $"{Name}: the ceiling is {Ceiling} and the largest is now {largest.Where} at {largest.Value}. " +
                $"Lower {Constant} to {largest.Value} in this change - a ceiling above the measurement " +
                "grants room nobody decided to grant, and it may only ever go down."
            : null;
    }

    private static ShapeItem Largest(IReadOnlyList<ShapeItem> items) =>
        items.Count == 0 ? new ShapeItem("(nothing)", "(nothing)", 0) : items[0];

    /// <summary>Null when the crowd count is the measurement, otherwise the sentence saying what to do.</summary>
    internal string? CrowdVerdict()
    {
        var near = Items().Where(item => Near(item.Value)).ToArray();
        var list = Environment.NewLine + string.Join(Environment.NewLine, near.Select(item => $"  {item.Value,5}  {item.Where}"));

        if (near.Length > Crowd)
        {
            return $"{Name}: {near.Length} stand close to the ceiling of {Ceiling} {Unit}, against {Crowd} when this " +
                "was set. Nothing crossed the ceiling - something climbed towards it, which is the shape nobody " +
                "notices. Bring one back down before adding another, raising the count is the owner's decision:" + list;
        }

        return near.Length < Crowd
            ? $"{Name}: {near.Length} stand close to the ceiling now, and the count still says {Crowd}. " +
                $"Lower {CrowdConstant} to {near.Length} in this change:" + list
            : null;
    }
}

/// <summary>One measured thing: its name as a person searches for it, where it is, and how much.</summary>
internal sealed record ShapeItem(string Name, string Where, int Value);

/// <summary>
/// A named exception to one axis, with its reason. The list may only get shorter - an exemption for
/// something that no longer stands over the ceiling is refused as stale, so it cannot linger as a
/// free allowance after the work that justified it is done.
/// </summary>
internal sealed record ShapeExemption(string Unit, string Axis, string Reason);
