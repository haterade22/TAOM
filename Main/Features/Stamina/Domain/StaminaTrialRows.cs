using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Features.Stamina.Domain;

/// <summary>
/// The stamina rows of the command trial's step table (#1506): the gate, the walk of the exhausted and the fight. They are known
/// by name and are not in the list a run with no step list arms, because they run in a battle of their own against the crew
/// battle's melee enemy, and the other rows were proved without it.
/// </summary>
internal static class StaminaTrialRows
{
    internal const string Gate = "stamina_gate";
    internal const string Walk = "stamina_walk";
    internal const string Fight = "stamina_fight";

    /// <summary>The budget of each row covers its longest wait and its windows in real seconds, and ten seconds for the verdict and the release.</summary>
    internal static readonly IReadOnlyList<SeatTrialStep> Rows = new[]
    {
        new SeatTrialStep(Gate, Array.Empty<string>(), 45f),
        new SeatTrialStep(Walk, new[] { Gate }, 35f),
        new SeatTrialStep(Fight, new[] { Gate }, 150f),
    };

    /// <summary>
    /// The first chosen step that is not a stamina step, when the list also holds a stamina step; null when the list may run.
    /// A stamina battle forces bands on its soldiers, which the other rows' verdicts would read as their own result.
    /// </summary>
    internal static string? StepThatCannotShareAStaminaBattle(IReadOnlyList<string> chosen)
    {
        if (!chosen.Any(IsStaminaStep))
            return null;
        return chosen.FirstOrDefault(name => !IsStaminaStep(name));
    }

    private static bool IsStaminaStep(string name) => Rows.Any(row => row.Name == name);

    /// <summary>True when the step list names a stamina step.</summary>
    internal static bool NamedIn(string? steps) =>
        (steps ?? "").Split(',').Select(SeatTrialSteps.NameOf).Any(IsStaminaStep);
}
