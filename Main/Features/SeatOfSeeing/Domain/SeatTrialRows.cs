using System;
using System.Collections.Generic;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>The Seat's rows of the field trial's step table, in the order they run (build-spec section 12).</summary>
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
    };
}
