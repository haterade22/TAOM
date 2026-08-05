using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Quests;

/// <summary>
/// Thin <see cref="QuestBase"/> shell for one data-driven career tier quest. It carries the saveable
/// progress + journal logs and forwards verified-1.4.5 campaign events / ticks to
/// <see cref="ICareerQuestService"/>; all decision logic (progress math, completion, rewards) lives
/// in the service. Count objectives are event-driven (<c>OnPlayerBattleEndEvent</c>,
/// <c>OnSettlementOwnerChangedEvent</c>, <c>TournamentFinished</c>, <c>HeroPrisonerTaken</c>,
/// <c>SettlementEntered</c>); threshold objectives (skill/renown/gold) are polled in
/// <see cref="DailyTick"/>. Engine-constructed/deserialized, so services resolve lazily via IoC
/// (boundary exception to the no-service-locator rule).
/// </summary>
public class CareerQuest : QuestBase
{
    [SaveableField(1)] private string _questDefId;
    [SaveableField(2)] private string _heroStringId;
    [SaveableField(3)] private string _titleKey;
    [SaveableField(4)] private List<int> _progress;
    [SaveableField(5)] private List<JournalLog> _logs;

    private CareerQuestDefinition _def;
    private IQuestHeroAdapter _heroAdapter;
    private ICareerQuestService _service;
    private IQuestHeroAdapterFactory _heroFactory;
    private IModLogger _logger;

    public CareerQuest(string questId, Hero questGiver, CareerQuestDefinition def)
        // QuestDueTime is ABSOLUTE (verified 1.4.5) — use a far-future relative time so the quest
        // never times out (the remaining time is hidden anyway). NOT CampaignTime.Years(n), which
        // is an absolute year < campaign start and would make the quest instantly overdue.
        : base(questId, questGiver, CampaignTime.DaysFromNow(1000000f), 0)
    {
        _def = def;
        _questDefId = def?.Id;
        _heroStringId = questGiver?.StringId;
        _titleKey = def?.TitleKey;
        _progress = new List<int>();
        _logs = new List<JournalLog>();
    }

    /// <summary>
    /// StringId of the hero this quest belongs to. Survives save/load via
    /// <c>_heroStringId</c>, unlike <c>QuestGiver</c>, which can be null before objects resolve.
    ///
    /// CO-OP: the reason this is exposed. Career quests are per-player, but
    /// <c>QuestManager.Quests</c> is global and a client loads the HOST's save — so a dedup scan
    /// that does not filter by owner blocks a client from ever being offered a career quest while
    /// the host has one running. (Codex P2, 2026-08-01.)
    /// </summary>
    public string OwnerHeroStringId => _heroStringId;

    /// <summary>The career-quest definition id this quest is tracking (for dedup against active quests).</summary>
    public string CareerQuestDefId => _questDefId;

    public override TextObject Title =>
        new TextObject(string.IsNullOrEmpty(_titleKey) ? "{=taom_cq_title}Career Quest" : _titleKey);

    public override bool IsRemainingTimeHidden => true;

    // QuestManager.OnGameLoaded (1.4.5) silently CompleteQuestWithCancel's any ongoing quest that has
    // no associated IssueBase UNLESS IsSpecialQuest (= !IsNullOrEmpty(SpecialQuestType)) is true.
    // Career quests have no issue, so without this override every CareerQuest dies on the first
    // save-load. (deep-review API-compat finding, 2026-06-01; verified vs QuestManager.OnGameLoaded.)
    public override string SpecialQuestType => "taom_career_quest";

    // No NPC dialog in v1 (self-driven arc). Phase 2 adds turn-in dialogs here.
    protected override void SetDialogs() { }

    protected override void InitializeQuestOnGameLoad()
    {
        EnsureDef();
    }

    protected override void OnStartQuest()
    {
        EnsureDef();
        if (_def == null) return;

        // Build one discrete journal log + progress slot per objective. JournalLog is itself a
        // saveable type, so storing the refs in a [SaveableField] List restores the same instances
        // the base class restores in _journalEntries (shared object graph) — no re-acquire on load.
        _progress = new List<int>();
        _logs = new List<JournalLog>();
        var body = new TextObject(string.IsNullOrEmpty(_def.DescriptionKey) ? "{=taom_cq_body}Prove yourself." : _def.DescriptionKey);
        foreach (var obj in _def.Objectives)
        {
            var task = new TextObject(string.IsNullOrEmpty(obj.TaskKey) ? "{=taom_cq_task}Objective" : obj.TaskKey);
            _logs.Add(AddDiscreteLog(body, task, 0, obj.Target));
            _progress.Add(0);
        }

        // Diagnose a misconfigured SkillThreshold id once at start — an unresolvable skill silently
        // returns 0 forever, so the objective could never progress (data-flow review finding).
        foreach (var obj in _def.Objectives)
        {
            if (obj.Type == CareerQuestObjectiveType.SkillThreshold
                && MBObjectManager.Instance?.GetObject<SkillObject>(obj.Param) == null)
                Logger?.LogWarning($"CareerQuest '{_def.Id}': SkillThreshold objective references unknown skill id '{obj.Param}' — it can never progress.");
        }
        foreach (var reward in _def.Rewards)
        {
            if (reward.Type == CareerQuestRewardType.GrantItem
                && MBObjectManager.Instance?.GetObject<ItemObject>(reward.Param) == null)
                Logger?.LogWarning($"CareerQuest '{_def.Id}': GrantItem reward references unknown item id '{reward.Param}' — it will be a no-op.");
        }

        // Seed threshold objectives from current state so the journal shows real starting progress.
        PollThresholdsAndRefresh();
    }

    protected override void RegisterEvents()
    {
        EnsureDef();
        if (_def == null) return;

        if (HasType(CareerQuestObjectiveType.WinBattles))
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
        if (HasType(CareerQuestObjectiveType.SettlementsCaptured))
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        if (HasType(CareerQuestObjectiveType.TournamentsWon))
            CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
        if (HasType(CareerQuestObjectiveType.DefeatEnemyLords))
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        if (HasType(CareerQuestObjectiveType.VisitSettlementType))
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
    }

    protected override void DailyTick()
    {
        PollThresholdsAndRefresh();
    }

    // ── Event handlers (count objectives) ───────────────────────────────────

    private void OnPlayerBattleEnd(MapEvent mapEvent)
    {
        if (mapEvent == null) return;
        if (mapEvent.WinningSide == BattleSideEnum.None || mapEvent.WinningSide != mapEvent.PlayerSide) return;
        Bump(CareerQuestObjectiveType.WinBattles, null);
    }

    private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
        Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        // Enlisted service (#375 attribution audit, MED): a siege the player assaults inside the
        // commander's column is captured BY the commander — credit it, or the objective is frozen
        // for the whole service term.
        var credited = capturerHero == Hero.MainHero
            || (EnlistQuery?.IsEnlisted == true && capturerHero?.StringId == EnlistQuery.CommanderHeroId);
        if (!credited) return;
        Bump(CareerQuestObjectiveType.SettlementsCaptured, null);
    }

    private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
    {
        if (winner != CharacterObject.PlayerCharacter) return;
        Bump(CareerQuestObjectiveType.TournamentsWon, null);
    }

    // Defeat = capture in battle (NOT execute/kill). Fires when the player's party takes a lord
    // prisoner; gated to ENEMY lords (at war), excluding allies / own-faction. Enlisted service
    // (#375 attribution audit, MED): commander-party captures count, and the war check falls back
    // to the capturer's faction for the kingdom-less freelancer.
    private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
    {
        var playersCapture = capturer == PartyBase.MainParty
            || (EnlistQuery?.IsEnlisted == true
                && capturer?.MobileParty?.StringId != null
                && EnlistQuery.IsCommanderParty(capturer.MobileParty.StringId));
        if (!playersCapture || prisoner == null || !prisoner.IsLord) return;
        var playerFaction = Hero.MainHero.MapFaction ?? capturer?.MapFaction;
        if (playerFaction == null || prisoner.MapFaction == null || !prisoner.MapFaction.IsAtWarWith(playerFaction)) return;
        Bump(CareerQuestObjectiveType.DefeatEnemyLords, null);
    }

    // Lazy service-locator (QuestBase has no ctor injection — same pattern as Service/Logger
    // below); null when resolution fails so every consumer stays fail-open.
    private Features.Enlistment.IEnlistmentStateQuery _enlistQuery;
    private Features.Enlistment.IEnlistmentStateQuery EnlistQuery
    {
        get
        {
            if (_enlistQuery == null)
            {
                try { _enlistQuery = IoC.Resolve<Features.Enlistment.IEnlistmentStateQuery>(); }
                catch { /* fail open — vanilla attribution */ }
            }
            return _enlistQuery;
        }
    }

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (party != MobileParty.MainParty || settlement == null) return;
        Bump(CareerQuestObjectiveType.VisitSettlementType, SettlementTypeName(settlement));
    }

    // ── Progress plumbing ────────────────────────────────────────────────────

    private void Bump(CareerQuestObjectiveType type, string matchParam)
    {
        if (_def == null || !IsOngoing) return;
        var svc = Service;
        for (int i = 0; i < _def.Objectives.Count && i < _progress.Count; i++)
        {
            var obj = _def.Objectives[i];
            if (obj.Type != type) continue;
            if (matchParam != null && !string.Equals(obj.Param, matchParam, System.StringComparison.OrdinalIgnoreCase)) continue;
            _progress[i] = svc.ComputeProgress(type, _progress[i], 1);
        }
        RefreshAndMaybeComplete();
    }

    private void PollThresholdsAndRefresh()
    {
        EnsureDef();
        if (_def == null || !IsOngoing) return;
        var hero = HeroAdapter();
        if (hero != null)
        {
            var svc = Service;
            for (int i = 0; i < _def.Objectives.Count && i < _progress.Count; i++)
            {
                var obj = _def.Objectives[i];
                int observed;
                switch (obj.Type)
                {
                    case CareerQuestObjectiveType.SkillThreshold: observed = hero.GetSkillValue(obj.Param); break;
                    case CareerQuestObjectiveType.RenownThreshold: observed = hero.ClanRenown; break;
                    case CareerQuestObjectiveType.GoldAccumulated: observed = hero.Gold; break;
                    default: continue; // not a threshold objective
                }
                _progress[i] = svc.ComputeProgress(obj.Type, _progress[i], observed);
            }
        }
        RefreshAndMaybeComplete();
    }

    private void RefreshAndMaybeComplete()
    {
        for (int i = 0; i < _logs.Count && i < _progress.Count; i++)
            UpdateQuestTaskStage(_logs[i], _progress[i]);

        if (IsOngoing && Service.AreAllObjectivesSatisfied(_def, _progress))
        {
            Service.ApplyRewards(_heroStringId, _def, HeroAdapter());
            Logger?.LogInfo($"CareerQuest '{_def.Id}': all objectives complete — applying rewards + finishing");
            CompleteQuestWithSuccess();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private bool HasType(CareerQuestObjectiveType type)
    {
        foreach (var o in _def.Objectives)
            if (o.Type == type) return true;
        return false;
    }

    private static string SettlementTypeName(Settlement s)
    {
        if (s.IsTown) return "Town";
        if (s.IsCastle) return "Castle";
        if (s.IsVillage) return "Village";
        return "";
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_questDefId))
            _def = Service?.GetQuestById(_questDefId);
    }

    private IQuestHeroAdapter HeroAdapter()
    {
        if (_heroAdapter != null && _heroAdapter.IsValid) return _heroAdapter;   // cache (avoids per-poll alloc)
        var hero = QuestGiver
            ?? (string.IsNullOrEmpty(_heroStringId) ? null : Campaign.Current?.CampaignObjectManager?.Find<Hero>(_heroStringId));
        _heroAdapter = hero != null ? HeroFactory?.Create(hero) : null;
        return _heroAdapter;
    }

    private ICareerQuestService Service => _service ??= IoC.Resolve<ICareerQuestService>();
    private IQuestHeroAdapterFactory HeroFactory => _heroFactory ??= IoC.Resolve<IQuestHeroAdapterFactory>();
    private IModLogger Logger => _logger ??= IoC.Resolve<IModLogger>();
}
