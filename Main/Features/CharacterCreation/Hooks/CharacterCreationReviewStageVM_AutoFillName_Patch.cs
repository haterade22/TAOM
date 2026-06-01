using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation.Hooks;

/// <summary>
/// Pre-fills the Character Creation Review stage "Enter your name" field with a culture-appropriate
/// first name when it would otherwise be blank. Vanilla leaves <c>MainCharacterName</c> empty until the
/// player types or clicks the randomize dice (see <c>CharacterCreationReviewStageVM.cs:221</c> — the
/// constructor seeds <c>Name</c> from the empty <c>MainCharacterName</c>). This postfix runs at the
/// Review stage (not at culture-confirm) on purpose: gender is finalized by then, so the generated name
/// matches the chosen sex. Delegates to the VM's own public <c>ExecuteRandomizeName()</c>, which draws
/// from <c>SelectedCulture</c> + <c>Hero.MainHero.IsFemale</c> and raises the bound property change.
/// The empty-guard means a name the player already entered is never clobbered, and the field stays fully
/// editable (typing or re-rolling the dice overrides it).
/// </summary>
[HarmonyPatch(typeof(CharacterCreationReviewStageVM), MethodType.Constructor, new Type[]
{
    typeof(CharacterCreationManager), typeof(Action), typeof(TextObject),
    typeof(Action), typeof(TextObject), typeof(bool)
})]
[HarmonyPatchCategory("Patch44_CCNameAutofill")]
public static class CharacterCreationReviewStageVM_AutoFillName_Patch
{
    [HarmonyPostfix]
    public static void Postfix(CharacterCreationReviewStageVM __instance)
    {
        if (__instance == null) return;

        try
        {
            if (string.IsNullOrWhiteSpace(__instance.Name))
                __instance.ExecuteRandomizeName();
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>()?.LogWarning($"[Patch44-CCNameAutofill] failed: {ex.Message}"); } catch { }
        }
    }
}
