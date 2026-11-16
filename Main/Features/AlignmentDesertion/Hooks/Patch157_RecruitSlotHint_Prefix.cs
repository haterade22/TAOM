using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.AlignmentDesertion.Hooks;

/// <summary>
/// Patch157, #1189: prefix on <c>RecruitVolunteerTroopVM.ExecuteBeginHint()</c> (v1.5.7). The recruit cap
/// (<see cref="DesertionRecruitCap.CapVolunteerIndex"/>) locks a volunteer who would desert the player's party and every
/// slot after it, at any relation. The VM's constructor searches relations -100 to 99 for one that unlocks the slot,
/// finds none for those slots or for the slot just before the deserter, and vanilla's hint shows "You need 0". For a
/// locked slot the prefix names the cause; for the slot before the deserter it shows the relation that opens it. It then
/// skips vanilla. Every other slot runs vanilla. Display only: the cap itself is unchanged.
/// Notes: docs/reference/harmony-patch-registry.md "Patch157_DesertionRecruitHint".
/// </summary>
[HarmonyPatchCategory(AlignmentDesertionModule.HintPatchCategory)]
[HarmonyPatch(typeof(RecruitVolunteerTroopVM), nameof(RecruitVolunteerTroopVM.ExecuteBeginHint))]
[ClientOnly]
public static class Patch157_RecruitSlotHint_Prefix
{
    private static IDesertionRecruitGuard? _guard;
    private static IModLogger? _logger;
    private static bool _logged;

    public static void Initialize(IDesertionRecruitGuard guard, IModLogger logger)
    {
        _guard = guard;
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(RecruitVolunteerTroopVM __instance)
    {
        var guard = _guard;
        if (guard == null || __instance == null || __instance.PlayerHasEnoughRelation)
            return true;

        try
        {
            var party = MobileParty.MainParty?.Party;
            var owner = __instance.Owner?.OwnerHero;
            var slots = owner?.VolunteerTypes;
            var index = __instance.Index;
            if (party == null || owner == null || slots == null || index < 0 || index >= slots.Length)
                return true;

            var first = DesertionRecruitCap.NoDeserter;
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && guard.WouldDesertFromParty(party, slots[i]))
                {
                    first = i;
                    break;
                }
            }

            // The hovered slot is tested for itself, so a deserter behind another deserter is named as one.
            var slotDeserts = first != DesertionRecruitCap.NoDeserter && slots[index] != null
                && (index == first || guard.WouldDesertFromParty(party, slots[index]));
            var line = Line(DesertionRecruitCap.LockReason(index, first, slotDeserts), index, owner);
            if (line == null)
                return true;

            Show(__instance, line);
            return false;
        }
        catch (Exception ex)
        {
            if (!_logged)
            {
                _logged = true;
                _logger?.LogError($"[AlignmentDesertion] Patch157 recruit hint failed, vanilla hint shown: {ex}");
            }
            return true;
        }
    }

    // The line that replaces vanilla's relation line, or null to run vanilla.
    private static string? Line(SlotLockReason reason, int index, Hero owner)
    {
        switch (reason)
        {
            case SlotLockReason.WouldDesert:
                return new TextObject("{=taom_recruit_slot_would_desert}This volunteer would desert your party: their people will not serve your side.").ToString();
            case SlotLockReason.BehindDeserter:
                return new TextObject("{=taom_recruit_slot_behind_deserter}An earlier volunteer here would desert your party, and volunteers are recruited in order.").ToString();
            case SlotLockReason.BeforeDeserter:
                var required = DesertionRecruitCap.RequiredRelation(index,
                    relation => Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(Hero.MainHero, owner, relation));
                if (required == null)
                    return null;
                GameTexts.SetVariable("REL1", Hero.MainHero.GetRelation(owner));
                GameTexts.SetVariable("REL2", required.Value);
                return GameTexts.FindText("str_recruit_volunteers_not_enough_relation").ToString();
            default:
                return null;
        }
    }

    // Vanilla's two layouts, with the line in place of vanilla's relation line: a tooltip for a volunteer, a hint for an
    // empty slot.
    private static void Show(RecruitVolunteerTroopVM vm, string line)
    {
        if (vm.Character == null)
        {
            GameTexts.SetVariable("newline", "\n");
            GameTexts.SetVariable("STR1", GameTexts.FindText("str_recruit_volunteers_new_troop"));
            GameTexts.SetVariable("STR2", line);
            MBInformationManager.ShowHint(GameTexts.FindText("str_string_newline_string").ToString());
            return;
        }

        var none = "";
        GameTexts.SetVariable("LEVEL", vm.Character.Level);
        InformationManager.ShowTooltip(typeof(List<TooltipProperty>), new List<TooltipProperty>
        {
            new(none, vm.Character.Name.ToString(), 1),
            new(none, none, -1),
            new(none, GameTexts.FindText("str_level_with_value").ToString(), 0),
            new(none, line, 0),
        });
    }
}
