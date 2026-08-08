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

    /// <summary>
    /// The same evaluation with none of the mutation — for the status board, which has to show the
    /// ladder without ever promoting anyone. It runs through the identical
    /// <c>PromotionEvaluator.Evaluate</c> call as <see cref="EvaluateAndApply"/> precisely so the
    /// numbers a player reads cannot drift from the numbers that promote them; that drift is the
    /// donor's 12-evaluation-site bug in a new costume.
    /// </summary>
    PromotionEvaluation Peek();
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
        var evaluation = Evaluate();
        if (!evaluation.Promote)
            return outcome;

        var record = _contentStore.Record;
        record.Rank = evaluation.ToRank;
        outcome.Promoted = true;
        outcome.NewRank = evaluation.ToRank;
        _logger?.LogInfo($"[Enlistment] promoted to {evaluation.ToRank} on day {record.DaysServed} of service");
        return outcome;
    }

    public PromotionEvaluation Peek() => Evaluate();

    /// <summary>
    /// The shared read. Every line here is a READ — the only mutation in this service is the
    /// <c>record.Rank</c> assignment in <see cref="EvaluateAndApply"/>, which is what makes
    /// <see cref="Peek"/> a safe call from a render path.
    /// </summary>
    private PromotionEvaluation Evaluate()
    {
        var record = _contentStore.Record;
        var leadership = _skillXp.GetSkillValue(_store.Record.EnlistedHeroId, "Leadership");

        return PromotionEvaluator.Evaluate(
            record.ToProgressSnapshot(leadership), _config.GetConfig().Promotions);
    }
}
