using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;

namespace TAOM.Features.StaleCharacterRepair.Hooks;

/// <summary>
/// Patch83, the birth seam (crash bundle 3fbdc0c5, <c>docs/features/stale-character-repair.md</c>).
/// <c>DeliverOffSpring</c> clones the same-sex parent's character, <c>FillFrom</c> copies its raw fields, and the
/// <c>Hero</c> constructor reads <c>StealthEquipments</c> on a clone that is not a hero yet, so a null raw culture throws
/// at <c>CharacterObject.cs:150</c>. The Prefix's <c>__state</c> carries "a parent was repaired" to the Finalizer, whose
/// line says how the birth ended.
/// </summary>
[HarmonyPatch(typeof(HeroCreator), nameof(HeroCreator.DeliverOffSpring))]
[HarmonyPatchCategory("Patch83_StaleCharacterRepair")]
public static class Patch83_OffspringParentRepair
{
    [HarmonyPrefix]
    public static void Prefix(Hero mother, Hero father, out bool __state)
    {
        __state = false;
        var service = Patch83_StaleCharacterRepair.Service;
        if (service == null) return;

        // CampaignCheats.GenerateChild passes a null spouse for an unmarried hero.
        if (mother != null) __state |= service.RepairOffspringParent(new HeroCharacterStateAdapter(mother));
        if (father != null) __state |= service.RepairOffspringParent(new HeroCharacterStateAdapter(father));
    }

    [HarmonyFinalizer]
    public static void Finalizer(Exception __exception, Hero __result, bool __state)
    {
        if (__state)
            Patch83_StaleCharacterRepair.Service?.ReportBirthAfterRepair(__result?.StringId, __exception);
    }
}
