using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
using TAOM.Core.Logging;

namespace TAOM.Features.Music.Hooks;

[HarmonyPatchCategory("Patch46_Music")]
public static class CharacterCreationCultureVM_ExecuteSelectCulture_MusicPatch
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CharacterCreationCultureVM),
            nameof(CharacterCreationCultureVM.ExecuteSelectCulture),
            Type.EmptyTypes);
    }

    [HarmonyPostfix]
    public static void Postfix(CharacterCreationCultureVM __instance)
    {
        if (__instance?.Culture == null)
            return;

        try
        {
            var cultureId = __instance.Culture.StringId;
            IoC.Resolve<ICharacterCreationMusicContextService>()?.SelectCulture(cultureId);
            CharacterCreationMusicSmokeTrace.CultureSelected(cultureId, "vanilla_culture_vm_execute_select");
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>()?.LogWarning($"[Patch46-Music] character creation execute-select culture signal failed: {ex.Message}"); } catch { }
        }
    }
}
