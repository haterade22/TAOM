using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

[HarmonyPatch(typeof(EncounterManager), nameof(EncounterManager.StartPartyEncounter))]
[HarmonyPatchCategory(Category)]
public static class Patch173_OrphanedBattleEncounter
{
    internal const string Category = "Patch173_OrphanedBattleEncounter";

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
    public static bool Prefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (_service == null)
            return true;

        try
        {
            var main = PartyBase.MainParty;
            var armyLeader = MobileParty.MainParty?.Army?.LeaderParty?.Party;
            var battle = defenderParty?.MapEvent;
            bool inPlayerBattle = battle != null && (battle.IsPlayerMapEvent || battle == PlayerEncounter.Battle);
            if (battle == null || !OrphanedBattleService.IsOrphanedEncounter(main != null && attackerParty == main,
                    armyLeader != null && attackerParty == armyLeader, battle.IsFinalized, inPlayerBattle))
                return true;
            return !_service.FinishBeforeEncounter(battle, () => defenderParty!.MapEvent?.IsFinalized == true);
        }
        catch (Exception ex)
        {
            if (!_loggedFault)
            {
                _loggedFault = true;
                _logger?.LogError($"[MapEventGuard] the orphaned battle encounter guard threw and is deferring to vanilla: {ex}");
            }
            return true;
        }
    }
}
