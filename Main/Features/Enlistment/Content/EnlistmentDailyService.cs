using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

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
    private readonly TAOM.Adapters.ICommanderLordAdapter _commander;
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
        TAOM.Adapters.ICommanderLordAdapter commander,
        IModLogger logger)
    {
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _rewards = rewards;
        _world = world;
        _commander = commander;
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
    /// Base HP the company surgeon restores per day. Matches vanilla's mobile-party baseline
    /// (`DefaultPartyHealingModel` adds 11) so serving is never WORSE than running your own party.
    /// </summary>
    private const int ServiceDailyHealing = 11;

    /// <summary>
    /// Extra HP per 10 points of the player's own Medicine. A flat rate made both the skill and the
    /// surgeon duty inert — a physician and a farmhand recovered at exactly the same speed, which
    /// tells the player their Medicine investment does nothing while serving.
    /// </summary>
    private const int MedicinePerHealingPoint = 10;

    /// <summary>
    /// Multiplier while the commander's column is resting in a settlement. Recovering in a town with
    /// the baggage unpacked should beat recovering on the march; vanilla scales healing on the same
    /// idea, and the reference mod triples its hourly rate on exactly this condition.
    /// </summary>
    private const int SettlementHealingMultiplier = 2;

    /// <summary>
    /// True only while the player's party is parked on the commander — the states in which
    /// <c>IsActive</c> is false and the engine's own healing path therefore cannot see them.
    /// Read from state rather than from a live presence flag on purpose: state is what the save
    /// carries and what the reconciler drives, so the gate cannot disagree with the transition
    /// that caused it.
    /// </summary>
    private bool IsParked() =>
        _store.Record.State == EnlistmentState.EnlistedAttached
        || _store.Record.State == EnlistmentState.EnlistedBattle;

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

        // Healing LAST, and only in the PARKED regime. Service still has two regimes, and they
        // differ in exactly the property vanilla's healing keys on:
        //
        //   parked   (EnlistedAttached / EnlistedBattle)  IsActive == false — vanilla's
        //            PartyHealCampaignBehavior never reaches the party, so nothing heals the
        //            player at all. This is the 19%-HP report from 2026-08-08. We heal.
        //   detached (CommanderUnavailable grace only)    RestorePresence() has set
        //            IsActive == true, so vanilla is already healing +11/day. We must not.
        //
        // THE DETACHED REGIME SHRANK on 2026-08-09. It used to include EnlistedDetachedOnDuty, and
        // 12 of the 13 field duties detached for 4-6 days at a time, which made detached the COMMON
        // case and the double-pay a routine one. Field duties no longer detach at all (#428), so a
        // duty day is now a parked day and TAOM does the healing on it where vanilla used to. That
        // is the intended behaviour — you are with the column, so the company surgeon tends you —
        // but it IS a behaviour change and it belongs in the comment rather than in a reader's
        // surprise.
        //
        // The gate stays because the grace period still detaches, and because the failure mode is
        // silent. Two earlier versions of this comment were wrong in the same direction: one
        // asserted the party is "hidden, inactive" in all cases, the other kept describing a duty
        // model that no longer existed. A comment on a load-bearing gate is a claim; check it when
        // the regimes move.
        if (!IsParked())
            return;

        var healing = ServiceDailyHealing;

        var medicine = _skillXp?.GetSkillValue(_store.Record.EnlistedHeroId, "Medicine") ?? 0;
        if (medicine > 0)
            healing += medicine / MedicinePerHealingPoint;

        // Read from the COMMANDER, not the player: the column is what is resting, and a following
        // player is wherever it is.
        var resting = _commander?.GetSnapshot(_store.Record.CommanderHeroId)?.PartyIsInSettlement == true;
        if (resting)
            healing *= SettlementHealingMultiplier;

        if (_world.HealPlayerHero(healing))
            _logger?.LogInfo($"[Enlistment] the company surgeon tended the player (+{healing} HP; medicine {medicine}, resting {resting})");
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

        // NOTHING BELOW THIS LINE IS PAID BY A COMMANDER WHO NO LONGER HAS A COMMAND.
        //
        // RunDailyTick gates on IsEnlisted, which spans five states including
        // CommanderUnavailable — so before this guard a lord who was dead in all but name, or
        // sitting in an enemy dungeon, went on paying a daily wage, awarding service XP, and
        // could fire "You have been promoted to {RANK}." The promotion is the one that gives the
        // game away: there is no chain of command left to promote anyone.
        //
        // Rations, the morale floor and the surgeon are DELIBERATELY above this, in
        // ProvisionFromCommander. Those are the difference between waiting and starving, and the
        // player did not choose to be left behind. Pay and rank are the parts that require someone
        // to award them.
        if (_store.Record.State == EnlistmentState.CommanderUnavailable)
        {
            summary.CommanderUnavailable = true;
            return summary;
        }

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
