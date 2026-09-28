using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade.View;
using TAOM.Features.CreatureBandits.Diagnostics;

namespace TAOM.Features.CreatureBandits.Hooks;

/// <summary>
/// A spider brood (#692) shows on the campaign map as a spider alone. A bandit party's icon is its visual leader,
/// the troop at roster index 0 (the pale broodmother, first in the template), drawn as a rider (this helper) on its
/// Horse-slot mount (built separately from the leader's equipment, <c>MobilePartyVisual.cs:1128-1167</c>). No rider
/// visual means no rider: <c>AddCharacterToPartyIcon</c> guards every use of it, as it must at sea. No banner is lost:
/// the helper draws one only for a leader hero's clan banner (<c>SandBoxViewHelpers.cs:112-118</c>), and a brood has
/// no hero. The target is resolved by name, as BannerColorPersistence's transpiler on the same method does.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(CreatureBanditsConfig.CampaignPatchCategory)]
public static class Patch94_CreatureBroodMapIcon
{
    internal static MethodBase? TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("SandBox.View.SandBoxViewHelpers+MobilePartyVisualHelper"),
            "GetHumanAgentPartyVisual");

    [HarmonyPrefix]
    public static bool Prefix(PartyBase party, ref AgentVisuals? __result, ref float animationDuration)
    {
        if (!CreatureBanditRules.IsCreatureTroop(PartyBaseHelper.GetVisualPartyLeader(party)?.StringId))
            return true;
        CreatureBroodCampaignDiag.NoteMapIconRiderSkipped(party);
        __result = null;
        animationDuration = 0f;
        return false;
    }
}

/// <summary>
/// Spiders do not parley. Meeting a bandit party opens a conversation with its highest-tier troop, here the
/// broodmother, through <c>PlayerEncounter.DoMeeting</c>, which the <c>encounter_meeting</c> menu calls until the
/// meeting is done; once it is done, the same init starts the battle and opens the <c>encounter</c> menu
/// (<c>EncounterGameMenuBehavior.cs:2245-2271</c>). Marking the meeting done first sends a brood straight there.
/// </summary>
[HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_encounter_meeting_on_init")]
[HarmonyPatchCategory(CreatureBanditsConfig.CampaignPatchCategory)]
public static class Patch94_CreatureBroodNoParley
{
    [HarmonyPrefix]
    public static void Prefix()
    {
        if (PlayerEncounter.Current == null || PlayerEncounter.MeetingDone
            || !CreatureBanditRules.IsCreatureBroodClan(PlayerEncounter.EncounteredMobileParty?.ActualClan?.StringId))
            return;
        PlayerEncounter.SetMeetingDone();
        CreatureBroodCampaignDiag.NoteNoParley(PlayerEncounter.EncounteredMobileParty);
    }
}
