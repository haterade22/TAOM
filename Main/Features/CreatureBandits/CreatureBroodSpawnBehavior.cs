using System;
using System.Linq;
using Helpers;
using TAOM.Core.Logging;
using TAOM.Features.CreatureBandits.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// Keeps the spider broods (#692) around Mirkwood. The brood clan is a looter faction (its culture has no
/// settlements), and vanilla spawns looters around any town or village on the map; <c>TaomBanditDensityModel</c>
/// caps that at zero for this clan and this behavior spawns instead: one brood a day while fewer than
/// <see cref="CreatureBanditsConfig.MaxBroods"/> live, around a random Mirkwood anchor, told to patrol it. The steps
/// after creation are vanilla's own for a looter party (v1.5.3 <c>BanditSpawnCampaignBehavior.SpawnLooterParty</c>
/// and <c>InitializeBanditParty</c>, lines 465-476 and 575-582): visual dirty, clan, aggressiveness, trade. No food:
/// a bandit party never eats (<c>DefaultMobilePartyFoodConsumptionModel.cs:87-94</c>).
///
/// A brood that finished a chase or a fight without its patrol order gets it back on the next daily tick. No save
/// data: the count is read from the clan each day, so there is nothing to reset between campaigns. The clan is
/// defined in XML, which the engine reads only for a new campaign, so an older save without it spawns nothing. The
/// <c>CreatureBroodCampaignDiag</c> calls are temporary diagnostics; every campaign hook here is main-thread.
/// </summary>
internal sealed class CreatureBroodSpawnBehavior : CampaignBehaviorBase
{
    private readonly IModLogger _logger;
    private bool _missingClanLogged;
    private bool _missingAnchorsLogged;

    public CreatureBroodSpawnBehavior(IModLogger logger) => _logger = logger;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore) { }

    private static Clan? BroodClan => Clan.All.FirstOrDefault(c => CreatureBanditRules.IsCreatureBroodClan(c.StringId));

    // The MCM switch, read each day so a change applies at the next tick. It gates new broods only: the looter cap of 0
    // (TaomBanditDensityModel) and the battle swap stay, or vanilla would spawn the clan map-wide.
    private static bool SpawnEnabled => TaomSettings.Instance?.CreatureBanditSpawnBroods ?? CreatureBanditsConfig.DefaultSpawnBroods;

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        _missingClanLogged = _missingAnchorsLogged = false;
        CreatureBroodCampaignDiag.ResetForSession();
        CreatureBroodCampaignDiag.Session(BroodClan);
    }

    private void OnDailyTick()
    {
        try
        {
            var clan = BroodClan;
            if (clan?.DefaultPartyTemplate == null)
            {
                if (!_missingClanLogged)
                    _logger.LogInfo($"[CreatureBandits] No '{CreatureBanditsConfig.BroodClanId}' clan with a party template in this campaign (a save from before #692?); no broods spawn.");
                _missingClanLogged = true;
                return;
            }

            int strays = ReturnStraysToPatrol(clan);
            int existing = clan.WarPartyComponents.Count;
            bool spawn = CreatureBanditRules.BroodsToSpawnToday(existing, CreatureBanditsConfig.MaxBroods, SpawnEnabled) > 0;
            CreatureBroodCampaignDiag.Census(clan, existing, strays, spawn);
            if (spawn)
                SpawnBrood(clan);
        }
        catch (Exception e)
        {
            _logger.LogError($"[CreatureBandits] Daily brood tick failed: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    private void SpawnBrood(Clan clan)
    {
        Settlement[] anchors = CreatureBanditsConfig.BroodAnchorSettlementIds
            .Select(Settlement.Find).Where(s => s != null).ToArray();
        if (anchors.Length == 0)
        {
            if (!_missingAnchorsLogged)
                _logger.LogError("[CreatureBandits] None of the Mirkwood anchor settlements exist on this map; no broods spawn.");
            _missingAnchorsLogged = true;
            return;
        }

        Settlement anchor = anchors[MBRandom.RandomInt(anchors.Length)];
        float radius = CreatureBanditsConfig.SpawnRadiusDays * Campaign.Current.EstimatedAverageBanditPartySpeed * CampaignTime.HoursInDay;
        CampaignVec2 position = NavigationHelper.FindPointAroundPosition(anchor.GatePosition, MobileParty.NavigationType.Default, radius);
        MobileParty brood = BanditPartyComponent.CreateLooterParty(clan.StringId + "_1", clan, anchor, isBossParty: false,
            clan.DefaultPartyTemplate, position);

        brood.Party.SetVisualAsDirty();
        brood.ActualClan = clan;
        brood.Aggressiveness = 1f - 0.2f * MBRandom.RandomFloat;
        brood.InitializePartyTrade(0);
        brood.SetMovePatrolAroundSettlement(anchor, MobileParty.NavigationType.Default, isTargetingPort: false);
        CreatureBroodCampaignDiag.Spawned(brood, anchor, radius);
    }

    private static int ReturnStraysToPatrol(Clan clan)
    {
        int count = 0;
        foreach (WarPartyComponent component in clan.WarPartyComponents)
        {
            MobileParty party = component.MobileParty;
            Settlement? home = party?.HomeSettlement;
            if (party == null || !CreatureBanditRules.NeedsPatrolOrder(home != null, party.MapEvent != null,
                    party.DefaultBehavior == AiBehavior.PatrolAroundPoint))
                continue;
            string was = party.DefaultBehavior.ToString();
            party.SetMovePatrolAroundSettlement(home!, MobileParty.NavigationType.Default, isTargetingPort: false);
            count++;
            CreatureBroodCampaignDiag.Stray(party, was, home!);
        }
        return count;
    }
}
