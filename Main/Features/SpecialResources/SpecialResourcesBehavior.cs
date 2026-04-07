using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
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

    private PartyScreenLogic _activePartyScreenLogic;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, OnDailyTickHero);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, OnRaidCompleted);
        CampaignEvents.OnPrisonerTakenEvent.AddNonSerializedListener(this, OnPrisonerTaken);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
        CampaignEvents.OnHideoutBattleCompletedEvent.AddNonSerializedListener(this, OnHideoutCompleted);
        ScreenManager.OnPushScreen += OnScreenPushed;
    }

    public override void SyncData(IDataStore dataStore)
    {
        var data = _storage.GetAllData();
        dataStore.SyncData("_taom_specialResources", ref data);
        _storage.RestoreData(data);

        var hero = Hero.MainHero;
        var kingdomId = hero?.Clan?.Kingdom?.StringId;
        if (kingdomId != null)
        {
            var resource = _config.GetByKingdomId(kingdomId);
            if (resource != null)
                _storage.ClampAll(resource.Cap);
        }
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

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        var hero = Hero.MainHero;
        if (hero == null) return;

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        if (kingdomId == null) return;

        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return;

        var current = _storage.Get(hero.StringId);
        if (current <= 0f && resource.StartingAmount > 0f)
        {
            _storage.Set(hero.StringId, resource.StartingAmount);
            _logger.LogInfo($"SpecialResources: Seeded {resource.DisplayName} = {resource.StartingAmount} for legacy save ({hero.Name})");
        }
    }

    private void OnDailyTickHero(Hero hero)
    {
        if (hero != Hero.MainHero) return;

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        if (kingdomId == null) return;

        var resource = _service.GetResourceForKingdom(kingdomId);
        if (resource == null) return;

        var ownedTowns = CountOwnedTowns(hero);
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

            var enemySide = mapEvent.BattleState == BattleState.AttackerVictory
                ? mapEvent.DefenderSide : mapEvent.AttackerSide;
            var enemyCount = 0;
            foreach (var p in enemySide.Parties)
                enemyCount += p.Party?.NumberOfAllMembers ?? 0;

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
        if (component?.MapEvent == null || !component.MapEvent.IsPlayerMapEvent) return;

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

        var count = 0;
        if (roster != null)
            foreach (var _ in roster)
                count++;
        if (count > 0)
            _service.EarnFromPrisoners(hero.StringId, kingdomId, count);
    }

    private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
    {
        if (winner != Hero.MainHero?.CharacterObject) return;

        var kingdomId = Hero.MainHero?.Clan?.Kingdom?.StringId;
        if (kingdomId == null) return;

        _service.EarnFromTournament(Hero.MainHero.StringId, kingdomId);
    }

    private void OnHideoutCompleted(BattleSideEnum winnerSide, HideoutEventComponent component)
    {
        if (winnerSide != BattleSideEnum.Attacker) return;
        if (component?.MapEvent == null || !component.MapEvent.IsPlayerMapEvent) return;

        var hero = Hero.MainHero;
        var kingdomId = hero?.Clan?.Kingdom?.StringId;
        if (kingdomId == null) return;

        _service.EarnFromHideout(hero.StringId, kingdomId);
    }

    private void OnScreenPushed(ScreenBase screen)
    {
        if (screen?.GetType().Name != "GauntletPartyScreen") return;

        _service.BeginPartyScreenSession();
    }

    public void AttachToPartyScreen(PartyScreenLogic logic)
    {
        if (_activePartyScreenLogic != null) return;

        _activePartyScreenLogic = logic;
        _activePartyScreenLogic.PartyScreenClosedEvent += OnPartyScreenClosed;
        _activePartyScreenLogic.AfterReset += OnPartyScreenReset;
    }

    private void OnPartyScreenClosed(
        PartyBase leftOwner, TroopRoster leftMembers, TroopRoster leftPrisoners,
        PartyBase rightOwner, TroopRoster rightMembers, TroopRoster rightPrisoners,
        bool fromCancel)
    {
        if (_activePartyScreenLogic != null)
        {
            _activePartyScreenLogic.PartyScreenClosedEvent -= OnPartyScreenClosed;
            _activePartyScreenLogic.AfterReset -= OnPartyScreenReset;
            _activePartyScreenLogic = null;
        }

        if (fromCancel)
            _service.CancelSession();
        else
            _service.CommitSession(Hero.MainHero?.StringId);
    }

    private void OnPartyScreenReset(PartyScreenLogic logic, bool fromCancel)
    {
        _service.CancelSession();
        _service.BeginPartyScreenSession();
    }

    private static int CountOwnedTowns(Hero hero)
    {
        var settlements = hero.Clan?.Settlements;
        if (settlements == null) return 0;

        var count = 0;
        foreach (var settlement in settlements)
            if (settlement.IsTown)
                count++;
        return count;
    }

    private List<TroopUpkeepInfo> GetTroopUpkeepFromParty(MobileParty party)
    {
        if (party?.MemberRoster == null) return _emptyUpkeep;

        var result = new List<TroopUpkeepInfo>(8);
        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            if (element.Character != null && _config.GetTroopCost(element.Character.StringId) != null)
                result.Add(new TroopUpkeepInfo(element.Character.StringId, element.Number));
        }

        return result;
    }

    private static readonly List<TroopUpkeepInfo> _emptyUpkeep = new();
}
