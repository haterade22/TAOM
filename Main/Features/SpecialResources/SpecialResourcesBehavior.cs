using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
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
        _logger.LogInfo("[SpecRes] SyncData called (save/load)");
        var data = _storage.GetAllData();
        dataStore.SyncData("_taom_specialResources", ref data);
        _storage.RestoreData(data);
        _logger.LogInfo($"[SpecRes] SyncData restored {data?.Count ?? 0} entries");

        var hero = Hero.MainHero;
        GetHeroIds(hero, out var kingdomId, out var cultureId);
        var resource = _service.ResolveResource(kingdomId, cultureId);
        if (resource != null)
        {
            _storage.ClampAll(resource.Cap);
            _logger.LogInfo($"[SpecRes] SyncData clamped all values to cap={resource.Cap}");
        }
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        var hero = Hero.MainHero;
        if (hero == null) return;

        GetHeroIds(hero, out var kingdomId, out var cultureId);
        _service.InitializeHero(hero.StringId, kingdomId, cultureId);
        _logger.LogInfo($"SpecialResources: Initialized resource for {hero.Name}");
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        var hero = Hero.MainHero;
        if (hero == null) return;

        GetHeroIds(hero, out var kingdomId, out var cultureId);
        var resource = _service.ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var current = _storage.Get(hero.StringId, resource.Id);
        if (current <= 0f && resource.StartingAmount > 0f)
        {
            _storage.Set(hero.StringId, resource.Id, resource.StartingAmount);
            _logger.LogInfo($"SpecialResources: Seeded {resource.DisplayName} = {resource.StartingAmount} for legacy save ({hero.Name})");
        }
    }

    private void OnDailyTickHero(Hero hero)
    {
        if (hero != Hero.MainHero) return;

        GetHeroIds(hero, out var kingdomId, out var cultureId);
        var resource = _service.ResolveResource(kingdomId, cultureId);
        if (resource == null)
        {
            _logger.LogDebug($"[SpecRes] DailyTick: no resource for hero (kingdom='{kingdomId}', culture='{cultureId}')");
            return;
        }

        var ownedTowns = CountOwnedTowns(hero);
        var troopUpkeep = GetTroopUpkeepFromParty(hero.PartyBelongedTo);

        _service.ApplyDailyTick(hero.StringId, kingdomId, cultureId, ownedTowns, troopUpkeep);

        // Check balance for warnings and desertion
        var balance = _service.GetCurrentAmount(hero.StringId, kingdomId, cultureId);

        if (balance <= 0f && troopUpkeep.Count > 0)
        {
            // Desertion: remove troops from roster
            var desertions = _service.CalculateDesertion(hero.StringId, kingdomId, cultureId, troopUpkeep);
            var totalDeserted = ApplyDesertion(hero.PartyBelongedTo, desertions);

            if (totalDeserted > 0)
            {
                MBInformationManager.AddQuickInformation(
                    new TextObject($"{{=taom_res_desertion}}{totalDeserted} elite troops deserted — your {resource.DisplayName} are depleted!"),
                    extraTimeInMs: 3000);
            }
        }
        else if (balance > 0f && balance < resource.Cap * 0.1f)
        {
            // Low resource warning (below 10% of cap)
            InformationManager.DisplayMessage(new InformationMessage(
                $"{resource.DisplayName} running low: {balance:F0}/{resource.Cap:F0}",
                Colors.Yellow));
        }
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        if (!mapEvent.IsPlayerMapEvent) return;

        var hero = Hero.MainHero;
        GetHeroIds(hero, out var kingdomId, out var cultureId);
        _logger.LogDebug($"[SpecRes] MapEventEnded: state={mapEvent.BattleState}, isSiege={mapEvent.IsSiegeAssault || mapEvent.IsSiegeOutside}");

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

            var resource = _service.ResolveResource(kingdomId, cultureId);
            var before = _service.GetCurrentAmount(hero.StringId, kingdomId, cultureId);

            if (mapEvent.IsSiegeAssault || mapEvent.IsSiegeOutside)
                _service.EarnFromSiege(hero.StringId, kingdomId, cultureId);
            else
                _service.EarnFromBattle(hero.StringId, kingdomId, cultureId, ratio);

            if (resource != null)
            {
                var after = _service.GetCurrentAmount(hero.StringId, kingdomId, cultureId);
                var earned = after - before;
                if (earned > 0f)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"+{earned:F0} {resource.DisplayName} earned from victory",
                        Colors.Green));
                }
            }
        }
    }

    private void OnRaidCompleted(BattleSideEnum side, RaidEventComponent component)
    {
        if (side != BattleSideEnum.Attacker) return;
        if (component?.MapEvent == null || !component.MapEvent.IsPlayerMapEvent) return;

        var hero = Hero.MainHero;
        GetHeroIds(hero, out var kingdomId, out var cultureId);

        _service.EarnFromRaid(hero.StringId, kingdomId, cultureId);
        NotifyEarning(hero.StringId, kingdomId, cultureId, "raid");
    }

    private void OnPrisonerTaken(FlattenedTroopRoster roster)
    {
        var hero = Hero.MainHero;
        GetHeroIds(hero, out var kingdomId, out var cultureId);

        var count = 0;
        if (roster != null)
            foreach (var _ in roster)
                count++;
        if (count > 0)
        {
            var before = _service.GetCurrentAmount(hero.StringId, kingdomId, cultureId);
            _service.EarnFromPrisoners(hero.StringId, kingdomId, cultureId, count);
            NotifyEarningDelta(kingdomId, cultureId, hero.StringId, before, "prisoners");
        }
    }

    private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
    {
        if (winner != Hero.MainHero?.CharacterObject) return;

        GetHeroIds(Hero.MainHero, out var kingdomId, out var cultureId);
        _service.EarnFromTournament(Hero.MainHero.StringId, kingdomId, cultureId);
        NotifyEarning(Hero.MainHero.StringId, kingdomId, cultureId, "tournament");
    }

    private void OnHideoutCompleted(BattleSideEnum winnerSide, HideoutEventComponent component)
    {
        if (winnerSide != BattleSideEnum.Attacker) return;
        if (component?.MapEvent == null || !component.MapEvent.IsPlayerMapEvent) return;

        var hero = Hero.MainHero;
        GetHeroIds(hero, out var kingdomId, out var cultureId);

        _service.EarnFromHideout(hero.StringId, kingdomId, cultureId);
        NotifyEarning(hero.StringId, kingdomId, cultureId, "hideout");
    }

    private void NotifyEarning(string heroId, string kingdomId, string cultureId, string source)
    {
        var resource = _service.ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var amount = _service.GetCurrentAmount(heroId, kingdomId, cultureId);
        InformationManager.DisplayMessage(new InformationMessage(
            $"{resource.DisplayName} earned from {source} (total: {amount:F0})",
            Colors.Green));
    }

    private void NotifyEarningDelta(string kingdomId, string cultureId, string heroId, float before, string source)
    {
        var resource = _service.ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var after = _service.GetCurrentAmount(heroId, kingdomId, cultureId);
        var earned = after - before;
        if (earned > 0f)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"+{earned:F0} {resource.DisplayName} from {source}",
                Colors.Green));
        }
    }

    private int ApplyDesertion(MobileParty party, IReadOnlyList<TroopDesertionEntry> desertions)
    {
        if (party?.MemberRoster == null || desertions == null || desertions.Count == 0)
            return 0;

        var totalDeserted = 0;
        foreach (var entry in desertions)
        {
            var character = CharacterObject.Find(entry.TroopId);
            if (character == null) continue;

            var index = party.MemberRoster.FindIndexOfTroop(character);
            if (index < 0) continue;

            var currentCount = party.MemberRoster.GetElementNumber(index);
            var toRemove = System.Math.Min(entry.DesertCount, currentCount);
            if (toRemove <= 0) continue;

            party.MemberRoster.AddToCounts(character, -toRemove);
            totalDeserted += toRemove;
            _logger.LogInfo($"[SpecRes] Deserted: {entry.TroopId} x{toRemove}");
        }

        return totalDeserted;
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
        {
            _service.CancelSession();
        }
        else
        {
            var hero = Hero.MainHero;
            GetHeroIds(hero, out var kingdomId, out var cultureId);
            _service.CommitSession(hero?.StringId, kingdomId, cultureId);
        }
    }

    private void OnPartyScreenReset(PartyScreenLogic logic, bool fromCancel)
    {
        _service.CancelSession();
        _service.BeginPartyScreenSession();
    }

    private static void GetHeroIds(Hero hero, out string kingdomId, out string cultureId)
    {
        kingdomId = hero?.Clan?.Kingdom?.StringId;
        cultureId = hero?.Culture?.StringId;
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
