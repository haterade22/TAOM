using System;
using HarmonyLib;
using SandBox.Missions.MissionLogics.Arena;
using SandBox.ViewModelCollection.Missions;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Features.TrainingGrounds.Teams.Hooks;

/// <summary>
/// Patch174 (#1536): postfix on the practice HUD's <c>MissionArenaPracticeFightVM.Tick</c>. In a team fight it rewrites the
/// "remaining opponents" line without the player's allies, which vanilla's count includes; the prize line stays vanilla's.
/// Not a veto: vanilla ran first and this only overwrites one string. In any fight that is not a team fight
/// <see cref="TeamPracticeFight.TryOpponentsLeft"/> answers false and the postfix returns at once. It lives in TAOM.Client
/// because the view model is in a client-only assembly (ADR-013); the registry holds the target, the callers and the category.
/// </summary>
[HarmonyPatch(typeof(MissionArenaPracticeFightVM), nameof(MissionArenaPracticeFightVM.Tick))]
[HarmonyPatchCategory(TeamPracticeModule.PatchCategory)]
public static class MissionArenaPracticeFightVM_Tick_TeamPractice_Patch
{
    /// <summary>
    /// <c>____practiceMissionController</c> injects the private field <c>_practiceMissionController</c>: Harmony strips exactly three
    /// underscores, and the engine's own leading underscore stays (HarmonyFieldInjectionNamingTests).
    /// </summary>
    [HarmonyPostfix]
    public static void Postfix(MissionArenaPracticeFightVM __instance, ArenaPracticeFightMissionController ____practiceMissionController)
    {
        try
        {
            if (!TeamPracticeFight.TryOpponentsLeft(____practiceMissionController, out var count))
                return;

            GameTexts.SetVariable("REMAINING_OPPONENT_COUNT", count);
            __instance.OpponentsRemainingText = GameTexts.FindText("str_remaining_opponent").ToString();
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("TeamPractice", "MissionArenaPracticeFightVM.Tick", ex);
        }
    }
}
