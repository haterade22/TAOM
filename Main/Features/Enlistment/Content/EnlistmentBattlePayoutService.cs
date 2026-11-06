using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>
/// Pays a battle out at <c>MapEventEnded</c>: base XP, capped kill XP, the merit band and renown.
/// The single implementation is registered and injected as the concrete class (no interface to fake).
/// </summary>
public class EnlistmentBattlePayoutService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IBattleMeritAccumulator _accumulator;
    private readonly IServiceRewardService _rewards;
    private readonly IPromotionService _promotion;
    private readonly SitOutLedger _ledger;
    private readonly IModLogger _logger;

    public EnlistmentBattlePayoutService(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        IBattleMeritAccumulator accumulator,
        IServiceRewardService rewards,
        IPromotionService promotion,
        SitOutLedger ledger,
        IModLogger logger)
    {
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _accumulator = accumulator;
        _rewards = rewards;
        _promotion = promotion;
        _ledger = ledger;
        _logger = logger;
    }

    public string LastBandGradeKey { get; private set; } = "";

    public bool LastBattlePromoted { get; private set; }

    public Domain.ServiceRank LastPromotedRank { get; private set; }

    /// <summary>
    /// Why the last paid battle counted as sat out (<see cref="SitOutVerdict.WoundedFree"/>,
    /// <see cref="SitOutVerdict.ForcedOpen"/> or <see cref="SitOutVerdict.OverwhelmingCost"/>), or null
    /// when it did not. Read by the behavior to show the "you stayed back" line.
    /// </summary>
    public SitOutVerdict? LastSatOutReason { get; private set; }

    /// <summary>
    /// Pay the battle out (base XP + capped kill XP + merit band). False when not enlisted.
    /// <paramref name="playerSimulated"/> is the map event's <c>IsPlayerSimulation</c>: the player chose
    /// 'Send troops' and the battle auto-resolved. It is required so the call site cannot forget it (#830).
    /// </summary>
    public bool PayOutBattle(bool won, bool playerSimulated)
    {
        LastBandGradeKey = "";
        LastBattlePromoted = false;
        LastSatOutReason = null;
        // The ledger belongs to the battle being paid, so it is consumed on every path.
        var recorded = _ledger.Consume();
        if (!_store.Record.IsEnlisted)
        {
            _accumulator.Consume(); // drop any stale sample
            return false;
        }

        var record = _contentStore.Record;
        var config = _config.GetConfig();
        var tables = config.Progression;

        var sample = _accumulator.Consume();

        // A simulated player battle (he chose 'Send troops') is a battle he sat out when the menu gate
        // recorded an allowed verdict and the mission never ran for him: no sample, or a walkout from
        // a mission he was never downed in. A recorded Unchanged or Refused verdict means the
        // simulation did not come from an option the gate let him use, so it pays as normal.
        // Renown is withheld for every allowed reason. Only the healthy allowance (OverwhelmingCost)
        // also cuts the base XP and docks trust; the wounded and the forced-open keep the full base XP.
        var allowed = recorded == SitOutVerdict.WoundedFree
            || recorded == SitOutVerdict.ForcedOpen
            || recorded == SitOutVerdict.OverwhelmingCost;
        var satOut = playerSimulated && allowed
            && (sample == null || (sample.LeftTheField && sample.SurvivalRatio >= 1f));
        var baseXp = won ? tables.BattleWinXp : tables.BattleLossXp;
        if (satOut)
        {
            LastSatOutReason = recorded;
            if (recorded == SitOutVerdict.OverwhelmingCost)
            {
                baseXp = (int)System.Math.Round(baseXp * config.SitOut.BaseXpFactor, System.MidpointRounding.AwayFromZero);
                if (config.SitOut.TrustCost > 0)
                    _rewards.Grant(new RewardSpec { Trust = -config.SitOut.TrustCost }, "sat-out");
            }
            _logger?.LogInfo($"[Enlistment] sat out a simulated battle ({recorded}, won={won}); base XP {baseXp}");
        }
        record.ServiceXp += baseXp;

        var bandRenown = 0;
        if (sample != null)
        {
            // Kill XP is a battle-end line item with the kill COUNT capped — the donor
            // paid 25 XP per kill live during the mission, uncapped.
            var countedKills = System.Math.Min(System.Math.Max(0, sample.Kills), tables.KillXpCap);
            if (countedKills > 0 && tables.XpPerKill > 0)
                record.ServiceXp += countedKills * tables.XpPerKill;

            var score = BattleMeritScorer.Score(sample, config.MeritScoring);
            var band = BattleMeritScorer.ResolveBand(score, config.MeritBands);
            if (band != null)
            {
                // Standing is the one band reward a player who WALKED OUT may never collect, and
                // the guard lives here rather than in the ladder because the ladder's protection is
                // emergent. leftFieldPenalty cancels the survival weight and nothing else, so kills,
                // cohesion, proximity, engagement and role fit all survive the exit: the walkout
                // ceiling is 45 of 100, close enough to the band boundaries that one tuning edit
                // puts quitting back into a paying band. XP and gold still reflect what the player
                // did before he left, which is the same reasoning that made LeftTheField zero the
                // survival term alone. When the same walkout also sat the battle out (#830), the sit-out
                // rule governs the base pay and the renown below; the band still pays as here.
                _rewards.Grant(new RewardSpec
                {
                    ServiceXp = band.ServiceXp,
                    Gold = band.Gold,
                    Trust = sample.LeftTheField ? 0 : band.Trust,
                }, $"merit-{band.GradeKey}");
                LastBandGradeKey = band.GradeKey;
                bandRenown = band.Renown;
                _logger?.LogInfo($"[Enlistment] battle merit {score} -> band '{band.GradeKey}' (kills={sample.Kills}, won={won})");
            }
        }

        // Renown is granted ONCE for the battle, not per band, and deliberately outside the
        // sample block: a battle the accumulator never sampled (joined late, no geometry) still
        // earned the base. Field report 3 — TAOM granted none at all, and vanilla's share is
        // contribution-scaled, which rounds to nothing for a party of one hero.
        var renown = BattleRenownPolicy.Compute(won, bandRenown, tables);
        if (renown > 0 && !satOut)
            _rewards.Grant(new RewardSpec { Renown = renown }, won ? "battle-won" : "battle-lost");

        // The SECOND promotion evaluation point (the daily tick is the first). Both route
        // through the single IPromotionService chokepoint so the thresholds can't drift —
        // without this, a battle that earns the last XP you need wouldn't promote you until
        // the next day rolled over.
        //
        // Not in the grace state: no chain of command is left to promote anyone (#835; the daily tick
        // makes the same rule).
        if (_store.Record.State == TAOM.Features.Enlistment.Domain.EnlistmentState.CommanderUnavailable)
            return true;

        var promotion = _promotion.EvaluateAndApply();
        LastBattlePromoted = promotion.Promoted;
        LastPromotedRank = promotion.NewRank;

        return true;
    }
}
