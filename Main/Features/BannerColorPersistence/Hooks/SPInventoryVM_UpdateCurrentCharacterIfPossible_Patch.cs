using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace TAOM.Features.BannerColorPersistence.Hooks;

/// <summary>
/// Paints the inventory doll in the clan's colours. Postfix on the private
/// <c>SPInventoryVM.UpdateCharacterArmorColor()</c> (v1.4.8 SPInventoryVM.cs:4421), which writes
/// <c>MapFaction</c>'s colours (the kingdom's, for a vassal). It is the only vanilla repaint of the doll's
/// colours after the screen is built, reached through <c>UpdateRightCharacter</c> (:4381): opening the screen
/// (:3122), the outfit toggle (:1645), Reset (:3806), a character switch (:4314) and TAOM's
/// <c>InventoryVMAdapter.RefreshPanel</c>. An ordinary equip refreshes the doll without it (:4019-4024) and
/// writes no colour, so the clan colours stay. It targeted
/// <c>UpdateCurrentCharacterIfPossible</c> until #973, so only a character switch kept the clan colours; the
/// class keeps that name because <c>Main/SubModule.cs</c> calls its <see cref="Initialize"/>.
/// </summary>
[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(SPInventoryVM), "UpdateCharacterArmorColor", Type.EmptyTypes);

    [HarmonyPostfix]
    public static void Postfix(SPInventoryVM __instance)
    {
        var currentCharacter = Traverse.Create(__instance)
            .Field("_currentCharacter")
            .GetValue<CharacterObject>();

        if (currentCharacter == null) return;

        // Vanilla paints a stealth outfit one fixed colour (:4423-4427); in stealth mode its ActiveEquipment
        // is FirstStealthEquipment (:462-463).
        if (__instance.EquipmentMode == (int)SPInventoryVM.EquipmentModes.Stealth
            && currentCharacter.FirstStealthEquipment?.IsStealth == true)
            return;

        var info = _heroAdapter?.GetClanColorInfo(currentCharacter);
        if (info == null) return;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        __instance.MainCharacter.ArmorColor1 = info.Value.Color1;
        __instance.MainCharacter.ArmorColor2 = info.Value.Color2;
    }
}
