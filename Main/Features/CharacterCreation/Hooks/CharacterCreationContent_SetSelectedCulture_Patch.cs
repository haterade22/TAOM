using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.Music;

namespace TAOM.Features.CharacterCreation.Hooks;

/// <summary>
/// After the player selects a culture during Character Creation, apply the TAOM-configured
/// per-culture default <see cref="BodyProperties"/> from
/// <c>Main/_Module/ModuleData/character_creation/cc_body_properties.xml</c>. Re-applies on every
/// culture change so the preview reflects the freshly-selected culture's body silhouette.
/// </summary>
[HarmonyPatch(typeof(CharacterCreationContent), nameof(CharacterCreationContent.SetSelectedCulture))]
[HarmonyPatchCategory("Patch29_CCBodyProperties")]
public static class CharacterCreationContent_SetSelectedCulture_Patch
{
    [HarmonyPostfix]
    public static void Postfix(CultureObject culture)
    {
        if (culture == null) return;

        try
        {
            var service = IoC.Resolve<ICCBodyPropertiesService>();
            service?.ApplyForCulture(culture.StringId);
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>()?.LogWarning($"[Patch29-CCBodyProperties] failed: {ex.Message}"); } catch { }
        }

        try
        {
            var musicContext = IoC.Resolve<ICharacterCreationMusicContextService>();
            musicContext?.ConfirmCulture(culture.StringId);
            CharacterCreationMusicSmokeTrace.CultureConfirmed(culture.StringId);
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>()?.LogWarning($"[Patch46-Music] character creation culture signal failed: {ex.Message}"); } catch { }
        }
    }
}
