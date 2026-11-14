using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.ArmyTargeting;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// The War Chronicle's event chain (docs/features/war-chronicle.md "The event chain", #765 M3): the state machine
/// over <see cref="ChronicleStateStore"/>. Times are whole campaign days: the floor of
/// <see cref="IChronicleWorldAdapter.GetElapsedDays"/>, the same day as the ledger's <c>day=</c>.
///
/// <list type="bullet">
/// <item><b>Adoption (D5):</b> an event with no saved row gets one; when its open day is already past, or
/// its anchor was Skipped, it is Skipped with no retroactive effect.</item>
/// <item><b>Pending:</b> opens once its anchor is known and its day has come. A phase anchor needs the phase
/// reached and counts from the later of <see cref="IWarOfTheRingService.GetEffectivePhaseDay"/> and the day
/// before the chronicle first saw the phase (decided by the maintainer 2026-10-14; see <see cref="PhaseDay"/>);
/// an Event anchor needs the anchor resolved, not Skipped, and with the required outcome. A failed
/// precondition Skips it.</item>
/// <item><b>Narrative:</b> Occurred at once, effects applied.</item>
/// <item><b>Siege:</b> Fell at once when an attacker already owns the target as it opens (decided by the
/// maintainer 2026-10-14), so its Event-anchored children still open; otherwise Active with a stored window
/// and an army focus; assaulted when an attacker besieges the
/// target. An attacker owner means Fell, at once on a capture and on the next daily step when a clan's
/// change of kingdom moved the town with no owner-changed event; any other owner that is not the defender
/// means Lapsed. At the close, assaulted means Held, otherwise Lapsed; while an attacker still besieges the
/// target the close waits, for at most one more window.</item>
/// <item><b>War Ended:</b> every Pending or Active event is Skipped and the focus clears.</item>
/// </list>
/// Every owner-changed handler is idempotent: the event fires twice per conquest, and the second fire finds
/// the window resolved. The MCM switch stops new work (nothing opens or resolves, the focus clears) and
/// keeps the saved states. The caller has checked that this peer may run world work; a throwing engine
/// read is logged and the step skipped, so the campaign tick never fails here.
/// </summary>
public sealed class ChronicleService : IChronicleStoryPort
{
    /// <summary>
    /// What a window says when the target changed hands to neither side: a third kingdom took it, or its clan
    /// left the defender's kingdom with it. The events' own lapsed lines all say no attack came.
    /// </summary>
    public const string LostText = "{=taom_wc_siege_lost}{TARGET} changes hands before the battle can be decided.";

    private readonly IChronicleConfigProvider _config;
    private readonly ChronicleStateStore _store;
    private readonly IChronicleWorldAdapter _world;
    private readonly IAllianceAdapter _alliance;
    private readonly IWarOfTheRingService _wotr;
    private readonly ChronicleOutcomeService _outcomes;
    private readonly ChronicleAnnouncementQueue _announcements;
    private readonly ArmyTargetFocusRegistry _focus;
    private readonly IWarChronicleSettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly WarChronicleDailyGate _daily;

    public ChronicleService(
        IChronicleConfigProvider config, ChronicleStateStore store, IChronicleWorldAdapter world,
        IAllianceAdapter alliance, IWarOfTheRingService wotr, ChronicleOutcomeService outcomes,
        ChronicleAnnouncementQueue announcements, ArmyTargetFocusRegistry focus, IWarChronicleSettingsProvider settings,
        IModLogger logger, WarChronicleDailyGate daily)
    {
        _config = config;
        _store = store;
        _world = world;
        _alliance = alliance;
        _wotr = wotr;
        _outcomes = outcomes;
        _announcements = announcements;
        _focus = focus;
        _settings = settings;
        _logger = logger;
        _daily = daily;
    }

    /// <summary>Drops the focus an earlier campaign left: called from the events behavior's constructor.</summary>
    public void ResetTransientState() => _focus.ResetForNewSession();

    /// <summary>Session launch: adopt missing rows, Skip events the campaign cannot host, push the focus again.</summary>
    public void LaunchChronicle() => Guarded("session launch", now =>
    {
        NotePhasesSeen(now);
        Adopt(now);
        foreach (var definition in _config.GetEvents())
        {
            var record = _store.Get(definition.Id);
            if (record != null && IsOpen(record.State) && !KnowsEveryId(definition, out var unknown))
            {
                _logger.LogWarning($"[WarChronicle] '{definition.Id}' names '{unknown}', which does not exist in this campaign, so it is skipped.");
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, $"'{unknown}' does not exist in this campaign");
            }
        }

        if (_wotr.CurrentPhase == WarPhase.WarEnded)
            SkipEveryOpenEvent(now);
    });

    /// <summary>
    /// The daily step: open what is due, watch the Active windows, close what has run out. At most once per
    /// campaign day (<see cref="WarChronicleDailyGate"/>): after a load the next daily tick can repeat the save's day.
    /// </summary>
    public void RunChronicleDay() => Guarded("daily step", WarChronicleDailyGate.ChronicleLane, now =>
    {
        NotePhasesSeen(now);
        Adopt(now);
        if (_wotr.CurrentPhase == WarPhase.WarEnded)
        {
            SkipEveryOpenEvent(now);
            return;
        }

        foreach (var definition in _config.GetEvents())
            Step(definition, now);
    });

    /// <summary>A settlement changed owner (twice per conquest): resolve an Active window on it.</summary>
    public void OnTargetOwnerChanged(string? settlementId)
    {
        if (settlementId == null)
            return;

        Guarded("owner change", now =>
        {
            foreach (var definition in _config.GetEvents())
            {
                var record = ActiveSiegeAt(definition, settlementId);
                if (record != null)
                    CheckOwner(definition, record, now);
            }
        });
    }

    /// <summary>A siege began somewhere: an attacker besieging an Active target counts as the assault.</summary>
    public void OnTargetBesieged(string? settlementId)
    {
        if (settlementId == null)
            return;

        Guarded("siege start", _ =>
        {
            foreach (var definition in _config.GetEvents())
            {
                var record = ActiveSiegeAt(definition, settlementId);
                if (record != null)
                    MarkAssaultIfBesieged(definition, record);
            }
        });
    }

    /// <summary>
    /// Console: opens a Pending event now, ignoring its day but not its preconditions. With
    /// <paramref name="dryRun"/> it changes nothing (no adoption, no phase days, no row) and says what a
    /// confirmed open would do; true then means a confirmed open would act.
    /// </summary>
    public bool OpenChronicleEventNow(string? eventId, bool dryRun, out string reason)
    {
        reason = string.Empty;
        if (!_settings.ChronicleEnabled)
        {
            reason = "The War Chronicle is off (MCM War of the Ring/War Chronicle).";
            return false;
        }

        var definition = _config.GetEvents().FirstOrDefault(d => string.Equals(d.Id, eventId, StringComparison.Ordinal));
        if (definition == null)
        {
            reason = $"No chronicle event '{eventId}'. Known: {string.Join(", ", _config.GetEvents().Select(d => d.Id))}.";
            return false;
        }

        var now = Today();
        if (!FiniteFloatValidator.IsFinite(now))
        {
            reason = "The campaign clock is not readable.";
            return false;
        }

        if (_wotr.CurrentPhase == WarPhase.WarEnded)
        {
            reason = "The War of the Ring has ended; the chronicle is closed.";
            return false;
        }

        if (dryRun)
            return PreviewOpen(definition, now, out reason);

        NotePhasesSeen(now);
        Adopt(now);
        var record = _store.Get(definition.Id)!;
        if (record.State != ChronicleEventState.Pending)
        {
            reason = NotPending(definition, record);
            return false;
        }

        if (!TryOpen(definition, record, now, out reason))
            return false;

        SyncFocus();
        reason = record.State == ChronicleEventState.Active
            ? $"'{definition.Id}' is Active until day {Day(record.CloseDay)}."
            : $"'{definition.Id}' {record.State}.";
        return true;
    }

    /// <summary>
    /// The dry run of <see cref="OpenChronicleEventNow"/>: the same checks and preconditions, read only. A row
    /// not yet adopted is reported, not adopted, because adoption can itself resolve the event Skipped.
    /// </summary>
    private bool PreviewOpen(ChronicleEventDefinition definition, double now, out string reason)
    {
        var record = _store.Get(definition.Id);
        if (record == null)
        {
            reason = $"'{definition.Id}' has no saved row yet, so there is nothing to preview. Adoption adds it "
                     + "as Pending, or as Skipped when its day has passed or its anchor fails; only a Pending "
                     + "event can be opened.";
            return false;
        }

        if (record.State != ChronicleEventState.Pending)
        {
            reason = NotPending(definition, record);
            return false;
        }

        if (!PreconditionsHold(definition, out var why, out var takenBy))
        {
            reason = $"'{definition.Id}' would stay Pending: {why}.";
            return false;
        }

        if (!definition.IsSiege)
            reason = $"'{definition.Id}' would occur now and apply its effects.";
        else if (takenBy != null)
            reason = $"'{definition.Id}' would resolve Fell at once ({takenBy} already holds "
                     + $"{definition.TargetSettlementId}) and apply its Fell outcome, if it defines one.";
        else
            reason = $"'{definition.Id}' would open its window until day {Day(now + definition.WindowDays)}.";
        return true;
    }

    private static string NotPending(ChronicleEventDefinition definition, ChronicleEventRecord record) =>
        $"'{definition.Id}' is {record.State}; only a Pending event can be opened.";

    /// <summary>Every configured event with its state and days, for <c>taom.chronicle_status</c>.</summary>
    public IReadOnlyList<ChronicleStatusRow> Status()
    {
        var rows = new List<ChronicleStatusRow>();
        foreach (var definition in _config.GetEvents())
        {
            var record = _store.Get(definition.Id);
            var state = record?.State ?? ChronicleEventState.Pending;
            rows.Add(new ChronicleStatusRow
            {
                Id = definition.Id,
                Kind = definition.Kind,
                State = state,
                OpenDay = record?.OpenDay ?? (state == ChronicleEventState.Pending ? OpenDayOf(definition, out _) : null),
                CloseDay = record?.CloseDay,
                Assaulted = record?.Assaulted ?? false,
                ResolvedDay = record?.ResolvedDay,
                Target = definition.TargetSettlementId,
            });
        }

        return rows;
    }

    private void Guarded(string step, Action<double> body) => Guarded(step, null, body);

    /// <param name="dailyLane">The daily step's <see cref="WarChronicleDailyGate"/> lane: a day already run is
    /// skipped (a save made in the first half of an elapsed campaign day ticks that day again after a load;
    /// the elapsed day starts at CampaignStartTime, 09:00 on vanilla's calendar clock, not at midnight); null
    /// for every other entry.</param>
    private void Guarded(string step, string? dailyLane, Action<double> body)
    {
        if (!_settings.ChronicleEnabled)
        {
            _focus.Clear();
            return;
        }

        try
        {
            var now = Today();
            // Positive requirement on the engine clock: a NaN day would pass no `>=` gate, but it must not
            // reach a stored window either.
            if (!FiniteFloatValidator.IsFinite(now))
                return;

            if (dailyLane != null && !_daily.TryClaim(dailyLane, now))
                return;

            // Every entry point, callbacks included, stops at War Ended: no payout can land after the war.
            if (_wotr.CurrentPhase == WarPhase.WarEnded)
            {
                NotePhasesSeen(now);
                Adopt(now);
                SkipEveryOpenEvent(now);
                SyncFocus();
                return;
            }

            body(now);
            SyncFocus();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[WarChronicle] Chronicle {step} skipped ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    /// <summary>
    /// Whole elapsed campaign days, like vanilla's weekly tick (Campaign.cs) and the ledger's <c>day=</c>.
    /// An elapsed day starts at CampaignStartTime plus whole days (09:00 on vanilla's calendar clock, not
    /// midnight). The daily tick fires a few game minutes past that boundary, or at a fractional time after
    /// a load, so fractional days would let a window or a child slip a day. Math.Floor keeps NaN and Infinity, so the
    /// finite gates still stop a bad clock.
    /// </summary>
    private double Today() => Math.Floor(_world.GetElapsedDays());

    /// <summary>Records the first day the chronicle sees each anchoring phase reached; a saved day is kept.</summary>
    private void NotePhasesSeen(double now)
    {
        if (PhaseReached(WarPhase.IsengardWar))
            _store.NotePhaseSeen(WarPhase.IsengardWar, now);
        if (PhaseReached(WarPhase.FullWar))
            _store.NotePhaseSeen(WarPhase.FullWar, now);
    }

    private void Adopt(double now)
    {
        foreach (var definition in _config.GetEvents())
        {
            if (_store.Get(definition.Id) != null)
                continue;

            var record = new ChronicleEventRecord(definition.Id, ChronicleEventState.Pending);
            _store.Set(record);
            // D5 judges a missing row by the configured day: a save from before the chronicle gets no phase
            // event whose configured day has passed, whatever day the chronicle first saw the phase.
            var openDay = OpenDayOf(definition, out var anchorFailure, fromFirstSighting: false);
            if (anchorFailure != null)
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, anchorFailure);
            else if (openDay.HasValue && openDay.Value < now)
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, $"its day {Day(openDay)} had passed when the save first saw it");
        }
    }

    private void Step(ChronicleEventDefinition definition, double now)
    {
        var record = _store.Get(definition.Id);
        if (record == null)
            return;

        if (record.State == ChronicleEventState.Pending)
        {
            var openDay = OpenDayOf(definition, out var anchorFailure);
            if (anchorFailure != null)
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, anchorFailure);
            else if (openDay.HasValue && now >= openDay.Value && !TryOpen(definition, record, now, out var why))
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, why);
            return;
        }

        if (record.State != ChronicleEventState.Active)
            return;

        // An owner change can raise no event (a clan that defects or leaves with rebellion keeps its fiefs,
        // and a change while the chronicle is off is unseen): the daily step catches it.
        if (CheckOwner(definition, record, now))
            return;

        MarkAssaultIfBesieged(definition, record);
        if (!record.CloseDay.HasValue || now < record.CloseDay.Value)
            return;

        // A siege still being fought decides the window: lifted, Held on the next daily step; taken, Fell
        // through OnTargetOwnerChanged. The overtime is capped at one more window, because the engine never
        // makes a besieging party leave (a player's camp could otherwise hold the window and its focus open).
        if (AttackerBesieges(definition) && now < record.CloseDay.Value + definition.WindowDays)
            return;

        Close(definition, record, now);
    }

    /// <summary>
    /// The open day, or null while the anchor is unknown. A non-null failure means the event can never open.
    /// <paramref name="fromFirstSighting"/> false reads a phase anchor's configured day alone (adoption).
    /// </summary>
    private double? OpenDayOf(ChronicleEventDefinition definition, out string? failure, bool fromFirstSighting = true)
    {
        failure = null;
        switch (definition.Anchor)
        {
            case ChronicleAnchor.CampaignStart:
                return definition.OffsetDays;

            case ChronicleAnchor.Phase1:
                return PhaseDay(WarPhase.IsengardWar, fromFirstSighting) + definition.OffsetDays;

            case ChronicleAnchor.Phase2:
                return PhaseDay(WarPhase.FullWar, fromFirstSighting) + definition.OffsetDays;

            case ChronicleAnchor.Event:
                var anchor = _store.Get(definition.AnchorEventId);
                if (anchor == null || IsOpen(anchor.State))
                    return null;
                if (anchor.State == ChronicleEventState.Skipped)
                {
                    failure = $"its anchor '{definition.AnchorEventId}' was skipped";
                    return null;
                }

                if (definition.RequiresOutcome.HasValue && anchor.State != definition.RequiresOutcome.Value)
                {
                    failure = $"its anchor '{definition.AnchorEventId}' ended {anchor.State}, not {definition.RequiresOutcome.Value}";
                    return null;
                }

                return anchor.ResolvedDay + definition.OffsetDays;

            default:
                return null;
        }
    }

    /// <summary>
    /// The day a phase anchor counts from once the phase is reached; null before. It is the later of the
    /// configured effective day and the day before the chronicle first saw the phase, so a phase entered
    /// late (the war switched on late, a phase day lowered below today, Test Mode switched on, the first load of
    /// a save without the saved days) opens its events from that day plus their offsets instead of all on one step. The day
    /// before, because the chronicle's daily listener runs before the phase machine's: a phase entered on
    /// day N's tick is first seen on N + 1, so a campaign on schedule keeps its configured days. Raising a
    /// phase day after the phase began still postpones its Pending events. A step that has not yet noted
    /// the phase (<see cref="Status"/>), and adoption, read the configured day alone.
    /// </summary>
    private double? PhaseDay(WarPhase phase, bool fromFirstSighting)
    {
        if (!PhaseReached(phase))
            return null;

        var configured = _wotr.GetEffectivePhaseDay(phase);
        var seen = fromFirstSighting ? _store.GetPhaseSeenDay(phase) : null;
        if (!configured.HasValue || !seen.HasValue)
            return configured;
        return Math.Max(configured.Value, seen.Value - 1d);
    }

    private bool PhaseReached(WarPhase phase) => (int)_wotr.CurrentPhase >= (int)phase;

    private bool TryOpen(ChronicleEventDefinition definition, ChronicleEventRecord record, double now, out string reason)
    {
        if (!PreconditionsHold(definition, out reason, out var takenBy))
            return false;

        record.OpenDay = now;
        if (!definition.IsSiege)
        {
            Resolve(definition, record, ChronicleEventState.Occurred, now, true, "its day came");
            return true;
        }

        // Decided by the maintainer 2026-10-14: a target an attacker already holds has fallen. Recording it
        // Fell, not Skipped, pays the Fell outcome and lets the Event-anchored children open.
        if (takenBy != null)
        {
            Resolve(definition, record, ChronicleEventState.Fell, now, true, takenBy + " already held it when it opened");
            return true;
        }

        record.State = ChronicleEventState.Active;
        record.CloseDay = now + definition.WindowDays;
        record.Assaulted = false;
        MarkAssaultIfBesieged(definition, record);
        _announcements.Enqueue(new ChronicleAnnouncement(
            definition.Id, definition.Title, definition.OpenText, definition.TargetSettlementId, definition.WindowDays));
        _logger.LogInfo($"[WarChronicle] '{definition.Id}' opened on day {Day(now)}: {definition.TargetSettlementId}, "
                        + $"{string.Join("+", definition.Attackers)} against {definition.Defender}, closes day {Day(record.CloseDay)}.");
        return true;
    }

    /// <summary>
    /// True when the event may open. For a siege whose target an attacker already owns, <paramref name="takenBy"/>
    /// names that attacker and the event resolves Fell instead of opening a window: then only the anchor
    /// outcome and the Full War are required, because the defender may be gone and nobody need be at war.
    /// </summary>
    private bool PreconditionsHold(ChronicleEventDefinition definition, out string reason, out string? takenBy)
    {
        reason = string.Empty;
        takenBy = null;
        if (definition.RequiresOutcome.HasValue)
        {
            var anchor = _store.Get(definition.AnchorEventId);
            if (anchor == null || anchor.State != definition.RequiresOutcome.Value)
            {
                reason = $"'{definition.AnchorEventId}' has not ended {definition.RequiresOutcome.Value}";
                return false;
            }
        }

        if (!definition.IsSiege)
            return true;

        // D2: peace between Hostile pairs is blocked only in the Full War.
        if (_wotr.CurrentPhase != WarPhase.FullWar)
        {
            reason = $"a siege opens only in the Full War (the phase is {_wotr.CurrentPhase})";
            return false;
        }

        var owner = _world.GetOwnerKingdomId(definition.TargetSettlementId!);
        if (owner != null && definition.Attackers.Contains(owner))
        {
            takenBy = owner;
            return true;
        }

        var live = new HashSet<string>(_alliance.GetAllKingdomIds() ?? new List<string>(), StringComparer.Ordinal);
        if (!live.Contains(definition.Defender!))
        {
            reason = $"the defender {definition.Defender} is gone";
            return false;
        }

        var attackers = definition.Attackers.Where(live.Contains).ToList();
        if (attackers.Count == 0)
        {
            reason = "every attacker is gone";
            return false;
        }

        if (!string.Equals(owner, definition.Defender, StringComparison.Ordinal))
        {
            reason = $"{definition.TargetSettlementId} is held by {owner ?? "no kingdom"}, not {definition.Defender}";
            return false;
        }

        if (!attackers.Any(a => _alliance.AreAtWar(definition.Defender!, a)))
        {
            reason = $"{definition.Defender} is at war with none of {string.Join(", ", attackers)}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves the window when the target left the defender: Fell when an attacker owns it, Lapsed (with
    /// <see cref="LostText"/>) when a third kingdom or no kingdom does. True when it resolved the window.
    /// </summary>
    private bool CheckOwner(ChronicleEventDefinition definition, ChronicleEventRecord record, double now)
    {
        var owner = _world.GetOwnerKingdomId(definition.TargetSettlementId!);
        if (owner != null && definition.Attackers.Contains(owner))
        {
            Resolve(definition, record, ChronicleEventState.Fell, now, true, owner + " took it");
            return true;
        }

        if (!string.Equals(owner, definition.Defender, StringComparison.Ordinal))
        {
            Resolve(definition, record, ChronicleEventState.Lapsed, now, true, (owner ?? "no kingdom") + " took it", LostText);
            return true;
        }

        return false;
    }

    /// <summary>
    /// The close: Step's <see cref="CheckOwner"/> has just confirmed the defender holds the target. A window with a story offer that was never
    /// assaulted ends by its fate (#1105 slice 4): Held with the fate line, or Lapsed as a window without a story.
    /// </summary>
    private void Close(ChronicleEventDefinition definition, ChronicleEventRecord record, double now)
    {
        if (record.Assaulted)
        {
            Resolve(definition, record, ChronicleEventState.Held, now, true, "assaulted and held");
            return;
        }

        if (definition.Story is { Fate: ChronicleEventState.Held } story)
        {
            Resolve(definition, record, ChronicleEventState.Held, now, true, "never assaulted; held by fate", story.FateText);
            return;
        }

        Resolve(definition, record, ChronicleEventState.Lapsed, now, true, "never assaulted");
    }

    /// <summary>Every Active story window the player has not declined, in configuration order (#1105 slice 4).</summary>
    public IReadOnlyList<ChronicleStoryOffer> GetStoryOffers()
    {
        var offers = new List<ChronicleStoryOffer>();
        // War Ended: a window stays Active in the store until the next guarded step skips it, and no story battle may open after the war.
        // A queued announcement shows first: the window's opening line explains the offer.
        if (!_settings.ChronicleEnabled || !_settings.StoryOffersEnabled || _wotr.CurrentPhase == WarPhase.WarEnded || _announcements.Count > 0)
            return offers;

        foreach (var definition in _config.GetEvents())
        {
            var record = _store.Get(definition.Id);
            // The map decides first: no offer once an attacker has besieged the target or the defender no longer holds it (an owner
            // change can raise no event, so the owner is read live, as the daily step's CheckOwner reads it).
            if (definition.Story != null && record is { State: ChronicleEventState.Active, StoryDeclined: false, Assaulted: false }
                && !AttackerBesieges(definition)
                && string.Equals(_world.GetOwnerKingdomId(definition.TargetSettlementId!), definition.Defender, StringComparison.Ordinal))
                offers.Add(new ChronicleStoryOffer(definition.Id, definition.Story.MissionId, definition.Title, definition.TargetSettlementId, definition.Attackers));
        }

        return offers;
    }

    /// <summary>"Let fate decide": the window stays open and the map or its fate decides it. False when no Active story window has this id.</summary>
    public bool DeclineStory(string? eventId)
    {
        var record = ActiveStoryWindow(eventId, out _);
        if (record == null)
            return false;

        record.StoryDeclined = true;
        _logger.LogInfo($"[WarChronicle] '{eventId}': the player let fate decide the story offer.");
        return true;
    }

    /// <summary>
    /// The player won the window's story mission: Held at once, with its effects. False when no Active story window has this id, or the
    /// chronicle step was skipped (switched off, War Ended, a bad clock). A lost mission needs no call: the transfer to the attacker
    /// resolves the window Fell through <see cref="OnTargetOwnerChanged"/>.
    /// </summary>
    public bool ResolveStoryWon(string? eventId)
    {
        var resolved = false;
        Guarded("story result", now =>
        {
            var record = ActiveStoryWindow(eventId, out var definition);
            if (record == null || CheckOwner(definition!, record, now))
                return;

            Resolve(definition!, record, ChronicleEventState.Held, now, true, "the player won the story mission");
            resolved = true;
        });
        return resolved;
    }

    public bool ConfirmStoryWindow(string? eventId)
    {
        var open = false;
        Guarded("story transfer", now =>
        {
            var record = ActiveStoryWindow(eventId, out var definition);
            open = record != null && !CheckOwner(definition!, record, now);
        });
        return open;
    }

    private ChronicleEventRecord? ActiveStoryWindow(string? eventId, out ChronicleEventDefinition? definition)
    {
        definition = _config.GetEvents().FirstOrDefault(d => d.Story != null && string.Equals(d.Id, eventId, StringComparison.Ordinal));
        var record = definition == null ? null : _store.Get(definition.Id);
        return record?.State == ChronicleEventState.Active ? record : null;
    }

    private void MarkAssaultIfBesieged(ChronicleEventDefinition definition, ChronicleEventRecord record)
    {
        if (record.Assaulted || !AttackerBesieges(definition))
            return;

        record.Assaulted = true;
        _logger.LogInfo($"[WarChronicle] '{definition.Id}': an attacker besieges {definition.TargetSettlementId}; the window counts as assaulted.");
    }

    private bool AttackerBesieges(ChronicleEventDefinition definition)
    {
        var besieger = _world.GetBesiegerKingdomId(definition.TargetSettlementId!);
        return besieger != null && definition.Attackers.Contains(besieger);
    }

    private void Resolve(ChronicleEventDefinition definition, ChronicleEventRecord record, ChronicleEventState outcome,
        double now, bool announce, string why, string? text = null)
    {
        record.State = outcome;
        record.ResolvedDay = now;
        _outcomes.Apply(definition, outcome, _world.GetNowHours());
        if (announce)
        {
            _announcements.Enqueue(new ChronicleAnnouncement(
                definition.Id, definition.Title, text ?? TextFor(definition, outcome), definition.TargetSettlementId, definition.WindowDays));
        }

        _logger.LogInfo($"[WarChronicle] '{definition.Id}' {outcome} on day {Day(now)} ({why}).");
    }

    private void SkipEveryOpenEvent(double now)
    {
        foreach (var definition in _config.GetEvents())
        {
            var record = _store.Get(definition.Id);
            if (record != null && IsOpen(record.State))
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, "the War of the Ring has ended");
        }
    }

    /// <summary>
    /// Writes every Active siege window into ArmyTargeting's registry (#765 M4), replacing the whole set, so a
    /// resolved window drops out by not being listed. The MCM multiplier is read here and baked in, so the
    /// scoring path never reads a setting.
    /// </summary>
    private void SyncFocus()
    {
        var multiplier = _settings.ArmyFocusMultiplier;
        var focuses = new List<ArmyTargetFocus>();
        foreach (var definition in _config.GetEvents())
        {
            if (definition.IsSiege && _store.Get(definition.Id)?.State == ChronicleEventState.Active)
                focuses.Add(new ArmyTargetFocus(definition.TargetSettlementId, definition.Attackers, definition.Defender, multiplier));
        }

        _focus.Replace(focuses);
    }

    /// <summary>The event's record when it is an Active siege window on this settlement; null otherwise.</summary>
    private ChronicleEventRecord? ActiveSiegeAt(ChronicleEventDefinition definition, string settlementId)
    {
        if (!definition.IsSiege || !string.Equals(definition.TargetSettlementId, settlementId, StringComparison.Ordinal))
            return null;
        var record = _store.Get(definition.Id);
        return record?.State == ChronicleEventState.Active ? record : null;
    }

    /// <summary>Every settlement and kingdom id the event names exists in this campaign; the first missing one otherwise.</summary>
    private bool KnowsEveryId(ChronicleEventDefinition definition, out string unknown)
    {
        unknown = string.Empty;
        if (definition.IsSiege)
        {
            if (!_world.SettlementExists(definition.TargetSettlementId!))
            {
                unknown = definition.TargetSettlementId!;
                return false;
            }

            foreach (var kingdom in definition.Attackers.Concat(new[] { definition.Defender! }))
            {
                if (!_world.KingdomExists(kingdom))
                {
                    unknown = kingdom;
                    return false;
                }
            }
        }

        foreach (var outcome in new[] { definition.Occurred, definition.Held, definition.Fell })
        {
            if (outcome == null)
                continue;
            foreach (var effect in outcome.Effects)
            {
                if (IsKeyword(effect.Target) || _world.KingdomExists(effect.Target))
                    continue;
                unknown = effect.Target;
                return false;
            }
        }

        return true;
    }

    private static bool IsKeyword(string target) =>
        target == ChronicleConfigValidator.TargetAttacker || target == ChronicleConfigValidator.TargetDefender
        || target == ChronicleConfigValidator.TargetFree || target == ChronicleConfigValidator.TargetEvil;

    private static bool IsOpen(ChronicleEventState state) =>
        state == ChronicleEventState.Pending || state == ChronicleEventState.Active;

    private static string? TextFor(ChronicleEventDefinition definition, ChronicleEventState outcome) =>
        outcome == ChronicleEventState.Lapsed ? definition.LapsedText : definition.OutcomeFor(outcome)?.Text;

    private static string Day(double? day) =>
        day.HasValue ? day.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : "?";
}
