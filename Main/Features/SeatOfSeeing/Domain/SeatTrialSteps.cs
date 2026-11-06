using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>One row of the field trial's step order: the steps chosen with it, and the real seconds it may take before it fails on a timeout.</summary>
internal sealed record SeatTrialStep(string Name, IReadOnlyList<string> Requires, float BudgetSeconds);

/// <summary>The field trial's steps in the order they run, joined from each feature's row list, and the choice of a subset.</summary>
internal static class SeatTrialSteps
{
    internal static readonly IReadOnlyList<SeatTrialStep> All = SeatTrialRows.Rows.Concat(MarshalTrialRows.Rows).ToList();

    /// <summary>The form a step name is compared in, shared by the step chooser and the roster that depends on which steps were chosen.</summary>
    internal static string NameOf(string name) => name.Trim().ToLowerInvariant();

    /// <summary>The steps to run in the table's order (null or blank is every step), or false with <paramref name="error"/> naming an unknown step or a missing requirement and <paramref name="steps"/> empty.</summary>
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
        var unknown = names.Where(n => All.All(s => s.Name != n)).Distinct().ToList();
        if (unknown.Count > 0)
        {
            error = "unknown step '" + string.Join("', '", unknown) + "'; known: " + string.Join(", ", All.Select(s => s.Name));
            return false;
        }

        var chosen = All.Where(s => names.Contains(s.Name)).ToList();
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

        steps = chosen;
        return true;
    }
}
