using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// Patch141 (docs/features/marshals-word.md, build-spec 6.1): keep the array. A prefix on the private static
/// <c>OrderController.SimulateNewOrderWithPositionAndDirectionAux</c>, the one place both the order preview and the commit
/// (<c>MoveToLineSegment</c>) go through. While Marshal's Word is live and the setting holds, a vertical layout of two or more of the
/// player's companies is laid out as one body by <see cref="MarshalArrayLayout"/>, and the method returns the same outputs, so vanilla's
/// commit code still runs. Any other call returns true at once. A throw latches the takeover off and lets vanilla run.
/// </summary>
[HarmonyPatch(typeof(OrderController), "SimulateNewOrderWithPositionAndDirectionAux")]
[HarmonyPatchCategory(MarshalsWordModule.PatchCategory)]
public static class MarshalArrayPatch
{
    private static readonly OutcomeCounter Preview = Outcomes.Counter(MarshalChecks.PreviewCounterId);

    [HarmonyPrefix]
    public static bool Prefix(IEnumerable<Formation> formations, Dictionary<Formation, Formation> simulationFormations,
        WorldPosition formationLineBegin, WorldPosition formationLineEnd, bool isSimulatingAgentFrames,
        out List<WorldPosition> simulationAgentFrames, bool isSimulatingFormationChanges,
        out List<(Formation, int, float, WorldPosition, Vec2)> simulationFormationChanges, out bool isLineShort, bool isFormationLayoutVertical)
    {
        simulationAgentFrames = null!;
        simulationFormationChanges = null!;
        isLineShort = false;
        if (!MarshalHooks.IsLive || !MarshalHooks.KeepArray || !isFormationLayoutVertical)
            return true;

        try
        {
            var took = MarshalArrayLayout.TryTakeOver(formations, simulationFormations, formationLineBegin, formationLineEnd,
                isSimulatingAgentFrames, out var frames, isSimulatingFormationChanges, out var changes, out var lineIsShort);
            if (!took)
                return true;

            simulationAgentFrames = frames!;
            simulationFormationChanges = changes!;
            isLineShort = lineIsShort;
            if (isSimulatingFormationChanges)
                MarshalArrayLayout.AfterCommit(changes!, lineIsShort);
            else
                Preview.Pass();
            return false;
        }
        catch (Exception exception)
        {
            Outcomes.Swallowed("MarshalsWord", "MarshalArrayPatch.Prefix", exception);
            MarshalHooks.LatchArrayOff(exception);
            return true;
        }
    }
}
