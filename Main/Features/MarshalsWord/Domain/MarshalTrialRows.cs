using System;
using System.Collections.Generic;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>Marshal's Word's rows of the field trial's step table, in the order they run, with each budget in real seconds.</summary>
internal static class MarshalTrialRows
{
    internal static readonly IReadOnlyList<SeatTrialStep> Rows = new[]
    {
        new SeatTrialStep("queue_two_moves", Array.Empty<string>(), 80f),
        new SeatTrialStep("direct_order_clears", Array.Empty<string>(), 45f),
        new SeatTrialStep("array_keeps_offsets", Array.Empty<string>(), 90f),
        new SeatTrialStep("arrive_together", Array.Empty<string>(), 200f),
        new SeatTrialStep("volley_holds", Array.Empty<string>(), 280f),
        new SeatTrialStep("volley_looses", new[] { "volley_holds" }, 45f),
        new SeatTrialStep("mark_keeps_order", Array.Empty<string>(), 170f),
        new SeatTrialStep("outline_clears", Array.Empty<string>(), 30f),
        new SeatTrialStep("volley_massed", Array.Empty<string>(), 330f),
        new SeatTrialStep("square_side_kept", Array.Empty<string>(), 90f),
        new SeatTrialStep("spacing_one_rider", Array.Empty<string>(), 70f),
        new SeatTrialStep("hold_line_proof", Array.Empty<string>(), 250f),
        new SeatTrialStep("hold_line_slot", Array.Empty<string>(), 260f),
        new SeatTrialStep("hold_line_strike", Array.Empty<string>(), 260f),
        new SeatTrialStep("hold_line_flag", Array.Empty<string>(), 90f),
        new SeatTrialStep("advance_throwers", Array.Empty<string>(), 330f),
        new SeatTrialStep("advance_throwers_vanilla", Array.Empty<string>(), 200f),
    };
}
