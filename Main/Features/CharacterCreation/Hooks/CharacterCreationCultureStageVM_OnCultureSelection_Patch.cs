using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation.Hooks;

/// <summary>
/// Re-applies the TAOM-configured per-culture body AFTER vanilla
/// <c>CharacterCreationCultureStageVM.InitializePlayersFaceKeyAccordingToCultureSelection</c>
/// (called from <c>OnCultureSelection</c>) overwrites it with
/// <c>culture.DefaultCharacterCreationBodyProperty.BodyPropertyMax</c> from
/// <c>sandbox_bodyproperties.xml</c>.
///
/// Verified on installed v1.3.15: <c>OnCultureSelection(CharacterCreationCultureVM)</c> calls
/// <c>InitializePlayersFaceKeyAccordingToCultureSelection(selectedCulture)</c> as its first
/// statement, which calls <c>UpdatePlayerCharacterBodyProperties(culture.DefaultCharacterCreationBodyProperty.BodyPropertyMax, ...)</c>.
/// TAOM's <c>FactionMap.CultureSettingService</c> invokes <c>SetSelectedCulture</c> first
/// (our Patch29 SetSelectedCulture postfix applies our body), THEN
/// <c>cultureVM.ExecuteSelectCulture()</c> which routes through <c>OnCultureSelection</c> —
/// vanilla then writes the culture default body OVER ours. This postfix runs after that
/// overwrite so our body is the final state.
/// </summary>
[HarmonyPatch(typeof(CharacterCreationCultureStageVM), nameof(CharacterCreationCultureStageVM.OnCultureSelection))]
[HarmonyPatchCategory("Patch29_CCBodyProperties")]
public static class CharacterCreationCultureStageVM_OnCultureSelection_Patch
{
    [HarmonyPostfix]
    public static void Postfix(CharacterCreationCultureVM selectedCulture)
    {
        if (selectedCulture?.Culture == null) return;

        try
        {
            var service = IoC.Resolve<ICCBodyPropertiesService>();
            service?.ApplyForCulture(selectedCulture.Culture.StringId);
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>()?.LogWarning($"[Patch29-OnCultureSelection] failed: {ex.Message}"); } catch { }
        }
    }
}
