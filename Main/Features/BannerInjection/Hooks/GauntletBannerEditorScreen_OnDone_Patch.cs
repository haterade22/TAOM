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
        var playerClanId = Clan.PlayerClan?.StringId;
        if (playerClanId != null)
        {
            _hook?.OnBannerEditorDone(playerClanId);
        }
    }
}
