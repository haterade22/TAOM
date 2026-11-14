using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TAOM.Core.Diagnostics;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch(typeof(CampaignSceneNotificationHelper),
    nameof(CampaignSceneNotificationHelper.CreateNotificationCharacterFromHero))]
[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler
{
    private static IBannerColorService? _service;

    public static void Initialize(IBannerColorService service) => _service = service;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var list = new List<CodeInstruction>(instructions);
        var getMapFaction = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.MapFaction));
        var helperMethod = AccessTools.Method(
            typeof(CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler),
            nameof(GetFactionOrClanFaction));

        int replacements = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].opcode == OpCodes.Callvirt &&
                list[i].operand is MethodInfo mi &&
                mi == getMapFaction)
            {
                list[i] = new CodeInstruction(OpCodes.Call, helperMethod);
                replacements++;
            }
        }

        if (replacements == 0)
            SelfDisables.Report("Patch23_BannerColorPersistence.SceneNotification", "no Hero.MapFaction read in CreateNotificationCharacterFromHero");

        return list;
    }

    public static IFaction? GetFactionOrClanFaction(Hero hero)
    {
        if (_service?.IsEnabled() ?? false)
        {
            var clan = hero?.Clan;
            if (clan != null)
                return clan;
        }

        return hero?.MapFaction;
    }
}
