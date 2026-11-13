using System.Reflection;
using HarmonyLib;

namespace TAOM.Features.FactionMap.Hooks;

[HarmonyPatch]
[HarmonyPatchCategory("Patch7_FactionMap")]
[ClientOnly]
public static class CultureStageView_Constructor_Patch
{
    private static IOnCultureStageViewCreated? _hook;

    public static void Initialize(IOnCultureStageViewCreated hook) => _hook = hook;

    static bool Prepare() => CultureStageViewType.Find() != null;

    static MethodBase? TargetMethod()
    {
        var type = CultureStageViewType.Find();
        if (type == null) return null;
        var ctors = type.GetConstructors();
        return ctors.Length > 0 ? ctors[0] : null;
    }

    // CharacterCreationCultureStageView is in SandBox.GauntletUI/SandBox.View which is not
    // directly referenceable at compile time (loaded after View assembly init), so we use
    // dynamic TargetMethod() resolution instead of [HarmonyPatch(typeof(...), ...)] attributes.

    [HarmonyPostfix]
    static void Postfix(object __instance) => _hook?.OnCreated(__instance);
}
