using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Content;

public class ServiceRewardService : IServiceRewardService, IEnlistmentWagePreview
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IHeroSkillXpAdapter _skillXp;
    private readonly IGoldGiftAdapter _goldGift;
    private readonly IGoldTransferAdapter _goldTransfer;
    private readonly ICommanderLordAdapter _commander;
    private readonly IPlayerPartyAdapter _playerParty;
    private readonly IHeroRenownAdapter _renown;
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
        IHeroRenownAdapter renown,
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
        _renown = renown;
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

        // Renown goes to the PLAYER's clan, so it uses playerId like the gold and skill sinks above
        // rather than the commander id used for relation. Serving in someone else's army builds your
        // OWN name — that is the whole point of the reward.
        if (reward.Renown > 0)
            _renown.AddClanRenown(playerId, reward.Renown);

        if (reward.RepDomain != ReputationDomain.None && reward.RepAmount != 0)
            ApplyReputation(reward.RepDomain, reward.RepAmount);

        _logger?.LogDebug($"[Enlistment] reward '{sourceLabel}': xp={reward.ServiceXp} gold={reward.Gold} skill={reward.SkillId}/{reward.SkillXp} trust={reward.Trust} renown={reward.Renown}");
    }

    /// <inheritdoc />
    public int GetDailyWage()
        => IsWageEarningToday()
            ? WagePolicy.DailyWageForRank(
                (int)_contentStore.Record.Rank, _config.GetConfig().Progression.DailyWageByRank)
            : 0;

    /// <summary>
    /// Whether a wage is actually due today, which is NARROWER than <c>IsEnlisted</c>.
    ///
    /// <c>IsEnlisted</c> spans five states, and <c>EnlistmentDailyService.RunDailyTick</c>
    /// early-returns before <see cref="PayDailyWage"/> whenever the state is
    /// <c>CommanderUnavailable</c> — there is no chain of command left to pay anyone. Gating the
    /// projection on <c>IsEnlisted</c> alone therefore promised the player money on exactly the
    /// days none arrived, for a grace window up to a week long, and the wallet tooltip is the one
    /// surface a player checks when they suspect they are not being paid. Both gates must name the
    /// same condition or the projection is a lie.
    /// </summary>
    private bool IsWageEarningToday()
        => _store.Record.IsEnlisted
           && _store.Record.State != EnlistmentState.CommanderUnavailable;

    public WageDecision PayDailyWage()
    {
        var record = _contentStore.Record;
        var config = _config.GetConfig();
        var wage = WagePolicy.DailyWageForRank((int)record.Rank, config.Progression.DailyWageByRank);

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
        // player IS the new debt, clamped to the cap. The cap is day-denominated, so it
        // scales with the rank wage it caps — see WagePolicy.ArrearsCap.
        var arrearsCap = WagePolicy.ArrearsCap(wage, config.WagePolicy);
        var stillOwed = System.Math.Max(0, wage + priorArrears - delivered);

        // The cap limits ACCRUAL; it must never retroactively confiscate arrears the player
        // already banked at a higher wage. Because the ceiling is now wage-relative it can
        // SHRINK — a demotion, a retuned dailyWageByRank, even a rank whose wage is 0 — and a
        // bare Min() would have wiped the whole standing debt on the next tick. Floor it at
        // whatever is left of yesterday's debt.
        var alreadyBanked = System.Math.Max(0, priorArrears - delivered);
        var carried = System.Math.Min(stillOwed, System.Math.Max(arrearsCap, alreadyBanked));
        var forfeited = stillOwed - carried;
        record.DeferredWages = carried;

        // Clamping DESTROYS gold the commander still owes. That used to happen in total
        // silence, so a long insolvent stretch erased hundreds of gold of back pay with
        // nothing in the log to explain where it went. Never clamp player money quietly.
        if (forfeited > 0)
            _logger?.LogWarning(
                $"[Enlistment] deferred-wage cap reached — {forfeited} gold of back pay forfeited "
                + $"(owed {stillOwed}, cap {arrearsCap} = {config.WagePolicy.MaxDeferredWageDays} days x {wage}/day)");

        // Report what actually happened, not what was planned.
        return new WageDecision
        {
            PaidFromCommander = config.WagePolicy.PayFromCommanderGold ? System.Math.Min(delivered, wage) : 0,
            Minted = config.WagePolicy.PayFromCommanderGold ? 0 : System.Math.Min(delivered, wage),
            ArrearsReleased = System.Math.Max(0, delivered - System.Math.Min(delivered, wage)),
            NewlyDeferred = System.Math.Max(0, wage - System.Math.Min(delivered, wage)),
            Forfeited = forfeited,
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
