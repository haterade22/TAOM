using HarmonyLib;
using TAOM.Features.SupplyLines.Components;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.SupplyLines.Hooks;

/// <summary>
/// Prefix on the static <see cref="PlayerEncounter"/>.<c>DoMeeting</c>: meeting your own supply
/// caravan never opens vanilla's meeting flow. The caravan's <c>PartyComponent.Leader</c> is null,
/// so vanilla's <c>ConversationHelper.GetConversationCharacterPartyLeader</c> would strike a
/// stranger conversation with the highest-tier roster troop (a template guard or a purchased
/// recruit), and an empty roster hands the conversation a null partner (Codex round-1 P2).
///
/// <para>Instead the encounter is finished on the spot with a one-line info message, exactly the
/// teardown vanilla's own leave path runs from the same menu-init context
/// (<c>game_menu_encounter_meeting_on_init</c>: <c>PlayerEncounter.Finish()</c> +
/// <c>SetMoveModeHold()</c>, verified against the installed 1.4.8). The caravan itself needs no
/// dialog: delivery is proximity-driven and the route visual already narrates progress.</para>
///
/// <para>Coexists with Refuge's prefix on the same method (Patch75): each guard acts only on its
/// own component type, so at most one ever returns false for a given encounter.</para>
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), "DoMeeting")]
[HarmonyPatchCategory("Patch73_SupplyLines")]
public static class SupplyCaravanEncounterPatch
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        try
        {
            var party = PlayerEncounter.EncounteredMobileParty;
            if (party?.PartyComponent is not SupplyCaravanComponent)
                return true;

            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=taom_sl_caravan_meet}Your supply caravan presses on toward your position.").ToString(),
                Colors.Green));
            PlayerEncounter.Finish();
            MobileParty.MainParty?.SetMoveModeHold();
            return false;
        }
        catch
        {
            // A failed guard must degrade to vanilla's meeting, never to a dead encounter.
            return true;
        }
    }
}
