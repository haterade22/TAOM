using HarmonyLib;
using System;
using TAOM.Features.HeroRace.Diagnostics;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace.Hooks;

/// <summary>
/// Drops a tableau's #389 census state when its race changes. TAOM's own Patch3_SetRace resets both
/// <c>AgentVisuals</c> and re-runs the private <c>InitializeAgentVisuals</c>, after which the next
/// refresh re-arms the loading counters — the character genuinely starts a fresh load, so it should
/// report again.
///
/// (Vanilla <c>SetRace</c> itself only assigns the field and marks the visuals dirty. The re-init on
/// this path is TAOM's, which is precisely why the reset belongs here.)
/// </summary>
[HarmonyPatch(typeof(CharacterTableau), nameof(CharacterTableau.SetRace))]
[HarmonyPatchCategory("Patch67_TableauResidencyDiag")]
public class CharacterTableau_SetRace_ResidencyReset_Patch
{
    [HarmonyPostfix]
    public static void Postfix(CharacterTableau __instance, string ____charStringId)
    {
        try
        {
            // Keyed on the tableau instance plus the character id, neither of which SetRace changes —
            // and `CharacterViewModel.FillFrom` assigns CharStringId BEFORE Race in both overloads
            // (decompile-verified), so this resolves to the same entry OnTick tracked.
            TableauCensusSession.Forget(__instance, ____charStringId);
        }
        catch (Exception e)
        {
            TableauDiagnostics.LogError($"Patch67 census reset THREW (rendering is unaffected): {e}");
        }
    }
}

/// <summary>
/// Releases a tableau's tracked slot when the widget is torn down (screen close).
///
/// Without this, a session that browses more than <see cref="TableauResidencyTracker.DefaultMaxTrackedCharacters"/>
/// characters fills the tracker with entries for tableaus that no longer exist, and every later troop
/// reports nothing — the instrument goes quiet exactly when someone is trying to use it. Deep review
/// 2026-08-06 (data-flow agent) confirmed `OnFinalize` is the destruction path and that nothing was
/// hooked to it; the per-instance key cache is weak and self-cleans, but the tracker's own entry is
/// keyed by string and does not.
/// </summary>
[HarmonyPatch(typeof(CharacterTableau), nameof(CharacterTableau.OnFinalize))]
[HarmonyPatchCategory("Patch67_TableauResidencyDiag")]
public class CharacterTableau_OnFinalize_ResidencyReset_Patch
{
    [HarmonyPostfix]
    public static void Postfix(CharacterTableau __instance, string ____charStringId)
    {
        try
        {
            TableauCensusSession.Forget(__instance, ____charStringId);
        }
        catch (Exception e)
        {
            TableauDiagnostics.LogError($"Patch67 census teardown reset THREW (rendering is unaffected): {e}");
        }
    }
}
