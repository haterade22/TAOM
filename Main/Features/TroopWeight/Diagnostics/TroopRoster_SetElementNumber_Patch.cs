using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight.Diagnostics;

// TEMPORARY sibling of TroopRoster_AddToCountsAtIndex_Patch (same report, same removal checklist).
// SetElementNumber writes a slot without going through AddToCountsAtIndex and without updating the
// cached totals. Vanilla's only caller is the battle cheat on the opponent's parties; the co-op mod
// calls it on rosters. LogMainRemoval holds the gates and the try/catch; the subtraction below sits
// in its own try so nothing can throw out of the prefix.
[HarmonyPatch(typeof(TroopRoster), nameof(TroopRoster.SetElementNumber))]
[HarmonyPatchCategory("Patch_MainRosterLossDiag")]
public static class TroopRoster_SetElementNumber_Patch
{
    static void Prefix(TroopRoster __instance, int index, int number)
    {
        if (number < 0)
            return;

        try
        {
            // GetElementNumber returns 0 for an out-of-range index, so this never throws on a bad index.
            TroopRoster_AddToCountsAtIndex_Patch.LogMainRemoval(
                __instance, index, __instance.GetElementNumber(index) - number);
        }
        catch (Exception)
        {
            // Diagnostic only; the sibling patch owns the one-time failure warning.
        }
    }
}
