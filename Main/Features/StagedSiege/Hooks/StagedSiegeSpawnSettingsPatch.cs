using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.StagedSiege.Hooks;

/// <summary>
/// Patch126, the staged siege's reinforcement settings. <c>DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase</c> (public,
/// v1.5.6 :195) takes the spawn settings by <c>in</c> and hands them to <c>Init</c> (:199). For an armed staged siege this prefix
/// installs <see cref="StagedReinforcementTimer"/> and replaces the settings with the Balanced method on a custom timer, keeping
/// the incoming initial method, wave percentage, defender advantage and maximum side ratio and writing the two batch
/// percentages, a wave count of 0 and the larger of the incoming global interval and the staged trickle. The interval matters
/// because, under a custom timer, its only reader is <c>BattleEndLogic</c>'s retreat check (interval plus 3 s of every active
/// agent of a side running away ends the battle as a rout), and a 3 s interval would rout a side whose reserve the stage budget
/// is holding back. It runs for every battle that calls <c>InitWithSinglePhase</c>, so the first
/// guard is a pure read of TAOM state (<see cref="StagedSiegeSlot.HasArmed"/>), and a mission other than the armed one is
/// left alone. The body names no engine member: the engine work is in the NoInlining helper below, called inside the try. The
/// timer is installed before the settings are replaced, so a fault between the two leaves the global timer in charge, never a
/// custom timer with nobody behind it.
///
/// Coexistence with Patch102 (Siege Forces): Patch102 is Normal priority and writes only the four spawn-total ints; this writes
/// only <c>spawnSettings</c> and reads none of the ints, so the order cannot change either result, and Low priority runs it
/// second anyway. Harmony binds the parameters by NAME, pinned by <c>StagedSiegeBindingTests</c>. The write through the <c>in</c>
/// argument reaches <c>Init</c>: the vanilla siege handler passes its local by address (SandBoxSiegeMissionSpawnHandler.cs:18-19),
/// <c>InitWithSinglePhase</c> forwards the same pointer (DefaultBattleMissionAgentSpawnLogic.cs:199) and <c>Init</c> copies it (:397); the "S2 deployment over:
/// settings live" line confirms it at runtime.
/// Once per battle; not on <c>ExcludedTargetMethods</c>.
/// </summary>
[HarmonyPatch(typeof(DefaultBattleMissionAgentSpawnLogic), nameof(DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase))]
[HarmonyPatchCategory(StagedSiegeModule.PatchCategory)]
[HarmonyPriority(Priority.Low)]
public static class StagedSiegeSpawnSettingsPatch
{
    [HarmonyPrefix]
    public static void Prefix(object __instance, ref MissionSpawnSettings spawnSettings)
    {
        if (!StagedSiegeSlot.HasArmed)
            return;

        try
        {
            Swap(__instance, ref spawnSettings);
        }
        catch (Exception ex)
        {
            LogFault(ex);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Swap(object instance, ref MissionSpawnSettings spawnSettings)
    {
        var logic = (DefaultBattleMissionAgentSpawnLogic)instance;
        var service = StagedSiegeSlot.ServiceFor(logic.Mission);
        if (service == null)
            return;

        var old = spawnSettings;
        var plan = service.SpawnPlan;
        logic.SetCustomReinforcementSpawnTimer(new StagedReinforcementTimer(service));
        spawnSettings = new MissionSpawnSettings(
            old.InitialTroopsSpawnMethod,
            MissionSpawnSettings.ReinforcementTimingMethod.CustomTimer,
            MissionSpawnSettings.ReinforcementSpawnMethod.Balanced,
            Math.Max(old.GlobalReinforcementInterval, plan.TrickleSeconds),
            plan.BatchPercentage,
            plan.DesiredPercentage,
            old.ReinforcementWavePercentage,
            0,
            0f,
            0f,
            old.DefenderAdvantageFactor,
            old.MaximumBattleSideRatio);
        service.NoteSpawnSettingsSwapped(old.ReinforcementTroopsSpawnMethod.ToString());
    }

    private static void LogFault(Exception ex)
    {
        try
        {
            TAOM.IoC.Resolve<IModLogger>()?.LogWarning(
                $"[StagedSiege] Patch126: spawn settings not swapped ({ex.GetType().Name}: {ex.Message}); vanilla reinforcements");
        }
        catch (Exception)
        {
            // Nothing left to report to.
        }
    }
}
