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
    private readonly IModLogger _logger;

    public EnlistmentDailyService(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        IServiceRewardService rewards,
        IArmyRhythmSnapshotService rhythm,
        IHeroSkillXpAdapter skillXp,
        IPromotionService promotion,
        IModLogger logger)
    {
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _rewards = rewards;
        _rhythm = rhythm;
        _skillXp = skillXp;
        _promotion = promotion;
        _logger = logger;
    }

    public DailySummary RunDailyTick(double nowDays, double hourOfDay)
    {
        var summary = new DailySummary();
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
