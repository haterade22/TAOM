using HarmonyLib;
using TaleWorlds.Core;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(FaceGen), "GetBaseMonsterFromRace")]
public class FaceGen_GetBaseMonsterFromRace_Patch
{
    private static IOnFaceGenGetBaseMonsterFromRace _hook;

    public static void Initialize(IOnFaceGenGetBaseMonsterFromRace hook)
    {
        _hook = hook;
    }

    [HarmonyPostfix]
    public static void Postfix(ref Monster __result, int race)
    {
        _hook?.OnGetBaseMonsterFromRace(ref __result, race);
    }
}
