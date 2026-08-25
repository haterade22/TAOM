using System;
using System.Collections.Generic;
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
    private readonly IEnlistmentDiagnosticsSettingsProvider _diag;
    private readonly IEnlistmentFeatureSettingsProvider _feature;
    private readonly IArmyMembershipAdapter _army;
    private readonly IModLogger _logger;

    /// <summary>
    /// RE-ENTRANCY GUARD, and the hazard is live rather than theoretical. Settlement following
    /// made the reconciler call LeaveSettlementAction, which dispatches OnSettlementLeft — the
    /// very edge this class now subscribes. Without this flag the reconciler re-enters itself
    /// mid-pass, on a record it is halfway through mutating.
    /// </summary>
    private bool _reconcileInFlight;

    /// <summary>Fallback when the configured grace window is unusable. Matches EnlistmentCoreConfig.</summary>
    private const double DefaultGraceDays = 7.0;

    private readonly IInquiryAdapter _inquiry;

    /// <summary>
    /// Commander the loss modal has already been raised for, so the hourly tick cannot re-raise it
    /// every hour of the grace. Not persisted: after a save/load the player is re-told once, which
    /// is the right side to fail on — this is the message explaining why they are suddenly visible
    /// and alone, and hearing it twice beats resuming a campaign with no idea.
    /// </summary>
    private string _lossAnnouncedFor;

    public EnlistmentReconciler(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IServiceAttachmentService attachment,
        ICommanderLordAdapter commander,
        IDischargeService discharge,
        IEnlistmentConfigProvider config,
        IEncounterAdapter encounter,
        IEncounterOwnershipPolicy ownership,
        IEnlistmentDiagnosticsSettingsProvider diag,
        IEnlistmentFeatureSettingsProvider feature,
        IInquiryAdapter inquiry,
        IArmyMembershipAdapter army,
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
        _diag = diag;
        _feature = feature;
        _inquiry = inquiry;
        _army = army;
        _logger = logger;
    }

    public event Action<string> BattleJoinRequested;

    public void ReconcileHourly(double nowDays) => Reconcile(nowDays, "hourly");

    /// <summary>
    /// Run a full reconcile now, off an edge rather than the hourly tick. The trigger string is
    /// diagnostic only — it names which edge woke us in the log.
    /// </summary>
    public void ReconcileNow(double nowDays, string trigger) => Reconcile(nowDays, trigger);

    private void Reconcile(double nowDays, string trigger)
    {
        var record = _store.Record;
        if (!record.IsEnlisted)
            return;

        if (_reconcileInFlight)
            return;

        _reconcileInFlight = true;
        try
        {
            ReconcileCore(record, nowDays, trigger);
        }
        finally
        {
            _reconcileInFlight = false;
        }
    }

    private void ReconcileCore(EnlistmentRecord record, double nowDays, string trigger)
    {

        // MCM master switch, checked before any other reconciliation. Turning the feature off
        // mid-service cannot simply stop the loop: the player is parked HIDDEN and INACTIVE,
        // and the code that restores them is the code being switched off — halting in place
        // would strand them invisible on the map with no menu, a soft-lock caused by a
        // settings toggle. One honourable discharge through the normal pipeline instead, which
        // restores presence, closes any encounter and hands them back somewhere they can act.
        if (_feature?.IsEnabled == false)
        {
            _logger?.LogInfo("[Enlistment] feature disabled in settings while serving — releasing the player honourably");
            _discharge.Execute(DischargeReason.PlayerRequest);
            return;
        }

        // WITH the commander id: without it distanceToCommander is hard-wired to -1 and the
        // TICK line prints 'distToCommander=?', which is the one number that answers
        // "am I actually being left behind". It printed '?' for the whole first live session.
        var presence = _attachment.GetPresence(record.CommanderHeroId);

        switch (record.State)
        {
            case EnlistmentState.EnlistedPlayerCaptive:
                ReconcileCaptive(record, presence);
                return;
            case EnlistmentState.CommanderUnavailable:
                ReconcileGrace(record, presence, nowDays);
                return;
            case EnlistmentState.EnlistedDetachedOnDuty:
                ReconcileRetiredDetachedDuty();
                return;
            case EnlistmentState.EnlistedAttached:
            case EnlistmentState.EnlistedBattle:
                ReconcileAttached(record, presence, nowDays, trigger);
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

    /// <summary>
    /// The one moment the player is told their service just changed underneath them. Raised at the
    /// transition, once per commander, with a real choice.
    ///
    /// WHY A MODAL AND NOT A TOAST. This fires in the tick after a battle, which is exactly when
    /// the player is accelerating time — and a toast at speed is not a message. #436 measured that
    /// precisely: two duty toasts four real seconds apart read as one, and the player said so.
    /// Unlike a duty assignment, this one also asks a question, and questions get modals.
    ///
    /// WHY prioritize: true. <c>InformationManager.ShowInquiry(data, pauseGameActiveState,
    /// prioritize)</c> (verified 1.4.7) ENQUEUES a non-prioritized inquiry behind whatever is
    /// already on screen — and the tick after a battle is when vanilla raises its own ransom and
    /// peace popups. Queued, this arrives minutes later with no context.
    ///
    /// WHY NO EXPLICIT TimeControlMode = Stop, which was the original plan.
    /// <c>pauseGameActiveState: true</c> already holds the clock while the popup is up, and that is
    /// the whole window that matters. Forcing Stop as well would be a second engine mutation from a
    /// service, and it is one BannerlordTogether prefixes and rewrites outright in a co-op session
    /// — see CoopSuppressedUiAttribute. A control that lies is worse than no control.
    /// </summary>
    private void AnnounceCommanderLoss(EnlistmentRecord record, CommanderSnapshot snapshot, double nowDays)
    {
        if (_inquiry == null || _lossAnnouncedFor == record.CommanderHeroId)
            return;

        _lossAnnouncedFor = record.CommanderHeroId;

        var days = record.GraceEndsAtDay.HasValue
            ? Math.Max(0, (int)Math.Ceiling(record.GraceEndsAtDay.Value - nowDays))
            : (int)DefaultGraceDays;

        var vars = new Dictionary<string, string>
        {
            ["COMMANDER"] = snapshot.Name ?? "your commander",
            ["DAYS"] = days.ToString(),
        };

        // CASE SPLIT IN THE TEXT, NOT THE CLOCK. One 7-day window serves both: a captured lord has
        // a 4%/day escape chance (PrisonerReleaseCampaignBehavior.DailyHeroTick:206, verified
        // 1.4.7) = ~25% inside the window, plus the peace and ransom release paths the same
        // behavior runs; a party-less free lord waits on a respawn tick and then a clan-tier gate.
        // Neither is a certainty and neither is a no-op, so two timers would be two things to tune
        // and two things to explain for no measured gain.
        //
        // What genuinely differs is what can be SAID. Captivity is the only case with a location —
        // a lord whose party was merely destroyed has no position at all until the engine respawns
        // him, so there is nothing to name.
        string bodyKey, bodyFallback;
        if (snapshot.IsPrisoner && !string.IsNullOrEmpty(snapshot.CaptivitySettlementName))
        {
            vars["CAPTOR"] = snapshot.CaptorName ?? "the enemy";
            vars["TOWN"] = snapshot.CaptivitySettlementName;
            bodyKey = "taom_enlist_lost_captured_body";
            bodyFallback = "{COMMANDER} was taken on the field. {CAPTOR} hold him in {TOWN}, and the company is scattered. "
                + "Hold to your oath and wait for word, or count your service at an end. "
                + "If no word comes in {DAYS} days, the oath lapses and you are paid what you are owed.";
        }
        else if (snapshot.IsPrisoner)
        {
            bodyKey = "taom_enlist_lost_captured_unknown_body";
            bodyFallback = "{COMMANDER} was taken on the field, and no one can say where he is held. The company is scattered. "
                + "Hold to your oath and wait for word, or count your service at an end. "
                + "If no word comes in {DAYS} days, the oath lapses and you are paid what you are owed.";
        }
        else
        {
            bodyKey = "taom_enlist_lost_broken_body";
            bodyFallback = "{COMMANDER}'s company is broken. He lives, but there is no banner left to march with. "
                + "Hold to your oath and wait for him to raise another, or count your service at an end. "
                + "If no word comes in {DAYS} days, the oath lapses and you are paid what you are owed.";
        }

        _inquiry.ShowTwoOptionInquiry(
            "taom_enlist_lost_title", "Word from the column",
            bodyKey, bodyFallback,
            "taom_enlist_lost_hold", "Hold to your oath",
            "taom_enlist_lost_leave", "Count your service ended",
            onOptionA: null,
            // PlayerRequest, not CommanderUnavailableGraceExpired: the player ASKED, no timer ran.
            // The consequence arms differ, and calling this an expiry would settle them as though
            // they had waited the full term out.
            onOptionB: () => _discharge.Execute(DischargeReason.PlayerRequest),
            bodyVariables: vars,
            prioritize: true);
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

            // Re-arm the announcement with the grace timer it belongs to. The latch exists to stop
            // the modal repeating every hour WITHIN one episode; leaving it set past a recovery
            // made it a once-per-commander-per-process latch instead, so a lord who was captured,
            // ransomed, and then lost his party again later took the player back into a silent
            // grace — visible and alone on the map with nothing said. Same commander, second
            // episode, and the message that explains it is exactly the one being suppressed.
            _lossAnnouncedFor = null;

            _machine.TryTransition(EnlistmentState.EnlistedAttached);
            _attachment.EnsureParked(record.CommanderHeroId);
            _logger?.LogInfo($"[Enlistment] commander {record.CommanderHeroId} recovered — service resumes");
            return;
        }

        if (!record.GraceEndsAtDay.HasValue)
        {
            record.GraceEndsAtDay = GraceDeadline(nowDays);
            return;
        }

        if (nowDays >= record.GraceEndsAtDay.Value)
            _discharge.Execute(DischargeReason.CommanderUnavailableGraceExpired);
    }

    /// <summary>
    /// RETIRED STATE, RECOVERY ONLY. Nothing produces <c>EnlistedDetachedOnDuty</c> since field
    /// duties stopped detaching (2026-08-09) — its only producer was <c>FieldDutyRuntime.Start</c>,
    /// and <c>EnlistmentRecord.ToPersistedState</c> coerces it to attached on parse, so no save can
    /// restore it either.
    ///
    /// The 30-line handler that lived here (captivity sync, commander-death discharge, grace start)
    /// went with it. This stub survives because "unreachable" rests entirely on that one coercion:
    /// if it ever regressed, a player in state 4 with no handler would sit in a state the
    /// reconciler ignores forever. Returning them to attached costs three lines and cannot be wrong
    /// — attached is where every path out of a duty led anyway.
    /// </summary>
    private void ReconcileRetiredDetachedDuty()
    {
        _logger?.LogWarning(
            "[Enlistment] reconciler saw the retired EnlistedDetachedOnDuty state — nothing should "
            + "produce it since field duties stopped detaching. Returning to attached; if this line "
            + "ever appears, the parse-time coercion in EnlistmentRecord.ToPersistedState has failed.");
        _machine.TryTransition(EnlistmentState.EnlistedAttached);
    }

    private void ReconcileAttached(EnlistmentRecord record, PlayerPresenceSnapshot presence, double nowDays, string trigger)
    {
        var snapshot = _commander.GetSnapshot(record.CommanderHeroId);

        // No map event on either side and no open encounter — there is no battle anywhere. An OPEN
        // encounter means the battle is still live (loot/aftermath runs inside it, and the map event
        // reads as gone before the encounter closes), so treating that as "over" would re-park
        // mid-battle and hand the menu guard the aftermath menus.
        var noBattleAnywhere = !presence.IsInMapEvent
            && !snapshot.PartyIsInMapEvent
            && !_encounter.HasCurrent;

        // THE ARMY COMES OFF WHENEVER THERE IS NO BATTLE, and deliberately NOT only when the state
        // still reads EnlistedBattle. Army membership is acquired by ServiceBattleService.TryJoin
        // and released by OnCommanderBattleEnded; when the battle resolves without that edge — a
        // throw, a co-op host handoff, a save/load across the MapEventEnded — this is the only code
        // that notices.
        //
        // The save/load case is why the guard cannot key on EnlistedBattle. EnlistmentRecord
        // COERCES EnlistedBattle to EnlistedAttached on persist (ToPersistedState), so a save taken
        // mid-battle reloads reading EnlistedAttached while the main party is still merged into the
        // commander's army — the one shape a state-keyed check is structurally blind to. Found by
        // the Codex pass, 2026-08-12.
        //
        // Leaving it attached is not cosmetic: MobileParty.AttachedTo stays set, and
        // PlayerEncounter.FinishEncounterInternal grants the post-defeat escape ONLY when
        // AttachedTo == null — so the next unrelated ambush re-creates field report 7b's "jumped
        // immediately after being defeated" exactly, with no army fight anywhere to explain it.
        //
        // Gated on IsInArmy so the ordinary parked tick does no work and cannot race the battle
        // path for ownership of an army raised seconds earlier.
        if (noBattleAnywhere && _army?.IsInArmy == true)
            _army.LeaveArmy();

        if (record.State == EnlistmentState.EnlistedBattle && noBattleAnywhere)
            _machine.TryTransition(EnlistmentState.EnlistedAttached);

        var assessment = _attachment.Assess(record.State, snapshot, presence, record.OnTownLeave);

        // GATE SHAPE RULE (every [EnlistDiag] gate in this feature): the gate is an
        // `if (_diag?.IsEnabled == true)` wrapping EXACTLY ONE logging statement. Never a block,
        // never guarding a `return`, never placed above a call to _encounter / _attachment /
        // _machine / _store / _discharge. `?.` + `== true` so a null provider fails quiet.
        //
        // THIS METHOD IS WHY THE RULE EXISTS: the logging below is interleaved with the stranded-
        // encounter self-heal, the re-park and the position sync. An `if (!enabled) return;` here
        // would disable the enlistment self-heal for anyone who turned the toggle off, rather than
        // just silencing a log line. Pinned by EnlistmentDiagnosticsGateTests group B1.
        //
        // INFO, not DEBUG: the hourly tick fires many times per real second at accelerated campaign
        // speed (576 lines in one 32-minute session), but DEBUG is FileLogger's async queue and a
        // hard native CTD drops whatever is still queued — which would lose this trace at exactly
        // the moment it matters. The toggle, not the level, is what controls the volume. The anomaly
        // branches below stay at WARNING/ERROR and are NOT gated, so a genuine fault is still loud
        // with the toggle off.
        if (_diag?.IsEnabled == true)
            _logger?.LogInfo(
                $"[EnlistDiag] TICK trigger={trigger} state={record.State} verdict={assessment.Status}" +
                (assessment.Status == AttachmentStatus.Blocked ? $"({assessment.BlockReason})" : "") +
                $" | player: {presence.Describe()}" +
                $" | commander '{record.CommanderHeroId}': exists={snapshot.Exists} alive={snapshot.IsAlive} " +
                $"party={snapshot.PartyId ?? "NONE"} partyActive={snapshot.PartyIsActive} inMapEvent={snapshot.PartyIsInMapEvent} prisoner={snapshot.IsPrisoner} settlement={snapshot.SettlementId ?? "-"}");

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
                // Inside the commander's settlement the party is deliberately active, visible and
                // pinned to the gate — so it is legitimately NOT parked, and there is no position
                // to sync (the engine owns placement while CurrentSettlement is set). Without this
                // exemption the settlement stop logs an anomaly every hour and calls a correct
                // state a fault. The `else if` below still fires for a genuinely unparked party
                // OUTSIDE any settlement, which is the real anomaly.
                if (presence.IsHeldInsideSettlement)
                    return;

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

            case AttachmentStatus.SettlementFollowRequired:
                _attachment.FollowCommanderIntoSettlement(record.CommanderHeroId, snapshot.SettlementId);
                return;

            case AttachmentStatus.SettlementExitRequired:
                // Checked, not discarded: a persistently failing LeaveSettlement would return
                // SettlementExitRequired again every hour forever, silently, with the player
                // stuck inside a settlement the commander has left.
                if (!_attachment.ExitSettlementForService(record.CommanderHeroId))
                    _logger?.LogError($"[EnlistDiag] hourly EXIT failed — the player is stuck inside a settlement '{presence.SettlementId}' the commander has left");
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
                    record.GraceEndsAtDay = GraceDeadline(nowDays);
                    _attachment.RestorePresence();
                    _logger?.LogInfo($"[Enlistment] commander {record.CommanderHeroId} lost their party — grace until day {record.GraceEndsAtDay:F1}");
                    AnnounceCommanderLoss(record, snapshot, nowDays);
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

    /// <summary>
    /// Grace deadline, with the config value sanitised at the point of use.
    ///
    /// NaN is the failure that matters: `nowDays >= GraceEndsAtDay` is FALSE forever against a
    /// NaN deadline, so grace would never expire and the player would sit in CommanderUnavailable
    /// permanently with no auto-discharge — a soft-lock produced by one bad number. A negative
    /// value is the opposite failure: grace expires instantly and discharges the player the moment
    /// their commander so much as blinks. Both fall back to the compiled default.
    /// </summary>
    private double GraceDeadline(double nowDays)
    {
        var days = _config?.GetConfig()?.CommanderGraceDays ?? DefaultGraceDays;

        // Positive requirement: NaN and infinity both fail this and take the default.
        if (!(days > 0.0) || double.IsInfinity(days))
        {
            _logger?.LogWarning($"[Enlistment] CommanderGraceDays is not a usable number ({days}) — using {DefaultGraceDays} days instead");
            days = DefaultGraceDays;
        }

        return nowDays + days;
    }
}
