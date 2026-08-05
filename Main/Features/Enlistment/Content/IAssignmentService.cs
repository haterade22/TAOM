using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public enum AssignmentSwapResult
{
    Changed = 0,
    NotEnlisted = 1,
    AlreadyThatAssignment = 2,
    OnCooldown = 3,
}

/// <summary>
/// Owns the player's service assignment — the ONLY producer of
/// <see cref="ServiceAssignment"/> values other than the Infantry default. Swapping costs
/// a cooldown and a point of trust (the donor let you re-role free at any conversation,
/// which made the role choice weightless and the duty affinity gates meaningless).
/// </summary>
public interface IAssignmentService
{
    ServiceAssignment Current { get; }

    /// <summary>Days remaining before another swap is allowed (0 when free).</summary>
    double CooldownRemaining(double nowDays);

    AssignmentSwapResult Swap(ServiceAssignment to, double nowDays);
}

public class AssignmentService : IAssignmentService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IServiceRewardService _rewards;
    private readonly TAOM.Core.Logging.IModLogger _logger;

    public AssignmentService(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        IServiceRewardService rewards,
        TAOM.Core.Logging.IModLogger logger)
    {
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _rewards = rewards;
        _logger = logger;
    }

    public ServiceAssignment Current => _contentStore.Record.Assignment;

    public double CooldownRemaining(double nowDays)
    {
        var last = _contentStore.Record.LastAssignmentSwapDay;
        if (!last.HasValue)
            return 0.0;
        var readyOn = last.Value + _config.GetConfig().Progression.AssignmentSwapCooldownDays;
        // Positive requirement: a NaN nowDays reports "still on cooldown", the strict outcome.
        return nowDays >= readyOn ? 0.0 : readyOn - nowDays;
    }

    public AssignmentSwapResult Swap(ServiceAssignment to, double nowDays)
    {
        if (!_store.Record.IsEnlisted)
            return AssignmentSwapResult.NotEnlisted;

        var record = _contentStore.Record;
        if (record.Assignment == to)
            return AssignmentSwapResult.AlreadyThatAssignment;

        if (CooldownRemaining(nowDays) > 0.0)
            return AssignmentSwapResult.OnCooldown;

        var trustCost = _config.GetConfig().Progression.AssignmentSwapTrustCost;
        record.Assignment = to;
        record.LastAssignmentSwapDay = nowDays;
        if (trustCost > 0)
            _rewards.AdjustTrust(-trustCost);

        _logger?.LogInfo($"[Enlistment] assignment changed to {to} (trust -{trustCost})");
        return AssignmentSwapResult.Changed;
    }
}
