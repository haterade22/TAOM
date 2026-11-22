using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

[HarmonyPatch(typeof(EncounterManager), nameof(EncounterManager.StartSettlementEncounter))]
[HarmonyPatchCategory(Patch173_OrphanedBattleEncounter.Category)]
public static class Patch173_OrphanedBattleSettlementEncounter
{
    private static OrphanedBattleService? _service;
    private static IModLogger? _logger;
    private static bool _loggedFault;

    internal static void Initialize(OrphanedBattleService? service, IModLogger? logger)
    {
        _service = service;
        _logger = logger;
        _loggedFault = false;
    }

    [HarmonyPrefix]
    public static void Prefix(MobileParty attackerParty, Settlement settlement)
    {
        if (_service == null || settlement == null)
            return;

        try
        {
            var main = MobileParty.MainParty;
            var armyLeader = main?.Army?.LeaderParty;
            var battle = FinalizedBattleAt(settlement);
            bool inPlayerBattle = battle != null && (battle.IsPlayerMapEvent || battle == PlayerEncounter.Battle);
            if (battle == null || !OrphanedBattleService.IsOrphanedEncounter(main != null && attackerParty == main,
                    armyLeader != null && attackerParty == armyLeader, battle.IsFinalized, inPlayerBattle))
                return;
            _service.FinishBeforeEncounter(battle, () => FinalizedBattleAt(settlement) == battle);
        }
        catch (Exception ex)
        {
            if (!_loggedFault)
            {
                _loggedFault = true;
                _logger?.LogError($"[MapEventGuard] the orphaned battle settlement guard threw and is deferring to vanilla: {ex}");
            }
        }
    }

    private static MapEvent? FinalizedBattleAt(Settlement settlement)
    {
        var battle = settlement.Party?.MapEvent;
        if (battle?.IsFinalized == true)
            return battle;
        battle = settlement.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapEvent;
        return battle?.IsFinalized == true ? battle : null;
    }
}
