using HarmonyLib;
using TaleWorlds.Core;

namespace TAOM.Features.CharacterCreation.Hooks;

[HarmonyPatch(typeof(FaceGen), nameof(FaceGen.GetRaceNames))]
[HarmonyPatchCategory("Patch9_RaceFilter")]
public static class FaceGen_GetRaceNames_Patch
{
    private static IOnGetRaceNames _hook;

    public static void Initialize(IOnGetRaceNames hook) => _hook = hook;

    [HarmonyPostfix]
    static void Postfix(ref string[] __result)
    {
        if (_hook != null)
            __result = _hook.FilterRaceNames(__result);
    }
}
