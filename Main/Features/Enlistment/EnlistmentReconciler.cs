using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class EnlistmentReconciler : IEnlistmentReconciler
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentStateMachine _machine;
    private readonly IServiceAttachmentService _attachment;
    private readonly ICommanderLordAdapter _commander;
    private readonly IDischargeService _discharge;
    private readonly IEnlistmentConfigProvider _config;
    private readonly IEncounterAdapter _encounter;
    private readonly IModLogger _logger;

    public EnlistmentReconciler(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IServiceAttachmentService attachment,
        ICommanderLordAdapter commander,
        IDischargeService discharge,
        IEnlistmentConfigProvider config,
        IEncounterAdapter encounter,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _attachment = attachment;
        _commander = commander;
        _discharge = discharge;
        _config = config;
        _encounter = encounter;
        _logger = logger;
    }

    public event Action<string> BattleJoinRequested;

    public void ReconcileHourly(double nowDays)
    {
        var record = _store.Record;
        if (!record.IsEnlisted)
            return;

        var presence = _attachment.GetPresence();

        switch (record.State)
        {
            case EnlistmentState.EnlistedPlayerCaptive:
                ReconcileCaptive(record, presence);
                return;
            case EnlistmentState.CommanderUnavailable:
                ReconcileGrace(record, presence, nowDays);
                return;
            case EnlistmentState.EnlistedDetachedOnDuty:
                ReconcileDetachedDuty(record, presence, nowDays);
                return;
            case EnlistmentState.EnlistedAttached:
            case EnlistmentState.EnlistedBattle:
                ReconcileAttached(record, presence, nowDays);
                return;
        }
    }

    private void ReconcileCaptive(EnlistmentRecord record, PlayerPresenceSnapshot presence)
    {
        if (presence.IsCaptive)
            return;

        // Released — resume service; a missing commander is picked up by the next pass.
        _machine.TryTransition(EnlistmentState.EnlistedAttached);
        _attachment.EnsureParked(record.CommanderHeroId);
    }

    private void ReconcileGrace(EnlistmentRecord record, PlayerPresenceSnapshot presence, double nowDays)
    {
        // Grace is frozen during player captivity: expiring it would discharge and
        // restore presence while vanilla captivity owns the party.
        if (presence.IsCaptive)
            return;

        var snapshot = _commander.GetSnapshot(record.CommanderHeroId);
        if (!snapshot.Exists || !snapshot.IsAlive)
        {
            _discharge.Execute(DischargeReason.CommanderDead);
            return;
        }

        if (snapshot.HasParty && snapshot.PartyIsActive && !snapshot.IsPrisoner)
        {
            record.GraceEndsAtDay = null;
            _machine.TryTransition(EnlistmentState.EnlistedAttached);
            _attachment.EnsureParked(record.CommanderHeroId);
            _logger?.LogInfo($"[Enlistment] commander {record.CommanderHeroId} recovered — service resumes");
            return;
        }

        if (!record.GraceEndsAtDay.HasValue)
        {
            record.GraceEndsAtDay = nowDays + _config.GetConfig().CommanderGraceDays;
            return;
        }

        if (nowDays >= record.GraceEndsAtDay.Value)
            _discharge.Execute(DischargeReason.CommanderUnavailableGraceExpired);
    }

    private void ReconcileDetachedDuty(EnlistmentRecord record, PlayerPresenceSnapshot presence, double nowDays)
    {
        if (presence.IsCaptive)
        {
            _machine.TryTransition(EnlistmentState.EnlistedPlayerCaptive);
            return;
        }

        var snapshot = _commander.GetSnapshot(record.CommanderHeroId);
        if (!snapshot.Exists || !snapshot.IsAlive)
        {
            _discharge.Execute(DischargeReason.CommanderDead);
            return;
        }

        if (!snapshot.HasParty || !snapshot.PartyIsActive)
        {
            // Player is already free-roaming on duty — grace starts with no presence change.
            _machine.TryTransition(EnlistmentState.CommanderUnavailable);
            record.GraceEndsAtDay = nowDays + _config.GetConfig().CommanderGraceDays;
        }
    }

    private void ReconcileAttached(EnlistmentRecord record, PlayerPresenceSnapshot presence, double nowDays)
    {
        var snapshot = _commander.GetSnapshot(record.CommanderHeroId);

        // A battle state with no map event on either side is stale (event resolved while
        // we weren't looking) — return to attached before assessing. An OPEN encounter means
        // the battle is still live (loot/aftermath runs inside it, and the map event reads as
        // gone before the encounter closes), so demoting here would re-park mid-battle and
        // hand the menu guard the aftermath menus.
        if (record.State == EnlistmentState.EnlistedBattle
            && !presence.IsInMapEvent
            && !snapshot.PartyIsInMapEvent
            && !_encounter.HasCurrent)
        {
            _machine.TryTransition(EnlistmentState.EnlistedAttached);
        }

        var assessment = _attachment.Assess(record.State, snapshot, presence);
        switch (assessment.Status)
        {
            case AttachmentStatus.Attached:
                if (record.State == EnlistmentState.EnlistedAttached && presence.LooksParked)
                    _attachment.SyncPosition(record.CommanderHeroId);
                return;

            case AttachmentStatus.AttachRequired:
                _attachment.EnsureParked(record.CommanderHeroId);
                return;

            case AttachmentStatus.BattleJoinRequired:
                try
                {
                    BattleJoinRequested?.Invoke(record.CommanderHeroId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"[Enlistment] BattleJoinRequested subscriber threw: {ex.Message}");
                }
                return;

            case AttachmentStatus.Blocked:
                ReconcileBlocked(record, snapshot, assessment.BlockReason, nowDays);
                return;
        }
    }

    private void ReconcileBlocked(
        EnlistmentRecord record, CommanderSnapshot snapshot, AttachmentBlockReason reason, double nowDays)
    {
        switch (reason)
        {
            case AttachmentBlockReason.PlayerCaptive:
                _machine.TryTransition(EnlistmentState.EnlistedPlayerCaptive);
                return;

            case AttachmentBlockReason.CommanderPartyMissing:
                if (!snapshot.Exists || !snapshot.IsAlive)
                {
                    _discharge.Execute(DischargeReason.CommanderDead);
                    return;
                }

                // EnlistedBattle -> CommanderUnavailable is deliberately illegal; the
                // transition is retried after the battle resolves back to attached.
                if (_machine.TryTransition(EnlistmentState.CommanderUnavailable))
                {
                    record.GraceEndsAtDay = nowDays + _config.GetConfig().CommanderGraceDays;
                    _attachment.RestorePresence();
                    _logger?.LogInfo($"[Enlistment] commander {record.CommanderHeroId} lost their party — grace until day {record.GraceEndsAtDay:F1}");
                }
                return;

            case AttachmentBlockReason.PlayerInForeignMapEvent:
                // Let the foreign event resolve; nothing safe to do.
                return;

            default:
                // NotInAttachableState is unreachable from ReconcileAttached today; if a
                // refactor ever routes it (or a new reason) here, fail loudly, not silently.
                _logger?.LogError($"[Enlistment] unhandled attachment block reason {reason} in reconciler");
                return;
        }
    }
}
