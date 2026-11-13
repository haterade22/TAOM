using System.Reflection;
using HarmonyLib;

namespace TAOM.Features.FactionMap.Hooks;

[HarmonyPatch]
[HarmonyPatchCategory("Patch7_FactionMap")]
[ClientOnly]
public static class CultureStageView_Finalize_Patch
{
    private static IOnCultureStageViewFinalize? _hook;

    public static void Initialize(IOnCultureStageViewFinalize hook) => _hook = hook;

    static bool Prepare() => CultureStageViewType.Find() != null;

    static MethodBase? TargetMethod()
    {
        var type = CultureStageViewType.Find();
        return type != null ? AccessTools.Method(type, "OnFinalize") : null;
    }

    // CharacterCreationCultureStageView is not directly referenceable at compile time
    // (View assembly loaded after init), so we use dynamic TargetMethod() resolution.

    [HarmonyPrefix]
    static void Prefix() => _hook?.OnFinalize();
}
