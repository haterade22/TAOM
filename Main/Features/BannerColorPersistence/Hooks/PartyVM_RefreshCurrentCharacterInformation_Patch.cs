using System.Reflection;
using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class PartyVM_RefreshCurrentCharacterInformation_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(PartyVM), "RefreshCurrentCharacterInformation");

    [HarmonyPostfix]
    public static void Postfix(PartyVM __instance)
    {
        var currentCharacter = __instance.CurrentCharacter;
        if (currentCharacter == null) return;
        if (currentCharacter.IsPrisoner) return;

        PartyBase? ownerParty = null;
        int side = (int)currentCharacter.Side;
        if (side == 1)
            ownerParty = __instance.PartyScreenLogic.RightOwnerParty;
        else if (side == 0)
            ownerParty = __instance.PartyScreenLogic.LeftOwnerParty;

        var leaderHero = ownerParty?.LeaderHero;
        if (leaderHero == null) return;

        var info = _heroAdapter?.GetClanColorInfoFromHero(leaderHero);
        if (info == null) return;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        var selectedCharacter = __instance.SelectedCharacter;
        if (selectedCharacter == null) return;

        selectedCharacter.ArmorColor1 = info.Value.Color1;
        selectedCharacter.ArmorColor2 = info.Value.Color2;
    }
}
