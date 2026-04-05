using System.Reflection;
using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class ClanPartyItemVM_GetCharacterCode_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(ClanPartyItemVM), "GetCharacterCode",
            new[] { typeof(CharacterObject) });

    [HarmonyPostfix]
    public static void Postfix(ref CharacterCode __result, CharacterObject character)
    {
        if (__result == null) return;
        var info = _heroAdapter?.GetClanColorInfo(character);
        if (info == null) return;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        __result.Color1 = info.Value.Color1;
        __result.Color2 = info.Value.Color2;
    }
}
