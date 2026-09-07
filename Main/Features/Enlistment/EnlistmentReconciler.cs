using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
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

    /// <summary>Fallback when the configured latch window is unusable. Matches EnlistmentCoreConfig.</summary>
    private const double DefaultStaleBattleLatchDays = 1.0 / 24.0;

    /// <summary>
    /// Campaign day the current stale-battle-latch episode was first observed, or NaN when the
    /// player is not in that shape (issue #551). One CONTINUOUS episode: any tick where the shape
    /// is absent clears it, so two unrelated loot windows an hour apart never add up to a recovery.
    ///
    /// NOT PERSISTED, AND THAT IS NOT THE SAME AS SAFE. This class is <c>Reuse.Singleton</c>, so it
    /// outlives the campaign: a campaign that ends WHILE latched leaves a finite anchor behind, and
    /// campaign days are absolute. Load a later save and <c>elapsed</c> is enormous, so the recovery
    /// fires on the very first latched tick and finishes what may be a genuine loot screen with no
    /// real waiting at all: exactly the destructive <c>Finish</c> R1b exists to prevent, committed by
    /// the code meant to be the safety net. Two independent guards, because they cover different
    /// paths: <see cref="ResetForNewSession"/> handles the load path, and the backwards-clock
    /// re-anchor in <see cref="BreakStaleBattleLatch"/> handles a brand-new campaign, which never
    /// reaches <c>ResetSessionCaches</c> at all.
    /// </summary>
    private double _staleBattleLatchSinceDays = double.NaN;

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

    /// <inheritdoc/>
    public void ResetForNewSession() => _staleBattleLatchSinceDays = double.NaN;

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

    /// <summary>
    /// Close a PlayerEncounter that has outlived whatever opened it. Shared by the attached path and
    /// the grace window because a stranded encounter is the same fault in both, and because the two
    /// having separate copies is how the grace window came to have none at all.
    ///
    /// A live encounter with nobody in a map event is never legitimate: it stops map movement,
    /// blocks every future main-party encounter for the rest of the term, survives into discharge,
    /// and blocks <c>ServiceMaintenanceService.TryBreakBattleLatch</c>, which is what turned it into
    /// a permanent latch after a siege.
    ///
    /// The caller's state is NOT a guard here, and getting to that safely took a correction worth
    /// recording. The first version of this fix removed the old <c>EnlistedAttached</c> gate on the
    /// grounds that "the state was never the real guard". That was wrong: the state gate WAS doing
    /// real work, because <c>EnlistedBattle</c> is exactly the state the loot/aftermath window lives
    /// in, and this method's guards are <c>noBattleAnywhere</c> MINUS its <c>!HasCurrent</c> term.
    /// Removing the state gate alone would have let the sweep tear down the player's own battle
    /// result after a siege, which is the very scenario that was being fixed.
    ///
    /// What replaced it is a guard that names the real condition instead of proxying it:
    /// <see cref="EncounterOwnershipSnapshot.IsBattleEncounter"/> (policy rule R1b), so a battle
    /// encounter is protected in EVERY state and for every intent rather than only while the state
    /// happens to read <c>EnlistedAttached</c>. The two map-event checks and
    /// <see cref="IEncounterOwnershipPolicy"/> carry the rest.
    /// </summary>
    private void SweepStrandedEncounter(
        PlayerPresenceSnapshot presence, bool commanderInMapEvent, string commanderPartyId)
    {
        if (!presence.HasPlayerEncounter || presence.IsInMapEvent || commanderInMapEvent)
            return;

        // WHICH intent, decided by whether the player is actually in a settlement, because that is
        // the whole difference between a live encounter and a stranded one. Since #510 every
        // settlement placement opens an encounter deliberately, so a settlement-shaped encounter is
        // CORRECT while the player is inside one and R3 must keep skipping it: closing it would take
        // down the town menu the player is standing in. Out of the settlement there is nothing left
        // for R3 to protect and the encounter is pure blockage, so StrandedOutsideSettlement inverts
        // R3 exactly as ShoreLeaveEnd does.
        //
        // Passing StrandedOutsideSettlement while inside a settlement is the one way to misuse it,
        // which is why the precondition is enforced here, at the only place that chooses it.
        var sweepIntent = presence.IsHeldInsideSettlement
            ? EncounterFinishIntent.ParkedSweep
            : EncounterFinishIntent.StrandedOutsideSettlement;

        var sweepVerdict = _ownership.Evaluate(sweepIntent, _encounter.GetOwnership(commanderPartyId));
        if (sweepVerdict == EncounterFinishVerdict.Finish)
        {
            _logger?.LogWarning("[EnlistDiag] a PlayerEncounter is open with no battle in progress — closing it (it would block every future encounter)");
            if (!_encounter.Finish(true))
                _logger?.LogError("[EnlistDiag] failed to close the stranded PlayerEncounter — the player cannot start encounters until this clears");
        }
        else if (sweepVerdict != EncounterFinishVerdict.NothingToFinish)
        {
            _logger?.LogInfo($"[EnlistDiag] encounter sweep left the live encounter alone: {sweepVerdict}");
        }
    }

    private void ReconcileGrace(EnlistmentRecord record, PlayerPresenceSnapshot presence, double nowDays)
    {
        // Grace is frozen during player captivity: expiring it would discharge and
        // restore presence while vanilla captivity owns the party.
        if (presence.IsCaptive)
            return;

        var snapshot = _commander.GetSnapshot(record.CommanderHeroId);

        // The grace window needs the sweep as much as the attached path does, and used to lack it.
        // Nothing in this method touched the encounter, so a player who arrived here holding one
        // waited out a grace of up to seven campaign days, unable to move, until discharge finally
        // closed it. Bounded, unlike the battle latch, and still no way to play.
        //
        // Precision, because the first version of this comment got it wrong: a commander lost DURING
        // an assault does NOT land here. `EnlistedBattle -> CommanderUnavailable` is a deliberately
        // illegal edge (EnlistmentTransitionTable, and ReconcileBlocked says so in as many words),
        // so that player stays latched in EnlistedBattle and is freed by the sweep on the attached
        // path instead. This call is an independent backstop for the states that DO reach grace, not
        // the fix for the siege report.
        //
        // Below the captivity guard on purpose: vanilla owns the party during captivity and the
        // sweep must not reach around that, for the same reason grace itself is frozen there.
        SweepStrandedEncounter(presence, snapshot.PartyIsInMapEvent, snapshot.PartyId);
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

    /// <summary>
    /// Release a player latched in <c>EnlistedBattle</c> behind an encounter that has outlived the
    /// battle it belonged to (issue #551). Returns true when it actually recovered, so the caller
    /// knows the presence snapshot it is holding has gone stale.
    ///
    /// THE SHAPE: <c>EnlistedBattle</c>, an open <c>PlayerEncounter</c>, and the player in no map
    /// event. A join that lands and is torn down in the same second produces it, and nothing else
    /// in this feature can move it. <c>ServiceMaintenanceService.TryBreakBattleLatch</c> returns on
    /// <c>HasPlayerEncounter</c>; <c>SweepStrandedEncounter</c> returns while the commander is in a
    /// map event; the ownership policy's R1b defers every intent, discharge included, because the
    /// encounter reads as a battle; and <c>ServiceBattleService.TryJoin</c> refuses every rejoin
    /// because the state is not <c>EnlistedAttached</c>. Four correct guards, no exit between them.
    ///
    /// THE DISCRIMINATOR IS TIME, and only time. R1b protects the loot and aftermath window, which
    /// has this exact shape and is legitimate, so the recovery may not act on the shape alone. It
    /// waits out <c>StaleBattleLatchDays</c> of continuous latching first, which no loot screen
    /// survives. That is also why it hands R1c the decision rather than calling
    /// <c>_encounter.Finish</c> itself: the policy owns whether an encounter is ours to close, and
    /// R1 still refuses outright if the player turns out to be in a map event after all.
    /// </summary>
    private bool BreakStaleBattleLatch(
        EnlistmentRecord record, PlayerPresenceSnapshot presence, string commanderPartyId, double nowDays)
    {
        var latched = record.State == EnlistmentState.EnlistedBattle
            && presence != null
            && presence.HasPlayerEncounter
            && !presence.IsInMapEvent;

        if (!latched || !FiniteFloatValidator.IsFinite(nowDays))
        {
            _staleBattleLatchSinceDays = double.NaN;
            return false;
        }

        // Re-anchor on no anchor, and equally on an anchor in the FUTURE. A clock that ran backwards
        // cannot be a continuous episode; it means a different campaign or an earlier save, and the
        // anchor belongs to a world this one has nothing to do with. This is the guard for the path
        // ResetForNewSession does not reach: ResetSessionCaches is wired to OnGameLoaded only, so a
        // brand-new campaign in the same process never calls it, and a new campaign's low day count
        // puts the leftover anchor ahead of it.
        if (!FiniteFloatValidator.IsFinite(_staleBattleLatchSinceDays) || nowDays < _staleBattleLatchSinceDays)
        {
            _staleBattleLatchSinceDays = nowDays;
            return false;
        }

        var thresholdDays = _config?.GetConfig()?.StaleBattleLatchDays ?? DefaultStaleBattleLatchDays;
        if (!FiniteFloatValidator.IsFiniteAtLeast(thresholdDays, 0.0))
            thresholdDays = DefaultStaleBattleLatchDays;

        // POSITIVE REQUIREMENT rather than an inverted early exit: every comparison against NaN is
        // false, so `if (elapsed < threshold) return;` would let a poisoned clock through into the
        // recovery instead of holding it back.
        var elapsedDays = nowDays - _staleBattleLatchSinceDays;
        if (!(elapsedDays >= thresholdDays))
            return false;

        var verdict = _ownership.Evaluate(
            EncounterFinishIntent.StaleBattleLatch, _encounter.GetOwnership(commanderPartyId));
        if (verdict != EncounterFinishVerdict.Finish)
        {
            // A skip is not a reset. Whatever caused it (a conversation, a settlement) will pass,
            // and it is still the same episode, so the next tick retries against the same clock.
            _logger?.LogInfo($"[Enlistment] stale battle latch not cleared this tick: {verdict}");
            return false;
        }

        _logger?.LogWarning(
            $"[Enlistment] breaking a stale EnlistedBattle latch after {elapsedDays * 24.0:F1} in-game hours: " +
            "an encounter is open, the player is in no map event, and nothing else can release him (#551)");

        if (!_encounter.Finish(true))
        {
            _logger?.LogError("[Enlistment] could not close the latched encounter — the player is still stuck");
            return false;
        }

        _staleBattleLatchSinceDays = double.NaN;

        // The transition is what unblocks everything else: the maintenance pump gates its menu work
        // on EnlistedAttached, and ServiceBattleService refuses every join from any other state.
        if (!_machine.TryTransition(EnlistmentState.EnlistedAttached))
            return false;

        _attachment.EnsureParked(record.CommanderHeroId);
        return true;
    }

    private void ReconcileAttached(EnlistmentRecord record, PlayerPresenceSnapshot presence, double nowDays, string trigger)
    {
        var snapshot = _commander.GetSnapshot(record.CommanderHeroId);

        // BEFORE the assessment, not after, so a recovery lands in the same pass that noticed it.
        // Everything downstream reads `record.State` and `presence`, and the whole point of the
        // recovery is to change both. Running it after the assessment would leave the player one
        // more in-game hour without a menu for no reason.
        if (BreakStaleBattleLatch(record, presence, snapshot.PartyId, nowDays))
            presence = _attachment.GetPresence(record.CommanderHeroId);

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

        // Self-heal a stranded conversation encounter. Out of any map event there is no legitimate
        // reason for a live PlayerEncounter: the oath conversation's encounter should have been
        // closed at swear-in. Left open it blocks every main-party encounter for the whole term and
        // survives into discharge. Saves made before that fix are already in this state, so heal it
        // here rather than only at the source.
        //
        // ENLISTEDBATTLE IS INCLUDED, AND THAT IS THE WHOLE FIX FOR THE SIEGE STRAND. This used to
        // require EnlistedAttached, which made it one half of a mutual block:
        // ServiceMaintenanceService.TryBreakBattleLatch is the only exit from EnlistedBattle when no
        // battle is running, and it returns early while HasPlayerEncounter — so the encounter
        // stopped the return to Attached, and not being Attached stopped this sweep from clearing
        // the encounter. Neither side could ever move. The player cannot move (an open encounter
        // holds the map), cannot open any other encounter, and loses the service menu, because the
        // pump gates its menu work on Attached too.
        //
        // A siege is the way in. LeaveSettlementAction.ApplyForParty (installed v1.4.8) calls
        // PlayerEncounter.Finish() in exactly one branch, when the LEAVING party leads its army and
        // the main party is attached to it. An enlisted player is the main party and leads nothing,
        // so the encounter TAOM opens for every settlement placement since #510 is left open, and a
        // siege supplies the long map event that holds the state in EnlistedBattle.
        //
        // The state was never the real guard. The two IsInMapEvent terms are: while either party is
        // in a map event the encounter may belong to a battle being set up. Those stay, and the
        // ownership policy below still decides whether the encounter is ours to close at all, so
        // widening the state gate grants no new authority.
        SweepStrandedEncounter(presence, snapshot.PartyIsInMapEvent, snapshot.PartyId);

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
                if (_attachment.FollowCommanderIntoSettlement(record.CommanderHeroId, snapshot.SettlementId))
                    _attachment.StampSettlementEntry(nowDays * 24.0);
                return;

            case AttachmentStatus.SettlementExitRequired:
                // MINIMUM DWELL. An AI lord dips into a town for under a campaign hour, and
                // following him straight back out gave ten transitions across three towns in three
                // real minutes (measured 2026-08-25), twice in and out within the SAME second.
                // Hold the placement briefly so the stop is usable.
                //
                // THE BATTLE CARVE-OUT IS NOT OPTIONAL. The exit rule sits ABOVE the battle branch
                // in Assess on purpose: joining a map event with CurrentSettlement pointing at some
                // other settlement makes MapEvent.AddInvolvedPartyInternal rewrite a siege ASSAULT
                // to SiegeOutside for a joining defender. So a dwell that outranked a commander
                // battle would not merely delay the join, it would corrupt the battle everyone
                // else is fighting. When he is in a map event the dwell yields immediately.
                if (!snapshot.PartyIsInMapEvent && _attachment.IsWithinSettlementDwell(nowDays * 24.0))
                {
                    if (_diag?.IsEnabled == true)
                        _logger?.LogInfo($"[EnlistDiag] EXIT deferred — holding '{presence.SettlementId}' for the minimum dwell (commander is in '{snapshot.SettlementId ?? "the field"}')");
                    return;
                }

                // Checked, not discarded: a persistently failing LeaveSettlement would return
                // SettlementExitRequired again every hour forever, silently, with the player
                // stuck inside a settlement the commander has left.
                // ONE call, TWO outcomes folded together: ExitSettlementForService returns
                // `LeaveSettlement() && ParkNear()`, so a false can mean the player is still inside
                // the settlement OR that they left cleanly and only the re-park failed. Treating
                // false as "still inside" logged a message that was simply untrue in the second case
                // — and, worse, skipped the encounter finish below for a player who HAD left and was
                // therefore holding exactly the strand this branch exists to clear. ParkNear fails
                // whenever the commander party cannot be found, which is the defining condition of
                // the grace window, so that is not a rare pairing.
                var exited = _attachment.ExitSettlementForService(record.CommanderHeroId);
                if (!exited)
                    _logger?.LogError($"[EnlistDiag] hourly EXIT did not complete — the player was in '{presence.SettlementId}' and the commander is in '{snapshot.SettlementId ?? "the field"}' (either the settlement exit or the re-park failed; see the adapter line above)");

                // The engine will NOT close the encounter an exit leaves behind, so close it here
                // rather than waiting up to a campaign hour for the sweep above to notice.
                // `LeaveSettlementAction.ApplyForParty` (installed v1.4.8) calls
                // `PlayerEncounter.Finish()` only when the LEAVING party leads its army and the main
                // party is attached to it; an enlisted player is the main party and leads nothing,
                // so the branch never runs and the encounter TAOM opened on entry (#510) outlives
                // the settlement. Left open it stops map movement and blocks the battle-latch break.
                //
                // Attempted whether or not the re-park succeeded, and safe either way: R2c re-reads
                // PlayerInsideSettlement itself, so if the exit genuinely failed the player is still
                // inside, the rule declines, and nothing is destroyed. The sweep remains the backstop
                // for every other route into the same shape, including saves already in it.
                var exitVerdict = _ownership.Evaluate(
                    EncounterFinishIntent.StrandedOutsideSettlement, _encounter.GetOwnership(snapshot.PartyId));
                if (exitVerdict == EncounterFinishVerdict.Finish && !_encounter.Finish(false))
                    _logger?.LogError("[EnlistDiag] EXIT left a PlayerEncounter open and could not close it — the player cannot start encounters until this clears");
                else if (exitVerdict != EncounterFinishVerdict.Finish && exitVerdict != EncounterFinishVerdict.NothingToFinish)
                    _logger?.LogInfo($"[EnlistDiag] EXIT left the live encounter alone: {exitVerdict}");
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
