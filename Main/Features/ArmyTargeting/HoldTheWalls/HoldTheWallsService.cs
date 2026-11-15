using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using Inv = System.Globalization.CultureInfo;

namespace TAOM.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// Hold the Walls (#871, docs/features/hold-the-walls.md). After a siege win the engine pauses each capturing army leader's AI
/// for 10 hours (<c>KingdomManager.SiegeCompleted</c>). A good army that took a fortress stays longer: this service
/// lengthens that same pause with the same engine call (<see cref="IFortHoldAdapter.HoldForHours"/>), for 12 to 30 hours
/// from the capture (22 for an average lord) by the lord's Valor and Calculating. The engine saves and expires the pause
/// itself, so nothing is saved here and an army cannot be left stuck; the hold only ends early, by restoring what is left of
/// the engine's own 10 hours. The hold counts toward the army's 48 hour inactivity disband and releasing it does not reset
/// that counter, which is why the ceiling is 30 hours; the service counts the AI armies the engine still disbands for
/// inactivity (<see cref="OnInactivityDisband"/>) so a run can show whether holds cost any.
/// Nothing is minted. All engine work goes through <see cref="IFortHoldAdapter"/>; this class holds no engine type. Single-player
/// only: a co-op session, a co-op mod TAOM cannot probe or a dedicated server ends every hold and records nothing.
/// </summary>
/// <remarks>
/// Singleton state is the unjudged captures, the running holds and the day's counters, all per campaign: the behavior's
/// constructor calls <see cref="ResetForNewSession"/>. Main thread only.
/// </remarks>
public sealed class HoldTheWallsService : IHoldTheWallsService
{
    private const string Tag = "[HoldTheWalls]";

    // The release reasons the daily line lists, in order. Gone is reported as dispersedWhileHeld.
    private static readonly HoldEnd[] Listed =
    {
        HoldEnd.Siege, HoldEnd.Cohesion, HoldEnd.Food, HoldEnd.Off, HoldEnd.Coop, HoldEnd.Player, HoldEnd.Expired, HoldEnd.Moved,
    };

    private sealed class Capture
    {
        public Capture(string fortId, double hours)
        {
            FortId = fortId;
            Hours = hours;
        }

        public string FortId { get; }

        public double Hours { get; }
    }

    private sealed class Hold
    {
        public Hold(string fortId, string leaderId, double captureHours, double endHours)
        {
            FortId = fortId;
            LeaderId = leaderId;
            CaptureHours = captureHours;
            EndHours = endHours;
        }

        public string FortId { get; }

        public string LeaderId { get; }

        public double CaptureHours { get; }

        public double EndHours { get; }
    }

    private readonly IFortHoldAdapter _adapter;
    private readonly IArmyTargetingConfigProvider _configProvider;
    private readonly IArmyTargetingSettingsProvider _settings;
    private readonly IAlignmentService _alignment;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicatedServer;
    private readonly IModLogger _logger;

    private readonly List<Capture> _captures = new List<Capture>();
    private readonly List<Hold> _holds = new List<Hold>();
    private readonly int[] _ended = new int[Enum.GetValues(typeof(HoldEnd)).Length];
    private readonly List<string> _cohesionAtCapture = new List<string>();
    private int _inactivityDisbands;
    private double _maxHourMs;

    public HoldTheWallsService(
        IFortHoldAdapter adapter,
        IArmyTargetingConfigProvider configProvider,
        IArmyTargetingSettingsProvider settings,
        IAlignmentService alignment,
        ICoopSessionProvider coop,
        IDedicatedServerProvider dedicatedServer,
        IModLogger logger)
    {
        _adapter = adapter;
        _configProvider = configProvider;
        _settings = settings;
        _alignment = alignment;
        _coop = coop;
        _dedicatedServer = dedicatedServer;
        _logger = logger;
    }

    /// <summary>The MCM hold switch and the master switch of its group, AI Strategic Intelligence. Read live, hourly at most.</summary>
    private bool Enabled => _settings.EnableArmyStrategicIntelligence && _settings.HoldCapturedForts;

    /// <summary>Read live on every use, never cached (ICoopSessionProvider's contract).</summary>
    private bool IsSinglePlayer =>
        !_coop.IsSessionActive && !_coop.ShouldDeferToHost && !_dedicatedServer.IsDedicatedServer;

    public void ResetForNewSession()
    {
        _captures.Clear();
        _holds.Clear();
        Array.Clear(_ended, 0, _ended.Length);
        _cohesionAtCapture.Clear();
        _inactivityDisbands = 0;
        _maxHourMs = 0;
    }

    public void OnSettlementOwnerChanged(string? settlementId, bool isFortification, bool bySiege)
    {
        if (string.IsNullOrEmpty(settlementId) || !isFortification || !bySiege)
            return;
        if (!IsSinglePlayer || !Enabled || !TryNow(out double now))
            return;

        // A second siege capture replaces the unjudged record. What was held there is not touched: the next pass releases
        // a hold whose army no longer belongs to the fortress's realm.
        _captures.RemoveAll(c => c.FortId == settlementId);
        _captures.Add(new Capture(settlementId!, now));
    }

    public void OnInactivityDisband(bool isPlayersArmy)
    {
        if (!isPlayersArmy && IsSinglePlayer)
            _inactivityDisbands++;
    }

    public void OnHourlyTick()
    {
        if (_captures.Count == 0 && _holds.Count == 0)
            return;

        long started = Stopwatch.GetTimestamp();
        try
        {
            Pass();
        }
        finally
        {
            double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            if (ms > _maxHourMs)
                _maxHourMs = ms;
        }
    }

    private void Pass()
    {
        if (!IsSinglePlayer)
        {
            EndAll(HoldEnd.Coop);
            return;
        }
        if (!Enabled)
        {
            EndAll(HoldEnd.Off);
            return;
        }
        if (!TryNow(out double now))
            return;

        var config = _configProvider.GetConfig().CapturedFortHold ?? new CapturedFortHoldConfig();
        foreach (var hold in _holds.ToList())
        {
            try { Step(hold, now, config); }
            catch (Exception ex)
            {
                // A failing read drops the record unwritten: the engine's timer still ends the pause.
                _holds.Remove(hold);
                _logger.LogError($"{Tag} hold of {hold.LeaderId} at {hold.FortId} failed: {ex}");
            }
        }

        foreach (var capture in _captures.ToList())
        {
            _captures.Remove(capture);
            try { Start(capture, now, config); }
            catch (Exception ex) { _logger.LogError($"{Tag} capture of {capture.FortId} failed: {ex}"); }
        }
    }

    /// <summary>One running hold: drop it when its time is up, release it when its army is gone or moved or a reason stands.</summary>
    private void Step(Hold hold, double now, CapturedFortHoldConfig config)
    {
        if (now >= hold.EndHours)
        {
            Drop(hold, HoldEnd.Expired);
            return;
        }

        var army = _adapter.ReadArmy(hold.LeaderId, hold.FortId);
        if (!army.Exists)
        {
            Release(hold, HoldEnd.Gone, now);
            return;
        }
        if (!army.InFort || !army.SameRealmAsFort)
        {
            Release(hold, HoldEnd.Moved, now);
            return;
        }
        if (army.IsPlayerLed)
        {
            Release(hold, HoldEnd.Player, now);
            return;
        }

        var reason = HoldTheWallsMath.ReleaseReason(army.Cohesion, army.LowestFoodDays, army.FortUnderSiege, config);
        if (reason != HoldEnd.None)
            Release(hold, reason, now);
    }

    /// <summary>The first hourly pass after a capture: every army inside that qualifies starts a hold.</summary>
    private void Start(Capture capture, double now, CapturedFortHoldConfig config)
    {
        foreach (var army in _adapter.ReadArmiesIn(capture.FortId))
        {
            if (!army.Exists || string.IsNullOrEmpty(army.LeaderPartyId) || !army.InFort || !army.SameRealmAsFort)
                continue;
            if (army.IsPlayerLed || army.InMapEvent)
                continue;
            // The engine paused the leaders that took the fortress; a same-realm army that walked in later was never paused.
            // An army that fled in just before the first pass is paused 5 h by EnterSettlementAction and may be held; accepted.
            if (!army.AiPaused)
                continue;
            if (!config.HoldsArmy(army.Side.FactionId, _alignment.ResolveSide(army.Side)))
                continue;
            if (!HoldTheWallsMath.MayStart(army.Cohesion, army.LowestFoodDays, army.FortUnderSiege, config))
                continue;

            double end = capture.Hours + HoldTheWallsMath.HoldHours(army.Valor, army.Calculating, config);
            int engineHours = HoldTheWallsMath.EngineHours(end - now);
            if (engineHours <= 0)
                continue;

            _adapter.HoldForHours(army.LeaderPartyId, engineHours);
            // An army holds one fortress at a time: a record for another one is replaced, the new pause having overwritten its pause.
            _holds.RemoveAll(h => h.LeaderId == army.LeaderPartyId);
            _holds.Add(new Hold(capture.FortId, army.LeaderPartyId, capture.Hours, end));
            string cohesion = army.Cohesion.ToString("F0", Inv.InvariantCulture);
            _cohesionAtCapture.Add(cohesion);
            _logger.LogInfo($"{Tag} {army.LeaderPartyId} holds {capture.FortId} for {engineHours} more hours (cohesion {cohesion})");
        }
    }

    /// <summary>Ends every hold and forgets every capture: the switch is off or the gate closed.</summary>
    private void EndAll(HoldEnd reason)
    {
        bool hasNow = TryNow(out double now);
        foreach (var hold in _holds.ToList())
        {
            try { Release(hold, reason, hasNow ? now : double.NaN); }
            catch (Exception ex) { _logger.LogError($"{Tag} release of {hold.LeaderId} at {hold.FortId} failed: {ex}"); }
        }
        _holds.Clear();
        _captures.Clear();
    }

    /// <summary>
    /// Ends a hold early by restoring what is left of the engine's own 10 hour pause: the AI resumes at once when those hours
    /// are over (or the clock is unreadable), else it is paused for the rest of them. Whole hours round up, so the pause can
    /// outlast the engine's own by less than an hour.
    /// </summary>
    private void Release(Hold hold, HoldEnd reason, double now)
    {
        // The record goes first: a failing write must not leave a hold that is released again next hour.
        _holds.Remove(hold);
        int hours = HoldTheWallsMath.EngineHours(HoldTheWallsMath.VanillaRemainingHours(hold.CaptureHours, now));
        if (hours > 0)
            _adapter.HoldForHours(hold.LeaderId, hours);
        else
            _adapter.ResumeAi(hold.LeaderId);

        _ended[(int)reason]++;
        _logger.LogInfo($"{Tag} {hold.LeaderId} released at {hold.FortId}: {reason.ToString().ToLowerInvariant()}");
    }

    /// <summary>Ends a record with no engine write: the engine's own timer ends the pause.</summary>
    private void Drop(Hold hold, HoldEnd reason)
    {
        _holds.Remove(hold);
        _ended[(int)reason]++;
    }

    private bool TryNow(out double now)
    {
        now = _adapter.CampaignHours;
        return FiniteFloatValidator.IsFinite(now);
    }

    public void LogDailySummary()
    {
        double hours = _adapter.CampaignHours;
        long day = FiniteFloatValidator.IsFinite(hours) ? (long)Math.Floor(hours / 24.0) : -1;
        bool anyEnded = Listed.Any(r => _ended[(int)r] > 0);
        string released = anyEnded
            ? string.Join("/", Listed.Select(r => r.ToString().ToLowerInvariant() + ":" + _ended[(int)r].ToString(Inv.InvariantCulture)))
            : "none";
        string cohesion = _cohesionAtCapture.Count == 0 ? "none" : string.Join("/", _cohesionAtCapture);

        _logger.LogDebug(string.Format(Inv.InvariantCulture,
            "{0} day {1}: holds={2}, released={3}, cohesionAtCapture={4}, dispersedWhileHeld={5}, inactivityDisbands={6}, maxHourMs={7:F3}",
            Tag, day, _holds.Count, released, cohesion, _ended[(int)HoldEnd.Gone], _inactivityDisbands, _maxHourMs));

        Array.Clear(_ended, 0, _ended.Length);
        _inactivityDisbands = 0;
        _cohesionAtCapture.Clear();
        _maxHourMs = 0;
    }
}
