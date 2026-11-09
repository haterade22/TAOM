using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// Patch142 (#1099, crash bundles 9d1db520 and 66372c43): no sally-out check opens the player's meeting
/// with a besieger leader that has nobody left to speak for it. <see cref="SallyOutGuardService"/> holds the rule.
///
/// <c>CheckSallyOut</c> calls <c>StartPartyEncounter(garrison, besiegerLeader)</c>. With the besieger leader
/// inside the player's map event, <c>RestartPlayerEncounter</c> runs <c>Finish</c>, then <c>Init</c>, which can
/// open <c>encounter_meeting</c>. <c>GetConversationCharacterPartyLeader</c> returns null for a party with no
/// leader hero and no men, <c>CreateConversationTableau</c> then throws after the conversation layer exists,
/// and <c>MapConversationTableau.OnTick</c> throws on every later frame.
///
/// The guard sits here and not on <c>DoMeeting</c>: <c>PlayerEncounter.Finish</c> clears the encounter,
/// and on this path <c>RestartPlayerEncounter</c> reads <c>PlayerEncounter.Current</c> after the menu returns.
/// </summary>
[HarmonyPatch(typeof(SallyOutsCampaignBehavior), "CheckSallyOut")]
[HarmonyPatchCategory(Category)]
public static class Patch142_SallyOutEmptyPartyGuard
{
    internal const string Category = "Patch142_SallyOutEmptyPartyGuard";

    private static SallyOutGuardService? _service;
    private static IModLogger? _logger;

    internal static void Initialize(SallyOutGuardService? service, IModLogger? logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(Settlement settlement, ref bool salliedOut)
    {
        if (_service == null)
            return true;

        try
        {
            if (!_service.ShouldStopSally(new SallySiegeAdapter(settlement, MobileParty.MainParty)))
                return true;

            salliedOut = false;
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[MapEventGuard] the sally-out guard threw and is deferring to vanilla: {ex}");
            return true;
        }
    }
}
