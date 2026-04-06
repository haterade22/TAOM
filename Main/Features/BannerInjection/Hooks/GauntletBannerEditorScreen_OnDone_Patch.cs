using HarmonyLib;
using SandBox.GauntletUI.BannerEditor;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerInjection.Hooks;

[HarmonyPatch(typeof(GauntletBannerEditorScreen), "OnDone")]
[HarmonyPatchCategory("Patch6_BannerEditor")]
public static class GauntletBannerEditorScreen_OnDone_Patch
{
    private static IOnBannerEditorDone _hook;

    public static void Initialize(IOnBannerEditorDone hook)
    {
        _hook = hook;
    }

    [HarmonyPostfix]
    public static void Postfix()
    {
        var playerClan = Clan.PlayerClan;
        if (playerClan?.StringId != null)
        {
            var kingdomId = playerClan.Kingdom?.RulingClan == playerClan
                ? playerClan.Kingdom.StringId
                : null;
            _hook?.OnBannerEditorDone(playerClan.StringId, kingdomId);
        }
    }
}
