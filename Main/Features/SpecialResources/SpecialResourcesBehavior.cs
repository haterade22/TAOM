using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources;

public class SpecialResourcesBehavior : CampaignBehaviorBase
{
    private readonly ISpecialResourceService _service;
    private readonly ISpecialResourceStorageService _storage;
    private readonly ISpecialResourceConfigProvider _config;
    private readonly IModLogger _logger;

    public SpecialResourcesBehavior(
        ISpecialResourceService service,
        ISpecialResourceStorageService storage,
        ISpecialResourceConfigProvider config,
        IModLogger logger)
    {
        _service = service;
        _storage = storage;
        _config = config;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, OnDailyTickHero);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, OnRaidCompleted);
        CampaignEvents.OnPrisonerTakenEvent.AddNonSerializedListener(this, OnPrisonerTaken);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
    }

    public override void SyncData(IDataStore dataStore)
    {
        _storage.SyncData(dataStore);
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        var hero = Hero.MainHero;
        if (hero == null) return;

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        if (kingdomId == null) return;

        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return;

        _storage.Set(hero.StringId, resource.StartingAmount);
        _logger.LogInfo($"SpecialResources: Initialized {resource.DisplayName} = {resource.StartingAmount} for {hero.Name}");
    }

    private void OnDailyTickHero(Hero hero)
    {
        if (hero != Hero.MainHero) return;

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        if (kingdomId == null) return;

        var resource = _service.GetResourceForKingdom(kingdomId);
        if (resource == null) return;

        var ownedTowns = hero.Clan?.Settlements?.Count(s => s.IsTown) ?? 0;
        var troopUpkeep = GetTroopUpkeepFromParty(hero.PartyBelongedTo);

        _service.ApplyDailyTick(hero.StringId, kingdomId, ownedTowns, troopUpkeep);
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        if (!mapEvent.IsPlayerMapEvent) return;

        var hero = Hero.MainHero;
        var kingdomId = hero?.Clan?.Kingdom?.StringId;
        if (kingdomId == null) return;

        if (mapEvent.BattleState == BattleState.AttackerVictory || mapEvent.BattleState == BattleState.DefenderVictory)
        {
            var isPlayerVictor = (mapEvent.AttackerSide.LeaderParty?.LeaderHero == hero && mapEvent.BattleState == BattleState.AttackerVictory)
                || (mapEvent.DefenderSide.LeaderParty?.LeaderHero == hero && mapEvent.BattleState == BattleState.DefenderVictory);

            if (!isPlayerVictor) return;

            var enemyCount = mapEvent.BattleState == BattleState.AttackerVictory
                ? mapEvent.DefenderSide.Parties.Sum(p => p.Party?.NumberOfAllMembers ?? 0)
                : mapEvent.AttackerSide.Parties.Sum(p => p.Party?.NumberOfAllMembers ?? 0);

            var playerCount = hero.PartyBelongedTo?.MemberRoster?.TotalManCount ?? 1;
            var ratio = (float)enemyCount / playerCount;

            if (mapEvent.IsSiegeAssault || mapEvent.IsSiegeOutside)
                _service.EarnFromSiege(hero.StringId, kingdomId);
            else
                _service.EarnFromBattle(hero.StringId, kingdomId, ratio);
        }
    }

    private void OnRaidCompleted(BattleSideEnum side, RaidEventComponent component)
    {
        if (side != BattleSideEnum.Attacker) return;

        var hero = Hero.MainHero;
        var kingdomId = hero?.Clan?.Kingdom?.StringId;
        if (kingdomId == null) return;

        _service.EarnFromRaid(hero.StringId, kingdomId);
    }

    private void OnPrisonerTaken(FlattenedTroopRoster roster)
    {
        var hero = Hero.MainHero;
        var kingdomId = hero?.Clan?.Kingdom?.StringId;
        if (kingdomId == null) return;

        var count = roster?.Count() ?? 0;
        if (count > 0)
            _service.EarnFromPrisoners(hero.StringId, kingdomId, count);
    }

    private List<TroopUpkeepInfo> GetTroopUpkeepFromParty(MobileParty party)
    {
        var result = new List<TroopUpkeepInfo>();
        if (party?.MemberRoster == null) return result;

        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            if (element.Character != null && _config.GetTroopCost(element.Character.StringId) != null)
            {
                result.Add(new TroopUpkeepInfo(element.Character.StringId, element.Number));
            }
        }

        return result;
    }
}
