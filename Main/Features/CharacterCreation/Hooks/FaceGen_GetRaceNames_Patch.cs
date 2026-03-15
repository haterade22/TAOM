using HarmonyLib;
using TaleWorlds.Core;

namespace TAOM.Features.CharacterCreation.Hooks;

/// <summary>
/// Previously filtered GetRaceNames() by culture, but this globally breaks FaceGenVM
/// which uses array index as global race ID. Now a no-op — all races shown in dropdown.
/// </summary>
[HarmonyPatch(typeof(FaceGen), nameof(FaceGen.GetRaceNames))]
[HarmonyPatchCategory("Patch9_RaceFilter")]
public static class FaceGen_GetRaceNames_Patch
{
    [HarmonyPostfix]
    static void Postfix(ref string[] __result)
    {
        // Intentionally empty — race filtering broke FaceGenVM's index-based race lookup.
        // All races are shown; body properties handle race-appropriate proportions per culture.
    }
}
