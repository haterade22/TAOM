using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.Enlistment.Duties;

public class FieldDutyRuntime : IFieldDutyRuntime
{
    private const int FoodDeliveryAmount = 6;
    private const double ShiftDurationDays = 4.0 / 24.0;
    private const float EnemyContactRadius = 40f;
    private const int FailTrustPenalty = 2;
    private const int ForageBaseAmount = 6;
    private const int ForageBonusMin = 4;
    private const int ForageBonusSpread = 7; // Next(7) => 0..6, +min 4 => 4..10 inclusive bonus

    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IEnlistmentStateMachine _stateMachine;
    private readonly IServiceAttachmentService _attachment;
    private readonly IDutyWorldAdapter _world;
    private readonly ICommanderLordAdapter _commander;
    private readonly IServiceRewardService _rewards;
    private readonly IRandomProvider _random;
    private readonly IModLogger _logger;

    public FieldDutyRuntime(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        IEnlistmentStateMachine stateMachine,
        IServiceAttachmentService attachment,
        IDutyWorldAdapter world,
        ICommanderLordAdapter commander,
        IServiceRewardService rewards,
        IRandomProvider random,
        IModLogger logger)
    {
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _stateMachine = stateMachine;
        _attachment = attachment;
        _world = world;
        _commander = commander;
        _rewards = rewards;
        _random = random;
        _logger = logger;
    }

    public bool Start(DutyDefinition duty, double nowDays)
    {
        if (duty == null)
            return false;

        var record = _contentStore.Record;
        if (record.HasActiveDuty)
            return false;

        var commanderId = _store.Record.CommanderHeroId;
        string targetPartyId = null;
        string targetSettlementId = null;

        if (duty.TargetKind == DutyTargetKind.SpawnedLooterParty)
        {
            var commanderSnapshot = _commander.GetSnapshot(commanderId);
            targetPartyId = _world.SpawnLooterParty(
                "taom_enlist_duty", commanderSnapshot.SettlementId, patrolNotEngage: duty.TargetAi != DutyTargetAi.EngagePlayer);
            if (string.IsNullOrEmpty(targetPartyId))
            {
                _logger?.LogWarning($"[Enlistment.Duties] Start '{duty.Id}' failed — could not spawn hunt target");
                return false;
            }
        }
        else if (duty.TargetKind is DutyTargetKind.FriendlySettlement or DutyTargetKind.FriendlyVillage or DutyTargetKind.AllySettlement)
        {
            targetSettlementId = duty.TargetKind switch
            {
                DutyTargetKind.FriendlyVillage => _world.FindNearestFriendlyVillage(commanderId),
                DutyTargetKind.AllySettlement => _world.FindNearestAllySettlement(commanderId),
                _ => _world.FindNearestFriendlySettlement(commanderId),
            };
            if (string.IsNullOrEmpty(targetSettlementId))
            {
                _logger?.LogWarning($"[Enlistment.Duties] Start '{duty.Id}' failed — no eligible settlement");
                return false;
            }
        }

        var staysAttached = duty.Mechanic == DutyMechanic.WaitHours;
        if (!staysAttached && !_stateMachine.TryTransition(EnlistmentState.EnlistedDetachedOnDuty))
        {
            if (!string.IsNullOrEmpty(targetPartyId))
                _world.DestroyParty(targetPartyId);
            return false;
        }

        record.ActiveDutyId = duty.Id;
        record.ActiveDutyTargetPartyId = targetPartyId;
        record.ActiveDutySettlementId = targetSettlementId;
        var bonusDays = record.Trust >= duty.TrustBonusThreshold ? duty.TrustDeadlineBonusDays : 0;
        record.ActiveDutyDeadlineDay = nowDays + duty.DeadlineDays + bonusDays;
        record.ActiveDutyFoodRequired = duty.Mechanic == DutyMechanic.DeliverFood ? FoodDeliveryAmount : 0;
        record.ShiftEndDay = staysAttached ? nowDays + ShiftDurationDays : (double?)null;

        if (!staysAttached)
            _attachment.RestorePresence();

        return true;
    }

    public void HourlyUpdate(double nowDays)
    {
        var record = _contentStore.Record;
        if (!record.HasActiveDuty)
            return;

        if (!_store.Record.IsEnlisted)
        {
            CancelActive("discharge");
            return;
        }

        // A non-finite day would make BOTH the shift-complete and the expiry comparison
        // false forever, stranding the duty (and its spawned party) permanently. Skip the
        // tick — the duty resumes on the next finite one.
        if (double.IsNaN(nowDays) || double.IsInfinity(nowDays))
        {
            _logger?.LogError($"[Enlistment.Duties] non-finite campaign day ({nowDays}) — skipping duty update for '{record.ActiveDutyId}'");
            return;
        }

        var duty = FindDuty(record.ActiveDutyId);
        if (duty == null)
        {
            CancelActive("missing-definition");
            return;
        }

        if (duty.AltCompletion == "EnemyContact" && _world.IsEnemyNearPlayer(EnemyContactRadius))
        {
            Complete(duty, "enemy-contact");
            return;
        }

        if (duty.Mechanic == DutyMechanic.WaitHours && record.ShiftEndDay.HasValue && nowDays >= record.ShiftEndDay.Value)
        {
            Complete(duty, "shift-complete");
            return;
        }

        if (record.ActiveDutyDeadlineDay.HasValue && nowDays >= record.ActiveDutyDeadlineDay.Value)
        {
            Fail(duty, "expired");
        }
    }

    public void OnTargetPartyDestroyed(string partyId)
    {
        var record = _contentStore.Record;
        if (!record.HasActiveDuty || string.IsNullOrEmpty(partyId) || record.ActiveDutyTargetPartyId != partyId)
            return;

        var duty = FindDuty(record.ActiveDutyId);
        if (duty == null)
        {
            CancelActive("missing-definition");
            return;
        }

        // The party is already gone at the engine level — FinishActive()'s DestroyParty
        // call is a defensive no-op (the adapter checks IsActive before acting).
        Complete(duty, "target-destroyed");
    }

    public void OnSettlementEntered(string settlementId, double nowDays)
    {
        var record = _contentStore.Record;
        if (!record.HasActiveDuty || string.IsNullOrEmpty(settlementId) || record.ActiveDutySettlementId != settlementId)
            return;

        var duty = FindDuty(record.ActiveDutyId);
        if (duty == null)
        {
            CancelActive("missing-definition");
            return;
        }

        switch (duty.Mechanic)
        {
            case DutyMechanic.VisitSettlement:
                Complete(duty, "visited");
                break;
            case DutyMechanic.DeliverFood:
                if (_world.CountPlayerFood() >= record.ActiveDutyFoodRequired)
                {
                    // Complete on what was actually handed over, not what the count
                    // promised — the two used to disagree (livestock counted, never
                    // consumed), which completed the delivery for free.
                    var delivered = _world.ConsumePlayerFood(record.ActiveDutyFoodRequired);
                    if (delivered >= record.ActiveDutyFoodRequired)
                        Complete(duty, "delivered");
                }
                break;
            case DutyMechanic.CollectFood:
                var bonus = ForageBonusMin + _random.Next(ForageBonusSpread);
                _world.GrantPlayerFood(ForageBaseAmount + bonus);
                Complete(duty, "foraged");
                break;
        }
    }

    public void CancelActive(string reason)
    {
        var record = _contentStore.Record;
        if (!record.HasActiveDuty)
            return;

        FinishActive();
        _logger?.LogInfo($"[Enlistment.Duties] active duty cancelled ({reason})");
    }

    private void Complete(DutyDefinition duty, string reason)
    {
        _rewards.Grant(duty.ReportReward, "duty:" + duty.Id);
        _contentStore.Record.DutySuccesses++;
        FinishActive();
        _logger?.LogInfo($"[Enlistment.Duties] duty '{duty.Id}' completed ({reason})");
    }

    private void Fail(DutyDefinition duty, string reason)
    {
        _rewards.AdjustTrust(-FailTrustPenalty);
        _contentStore.Record.DutyFailures++;
        FinishActive();
        _logger?.LogInfo($"[Enlistment.Duties] duty '{duty.Id}' failed ({reason})");
    }

    private void FinishActive()
    {
        var record = _contentStore.Record;
        if (!string.IsNullOrEmpty(record.ActiveDutyTargetPartyId))
            _world.DestroyParty(record.ActiveDutyTargetPartyId);

        var wasDetached = _stateMachine.State == EnlistmentState.EnlistedDetachedOnDuty;
        record.ClearActiveDuty();

        if (wasDetached)
        {
            _stateMachine.TryTransition(EnlistmentState.EnlistedAttached);
            _attachment.EnsureParked(_store.Record.CommanderHeroId);
        }
    }

    private DutyDefinition FindDuty(string dutyId)
    {
        if (string.IsNullOrEmpty(dutyId))
            return null;
        return _config.GetDuties().FieldDuties.FirstOrDefault(d => d.Id == dutyId);
    }
}
