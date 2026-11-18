using System;
using System.Collections.Generic;
using Inv = System.Globalization.CultureInfo;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.AiPartySize;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;

namespace TAOM.Features.EyeOfSauron;

/// <summary>
/// The Eye of Sauron hunt (#765 M8, docs/features/eye-of-sauron.md). Battles the player wins against an enemy kingdom
/// build a decaying deed score; past the threshold, and only while the player's kingdom is at war with the hunter's,
/// a hunter rides: one of the Nine for a Free player, Glorfindel for an Evil one. A hunter who leads a party has that
/// party re-tasked and topped up; a free hunter gets a new party out of the player's sight. Out of sight the hunter
/// closes in steps each hour; within 1.5 of the player's seeing ranges the speed model lifts him above the player's
/// speed. While the player rides in another lord's army no hunt starts and a running one stands off. Every end releases the AI
/// pin the hunt set. All engine work goes through <see cref="IHunterPartyAdapter"/>; this class holds no engine type.
/// Single-player only: a co-op session (BannerlordCoop, host included), a co-op mod TAOM cannot probe (Bannerlord
/// Together) or a dedicated server stops and ends the hunt.
/// </summary>
public sealed class EyeOfSauronService : IEyeOfSauronHook
{
    /// <summary>No party at war with the hunter may stand this close to a landing point (vanilla's own scan uses 0.5).</summary>
    internal const float EnemyClearance = 3f;

    /// <summary>The landing point keeps this many of the player's seeing ranges away from him.</summary>
    internal const float SightMargin = 1.5f;

    /// <summary>A further gap, in map units, kept between the step's end and the sight margin.</summary>
    internal const float StepSlack = 3f;

    /// <summary>A spawned hunter appears in this ring around the player, in seeing ranges (beyond the sight margin).</summary>
    internal const float SpawnRingInner = 1.6f;

    internal const float SpawnRingOuter = 2.5f;

    /// <summary>A change in the player's speed beyond this fraction refreshes the hunter's cached speed.</summary>
    internal const float SpeedRefreshFraction = 0.05f;

    private const float MinFactorScale = 0.01f;

    private readonly IHunterPartyAdapter _adapter;
    private readonly IAlignmentService _alignment;
    private readonly IEyeOfSauronSettings _settings;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IDedicatedServerProvider _dedicatedServer;
    private readonly IModLogger _logger;

    private float _scoreVsEvil;
    private float _scoreVsFree;
    private float _lastDecayDay;
    private float _cooldownUntilDay;
    private string? _hunterHeroId;
    private string? _hunterPartyId;
    private float _huntStartDay;
    private string? _lastStep;

    // The hunt holds the hunter's AI pin. False while he stands off for another lord's army (E17); every release goes
    // through it, so a stand-off release and an end never release twice. Main thread only.
    private bool _pinnedByUs;

    // Published from the main thread, read by the speed and size models on the parallel party tick.
    private HuntSnapshot? _snapshot;
    private int _sizeRejectionLogged;

    public EyeOfSauronService(IHunterPartyAdapter adapter, IAlignmentService alignment, IEyeOfSauronSettings settings,
        ICoopSessionProvider coopSession, IDedicatedServerProvider dedicatedServer, IModLogger logger)
    {
        _adapter = adapter;
        _alignment = alignment;
        _settings = settings;
        _coopSession = coopSession;
        _dedicatedServer = dedicatedServer;
        _logger = logger;
    }

    public string? HunterHeroId => _hunterHeroId;

    public string? HunterPartyId => _hunterPartyId;

    public bool IsHunting => _hunterPartyId != null;

    /// <summary>
    /// Read live on every use, never cached (ICoopSessionProvider's contract). Stands down while a co-op session is live
    /// (host included), while a co-op mod TAOM cannot probe is loaded (Bannerlord Together cannot tell host from client),
    /// and on a dedicated server.
    /// </summary>
    private bool IsSinglePlayer =>
        !_coopSession.IsSessionActive && !_coopSession.ShouldDeferToHost && !_dedicatedServer.IsDedicatedServer;

    private float Now => (float)_adapter.NowDays;

    /// <summary>
    /// Clears every per-campaign field. Called from the behavior's constructor, which the feature module runs for every
    /// campaign before any event or SyncData, so campaign B never inherits campaign A's hunt (csharp-architecture.md).
    /// Releases nothing: the previous campaign's parties are gone.
    /// </summary>
    public void ResetForNewSession()
    {
        _scoreVsEvil = 0f;
        _scoreVsFree = 0f;
        _lastDecayDay = 0f;
        _cooldownUntilDay = 0f;
        ClearHunt();
    }

    public EyeOfSauronState CaptureState() => new()
    {
        ScoreVsEvil = _scoreVsEvil,
        ScoreVsFree = _scoreVsFree,
        LastDecayDay = _lastDecayDay,
        CooldownUntilDay = _cooldownUntilDay,
        HunterHeroId = _hunterHeroId ?? string.Empty,
        HunterPartyId = _hunterPartyId ?? string.Empty,
        HuntStartDay = _huntStartDay,
    };

    /// <summary>
    /// Restores a saved record. The in-sight state is dropped; the hunter's party stays registered for its size. A saved
    /// hunt is taken to hold its pin until <see cref="OnSessionLaunched"/> reads the live one (the engine saves
    /// <c>DoNotMakeNewDecisions</c>): a save taken while the hunter stood off holds none.
    /// </summary>
    public void LoadFrom(EyeOfSauronState state)
    {
        ResetForNewSession();
        if (state == null) return;

        _scoreVsEvil = FiniteOrZero(state.ScoreVsEvil);
        _scoreVsFree = FiniteOrZero(state.ScoreVsFree);
        _lastDecayDay = FiniteOrZero(state.LastDecayDay);
        _cooldownUntilDay = FiniteOrZero(state.CooldownUntilDay);
        if (string.IsNullOrEmpty(state.HunterHeroId) || string.IsNullOrEmpty(state.HunterPartyId))
            return;

        _hunterHeroId = state.HunterHeroId;
        _hunterPartyId = state.HunterPartyId;
        _huntStartDay = FiniteOrZero(state.HuntStartDay);
        _pinnedByUs = true;
        Publish(new HuntSnapshot(state.HunterPartyId, inSight: false, playerSpeed: 0f, multiplier: 0f));
    }

    /// <summary>
    /// After a load the live pin says first whether the hunt still holds it, so no end path releases a pin a hunt saved
    /// during a stand-off no longer owns, and a resume re-pins. Then a hunt restored into a co-op session, or whose
    /// party is gone, ends at once.
    /// </summary>
    public void OnSessionLaunched()
    {
        if (!IsHunting) return;
        var party = _adapter.ReadParty(_hunterPartyId!);
        _pinnedByUs = party.Exists && party.AiPinned;
        if (!IsSinglePlayer)
            EndHunt(HuntEndReason.SessionLive);
        else if (!party.Exists)
            EndHunt(HuntEndReason.PartyLost);
    }

    public float GetScore(FactionSide against) =>
        HuntMath.Decay(against == FactionSide.Evil ? _scoreVsEvil : against == FactionSide.Free ? _scoreVsFree : 0f,
            Now - _lastDecayDay, _settings.HalfLifeDays);

    /// <summary>Console: adds to the score earned against one side. Never below zero; a non-finite amount is ignored.</summary>
    public void AddScore(FactionSide against, float amount)
    {
        if (!FiniteFloatValidator.IsFinite(amount) || against == FactionSide.Neutral) return;
        ApplyDecay();
        if (against == FactionSide.Evil)
            _scoreVsEvil = Math.Max(0f, _scoreVsEvil + amount);
        else
            _scoreVsFree = Math.Max(0f, _scoreVsFree + amount);
    }

    public void OnMapEventEnded(HuntBattleSummary summary)
    {
        if (summary == null) return;

        if (IsHunting && EndsTheHunt(summary))
            EndHunt(summary.IsPlayerEvent && summary.PlayerWon ? HuntEndReason.HunterBeaten : HuntEndReason.BattleEnded);

        // A co-op peer never writes the save-backed score: the hunt is single-player only.
        if (!IsSinglePlayer) return;
        if (!summary.IsPlayerEvent || !summary.PlayerWon || string.IsNullOrEmpty(summary.LoserKingdomId)) return;

        // The loser by its kingdom id alone, never a culture fallback (AreEnemyAlignments would count Neutral as
        // everyone's enemy). The player's own kingdom is sided with ResolveSide instead, so a founded one counts.
        var side = _alignment.GetKingdomSide(summary.LoserKingdomId!);
        if (side == FactionSide.Neutral) return;
        AddScore(side, HuntMath.DeedWeight(summary.Kind));
    }

    /// <summary>
    /// E06 (the maintainer, 2026-10-14): a battle ends the hunt only when the hunter's side was defeated, or when it was
    /// the player's battle against the hunter and someone won it. A player who got away (no winner) keeps the hunt, and
    /// the hourly re-issue restores the engage order; a battle without the player that the hunter won keeps it too.
    /// </summary>
    private static bool EndsTheHunt(HuntBattleSummary s) =>
        s.InvolvesHunter && (s.HunterLost || (s.IsPlayerEvent && s.HasWinner));

    private void ApplyDecay()
    {
        float now = Now;
        float halfLife = _settings.HalfLifeDays;
        _scoreVsEvil = HuntMath.Decay(_scoreVsEvil, now - _lastDecayDay, halfLife);
        _scoreVsFree = HuntMath.Decay(_scoreVsFree, now - _lastDecayDay, halfLife);
        if (FiniteFloatValidator.IsFinite(now))
            _lastDecayDay = now;
    }

    /// <summary>
    /// The player kingdom's side: its id, else its own culture (ResolveSide), so a player-founded <c>new_kingdom</c>
    /// that alignment.json does not list is still Free or Evil. The start gate and the console share it.
    /// </summary>
    private FactionSide PlayerKingdomSide(string kingdomId) =>
        _alignment.ResolveSide(kingdomId, _adapter.PlayerKingdomCultureId ?? string.Empty);

    public void OnDailyTick()
    {
        if (IsHunting)
        {
            CheckActiveHunt();
            return;
        }

        if (!_settings.Enabled || !IsSinglePlayer) return;
        ApplyDecay();
        TryStartHunt(ignoreScoreAndCooldown: false);
    }

    /// <summary>
    /// Starts a hunt when every gate holds and returns a one-line verdict. The console passes
    /// <paramref name="ignoreScoreAndCooldown"/>; the switch, the single-player gate, the kingdom gate, the war gate
    /// and the E17 army deferral hold either way. The hunters are tried in order: one who cannot ride today is
    /// passed over for the next, and the hunt waits only when none can. A <paramref name="dryRun"/> returns the
    /// plan and changes nothing.
    /// </summary>
    public string TryStartHunt(bool ignoreScoreAndCooldown, bool dryRun = false)
    {
        if (IsHunting) return $"A hunt is already running ({_hunterHeroId}, party {_hunterPartyId}).";
        if (!_settings.Enabled) return "Refused: the hunt is switched off for this campaign or in MCM.";
        if (!IsSinglePlayer)
            return "Refused: single-player only, and a co-op session, a co-op mod TAOM cannot probe, or a dedicated server is live.";

        string? kingdomId = _adapter.PlayerKingdomId;
        if (string.IsNullOrEmpty(kingdomId)) return "No hunt: you belong to no kingdom.";
        var side = PlayerKingdomSide(kingdomId!);
        if (side == FactionSide.Neutral) return $"No hunt: your kingdom '{kingdomId}' is on neither side.";
        // E17: deferred, not refused: no cooldown starts, and the daily tick tries again.
        if (_adapter.PlayerInAnotherLordsArmy) return "No hunt: you ride in another lord's army.";

        if (!ignoreScoreAndCooldown)
        {
            float now = Now;
            if (now < _cooldownUntilDay) return $"No hunt: cooldown for {_cooldownUntilDay - now:0.0} more days.";
            float score = GetScore(side == FactionSide.Free ? FactionSide.Evil : FactionSide.Free);
            float threshold = _settings.Threshold;
            if (!(score >= threshold)) return $"No hunt: score {score:0.0} is under the threshold {threshold:0.0}.";
        }

        string? firstWait = null;
        var candidates = side == FactionSide.Free ? HuntTroopLists.WraithOrder : HuntTroopLists.ElvenOrder;
        foreach (string heroId in candidates)
        {
            var hero = _adapter.ReadHero(heroId);
            if (!CanHunt(hero) || !hero.AtWarWithPlayerKingdom) continue;
            string? wait = WaitReason(heroId, hero);
            if (wait != null)
            {
                firstWait ??= wait;
                continue;
            }
            // A refused spawn stops the search: the spawn ring depends only on the player, so the next hunter's would fail too.
            return dryRun ? DescribePlan(heroId, hero) : StartWith(heroId, hero, side);
        }

        if (firstWait == null) return "No hunt: no hunter is free and at war with your kingdom.";
        if (!dryRun) _logger.LogInfo("[EyeOfSauron] " + firstWait);
        return firstWait;
    }

    private static bool CanHunt(HunterHeroSnapshot hero) =>
        hero.Exists && hero.IsAlive && !hero.IsFugitive && !hero.IsPrisoner && !hero.IsHeldByAParty && !hero.IsMarkedForDeath;

    /// <summary>Why a fit hunter cannot ride today, or null when he can (re-task his party, or spawn one).</summary>
    private string? WaitReason(string heroId, HunterHeroSnapshot hero)
    {
        if (hero.LedPartyId != null)
        {
            string? busy = BusyReason(_adapter.ReadParty(hero.LedPartyId));
            return busy == null ? null : $"Waiting: {heroId}'s party {hero.LedPartyId} is {busy}; never a second party.";
        }
        if (hero.IsInAParty) return $"Waiting: {heroId} rides in another lord's party; never a second party.";
        if (!hero.IsActive || !hero.CanLeadParty || !hero.IsLord || hero.SettlementBlocksSpawn)
            return $"Waiting: {heroId} cannot lead a party today.";
        return null;
    }

    /// <summary>Why a party may not be re-tasked today, or null when it may (design 1.3).</summary>
    private static string? BusyReason(HuntPartySnapshot party)
    {
        if (!party.Exists || !party.IsActive) return "not on the map";
        if (party.InMapEvent) return "in a battle";
        if (party.InArmy) return "in an army";
        if (party.Besieging) return "besieging";
        if (party.InSettlement) return "in a settlement";
        if (party.IsDisbanding) return "disbanding";
        if (party.AiPinned) return "held by another order";
        return null;
    }

    private string StartWith(string heroId, HunterHeroSnapshot hero, FactionSide playerSide)
    {
        string? partyId = hero.LedPartyId ?? _adapter.SpawnHunterParty(heroId, SpawnRingInner, SpawnRingOuter, SightMargin);
        if (string.IsNullOrEmpty(partyId))
        {
            _logger.LogWarning($"[EyeOfSauron] No party could be spawned for {heroId} out of the player's sight; no hunt today.");
            return $"Refused: no party could be spawned for {heroId} out of your sight.";
        }

        // Register the host BEFORE its roster grows: PartySizeLimit is cached per roster version.
        _hunterHeroId = heroId;
        _hunterPartyId = partyId;
        _huntStartDay = Now;
        Publish(new HuntSnapshot(partyId!, inSight: false, playerSpeed: 0f, multiplier: 0f));
        // A top-up that adds nobody leaves the roster version, and so the cached size limit, unchanged.
        _adapter.InvalidatePartySize(partyId!);

        // Top up BEFORE the engage: CheckPartyNeedsUpdate can start the encounter at once (MobilePartyAi.cs:468-475).
        TopUpHost(partyId!, heroId, hero.FactionId);
        TopUpFood(partyId!);

        if (!_adapter.PinAndEngagePlayer(partyId!))
        {
            _logger.LogWarning($"[EyeOfSauron] {heroId}'s party {partyId} could not be ordered after the player; no hunt.");
            ClearHunt();
            _adapter.InvalidatePartySize(partyId!);
            return $"Refused: {heroId}'s party could not be ordered after you.";
        }
        _pinnedByUs = true;

        _logger.LogInfo($"[EyeOfSauron] Hunt started: {heroId} ({hero.FactionId}) with party {partyId}.");
        _adapter.Announce(playerSide == FactionSide.Free ? HuntAnnouncement.StartFree : HuntAnnouncement.StartEvil, heroId);
        return $"Hunt started: {heroId} rides with party {partyId}.";
    }

    /// <summary>The console's dry run: who rides, re-task or spawn, the top-up, and what outlasts the hunt. Read-only.</summary>
    private string DescribePlan(string heroId, HunterHeroSnapshot hero)
    {
        var lines = new List<string>();
        string who = $"{_adapter.HeroName(heroId)} ({heroId})";
        if (hero.LedPartyId != null)
        {
            lines.Add($"Dry run: {who} would ride; his party {hero.LedPartyId} is re-tasked and ordered after you.");
            var plan = PlanHost(hero.LedPartyId, heroId, hero.FactionId, out float deficit, out int room);
            lines.Add(plan.Count == 0
                ? "Top-up: none needed."
                : string.Format(Inv.InvariantCulture, "Top-up: {0} (deficit {1:0.0}, room {2}).",
                    string.Join(", ", plan.Select(p => $"{p.TroopId} x{p.Count}")), deficit, room));
        }
        else
        {
            lines.Add($"Dry run: {who} would ride; a new party is spawned out of your sight and ordered after you.");
            if (hero.GovernorOfSettlementId != null)
                lines.Add($"He gives up the governorship of {hero.GovernorOfSettlementId}.");
            lines.Add(string.Format(Inv.InvariantCulture,
                "Top-up: to {0:0.00} times your strength ({1:0.0}), at most {2} men, after the new party's roster is rolled.",
                _settings.HunterStrengthRatio, _adapter.PlayerStrength, _settings.HunterMaxTroops));
        }
        lines.Add("Grain: three days of food for the host, topped up daily.");
        lines.Add("The party, its troops and grain, and a lost governorship all remain after taom.clear_eye_hunt.");
        lines.Add("Run 'taom.start_eye_hunt confirm' to start it.");
        return string.Join("\n", lines);
    }

    private IReadOnlyList<(string TroopId, int Count)> PlanHost(string partyId, string heroId, string? factionId,
        out float deficit, out int room)
    {
        var party = _adapter.ReadParty(partyId);
        float playerStrength = _adapter.PlayerStrength;
        // A non-finite strength on either side makes a non-finite deficit, which PlanTopUp refuses.
        deficit = _settings.HunterStrengthRatio * playerStrength - party.Strength;
        room = _settings.HunterMaxTroops - party.HeadCount;
        if (!(playerStrength > 0f)) return Array.Empty<(string, int)>();
        return HuntMath.PlanTopUp(HuntTroopLists.HostFor(heroId, factionId), deficit, id => _adapter.GetTroopPower(id, heroId), room);
    }

    /// <summary>
    /// Fills the host to the strength ratio. Deficit-driven and capped, so it also replaces the men vanilla's wage-limit
    /// desertion takes from the roster's end (DefaultPartyDesertionModel) and follows a player who grows stronger.
    /// </summary>
    private void TopUpHost(string partyId, string heroId, string? factionId)
    {
        var plan = PlanHost(partyId, heroId, factionId, out float deficit, out int room);
        int added = 0;
        foreach (var (troopId, count) in plan)
            added += _adapter.AddTroops(partyId, troopId, count);
        if (added > 0)
            _logger.LogInfo($"[EyeOfSauron] {heroId}'s host topped up by {added} men (deficit {deficit:0.0}, room {room}).");
    }

    private void TopUpFood(string partyId)
    {
        var party = _adapter.ReadParty(partyId);
        if (party.Exists)
            _adapter.TopUpFood(partyId, HuntMath.FoodTarget(party.HeadCount));
    }

    /// <summary>
    /// Every end gate, shared by the daily and the hourly tick, so a peace, a kingdom change or the switch takes effect
    /// within the hour rather than at the next day. Ends the hunt and returns false when one holds.
    /// </summary>
    private bool HuntStillValid(out HunterHeroSnapshot hero, out HuntPartySnapshot party)
    {
        hero = HunterHeroSnapshot.Missing;
        party = HuntPartySnapshot.Missing;
        string heroId = _hunterHeroId!;
        if (!_settings.Enabled) { EndHunt(HuntEndReason.Disabled); return false; }
        if (!IsSinglePlayer) { EndHunt(HuntEndReason.SessionLive); return false; }

        hero = _adapter.ReadHero(heroId);
        if (!CanHunt(hero)) { EndHunt(HuntEndReason.HunterDown); return false; }

        party = _adapter.ReadParty(_hunterPartyId!);
        if (!party.Exists || !string.Equals(party.LeaderHeroId, heroId, StringComparison.Ordinal))
        {
            EndHunt(HuntEndReason.PartyLost);
            return false;
        }

        if (string.IsNullOrEmpty(_adapter.PlayerKingdomId)) { EndHunt(HuntEndReason.Kingdomless); return false; }
        if (!hero.AtWarWithPlayerKingdom) { EndHunt(HuntEndReason.WarEnded); return false; }
        if (!(Now - _huntStartDay < _settings.HuntTimeoutDays)) { EndHunt(HuntEndReason.TimedOut); return false; }
        return true;
    }

    /// <summary>The running hunt's daily check: the end gates, then troops (never mid-battle) and grain.</summary>
    private void CheckActiveHunt()
    {
        if (!HuntStillValid(out var hero, out var party)) return;
        if (!party.InMapEvent)
            TopUpHost(_hunterPartyId!, _hunterHeroId!, hero.FactionId);
        TopUpFood(_hunterPartyId!);
    }

    /// <summary>The hourly tick of one party; only the hunter's party is acted on (design 1.4 and 1.7).</summary>
    public void OnHourlyTickParty(string? partyId)
    {
        if (!IsHunting || !string.Equals(partyId, _hunterPartyId, StringComparison.Ordinal)) return;
        if (!HuntStillValid(out _, out var party)) return;

        // E17: while the player rides in another lord's army the hunter stands off: his order and pin are released, with
        // no step. The hunt itself goes on (the end gates above still run, the timeout counts).
        if (_adapter.PlayerInAnotherLordsArmy)
        {
            StandOff(partyId!);
            _lastStep = "player in another lord's army";
            return;
        }

        if (!_pinnedByUs)
        {
            // Resuming after a stand-off: re-pin and re-engage once, whatever the free AI chose. A hunter his own AI
            // left busy (a battle, an army he leads or rides in, a siege, a town, another's pin) is not his to claim:
            // the engage order cannot undo it. The resume waits, unpinned and unstepped, and retries next hour.
            string? busy = BusyReason(party);
            if (busy != null)
            {
                _lastStep = "resume waits: hunter " + busy;
                return;
            }
            if (!_adapter.PinAndEngagePlayer(partyId!))
            {
                _lastStep = "resume waits: the order was refused";
                return;
            }
            _pinnedByUs = true;
        }
        // Re-issue only when lost: every engage call resets all of the party's movement state.
        else if (!party.InMapEvent && !party.IsEngagingPlayer)
        {
            _adapter.PinAndEngagePlayer(partyId!);
        }

        TryStep(partyId!);
    }

    /// <summary>
    /// E17: drops the hunter's engage order and pin through the end's release path, once, so his own AI resumes while
    /// the hunt state stays (no end, no cooldown). The hourly tick re-pins him when the player leaves the army.
    /// </summary>
    private void StandOff(string partyId)
    {
        if (!_pinnedByUs) return;
        _adapter.ReleasePin(partyId);
        _pinnedByUs = false;
    }

    private void TryStep(string partyId)
    {
        var state = _adapter.ReadStepState(partyId);
        _lastStep = StepBlock(state) ?? StepAlongPath(partyId, state) ?? "stepped";
    }

    /// <summary>
    /// The first engine state that forbids an out-of-sight step, or null. The hunter must be free on the map, unseen
    /// and engaging the player; the player must be on the open map on the same island, with no true sight.
    /// </summary>
    internal static string? StepBlock(HuntStepState s)
    {
        if (!s.Valid) return "a party is missing";
        if (!s.HunterActive) return "hunter inactive";
        if (s.HunterInMapEvent) return "hunter in battle";
        if (s.HunterBesieging) return "hunter in a siege";
        if (s.HunterInArmy) return "hunter in an army";
        if (s.HunterInSettlement) return "hunter in a settlement";
        if (s.HunterAtSea) return "hunter at sea";
        if (s.HunterVisible) return "hunter visible";
        if (!s.HunterEngagingPlayer) return "hunter not engaging the player";
        if (!s.PlayerActive) return "player inactive";
        if (s.PlayerInMapEventOrSiege) return "player in battle or siege";
        if (s.PlayerInSettlement) return "player in a settlement";
        if (s.PlayerAtSea) return "player at sea";
        if (s.PlayerInEncounter) return "player in an encounter";
        if (s.TrueSight) return "true sight";
        if (!s.SameIsland) return "on another island";
        return null;
    }

    /// <summary>
    /// Steps the hunter along his path to the player, landing at least <see cref="SightMargin"/> sights away. The
    /// straight-line gate runs before the path rebuild, which is wasted on a hunter already inside the margin. Returns
    /// why it did not step, or null when it relocated the hunter.
    /// </summary>
    private string? StepAlongPath(string partyId, HuntStepState s)
    {
        float sight = s.SeeingRange;
        if (!(sight > 0f) || !FiniteFloatValidator.IsFinite(sight)) return "no seeing range";
        float keepAway = SightMargin * sight;
        if (!(HuntMath.Distance(s.HunterX, s.HunterY, s.PlayerX, s.PlayerY) >= keepAway)) return "already within the sight margin";

        var path = _adapter.TryRebuildPath(partyId);
        if (path.Count == 0) return "no path to the player";

        float step = HuntMath.ComputeStep(HuntMath.PathLength(s.HunterX, s.HunterY, path.Xs, path.Ys), sight, _settings.HunterStepCap);
        if (!(step > 0f)) return "the path is too short to step";

        var (x, y) = HuntMath.WalkPath(s.HunterX, s.HunterY, path.Xs, path.Ys, step);
        if (!(HuntMath.Distance(x, y, s.PlayerX, s.PlayerY) >= keepAway)) return "the step would end inside the sight margin";

        var landing = _adapter.ResolveLanding(partyId, x, y, EnemyClearance);
        if (!landing.Ok) return "no safe landing";
        if (!(HuntMath.Distance(landing.X, landing.Y, s.PlayerX, s.PlayerY) >= keepAway)) return "the landing was nudged inside the sight margin";

        _adapter.Relocate(partyId, landing.X, landing.Y);
        return null;
    }

    public void OnPartyDestroyed(string? partyId)
    {
        if (IsHunting && string.Equals(partyId, _hunterPartyId, StringComparison.Ordinal))
            EndHunt(HuntEndReason.PartyLost);
    }

    /// <summary>
    /// Ends the hunt: releases the AI pin the hunt set, clears the published snapshot, then refreshes the party's size
    /// limit and speed so the old allowance and floor cannot linger, and starts the cooldown.
    /// </summary>
    public void EndHunt(HuntEndReason reason)
    {
        if (!IsHunting) return;
        string partyId = _hunterPartyId!;
        string heroId = _hunterHeroId!;

        // A stand-off already released the pin; never twice.
        if (_pinnedByUs)
            _adapter.ReleasePin(partyId);
        ClearHunt();
        _adapter.InvalidatePartySize(partyId);
        _adapter.InvalidateSpeed(partyId);

        float now = Now;
        if (FiniteFloatValidator.IsFinite(now))
            _cooldownUntilDay = now + Math.Max(0, _settings.CooldownDays);
        _logger.LogInfo($"[EyeOfSauron] Hunt ended ({reason}): {heroId}, party {partyId}.");
        if (reason != HuntEndReason.SessionLive)
            _adapter.Announce(reason == HuntEndReason.HunterBeaten ? HuntAnnouncement.Won : HuntAnnouncement.Over, heroId);
    }

    /// <summary>Clears the hunt, its published snapshot and its once-per-hunt latch (end, reset, load, refused pin).</summary>
    private void ClearHunt()
    {
        _hunterHeroId = null;
        _hunterPartyId = null;
        _huntStartDay = 0f;
        _lastStep = null;
        _pinnedByUs = false;
        Interlocked.Exchange(ref _sizeRejectionLogged, 0);
        Publish(null);
    }

    /// <summary>
    /// Every campaign frame: works out whether the hunter is within the floor's band (1.5 of the player's seeing ranges)
    /// and publishes it, with the player's speed and the multiplier, for the speed model on the worker threads. The
    /// snapshot's <c>InSight</c> means that band. When the in-band state flips or
    /// the player's speed moves past 5%, the hunter's cached speed is refreshed (the cache never notices distance).
    /// </summary>
    public void OnTick()
    {
        string? partyId = _hunterPartyId;
        if (partyId == null) return;

        // E17: in another lord's army the hunter stands off at once, and no floor applies until the hunt re-pins him.
        if (_adapter.PlayerInAnotherLordsArmy)
            StandOff(partyId);
        var reading = _pinnedByUs ? _adapter.ReadSight(partyId, SightMargin) : HuntSightReading.Missing;
        bool inSight = IsInFloorBand(reading);
        // A non-finite speed is published as it is: SpeedMoved and TryGetSpeedFloor refuse it.
        float speed = reading.PlayerSpeed;
        float multiplier = _settings.HunterSpeedMultiplier;

        var old = Volatile.Read(ref _snapshot);
        bool flipped = old == null || old.InSight != inSight;
        bool speedMoved = old == null || SpeedMoved(old.PlayerSpeed, speed);
        bool multiplierMoved = old == null || !old.Multiplier.Equals(multiplier);
        if (!flipped && !speedMoved && !multiplierMoved) return;

        Publish(new HuntSnapshot(partyId, inSight, speedMoved ? speed : old!.PlayerSpeed, multiplier));
        // Out of sight no floor applies, so only a flip, or a speed or multiplier change in sight, needs a refresh.
        if (flipped || (inSight && (speedMoved || multiplierMoved)))
            _adapter.InvalidateSpeed(partyId);
    }

    /// <summary>
    /// E09 (the maintainer, 2026-10-14): the floor's band reaches <see cref="SightMargin"/> sights, the margin the steps
    /// keep, so a moving player cannot be shadowed between S and 1.5 S. Every comparison is a positive requirement:
    /// NaN fails it.
    /// </summary>
    private static bool IsInFloorBand(HuntSightReading r)
    {
        if (!r.Valid || !r.PlayerActive || !r.HunterActive || r.HunterInMapEvent || r.PlayerInMapEvent || r.PlayerAtSea)
            return false;
        float sight = r.SeeingRange, d = r.Distance;
        return sight > 0f && FiniteFloatValidator.IsFinite(sight) && FiniteFloatValidator.IsFinite(d)
               && d <= SightMargin * sight;
    }

    private static bool SpeedMoved(float old, float now)
    {
        bool oldOk = FiniteFloatValidator.IsFinite(old), nowOk = FiniteFloatValidator.IsFinite(now);
        if (oldOk != nowOk) return true;
        if (!nowOk) return false;
        return Math.Abs(now - old) > SpeedRefreshFraction * Math.Max(Math.Abs(old), 1e-3f);
    }

    private void Publish(HuntSnapshot? snapshot) => Volatile.Write(ref _snapshot, snapshot);

    // IEyeOfSauronHook: any thread, snapshot reads only.

    public void ApplyHunterSpeedFloor(string? partyId, ref ExplainedNumber result)
    {
        if (TryGetSpeedFloor(partyId, result.ResultNumber, out float floor))
            result.LimitMin(floor);
    }

    /// <summary>
    /// The hunter's floor: the player's speed times the multiplier, applied only to the hunter's party within 1.5 S,
    /// only when it is a finite number above the current speed, and never under 1 (LimitMin replaces vanilla's floor).
    /// </summary>
    internal bool TryGetSpeedFloor(string? partyId, float current, out float floor)
    {
        floor = 0f;
        var s = Volatile.Read(ref _snapshot);
        if (s == null || partyId == null || !s.InSight || !string.Equals(s.PartyId, partyId, StringComparison.Ordinal))
            return false;
        if (!FiniteFloatValidator.IsFiniteInRange(s.Multiplier, EyeOfSauronSettingsProvider.MinSpeedMultiplier,
                EyeOfSauronSettingsProvider.MaxSpeedMultiplier))
            return false;

        float candidate = s.PlayerSpeed * s.Multiplier;
        if (!FiniteFloatValidator.IsFinite(candidate) || !(candidate > current)) return false;
        floor = Math.Max(candidate, 1f);
        return true;
    }

    public float GetHostSizeBonus(string? partyId, float currentLimit, float sumOfFactors, int rawCount, object? party,
        Func<object?, float> weightedCountOf)
    {
        var s = Volatile.Read(ref _snapshot);
        if (s == null || partyId == null || !string.Equals(s.PartyId, partyId, StringComparison.Ordinal)) return 0f;
        if (!FiniteFloatValidator.IsFinite(currentLimit)) return Rejected($"limit {currentLimit} is not finite");
        if (!(1f + sumOfFactors > MinFactorScale)) return Rejected($"factor scale {1f + sumOfFactors} has collapsed");

        float weighted = weightedCountOf == null ? float.NaN : weightedCountOf(party);
        float target = Math.Max(rawCount, FiniteFloatValidator.IsFinite(weighted) ? (float)Math.Ceiling(weighted) : 0f);
        if (!(target > currentLimit)) return 0f;

        // Half a slot of headroom: the engine truncates ResultNumber, and a float sum can land a hair under the target.
        float bonus = target - currentLimit + 0.5f;
        if (bonus > AiPartySizeService.MaxFlatBonus)
        {
            Rejected($"bonus {bonus:0} clamped to {AiPartySizeService.MaxFlatBonus:0}");
            return AiPartySizeService.MaxFlatBonus;
        }
        return bonus;
    }

    /// <summary>Logs a refused or clamped size bonus once per hunt (this can run on a worker thread).</summary>
    private float Rejected(string why)
    {
        if (Interlocked.Exchange(ref _sizeRejectionLogged, 1) == 0)
            _logger.LogWarning($"[EyeOfSauron] Host size bonus for {_hunterPartyId}: {why}; the daily shed may trim the host.");
        return 0f;
    }

    public IReadOnlyList<string> Describe()
    {
        var lines = new List<string>
        {
            $"Eye of Sauron: {(_settings.Enabled ? "on" : "off")}, "
            + $"{(IsSinglePlayer ? "single-player" : "co-op, unprobeable co-op mod or server: stood down")}.",
            string.Format(Inv.InvariantCulture,
                "Score against Evil {0:0.0}, against Free {1:0.0} (threshold {2:0.0}, half-life {3:0} days).",
                GetScore(FactionSide.Evil), GetScore(FactionSide.Free), _settings.Threshold, _settings.HalfLifeDays),
        };
        string? kingdom = _adapter.PlayerKingdomId;
        lines.Add(string.IsNullOrEmpty(kingdom)
            ? "Your kingdom: none (deeds still score; no hunt)."
            : $"Your kingdom: {kingdom} ({PlayerKingdomSide(kingdom!)}).");

        float now = Now;
        lines.Add(now < _cooldownUntilDay
            ? string.Format(Inv.InvariantCulture, "Cooldown: {0:0.0} days left.", _cooldownUntilDay - now)
            : "Cooldown: none.");

        if (!IsHunting)
        {
            lines.Add("No hunt is running.");
            return lines;
        }

        var snapshot = Volatile.Read(ref _snapshot);
        var party = _adapter.ReadParty(_hunterPartyId!);
        lines.Add($"Hunter: {_adapter.HeroName(_hunterHeroId!)} ({_hunterHeroId}), party {_hunterPartyId}, "
                  + $"pinned: {(party.AiPinned ? "yes" : "no")}, engaging you: {(party.IsEngagingPlayer ? "yes" : "no")}.");
        lines.Add(string.Format(Inv.InvariantCulture,
            "Day {0:0.0} of {1}; in sight: {2}; your speed {3:0.00} (0 while the hunter is out of sight), multiplier {4:0.00}.",
            now - _huntStartDay, _settings.HuntTimeoutDays, snapshot?.InSight == true ? "yes" : "no",
            snapshot?.PlayerSpeed ?? 0f, snapshot?.Multiplier ?? 0f));
        lines.Add($"Last hourly step: {_lastStep ?? "none yet"}.");
        return lines;
    }

    private static float FiniteOrZero(float value) => FiniteFloatValidator.IsFinite(value) ? value : 0f;

    /// <summary>Immutable, so a worker thread reads one consistent set of values.</summary>
    private sealed class HuntSnapshot
    {
        public HuntSnapshot(string partyId, bool inSight, float playerSpeed, float multiplier)
        {
            PartyId = partyId;
            InSight = inSight;
            PlayerSpeed = playerSpeed;
            Multiplier = multiplier;
        }

        public string PartyId { get; }

        public bool InSight { get; }

        public float PlayerSpeed { get; }

        public float Multiplier { get; }
    }
}
