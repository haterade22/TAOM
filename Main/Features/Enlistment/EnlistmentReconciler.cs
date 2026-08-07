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
    private readonly IEncounterOwnershipPolicy _ownership;
    private readonly IModLogger _logger;

    public EnlistmentReconciler(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IServiceAttachmentService attachment,
        ICommanderLordAdapter commander,
        IDischargeService discharge,
        IEnlistmentConfigProvider config,
        IEncounterAdapter encounter,
        IEncounterOwnershipPolicy ownership,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _attachment = attachment;
        _commander = commander;
        _discharge = discharge;
        _config = config;
        _encounter = encounter;
        _ownership = ownership;
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

        // DEBUG: the hourly tick fires many times per real second at accelerated campaign speed
        // (576 lines in one 32-minute session). The anomaly branches below stay at WARNING/ERROR,
        // so a genuine fault is still loud without this routine line flushing on every tick.
        _logger?.LogDebug(
            $"[EnlistDiag] TICK state={record.State} verdict={assessment.Status}" +
            (assessment.Status == AttachmentStatus.Blocked ? $"({assessment.BlockReason})" : "") +
            $" | player: {presence.Describe()}" +
            $" | commander '{record.CommanderHeroId}': exists={snapshot.Exists} alive={snapshot.IsAlive} " +
            $"party={snapshot.PartyId ?? "NONE"} partyActive={snapshot.PartyIsActive} inMapEvent={snapshot.PartyIsInMapEvent} prisoner={snapshot.IsPrisoner}");

        // Self-heal a stranded conversation encounter. While EnlistedAttached and out of any map
        // event there is no legitimate reason for a live PlayerEncounter: the oath conversation's
        // encounter should have been closed at swear-in. Left open it blocks every main-party
        // encounter for the whole term and survives into discharge. Saves made before that fix
        // are already in this state, so heal it here rather than only at the source.
        if (record.State == EnlistmentState.EnlistedAttached
            && presence.HasPlayerEncounter
            && !presence.IsInMapEvent
            && !snapshot.PartyIsInMapEvent)
        {
            var sweepVerdict = _ownership.Evaluate(
                EncounterFinishIntent.ParkedSweep, _encounter.GetOwnership(snapshot.PartyId));
            if (sweepVerdict == EncounterFinishVerdict.Finish)
            {
                _logger?.LogWarning("[EnlistDiag] a PlayerEncounter is open while parked with no battle in progress — closing it (it would block every future encounter)");
                if (!_encounter.Finish(true))
                    _logger?.LogError("[EnlistDiag] failed to close the stranded PlayerEncounter — the player cannot start encounters until this clears");
            }
            else if (sweepVerdict != EncounterFinishVerdict.NothingToFinish)
            {
                _logger?.LogInfo($"[EnlistDiag] parked sweep left the live encounter alone: {sweepVerdict}");
            }
        }

        switch (assessment.Status)
        {
            case AttachmentStatus.Attached:
                if (record.State == EnlistmentState.EnlistedAttached && presence.LooksParked)
                {
                    if (!_attachment.SyncPosition(record.CommanderHeroId))
                        _logger?.LogError("[EnlistDiag] hourly SYNC failed — the player will keep drifting from the commander");
                }
                else if (record.State == EnlistmentState.EnlistedAttached && !presence.LooksParked)
                {
                    _logger?.LogWarning($"[EnlistDiag] verdict=Attached but the party is NOT parked ({presence.Describe()}) — no sync will run this tick");
                }
                return;

            case AttachmentStatus.AttachRequired:
                if (!_attachment.EnsureParked(record.CommanderHeroId))
                    _logger?.LogError($"[EnlistDiag] hourly PARK failed for commander '{record.CommanderHeroId}' — player is loose on the map while still enlisted");
                return;

            case AttachmentStatus.BattleJoinRequired:
                // ONE retry budget shared with the real-time pump, so adding the pump cannot
                // multiply the attempt rate. The budget is stored in campaign HOURS; the
                // reconciler is handed days, so it converts here. That equivalence is pinned by
                // a test — if it ever drifts, this check silently suppresses hourly recovery
                // entirely and restores exactly the bug this whole effort exists to fix.
                var nowHours = nowDays * 24.0;
                if (record.NextAttachRetryAtHours.HasValue && nowHours < record.NextAttachRetryAtHours.Value)
                    return;
                record.NextAttachRetryAtHours = nowHours + 1.0;
                record.PendingCommanderAttachment = true;
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
