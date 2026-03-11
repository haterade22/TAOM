using System;
using System.Reflection;
using HarmonyLib;

namespace TAOM.Features.FactionMap.Hooks;

[HarmonyPatch]
[HarmonyPatchCategory("Patch7_FactionMap")]
public static class CultureStageView_Tick_Patch
{
    private static IOnCultureStageViewTick? _hook;

    public static void Initialize(IOnCultureStageViewTick hook) => _hook = hook;

    private static readonly string[] PossibleTypeNames =
    {
        "SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView",
        "SandBox.View.CharacterCreation.CharacterCreationCultureStageView",
    };

    private static Type? FindCultureStageViewType()
    {
        foreach (var typeName in PossibleTypeNames)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type != null) return type;
        }
        return null;
    }

    static bool Prepare() => FindCultureStageViewType() != null;

    static MethodBase? TargetMethod()
    {
        var type = FindCultureStageViewType();
        return type != null ? AccessTools.Method(type, "Tick", new[] { typeof(float) }) : null;
    }

    // CharacterCreationCultureStageView is not directly referenceable at compile time
    // (View assembly loaded after init), so we use dynamic TargetMethod() resolution.

    [HarmonyPostfix]
    static void Postfix(object __instance, float dt) => _hook?.OnTick(__instance, dt);
}
