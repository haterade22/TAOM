using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public class ServiceRewardService : IServiceRewardService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IHeroSkillXpAdapter _skillXp;
    private readonly IGoldGiftAdapter _goldGift;
    private readonly IGoldTransferAdapter _goldTransfer;
    private readonly ICommanderLordAdapter _commander;
    private readonly IPlayerPartyAdapter _playerParty;
    private readonly IModLogger _logger;

    public ServiceRewardService(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        IHeroSkillXpAdapter skillXp,
        IGoldGiftAdapter goldGift,
        IGoldTransferAdapter goldTransfer,
        ICommanderLordAdapter commander,
        IPlayerPartyAdapter playerParty,
        IModLogger logger)
    {
        _playerParty = playerParty;
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _skillXp = skillXp;
        _goldGift = goldGift;
        _goldTransfer = goldTransfer;
        _commander = commander;
        _logger = logger;
    }

    public void Grant(RewardSpec reward, string sourceLabel)
    {
        if (reward == null)
            return;

        var record = _contentStore.Record;
        // The final-settlement grant runs AFTER the discharge pipeline cleared the core
        // record, so EnlistedHeroId is null by then — pay the player hero directly rather
        // than dropping the arrears into a null id (Codex P2-1: honorable discharge
        // silently erased what the column still owed).
        var playerId = _store.Record.EnlistedHeroId ?? _playerParty.GetMainHeroId();

        if (reward.ServiceXp > 0)
            record.ServiceXp += reward.ServiceXp;

        if (reward.SkillXp > 0 && !string.IsNullOrEmpty(reward.SkillId))
            _skillXp.AddSkillXp(playerId, reward.SkillId, reward.SkillXp);

        if (reward.Gold > 0)
            _goldGift.GiveGoldToHero(playerId, reward.Gold);

        if (reward.Trust != 0)
            AdjustTrust(reward.Trust);

        if (reward.Relation != 0)
            _commander.ApplyPlayerRelation(_store.Record.CommanderHeroId, reward.Relation);

        if (reward.RepDomain != ReputationDomain.None && reward.RepAmount != 0)
            ApplyReputation(reward.RepDomain, reward.RepAmount);

        _logger?.LogDebug($"[Enlistment] reward '{sourceLabel}': xp={reward.ServiceXp} gold={reward.Gold} skill={reward.SkillId}/{reward.SkillXp} trust={reward.Trust}");
    }

    public WageDecision PayDailyWage()
    {
        var record = _contentStore.Record;
        var config = _config.GetConfig();
        var wageTable = config.Progression.DailyWageByRank;
        var rankIndex = (int)record.Rank;
        var wage = rankIndex >= 0 && rankIndex < wageTable.Count ? wageTable[rankIndex] : 0;

        var commanderId = _store.Record.CommanderHeroId;
        var priorArrears = System.Math.Max(0, record.DeferredWages);
        var plan = WagePolicy.ComputeDaily(
            wage, _goldTransfer.GetHeroGold(commanderId), priorArrears, config.WagePolicy);

        // ONE payment channel per policy — the two are mutually exclusive. Inferring the
        // channel from Minted>0 (as an earlier revision did) paid ArrearsReleased through
        // BOTH the commander transfer and the mint, and drained real commander gold in
        // mint mode. Branching on the config makes that unrepresentable.
        int delivered;
        if (config.WagePolicy.PayFromCommanderGold)
        {
            var requested = plan.PaidFromCommander + plan.ArrearsReleased;
            delivered = requested > 0 ? _goldTransfer.TransferToPlayer(commanderId, requested) : 0;
            if (delivered < requested)
                _logger?.LogDebug($"[Enlistment] wage transfer shortfall {requested - delivered} — re-deferred");
        }
        else
        {
            delivered = plan.Minted + plan.ArrearsReleased;
            if (delivered > 0)
                _goldGift.GiveGoldToHero(_store.Record.EnlistedHeroId, delivered);
        }

        // Ledger by conservation, never by patching the plan's fields: everything owed
        // this tick (today's wage + the standing debt) minus what actually reached the
        // player IS the new debt, clamped to the cap (overflow is forfeited, as before).
        var stillOwed = System.Math.Max(0, wage + priorArrears - delivered);
        record.DeferredWages = System.Math.Min(stillOwed, System.Math.Max(0, config.WagePolicy.MaxDeferredWages));

        // Report what actually happened, not what was planned.
        return new WageDecision
        {
            PaidFromCommander = config.WagePolicy.PayFromCommanderGold ? System.Math.Min(delivered, wage) : 0,
            Minted = config.WagePolicy.PayFromCommanderGold ? 0 : System.Math.Min(delivered, wage),
            ArrearsReleased = System.Math.Max(0, delivered - System.Math.Min(delivered, wage)),
            NewlyDeferred = System.Math.Max(0, wage - System.Math.Min(delivered, wage)),
        };
    }

    public void AdjustTrust(int delta)
    {
        var record = _contentStore.Record;
        record.Trust = TrustLedger.AdjustTrust(record.Trust, delta, _config.GetConfig().Scheduler);
    }

    public void ApplyReputation(ReputationDomain domain, int amount)
    {
        TrustLedger.ApplyReputation(_contentStore.Record, domain, amount, _config.GetConfig().Scheduler);
    }
}
