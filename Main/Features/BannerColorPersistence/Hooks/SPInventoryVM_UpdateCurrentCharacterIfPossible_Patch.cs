using System.Reflection;
using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(SPInventoryVM), "UpdateCurrentCharacterIfPossible",
            new[] { typeof(int), typeof(bool) });

    [HarmonyPostfix]
    public static void Postfix(SPInventoryVM __instance, bool __result)
    {
        if (!__result) return;

        var currentCharacter = Traverse.Create(__instance)
            .Field("_currentCharacter")
            .GetValue<CharacterObject>();

        if (currentCharacter == null) return;

        var info = _heroAdapter?.GetClanColorInfo(currentCharacter);
        if (info == null) return;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        __instance.MainCharacter.ArmorColor1 = info.Value.Color1;
        __instance.MainCharacter.ArmorColor2 = info.Value.Color2;
    }
}
