using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Elephant;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.Spider;
using TAOM.Features.Stamina.Domain;
using TAOM.Features.Warg;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>One row of the field trial's step order: the steps chosen with it, and the real seconds it may take before it fails on a timeout.</summary>
internal sealed record SeatTrialStep(string Name, IReadOnlyList<string> Requires, float BudgetSeconds);

/// <summary>The field trial's steps in the order they run, joined from each feature's row list, and the choice of a subset.</summary>
internal static class SeatTrialSteps
{
    /// <summary>The steps a run with no step list arms.</summary>
    internal static readonly IReadOnlyList<SeatTrialStep> All = SeatTrialRows.Rows.Concat(MarshalTrialRows.Rows).ToList();

    /// <summary>Every step a list may name: <see cref="All"/>, then the crew rows, the stamina rows, the spider row and the warg row, which run only when they are named (<see cref="CrewTrialRows"/>, <see cref="StaminaTrialRows"/>, <see cref="SpiderTrialRows"/>, <see cref="WargTrialRows"/>).</summary>
    internal static readonly IReadOnlyList<SeatTrialStep> Known = All.Concat(CrewTrialRows.Rows).Concat(StaminaTrialRows.Rows).Concat(SpiderTrialRows.Rows).Concat(WargTrialRows.Rows).ToList();

    /// <summary>The form a step name is compared in, shared by the step chooser and the roster that depends on which steps were chosen.</summary>
    internal static string NameOf(string name) => name.Trim().ToLowerInvariant();

    /// <summary>The steps to run in the table's order (null or blank is every step of <see cref="All"/>), or false with <paramref name="error"/> naming an unknown step or a missing requirement and <paramref name="steps"/> empty.</summary>
    internal static bool TryChoose(string? list, out IReadOnlyList<SeatTrialStep> steps, out string error)
    {
        steps = Array.Empty<SeatTrialStep>();
        error = "";
        if (string.IsNullOrWhiteSpace(list))
        {
            steps = All;
            return true;
        }

        var names = list!.Split(',').Select(NameOf).Where(n => n.Length > 0).ToList();
        var unknown = names.Where(n => Known.All(s => s.Name != n)).Distinct().ToList();
        if (unknown.Count > 0)
        {
            error = "unknown step '" + string.Join("', '", unknown) + "'; known: " + string.Join(", ", Known.Select(s => s.Name));
            return false;
        }

        var chosen = Known.Where(s => names.Contains(s.Name)).ToList();
        if (chosen.Count == 0)
        {
            error = "the list names no step";
            return false;
        }

        foreach (var step in chosen)
        {
            var missing = step.Requires.FirstOrDefault(r => chosen.All(c => c.Name != r));
            if (missing != null)
            {
                error = "step '" + step.Name + "' needs step '" + missing + "' in the list";
                return false;
            }
        }

        var spiderBeside = SpiderTrialRows.StepThatCannotShareASpiderBattle(chosen.Select(s => s.Name).ToList());
        if (spiderBeside != null)
        {
            error = "a spider step runs with 'enter' only, in a battle of its own: step '" + spiderBeside + "' cannot share it";
            return false;
        }

        var wargBeside = WargTrialRows.StepThatCannotShareAWargBattle(chosen.Select(s => s.Name).ToList());
        if (wargBeside != null)
        {
            error = "a warg step runs with 'enter' only, in a battle of its own: step '" + wargBeside + "' cannot share it";
            return false;
        }

        var beside = CrewTrialRows.StepThatCannotShareACrewBattle(chosen.Select(s => s.Name).ToList());
        if (beside != null)
        {
            error = "a crew step runs with 'enter' only, in a battle of its own: step '" + beside + "' cannot share it";
            return false;
        }

        var staminaBeside = StaminaTrialRows.StepThatCannotShareAStaminaBattle(chosen.Select(s => s.Name).ToList());
        if (staminaBeside != null)
        {
            error = "a stamina step runs only beside other stamina steps, in a battle of its own: step '" + staminaBeside + "' cannot share it";
            return false;
        }

        steps = chosen;
        return true;
    }
}
