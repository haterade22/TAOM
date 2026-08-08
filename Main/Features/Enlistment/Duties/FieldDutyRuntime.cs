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
            // Anchor on the commander's CURRENT settlement only when they are actually inside one.
            // CommanderSnapshot.SettlementId is empty whenever the column is in the field — which
            // is nearly always — so using it alone meant hunt duties could only ever start while
            // the commander sat in a town. Observed in-game 2026-08-07: "SpawnLooterParty:
            // settlement='' or looters clan unresolved" followed by every recon_sweep failing.
            var commanderSnapshot = _commander.GetSnapshot(commanderId);
            var anchorSettlementId = string.IsNullOrEmpty(commanderSnapshot.SettlementId)
                ? _world.FindNearestFriendlySettlement(commanderId)
                : commanderSnapshot.SettlementId;

            if (string.IsNullOrEmpty(anchorSettlementId))
            {
                _logger?.LogWarning($"[Enlistment.Duties] Start '{duty.Id}' failed — no settlement to anchor the hunt target near (commander '{commanderId}' is not in one and none was found nearby)");
                return false;
            }

            targetPartyId = _world.SpawnLooterParty(
                "taom_enlist_duty", anchorSettlementId, patrolNotEngage: duty.TargetAi != DutyTargetAi.EngagePlayer);
            if (string.IsNullOrEmpty(targetPartyId))
            {
                _logger?.LogWarning($"[Enlistment.Duties] Start '{duty.Id}' failed — could not spawn hunt target near '{anchorSettlementId}'");
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
        {
            _attachment.RestorePresence();

            // Settlement following can leave the player INSIDE the commander's town when a duty
            // starts. Restoring presence alone would send them off on a duty while still held at
            // the gate — and nothing walks them out afterwards, because a detached duty state is
            // Blocked in Assess, so the reconciler's settlement-exit verdict never runs for it.
            if (_attachment.GetPresenceFlags().IsInSettlement)
                _attachment.ExitSettlementForDuty();
        }

        // Duty START, logged. Fail / complete / cancel all had a line and this did not, so when a
        // tester reported being unable to move immediately after accepting a duty (#375), the state
        // at onset could not be reconstructed from the log at all — the first evidence of the duty
        // existing was its outcome. Presence and attachment are in here because "detached but still
        // parked" and "detached inside a settlement" are the two shapes that produce that symptom.
        var flags = _attachment.GetPresenceFlags();
        _logger?.LogInfo(
            $"[Enlistment.Duties] duty '{duty.Id}' started — mechanic={duty.Mechanic} " +
            $"state={_stateMachine.State} staysAttached={staysAttached} " +
            $"target={(string.IsNullOrEmpty(targetPartyId) ? targetSettlementId ?? "none" : targetPartyId)} " +
            $"deadlineDay={record.ActiveDutyDeadlineDay:F2} " +
            $"parked={flags.LooksParked} inSettlement={flags.IsInSettlement}");

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

        // Reached re-entrantly: the engine raises MobilePartyDestroyed from inside our own
        // DestroyParty call. The guard above is what stops that becoming infinite recursion, and
        // it only works because FinishActive clears the record BEFORE destroying — see its doc.
        //
        // An earlier comment here claimed "the party is already gone at the engine level, so
        // FinishActive's DestroyParty is a defensive no-op (the adapter checks IsActive)". Both
        // halves are false: DestroyPartyAction dispatches the event before RemoveParty(), so the
        // party still reads IsActive, and the adapter guard passes straight through. That false
        // premise is what let the #375 stack overflow ship.
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

    /// <summary>
    /// CLEAR-BEFORE-DESTROY, and the order is load-bearing — reversing it kills the process.
    ///
    /// Destroying the target raises <c>CampaignEvents.MobilePartyDestroyed</c>, which routes
    /// straight back into <see cref="OnTargetPartyDestroyed"/>. That handler's only exit is
    /// <c>!record.HasActiveDuty</c>, so while the record still names the duty the callback runs
    /// <c>Complete → Grant → FinishActive → DestroyParty</c> and re-enters. Nothing breaks the
    /// cycle: the engine dispatches the event BEFORE it deactivates the party
    /// (<c>DestroyPartyAction.ApplyInternal</c> — <c>OnMobilePartyDestroyed</c> on line 23,
    /// <c>RemoveParty()</c> on line 25), so the adapter's <c>!IsActive</c> guard still reads the
    /// party as alive and calls <c>Apply</c> again, and <c>Debug.FailedAssert</c> does not throw
    /// in a shipping build.
    ///
    /// Observed in a live session 2026-08-08 (#375): 7,482 reward grants inside one wall-clock
    /// second, ~336k XP and ~450k gold granted, then a <c>StackOverflowException</c> — uncatchable,
    /// so the process died with no crash report. Clearing first makes the re-entrant call return at
    /// the guard on its first frame.
    ///
    /// If <c>DestroyParty</c> throws after the clear, the target party leaks on the map. That is
    /// the deliberate trade: a stray looter party is recoverable, a dead process is not.
    /// </summary>
    private void FinishActive()
    {
        var record = _contentStore.Record;
        var targetPartyId = record.ActiveDutyTargetPartyId;
        var wasDetached = _stateMachine.State == EnlistmentState.EnlistedDetachedOnDuty;

        record.ClearActiveDuty();

        if (!string.IsNullOrEmpty(targetPartyId))
            _world.DestroyParty(targetPartyId);

        if (wasDetached)
        {
            _stateMachine.TryTransition(EnlistmentState.EnlistedAttached);

            // Leave first if the duty ended inside a settlement. Parking hides and deactivates the
            // party WITHOUT leaving, producing hidden-and-inactive-inside-a-settlement — the exact
            // state the reconciler's Attached branch documents as impossible, and which it then
            // skips over because it treats "inside a settlement" as legitimately unparked. The
            // symmetric guard already exists in Start; it was missing here.
            if (_attachment.GetPresenceFlags().IsInSettlement)
                _attachment.ExitSettlementForService(_store.Record.CommanderHeroId);
            else
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
