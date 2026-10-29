using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Features.StagedSiege.Hooks;

/// <summary>
/// Patch126, the staged siege's reinforcement frames. <c>Mission.GetFormationSpawnFrame</c> (public, v1.5.6 :3999, one overload)
/// picks where a formation spawns; the spawn path calls it for reinforcements with <c>isReinforcement</c> true (:3964-3972,
/// :4266). For an armed staged siege this postfix overwrites the result with the stage's recorded frame: attackers on the
/// previous point facing the current one, defenders on the point's recorded hold frame. Stage A has neither, so it leaves
/// vanilla's frame. It reads only the immutable snapshot, so thread does not matter, and its first guards read only TAOM state.
/// The engine work is in the NoInlining helper, called inside the try; a fault leaves the frame vanilla chose.
///
/// UNVERIFIED in game: whether a navmesh-less <c>WorldPosition</c> resolves (the troop spawn projects it onto the navmesh,
/// :3983-3995); the "S2 reinforcement ... m from the staged frame" line proves it. Harmony binds the parameters by NAME, pinned
/// by <c>StagedSiegeBindingTests</c>. Not on <c>ExcludedTargetMethods</c>.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.GetFormationSpawnFrame))]
[HarmonyPatchCategory(StagedSiegeModule.PatchCategory)]
public static class StagedSiegeSpawnFramePatch
{
    [HarmonyPostfix]
    public static void Postfix(Mission __instance, Team team, bool isReinforcement, ref WorldPosition spawnPosition, ref Vec2 spawnDirection)
    {
        if (!isReinforcement || !StagedSiegeSlot.HasArmed)
            return;

        try
        {
            Place(__instance, team, ref spawnPosition, ref spawnDirection);
        }
        catch (Exception ex)
        {
            LogFault(ex);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Place(Mission mission, Team team, ref WorldPosition spawnPosition, ref Vec2 spawnDirection)
    {
        var snapshot = StagedSiegeSlot.SnapshotFor(mission);
        if (snapshot == null || snapshot.Finished)
            return;

        var side = team?.Side;
        var frame = side == BattleSideEnum.Attacker ? snapshot.AttackerSpawn
            : side == BattleSideEnum.Defender ? snapshot.DefenderSpawn
            : null;
        if (!frame.HasValue)
            return;

        var position = frame.Value.Position;
        spawnPosition = new WorldPosition(mission.Scene, UIntPtr.Zero, new Vec3(position.X, position.Y, position.Z), false);
        spawnDirection = new Vec2(frame.Value.Facing.X, frame.Value.Facing.Y);
    }

    private static void LogFault(Exception ex)
    {
        try
        {
            TAOM.IoC.Resolve<IModLogger>()?.LogWarning(
                $"[StagedSiege] Patch126: reinforcement frame not placed ({ex.GetType().Name}: {ex.Message}); vanilla frame");
        }
        catch (Exception)
        {
            // Nothing left to report to.
        }
    }
}
