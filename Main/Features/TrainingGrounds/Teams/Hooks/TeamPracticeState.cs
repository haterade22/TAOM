using System;
using System.Runtime.CompilerServices;

namespace TAOM.Features.TrainingGrounds.Teams.Hooks;

/// <summary>
/// #1528: the mode of one arena visit, keyed by the vanilla practice controller. The controller is built once for
/// each visit, so the mode is decided once and an MCM change never leaves a fight half team and half
/// free-for-all. The table holds the controller weakly: it does not outlive the mission.
/// </summary>
internal static class TeamPracticeState
{
    private static readonly ConditionalWeakTable<object, StrongBox<bool>> Modes = new();

    /// <summary>Runs <paramref name="decide"/> the first time a controller is seen and returns that answer from then on.</summary>
    internal static bool Decide(object controller, Func<bool> decide)
    {
        if (Modes.TryGetValue(controller, out var known))
            return known.Value;

        var teams = decide();
        Modes.Add(controller, new StrongBox<bool>(teams));
        return teams;
    }

    /// <summary>False for a controller nobody has decided for: vanilla's fight.</summary>
    internal static bool IsTeams(object controller) =>
        Modes.TryGetValue(controller, out var known) && known.Value;
}
