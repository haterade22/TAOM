using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public interface IEnlistmentBattlePayoutService
{
    /// <summary>Pay the battle out (base XP + capped kill XP + merit band). False when not enlisted.</summary>
    bool PayOutBattle(bool won);

    /// <summary>Grade key of the last paid merit band ("" when the battle had no sample), for the toast.</summary>
    string LastBandGradeKey { get; }

    /// <summary>Whether the battle payout promoted the player (the second evaluation point).</summary>
    bool LastBattlePromoted { get; }

    Domain.ServiceRank LastPromotedRank { get; }
}

public class EnlistmentBattlePayoutService : IEnlistmentBattlePayoutService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IBattleMeritAccumulator _accumulator;
    private readonly IServiceRewardService _rewards;
    private readonly IPromotionService _promotion;
    private readonly IModLogger _logger;

    public EnlistmentBattlePayoutService(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        IBattleMeritAccumulator accumulator,
        IServiceRewardService rewards,
        IPromotionService promotion,
        IModLogger logger)
    {
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _accumulator = accumulator;
        _rewards = rewards;
        _promotion = promotion;
        _logger = logger;
    }

    public string LastBandGradeKey { get; private set; } = "";

    public bool LastBattlePromoted { get; private set; }

    public Domain.ServiceRank LastPromotedRank { get; private set; }

    public bool PayOutBattle(bool won)
    {
        LastBandGradeKey = "";
        LastBattlePromoted = false;
        if (!_store.Record.IsEnlisted)
        {
            _accumulator.Consume(); // drop any stale sample
            return false;
        }

        var record = _contentStore.Record;
        var config = _config.GetConfig();
        var tables = config.Progression;

        if (won)
            record.BattleVictories++;
        else
            record.BattleDefeats++;

        record.ServiceXp += won ? tables.BattleWinXp : tables.BattleLossXp;

        var sample = _accumulator.Consume();
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
                _rewards.Grant(new RewardSpec
                {
                    ServiceXp = band.ServiceXp,
                    Gold = band.Gold,
                    Trust = band.Trust,
                    RepDomain = band.RepDomain,
                    RepAmount = band.RepAmount,
                }, $"merit-{band.GradeKey}");
                LastBandGradeKey = band.GradeKey;
                _logger?.LogInfo($"[Enlistment] battle merit {score} -> band '{band.GradeKey}' (kills={sample.Kills}, won={won})");
            }
        }

        // The SECOND promotion evaluation point (the daily tick is the first). Both route
        // through the single IPromotionService chokepoint so the thresholds can't drift —
        // without this, a battle that earns the last XP you need wouldn't promote you until
        // the next day rolled over.
        var promotion = _promotion.EvaluateAndApply();
        LastBattlePromoted = promotion.Promoted;
        LastPromotedRank = promotion.NewRank;

        return true;
    }
}
