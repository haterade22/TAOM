using System;
using System.Collections.Generic;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>The Seat's rows of the field trial's step table, in the order they run, with each budget in real seconds (build-spec section 12).</summary>
internal static class SeatTrialRows
{
    internal static readonly IReadOnlyList<SeatTrialStep> Rows = new[]
    {
        new SeatTrialStep("refuse_in_deployment", Array.Empty<string>(), 10f),
        new SeatTrialStep("enter", Array.Empty<string>(), 25f),
        new SeatTrialStep("fly", new[] { "enter" }, 8f),
        new SeatTrialStep("hero_stands", new[] { "enter" }, 8f),
        new SeatTrialStep("leave_glide", new[] { "enter" }, 12f),
        new SeatTrialStep("reenter_keeps_height", new[] { "leave_glide" }, 12f),
        new SeatTrialStep("cursor_with_panel", new[] { "enter" }, 15f),
        new SeatTrialStep("focus_follows_cursor", new[] { "enter" }, 15f),
        new SeatTrialStep("keep_panel_reopens", new[] { "enter" }, 15f),
        new SeatTrialStep("hide_bars_restored", new[] { "enter" }, 15f),
        new SeatTrialStep("keep_to_soldier", new[] { "enter" }, 25f),
        new SeatTrialStep("keep_to_company", new[] { "enter" }, 20f),
        new SeatTrialStep("let_go_on_death", new[] { "enter" }, 30f),
        new SeatTrialStep("fight_on", new[] { "enter" }, 40f),
        new SeatTrialStep("morale_held", new[] { "fight_on" }, 12f),
        new SeatTrialStep("back_to_stand", new[] { "fight_on" }, 30f),
        new SeatTrialStep("hero_falls", new[] { "enter" }, 30f),
        new SeatTrialStep("take_soldier", new[] { "hero_falls" }, 30f),
    };
}
