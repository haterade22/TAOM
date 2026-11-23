using System.Collections.Generic;
using System.Linq;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Features.Warg;

/// <summary>
/// The warg row of the command trial's step table (#1563): one custom battle step that fields warg riders beside the army's horsemen,
/// holds them, charges them and reports their speeds, speed limit and scale. It cannot see which gait clip plays. It is known by
/// name and is not in the list a run with no step list arms, because it puts wargs on the field and the other rows were proved in
/// a battle without them.
/// </summary>
internal static class WargTrialRows
{
    internal const string Gaits = "warg_gaits";

    /// <summary>The budget covers the wait for the riders, the stand, the charge and the verdict, in real seconds.</summary>
    internal static readonly IReadOnlyList<SeatTrialStep> Rows = new[]
    {
        new SeatTrialStep(Gaits, new[] { "enter" }, 120f),
    };

    /// <summary>The riders the step adds to the player's army (<c>fords_of_war/fords_troops.xml</c>, Nurn Warg Raider). The warg is each rider's mount.</summary>
    internal static readonly IReadOnlyList<SeatTrialGroup> Riders = new[]
    {
        new SeatTrialGroup("fow_mordor_warg_raider", 12),
    };

    /// <summary>The first chosen step that is not <c>enter</c>, when the list also holds the warg step; null when the list may run.</summary>
    internal static string? StepThatCannotShareAWargBattle(IReadOnlyList<string> chosen)
    {
        if (!chosen.Any(IsWargStep))
            return null;
        return chosen.FirstOrDefault(name => !IsWargStep(name) && name != "enter");
    }

    private static bool IsWargStep(string name) => Rows.Any(row => row.Name == name);

    /// <summary>True when the step list names the warg step.</summary>
    internal static bool NamedIn(string? steps) =>
        (steps ?? "").Split(',').Select(SeatTrialSteps.NameOf).Any(IsWargStep);
}
