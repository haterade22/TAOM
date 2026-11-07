using HarmonyLib;
using TAOM.Features.CoopInterop;
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
///
/// <para>CO-OP (#1086): a guest's caravans are the host's, so a guest who meets one gets the same
/// teardown (vanilla's meeting would still find no leader) with a line that does not call it theirs.</para>
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), "DoMeeting")]
[HarmonyPatchCategory("Patch73_SupplyLines")]
public static class SupplyCaravanEncounterPatch
{
    private static ICoopSessionProvider? _coop;

    /// <summary>The line shown when the player meets a supply caravan: theirs, or a host's in a shared campaign.</summary>
    internal static string MeetingLine(bool defersToHost) => defersToHost
        ? "{=taom_sl_caravan_meet_other}A supply caravan passes on toward its lord."
        : "{=taom_sl_caravan_meet}Your supply caravan presses on toward your position.";

    [HarmonyPrefix]
    public static bool Prefix()
    {
        try
        {
            var party = PlayerEncounter.EncounteredMobileParty;
            if (party?.PartyComponent is not SupplyCaravanComponent)
                return true;

            // Only the wording depends on the role, so a failed resolve keeps the teardown and the owner's line.
            bool defers = false;
            try { defers = (_coop ??= TAOM.IoC.Resolve<ICoopSessionProvider>())?.ShouldDeferToHost == true; }
            catch { /* swallow-ok: only the wording depends on the role; the teardown and the owner's line stand */ }
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject(MeetingLine(defers)).ToString(),
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
