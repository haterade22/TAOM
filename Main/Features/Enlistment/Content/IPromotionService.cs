using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public sealed class PromotionOutcome
{
    public bool Promoted { get; set; }
    public ServiceRank NewRank { get; set; }
}

/// <summary>
/// THE promotion chokepoint. Both evaluation points (daily tick and battle-end payout)
/// call this one method — the donor re-derived the check at 12 sites, which is exactly how
/// its thresholds drifted between the promotion path and the progress text.
/// </summary>
public interface IPromotionService
{
    PromotionOutcome EvaluateAndApply();
}

public class PromotionService : IPromotionService
{
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly TAOM.Adapters.IHeroSkillXpAdapter _skillXp;
    private readonly TAOM.Features.Enlistment.IEnlistmentStore _store;
    private readonly TAOM.Core.Logging.IModLogger _logger;

    public PromotionService(
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        TAOM.Adapters.IHeroSkillXpAdapter skillXp,
        TAOM.Features.Enlistment.IEnlistmentStore store,
        TAOM.Core.Logging.IModLogger logger)
    {
        _contentStore = contentStore;
        _config = config;
        _skillXp = skillXp;
        _store = store;
        _logger = logger;
    }

    public PromotionOutcome EvaluateAndApply()
    {
        var outcome = new PromotionOutcome();
        var record = _contentStore.Record;
        var leadership = _skillXp.GetSkillValue(_store.Record.EnlistedHeroId, "Leadership");

        var evaluation = PromotionEvaluator.Evaluate(
            record.ToProgressSnapshot(leadership), _config.GetConfig().Promotions);
        if (!evaluation.Promote)
            return outcome;

        record.Rank = evaluation.ToRank;
        outcome.Promoted = true;
        outcome.NewRank = evaluation.ToRank;
        _logger?.LogInfo($"[Enlistment] promoted to {evaluation.ToRank} on day {record.DaysServed} of service");
        return outcome;
    }
}
