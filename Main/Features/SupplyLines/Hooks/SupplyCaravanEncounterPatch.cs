using System;
using HarmonyLib;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SupplyLines.Components;
using TaleWorlds.CampaignSystem.Encounters;

namespace TAOM.Features.SupplyLines.Hooks;

/// <summary>
/// Prefix on the static <see cref="PlayerEncounter"/>.<c>DoMeeting</c>: meeting a supply caravan never opens
/// vanilla's meeting flow. The caravan's <c>PartyComponent.Leader</c> is null, so vanilla's
/// <c>ConversationHelper.GetConversationCharacterPartyLeader</c> would strike a stranger conversation with the
/// highest-tier roster troop (a template guard or a purchased recruit), and an empty roster hands the conversation a
/// null partner (Codex round-1 P2).
///
/// <para>The prefix opens <see cref="SupplyCaravanMeetingMenu"/> and leaves the encounter open. <c>DoMeeting</c> runs
/// inside <c>PlayerEncounter.Init</c>, and <c>EncounterManager.RestartPlayerEncounter</c> reads the current encounter
/// on the line after <c>Init</c> (v1.5.6 <c>EncounterManager.cs:339-340</c>), so the encounter ends from the menu's
/// Leave option and never from here (#1104).</para>
///
/// <para>Coexists with Patch75 and Patch143 on the same method: once a bool prefix returns false, Harmony skips the
/// later bool prefixes, so at most one returns false for a given encounter. When the menu does not open, vanilla's
/// meeting runs and Patch143 still guards an empty party.</para>
///
/// <para>Delivery fires at 1.2 map units and runs before the encounter check each frame. The meeting radius is
/// <c>Max(0.5, dt * 10)</c>, so a single player with the feature on reaches this prefix only on a large frame step
/// (dt over 0.12, for example 16x turbo below about 33 fps). A co-op guest, or a player with the feature off, reaches
/// it at normal speed.</para>
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), "DoMeeting")]
[HarmonyPatchCategory("Patch73_SupplyLines")]
public static class SupplyCaravanEncounterPatch
{
    internal const string MeetingCheckId = "taom.supply-lines.caravan-meeting";

    private static IGameMenuAdapter? _menus;
    private static IOutcomeLog? _outcomes;

    /// <summary>The line shown when the player meets a supply caravan: theirs, or a host's in a shared campaign.</summary>
    internal static string MeetingLine(bool defersToHost) => defersToHost
        ? "{=taom_sl_caravan_meet_other}A supply caravan passes on toward its lord."
        : "{=taom_sl_caravan_meet}Your supply caravan presses on toward your position.";

    /// <summary>Skips vanilla only when the caravan's menu verifiably opened.</summary>
    internal static bool RunsVanilla(bool isSupplyCaravan, string? partyId, IGameMenuAdapter menus, IOutcomeLog outcomes)
    {
        if (!isSupplyCaravan)
            return true;

        if (menus.EnsureMenuOpen(SupplyCaravanMeetingMenu.MenuId))
        {
            outcomes.Pass(MeetingCheckId, "", $"party={partyId}");
            return false;
        }

        outcomes.Fail(MeetingCheckId, "menu-not-open", $"party={partyId}");
        return true;
    }

    [HarmonyPrefix]
    public static bool Prefix()
    {
        try
        {
            var party = PlayerEncounter.EncounteredMobileParty;
            return RunsVanilla(
                party?.PartyComponent is SupplyCaravanComponent,
                party?.StringId,
                _menus ??= TAOM.IoC.Resolve<IGameMenuAdapter>(),
                _outcomes ??= TAOM.IoC.Resolve<IOutcomeLog>());
        }
        catch (Exception ex)
        {
            // A failed guard must degrade to vanilla's meeting, never to a dead encounter.
            Outcomes.Swallowed("SupplyLines", "caravan meeting", ex);
            return true;
        }
    }
}
