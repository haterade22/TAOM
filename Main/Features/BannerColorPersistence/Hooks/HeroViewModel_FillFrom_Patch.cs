using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch(typeof(HeroViewModel), nameof(HeroViewModel.FillFrom))]
[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class HeroViewModel_FillFrom_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    [HarmonyPostfix]
    public static void Postfix(HeroViewModel __instance, Hero hero)
    {
        var info = _heroAdapter?.GetClanColorInfoFromHero(hero);
        if (info == null) return;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        __instance.ArmorColor1 = info.Value.Color1;
        __instance.ArmorColor2 = info.Value.Color2;
    }
}
