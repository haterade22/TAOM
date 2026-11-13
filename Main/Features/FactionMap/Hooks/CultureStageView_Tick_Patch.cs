using System.Reflection;
using HarmonyLib;

namespace TAOM.Features.FactionMap.Hooks;

[HarmonyPatch]
[HarmonyPatchCategory("Patch7_FactionMap")]
[ClientOnly]
public static class CultureStageView_Tick_Patch
{
    private static IOnCultureStageViewTick? _hook;

    public static void Initialize(IOnCultureStageViewTick hook) => _hook = hook;

    static bool Prepare() => CultureStageViewType.Find() != null;

    static MethodBase? TargetMethod()
    {
        var type = CultureStageViewType.Find();
        return type != null ? AccessTools.Method(type, "Tick", new[] { typeof(float) }) : null;
    }

    // CharacterCreationCultureStageView is not directly referenceable at compile time
    // (View assembly loaded after init), so we use dynamic TargetMethod() resolution.

    [HarmonyPostfix]
    static void Postfix(object __instance, float dt) => _hook?.OnTick(__instance, dt);
}
