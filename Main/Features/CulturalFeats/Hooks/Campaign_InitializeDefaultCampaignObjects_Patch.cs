using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.CulturalFeats.Hooks;

[HarmonyPatch(typeof(Campaign), "InitializeDefaultCampaignObjects")]
[HarmonyPatchCategory("Patch18_CulturalFeats")]
public static class Campaign_InitializeDefaultCampaignObjects_Patch
{
    public static void Postfix()
    {
        TaomCulturalFeats.CreateAndRegister();
        IoC.Resolve<IModLogger>().LogInfo("CulturalFeats: all TAOM cultural feats registered");
    }
}
