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
    /// Morale floor while serving. Below 25 an attached party counts as "low morale" in
    /// `CalculateCohesionChangeInternal`, dragging the whole army's cohesion — so a miserable
    /// enlisted player is a burden on the commander as well as on themselves. Set clear of that
    /// line: a fed, paid soldier in a functioning company is not on the edge of desertion.
    /// Raises only — a player who has EARNED higher morale is never pulled down to it.
    /// </summary>
    private const float ServiceMoraleFloor = 40f;

    /// <summary>
    /// HP the company surgeon restores per day. Matches vanilla's baseline for a mobile party
    /// (`DefaultPartyHealingModel` adds 11) so serving is never WORSE for your health than running
    /// your own party — which is the whole bargain of enlisting.
    /// </summary>
    private const int ServiceDailyHealing = 11;

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
        // The company feeds its OWN. Guarded here rather than trusting the caller, because
        // every line below grants a resource — food, morale, health — and a discharged player
        // drawing rations from a lord they no longer serve is a supply exploit, not a bug you
        // would notice.
        if (_world == null || !_store.Record.IsEnlisted)
            return;

        var held = _world.CountPlayerFood();
        if (held < ProvisionedFoodDays)
        {
            _world.GrantPlayerFood(ProvisionedFoodDays - held);
            _logger?.LogInfo($"[Enlistment] commander's baggage topped the player's rations up to {ProvisionedFoodDays} (held {held})");
        }

        // Morale second: it depends on being fed, and a low-morale attached party also drags the
        // army's cohesion, so this is upkeep for the commander as much as comfort for the player.
        if (_world.RaisePlayerMoraleTo(ServiceMoraleFloor))
            _logger?.LogInfo($"[Enlistment] company morale lifted the player to the service floor ({ServiceMoraleFloor})");

        // Healing LAST and unconditional. Vanilla heals heroes in a mobile party +11/day, but an
        // enlisted player is a hidden, inactive, one-man party parked in a field — a shape the
        // engine's own healing path was never written for, and one a player reached at 19% HP with
        // no recovery. Serving under a lord must never be worse for your health than marching
        // alone, so the company surgeon tends you explicitly rather than by side effect.
        if (_world.HealPlayerHero(ServiceDailyHealing))
            _logger?.LogInfo($"[Enlistment] the company surgeon tended the player (+{ServiceDailyHealing} HP)");
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
