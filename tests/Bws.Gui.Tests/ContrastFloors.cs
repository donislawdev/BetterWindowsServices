namespace Bws.Gui.Tests;

/// <summary>
/// What each brush has to clear, against what, and why - the table <see cref="ContrastGuards"/>
/// measures by.
///
/// <b>Its own file since 2026-09-16, the day ContrastGuards stood at 529 lines and was the third
/// test file over a ratchet that allows two.</b> The table is half comments on purpose: every
/// floor that is not WCAG's is ours and carries the argument for its number, and that prose is
/// what the seam had to keep in one piece. The tests stayed where they were.
///
/// <b>"Against what" is the second column, and it is new the same day.</b> Until then every brush
/// was measured against the window, and backlog 286 had said since 2026-09-02 that half the text
/// stands on the panel, which is lighter. A floor names the surface it is measured against when
/// that is not the window, and ContrastGuards resolves the surface out of the theme by name - so
/// a brush that only has to be tellable ON THE PANEL is not passed for being tellable on the
/// window, which is what would have happened to the hover for the panel surfaces: 2.22 against
/// the window, 1.70 where it is drawn.
/// </summary>
internal static class ContrastFloors
{
    /// <summary>WCAG 2.2 SC 1.4.3, text against its background.</summary>
    internal const double ForText = 4.5;

    /// <summary>WCAG 2.2 SC 1.4.11, a component state that has to be tellable from another.</summary>
    internal const double ForState = 3.0;

    /// <summary>
    /// A ratio to clear and what it is measured against: the window when <see cref="Against"/> is
    /// null, otherwise a surface the theme declares under that name.
    /// </summary>
    internal sealed record Floor(double Ratio, string? Against = null);

    /// <summary>
    /// What each brush has to clear, against what, and why. <b>A brush missing from this table
    /// fails the last contrast test in ContrastGuards</b>, so a new colour cannot slip in without
    /// somebody deciding what it answers to - which is the whole reason the table is here rather
    /// than a rule about names. An entry that names no surface is measured against the window.
    ///
    /// The two floors that are not WCAG are marked as ours. SC 1.4.11 covers states needed to
    /// IDENTIFY a component: telling the selected row from the others qualifies, a pointer
    /// saying where the pointer already is does not. Calling those 3.0 would be borrowing
    /// authority the standard does not lend.
    /// </summary>
    internal static readonly Dictionary<string, Floor> Required = new(StringComparer.Ordinal)
    {
        ["MeaningUntrusted"] = new(ForText),
        ["MeaningWarning"] = new(ForText),
        ["MeaningRunning"] = new(ForText),
        ["MeaningUnknown"] = new(ForText),

        // THE THREE THAT ARE DRAWN ON THE PANEL AS WELL AS ON THE WINDOW, 2026-09-16, and the panel
        // is the check that can fail: it is lighter than the window, so light text reads lower on
        // it. Measured through this project's own arithmetic - the subdued grey 6.23 on the window
        // and 4.76 on the panel, where the details panel's labels have stood since 2026-09-16 -
        // white 16.29 and 12.45 - the refusal red 6.39 and 4.88, where the plan footer's danger
        // line stands since the same day. A brush that clears the lighter surface clears the
        // darker one by more, so one entry covers both places it is drawn.
        ["MeaningRejected"] = new(ForText, Against: "SurfacePanel"),
        ["TextSubdued"] = new(ForText, Against: "SurfacePanel"),
        ["TextOnSurface"] = new(ForText, Against: "SurfacePanel"),
        ["SurfaceSelected"] = new(ForState),

        // Ours, not WCAG's. Chosen at the point the state stops being visible at all: the
        // values these replaced measured 1.14 and 1.30.
        ["SurfaceHover"] = new(1.5),
        ["SurfaceChanged"] = new(1.8),

        // THE SAME HOVER FOR THE PANEL SURFACES, 2026-09-16, AND THE FIRST ENTRY MEASURED AGAINST
        // SOMETHING OTHER THAN THE WINDOW. SurfaceHover reads 1.70 on the window and 1.30 on the
        // panel - under the floor above, and 1.30 is exactly the value that floor was set to refuse.
        // The list under the search box, the close mark and the copy buttons of the plan sheet all
        // hovered in it, so on every panel surface the pointer was invisible. This brush reads 1.70
        // on the panel, the same distance the window's hover keeps, and against the window it would
        // pass at 2.22 for the wrong reason - which is why the entry names its surface. White on it
        // measures 7.34. The subdued grey on it measures 2.81, as it measures 3.67 on the window's
        // hover: neither is in the white-text test, and that is the same choice in both places.
        ["SurfaceHoverOnPanel"] = new(1.5, Against: "SurfacePanel"),

        // Ours too, and the lowest floor in the table on purpose. A line between two rows
        // IDENTIFIES NOTHING - it only has to be seen between them, which is a smaller job than
        // any state above. It also stays UNDER hover, so that pointing at a row remains the
        // stronger of the two signals rather than competing with the furniture.
        ["SurfaceRowLine"] = new(1.25),

        // THE SCROLLBAR THUMB ASLEEP AND AWAKE, 2026-09-02, AND THE PAIR IS THE POINT. The bar was
        // one colour at 2.84 and the owner said it was too big and ugly - the width was half of
        // that and the brightness was the other half. At rest the thumb answers "where am I in the
        // list" for somebody who is not reaching for it: it identifies no component and competes
        // with nothing, so SC 1.4.11 does not reach it and 1.4 is ours. It has to beat the line
        // between two rows, which carries less and sits at 1.25.
        //
        // The awake colour is the one somebody can act on, so it answers to the standard rather
        // than to us: 3.79 measured, against the 3.0 of 1.4.11. The step between the two is the
        // largest of any state pair in this theme, which is what makes the wake-up readable.
        ["SurfaceScrollThumb"] = new(1.4),
        ["SurfaceScrollThumbAwake"] = new(ForState),

        // THE ONE ENTRY IN THIS TABLE ASKING FOR NOTHING, 2026-09-02, AND THE REASON IS THAT A
        // RATIO IS THE WRONG QUESTION FOR IT. The scrim exists to REMOVE contrast - it dims the
        // window while a plan sheet is open. Asking whether it is tellable from the window would be
        // asking it to fail at the only job it has.
        //
        // What it must not do is make anything else worse, and it does the opposite: the sheet's own
        // #343434 measures 1.25 against the bare window and better than that against a window under
        // this scrim, which reads near #101010. So the check that matters is SurfacePanel's, which
        // is already in this table and already passes without any help from here.
        ["SurfaceScrim"] = new(1.0),

        // THE COMMAND INSET, 2026-09-02, MEASURED AGAINST THE SHEET IT IS DRAWN IN SINCE 2026-09-16.
        // Until that day the figure the test took was 1.08 against the window, which this brush
        // never sits on - backlog 286 showing up as an entry rather than as a sentence. Against the
        // sheet it reads 1.22. The floor stays at 1.0, because what an inset has to clear is still
        // undecided: the panel's own rule for a region, 1.25, would refuse it by three hundredths,
        // and moving the colour is a palette decision. What the table no longer does is pretend to
        // have checked it against the wrong thing - the half of 286 a guard can close on its own.
        ["SurfaceCommandBox"] = new(1.0, Against: "SurfacePanel"),

        // THE DESTRUCTIVE FILL AND ITS TWO DARKER STATES, 2026-09-02, and they answer to exactly the
        // same numbers as the primary action they sit beside - the fill to SC 1.4.11 because it is
        // what identifies the control, the other two to 1.5 of ours because they only have to be
        // TELLABLE from that fill. Measured: 3.00 at rest against the window, and white on it 5.44,
        // 6.21 hovering, 7.03 pressed. Darker at every step, so the words get easier to read.
        ["SurfaceDestructiveAction"] = new(ForState),
        ["SurfaceDestructiveActionHover"] = new(1.5),
        ["SurfaceDestructiveActionPressed"] = new(1.5),

        // THE PANEL AND ITS EDGE, 2026-08-19. Ours as well, and the floors say what each one is
        // for. The fill only has to make a whole column of the window tellable as its own
        // surface - a large region, which reads at a lower ratio than any thin thing in this
        // table - so it sits beside the row line rather than beside hover. The edge is the one
        // line the panel has and separates two regions doing different jobs, so it clears more
        // than the line between two rows of the same kind.
        ["SurfacePanel"] = new(1.25),
        ["SurfacePanelEdge"] = new(1.5),

        // THE TWO THE PALETTE STUDY ADDED, 2026-09-01, and both answer to WCAG rather than to a
        // floor of ours - which is the point of them. The primary action's fill is a component
        // state, and a chip that is off has nothing but its outline to say it is a control at all,
        // so the outline IS the component. Both measure over 3.2 and white on either clears the
        // 4.5 text asks: 5.07 on the action, 5.10 on the chip edge.
        ["SurfacePrimaryAction"] = new(ForState),
        ["SurfaceChipEdge"] = new(ForState),

        // THE SAME BUTTON UNDER THE POINTER AND UNDER THE FINGER, 2026-09-02. Ours at 1.5, the same
        // floor and the same argument as SurfaceHover above: SC 1.4.11 covers what IDENTIFIES a
        // component, and the button was identified by the fill it has at rest - which answers to
        // 3.0 on the line above and clears it. These two only have to be TELLABLE from that rest
        // fill, which is a smaller job, and they are darker rather than lighter so white on them
        // gets easier at every step: 5.07 at rest, 6.06 hovering, 7.17 pressed.
        ["SurfacePrimaryActionHover"] = new(1.5),
        ["SurfacePrimaryActionPressed"] = new(1.5),

        // WCAG 2.2 SC 1.4.11 again, and THE NUMBER HERE IS THE WEAKER OF THE TWO CHECKS IT GETS.
        // The test below this table measures it against the window, where it comes out at 11.21
        // and clears anything - which would be a guard passing for the wrong reason, because a
        // ring is drawn on top of whatever state the row already has. Its real floor is the
        // worst of four surfaces and lives in its own test further down.
        ["FocusRing"] = new(ForState),

        // THE FOUR START TYPES, 2026-08-17. SC 1.4.11 rather than the text floor, and the
        // distinction is not a rounding: nobody reads these, they are rings beside a word that
        // carries the same answer in text. What they have to do is be TELLABLE from the window and
        // from each other, which is exactly what 1.4.11 covers.
        //
        // Being tellable from each other is a second check this table cannot make, and it is made
        // by MarkDistinctionGuards instead - a ratio against the background says nothing about two
        // marks that never appear side by side. Both are required and neither implies the other.
        ["StartBoot"] = new(ForState),
        ["StartSystem"] = new(ForState),
        ["StartAutomatic"] = new(ForState),
        ["StartManual"] = new(ForState)
    };

    /// <summary>
    /// Every surface a row can be, which is what a ring drawn on that row has to be seen against.
    ///
    /// Named rather than discovered, because "every brush starting with Surface" would also
    /// sweep in the line between rows - furniture the ring is never drawn over - and a guard that
    /// quietly widens its own subject stops meaning what its name says.
    /// </summary>
    internal static readonly string[] UnderTheRing =
    [
        "SurfaceSelected", "SurfaceHover", "SurfaceChanged"
    ];

    /// <summary>
    /// The surfaces somebody's words are actually drawn on - three states a row takes, the panel a
    /// plan is written in, and the three fills of the one button this product draws itself.
    /// </summary>
    internal static readonly string[] CarriesText =
    [
        "SurfaceSelected", "SurfaceHover", "SurfaceHoverOnPanel", "SurfaceChanged", "SurfacePanel", "SurfaceCommandBox",
        "SurfacePrimaryAction", "SurfacePrimaryActionHover", "SurfacePrimaryActionPressed",
        "SurfaceDestructiveAction", "SurfaceDestructiveActionHover", "SurfaceDestructiveActionPressed"
    ];

    /// <summary>
    /// Furniture. Two lines, an outline, and the two states of a scrollbar thumb - nothing is ever
    /// written on any of them, and asking whether white would read on a hairline is a question with
    /// no reader behind it.
    ///
    /// <b>It exists so that the pair is exhaustive rather than so that anything is skipped.</b>
    /// Every Surface brush has to be in one list or the other, which is what stops a text-bearing
    /// surface being added one day and quietly checked by nothing.
    /// </summary>
    internal static readonly string[] CarriesNoText =
    [
        "SurfaceRowLine", "SurfacePanelEdge", "SurfaceChipEdge",
        "SurfaceScrollThumb", "SurfaceScrollThumbAwake", "SurfaceScrim"
    ];
}
