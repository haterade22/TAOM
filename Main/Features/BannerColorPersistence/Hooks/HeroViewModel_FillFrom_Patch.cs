using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;

namespace TAOM.Features.BannerColorPersistence.Hooks;

// FillFrom must be pinned by argument types: HeroViewModel inherits CharacterViewModel, which
// declares two more FillFrom overloads (FillFrom(BasicCharacterObject,int,string) and
// FillFrom(CharacterViewModel,int)). A name-only [HarmonyPatch] makes Harmony's AccessTools.Method
// search the full hierarchy, find 3 matches, and throw AmbiguousMatchException at patch time —
// the postfix then never applies (hero-portrait clan colors silently break). Surfaced by
// TAOM.Tests/Migration/HarmonyPatchBindingTests.
[HarmonyPatch(typeof(HeroViewModel), nameof(HeroViewModel.FillFrom),
    new[] { typeof(Hero), typeof(int), typeof(bool), typeof(bool) })]
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
