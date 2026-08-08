using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public class EnlistmentDailyService : IEnlistmentDailyService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IServiceRewardService _rewards;
    private readonly IArmyRhythmSnapshotService _rhythm;
    private readonly IHeroSkillXpAdapter _skillXp;
    private readonly IPromotionService _promotion;
    private readonly TAOM.Adapters.IDutyWorldAdapter _world;
    private readonly IModLogger _logger;

    public EnlistmentDailyService(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        IServiceRewardService rewards,
        IArmyRhythmSnapshotService rhythm,
        IHeroSkillXpAdapter skillXp,
        IPromotionService promotion,
        TAOM.Adapters.IDutyWorldAdapter world,
        IModLogger logger)
    {
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _rewards = rewards;
        _world = world;
        _rhythm = rhythm;
        _skillXp = skillXp;
        _promotion = promotion;
        _logger = logger;
    }

    /// <summary>
    /// Days of rations the commander's baggage keeps in the player's pack.
    /// Small on purpose: enough that a serving soldier never starves, not enough to be a supply
    /// exploit — a discharge leaves the player with a couple of days' food, not a full larder.
    /// </summary>
    private const int ProvisionedFoodDays = 3;

    /// <summary>
    /// Feed the soldier. Verified against DefaultPartyHealingModel: a mobile party heals its heroes
    /// +11 HP/day, BUT `if (party.IsStarving && CurrentSettlement == null) return -19f` — a starving
    /// hero LOSES 19 HP a day. An enlisted player is a single hero parked in the field for days
    /// with whatever food they happened to carry; once it runs out they stop healing and start
    /// dying, which is exactly what was reported in-game 2026-08-08 at 19% HP.
    ///
    /// Mechanically the player is not in the commander's party, so the baggage train that feeds
    /// every other soldier in the column does not feed them. This closes that gap at the only place
    /// it can be closed from outside the engine: top the pack back up each day.
    /// </summary>
    private void ProvisionFromCommander()
    {
        if (_world == null)
            return;

        var held = _world.CountPlayerFood();
        if (held >= ProvisionedFoodDays)
            return;

        _world.GrantPlayerFood(ProvisionedFoodDays - held);
        _logger?.LogInfo($"[Enlistment] commander's baggage topped the player's rations up to {ProvisionedFoodDays} (held {held})");
    }

    public DailySummary RunDailyTick(double nowDays, double hourOfDay)
    {
        var summary = new DailySummary();

        // Before anything else: a starving soldier loses 19 HP a day and no wage or promotion
        // makes up for that.
        ProvisionFromCommander();
        if (!_store.Record.IsEnlisted)
            return summary;

        var record = _contentStore.Record;
        var config = _config.GetConfig();
        var tables = config.Progression;
        var playerId = _store.Record.EnlistedHeroId;

        record.DaysServed++;

        summary.Wage = _rewards.PayDailyWage();

        var rankIndex = (int)record.Rank;
        if (rankIndex >= 0 && rankIndex < tables.DailyServiceXpByRank.Count)
            record.ServiceXp += tables.DailyServiceXpByRank[rankIndex];

        if (tables.DailyAssignmentXp > 0)
            _skillXp.AddSkillXp(playerId, AssignmentSkills.SignatureSkill(record.Assignment), tables.DailyAssignmentXp);
        if (tables.DailyLeadershipXp > 0)
            _skillXp.AddSkillXp(playerId, "Leadership", tables.DailyLeadershipXp);

        var rhythm = _rhythm.GetSnapshot(nowDays, hourOfDay);
        var contextSkill = AssignmentSkills.ContextSkill(rhythm);
        var contextXp = AssignmentSkills.ContextXp(rhythm, tables);
        if (contextSkill != null && contextXp > 0)
            _skillXp.AddSkillXp(playerId, contextSkill, contextXp);

        var promotion = _promotion.EvaluateAndApply();
        summary.Promoted = promotion.Promoted;
        summary.NewRank = promotion.NewRank;

        var contractEnd = _store.Record.ContractEndDay;
        if (contractEnd.HasValue && nowDays >= contractEnd.Value && nowDays - 1.0 < contractEnd.Value)
            summary.ContractExpiredToday = true;

        return summary;
    }

}
