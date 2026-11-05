using System.Reflection;
using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class PartyCharacterVM_GetCharacterCode_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(PartyCharacterVM), "GetCharacterCode",
            new[]
            {
                typeof(CharacterObject),
                typeof(TaleWorlds.CampaignSystem.Party.PartyScreenLogic.TroopType),
                typeof(TaleWorlds.CampaignSystem.Party.PartyScreenLogic.PartyRosterSide)
            });

    // `PartyCharacterVM` sets `IsPrisoner` (:1103) only after the `Troop` assignment (:1099) has run
    // `GetCharacterCode`, so the old prisoner check always read false; the postfix reads type and side
    // from the arguments (#993).
    internal static bool AppliesTo(PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side) =>
        type != PartyScreenLogic.TroopType.Prisoner && (int)side == 1;

    [HarmonyPostfix]
    public static void Postfix(ref CharacterCode __result, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side)
    {
        if (__result == null) return;
        if (!(_service?.IsEnabled() ?? false)) return;
        if (!AppliesTo(type, side)) return;

        var clan = Clan.PlayerClan;
        if (clan == null) return;

        // Code is built at creation (#993): rebuild it so the colours reach the portrait.
        __result = ClanColoredCharacterCode.Recolor(__result, clan.Color, clan.Color2);
    }
}
