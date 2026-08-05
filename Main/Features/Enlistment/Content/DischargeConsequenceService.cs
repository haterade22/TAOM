using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Content;

public class DischargeConsequenceService : IDischargeConsequenceService
{
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IServiceRewardService _rewards;
    private readonly IModLogger _logger;

    public DischargeConsequenceService(
        IEnlistmentContentStore contentStore,
        IServiceRewardService rewards,
        IModLogger logger)
    {
        _contentStore = contentStore;
        _rewards = rewards;
        _logger = logger;
    }

    public void ApplyConsequences(DischargeReason reason)
    {
        var record = _contentStore.Record;

        switch (reason)
        {
            case DischargeReason.Desertion:
                if (record.DeferredWages > 0)
                    _logger?.LogInfo($"[Enlistment] desertion — {record.DeferredWages} in deferred wages forfeited");
                record.DeferredWages = 0;
                break;

            case DischargeReason.PlayerRequest:
            case DischargeReason.Commission:
            case DischargeReason.ContractNotRenewed:
            case DischargeReason.CommanderDead:
            case DischargeReason.CommanderUnavailableGraceExpired:
                // Honorable exit: the paymaster settles what the column still owes.
                if (record.DeferredWages > 0)
                {
                    _rewards.Grant(new RewardSpec { Gold = record.DeferredWages }, "final-settlement");
                    record.DeferredWages = 0;
                }
                break;

            // HeirSuccessionOrPossessionMismatch / SaveNormalization are quiet, penalty-free
            // bookkeeping exits — no settlement, no forfeit.
        }

        _contentStore.Clear();
    }
}
