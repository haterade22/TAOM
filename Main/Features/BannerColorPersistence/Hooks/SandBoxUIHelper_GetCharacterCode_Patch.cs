using HarmonyLib;
using SandBox.ViewModelCollection;
using TAOM.Adapters;
using TaleWorlds.Core;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch(typeof(SandBoxUIHelper), nameof(SandBoxUIHelper.GetCharacterCode))]
[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class SandBoxUIHelper_GetCharacterCode_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    [HarmonyPostfix]
    public static void Postfix(CharacterCode __result, TaleWorlds.CampaignSystem.CharacterObject character)
    {
        if (__result == null) return;
        var info = _heroAdapter?.GetClanColorInfo(character);
        if (info == null) return;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        __result.Color1 = info.Value.Color1;
        __result.Color2 = info.Value.Color2;
    }
}
