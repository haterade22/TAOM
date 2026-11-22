using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.TournamentRewards;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>
/// The campaign side of the wave fight at a town's training master (docs/features/training-waves.md): whether the offer shows, the
/// stake and every move of gold, the run's state, and the master's debrief. It owns the run, the day ledger and the two flags of the
/// current mission; being a container singleton it lives for the process, so <see cref="ResetForNewSession"/> runs from the constructor
/// of the dialog behavior that the module builds for every campaign (csharp-architecture.md "Singleton Services Holding Per-Campaign
/// State"). The mission side (slice 3) drives the fight and calls the outcome methods. Single-player only; every gate is read live on
/// each call and re-checked when a stake is placed.
/// </summary>
internal sealed class WaveBetService
{
    private readonly ITrainingWavesConfigProvider _config;
    private readonly ITrainingWavesSettingsProvider _settings;
    private readonly ITournamentRewardsSettingsProvider _tournament;
    private readonly ICareerPurseAdapter _purse;
    private readonly ITrainingWavesAdapter _adapter;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private int? _goldAtRunStart;

    public WaveBetService(
        ITrainingWavesConfigProvider config,
        ITrainingWavesSettingsProvider settings,
        ITournamentRewardsSettingsProvider tournament,
        ICareerPurseAdapter purse,
        ITrainingWavesAdapter adapter,
        ICoopSessionProvider coop,
        IDedicatedServerProvider dedicated,
        IOutcomeLog outcomes,
        IModLogger logger)
    {
        _config = config;
        _settings = settings;
        _tournament = tournament;
        _purse = purse;
        _adapter = adapter;
        _coop = coop;
        _dedicated = dedicated;
        _outcomes = outcomes;
        _logger = logger;
    }

    /// <summary>The day each town last saw a run start; saved by the dialog behavior through <see cref="ExportDays"/>.</summary>
    public WaveDayLedger Ledger { get; } = new();

    /// <summary>The run in this mission; null until an offer is taken, and a fresh one replaces an ended run.</summary>
    public WaveRun? Run { get; private set; }

    public WaveRunState? RunState => Run?.State;

    /// <summary>A training-yard mission with the wave logic is running (set by the mission side).</summary>
    public bool ArenaAttached { get; private set; }

    /// <summary>The player has come back from a wave and the master owes the debrief; cleared when it is taken and when any conversation ends.</summary>
    public bool DebriefPending { get; private set; }

    public void ResetForNewSession()
    {
        Run = null;
        ArenaAttached = false;
        DebriefPending = false;
        _goldAtRunStart = null;
        Ledger.Clear();
    }

    public void AttachArena()
    {
        ArenaAttached = true;
        DebriefPending = false;
        Run = null;
        _goldAtRunStart = null;
    }

    /// <summary>
    /// The mission is over. The mission side has settled the run by now; should it not have (a fault, a mission that ended another way),
    /// a run still alive is settled here: a due wave is refunded, a fight forfeited, a pause between waves ends as a withdrawal.
    /// Safe to call twice.
    /// </summary>
    public void DetachArena()
    {
        switch (Run?.State)
        {
            case WaveRunState.WaveDue:
                Refund();
                break;
            case WaveRunState.Fighting:
                Forfeit();
                break;
            case WaveRunState.BetweenWaves:
                Withdraw();
                break;
        }

        ArenaAttached = false;
        DebriefPending = false;
    }

    public Dictionary<string, int> ExportDays() => Ledger.Export(_adapter.Today);

    public void ImportDays(Dictionary<string, int>? days) => Ledger.Import(days);

    public OfferRefusal Refusal
    {
        get
        {
            if (!_settings.Enabled || StakeChoices.Count == 0)
                return OfferRefusal.Disabled;
            if (!CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated))
                return OfferRefusal.NotSinglePlayer;

            var townId = _adapter.TownId;
            if (!ArenaAttached || townId == null || !_adapter.TalkingToPracticeMaster)
                return OfferRefusal.NotArena;
            if (_adapter.VanillaAfterPractice)
                return OfferRefusal.Practising;
            if (_adapter.TownHasTournament)
                return OfferRefusal.Tournament;
            if (_adapter.PlayerWounded)
                return OfferRefusal.Wounded;
            if (_adapter.PlayerDisguised)
                return OfferRefusal.Disguised;

            var rules = _config.GetTable().Rules;
            if (Run?.State != WaveRunState.BetweenWaves && rules.OneRunPerTownPerDay && Ledger.RanToday(townId, _adapter.Today))
                return OfferRefusal.RanToday;
            if (_purse.Gold < StakeChoices[0])
                return OfferRefusal.Poor;
            return OfferRefusal.None;
        }
    }

    /// <summary>
    /// The offer line is visible: open, or refused for a reason the player can read (a tournament, a wound, a disguise, a run today, a
    /// short purse). Where it would mean nothing (switched off, co-op, not in the yard, vanilla's debrief due) it is hidden. Logs the
    /// <c>offer</c> check: a pass for an open offer, a skip with its reason otherwise.
    /// </summary>
    public bool ShowsOffer
    {
        get
        {
            var refusal = Refusal;
            if (refusal == OfferRefusal.None)
                _outcomes.Pass(TrainingWavesChecks.Offer, "", $"town={_adapter.TownId}");
            else
                _outcomes.Skip(TrainingWavesChecks.Offer, refusal.ToString(), $"reason={refusal}");
            return refusal != OfferRefusal.Disabled && refusal != OfferRefusal.NotSinglePlayer
                && refusal != OfferRefusal.NotArena && refusal != OfferRefusal.Practising;
        }
    }

    /// <summary>The stakes on offer for the wave in hand, smallest first (the small one, a half and the cap, in tens), empty when none is allowed.</summary>
    public IReadOnlyList<int> StakeChoices
    {
        get
        {
            var run = Run is { State: not WaveRunState.Ended } live ? live : NewRun();
            return StakeOptions.For(run.MaxStake, _config.GetTable().Rules.MinStake, run.SmallStake);
        }
    }

    /// <summary>The stake in a slot counted from the largest (0 the full cap); 0 when that slot has none.</summary>
    public int StakeChoice(int fromTop)
    {
        var choices = StakeChoices;
        var index = choices.Count - 1 - fromTop;
        return fromTop >= 0 && index >= 0 ? choices[index] : 0;
    }

    public bool CanAfford(int amount) => _purse.Gold >= amount;

    /// <summary>
    /// The smallest stake on offer for the wave in hand: the first of <see cref="StakeChoices"/>, which holds nothing under the table's
    /// minimum but is often higher (the run's small stake). The table's minimum when no stake is on offer.
    /// </summary>
    public int SmallestStake
    {
        get
        {
            var choices = StakeChoices;
            return choices.Count > 0 ? choices[0] : _config.GetTable().Rules.MinStake;
        }
    }

    /// <summary>The run to bet on: the one in hand, or a new Idle one when there is none or it has ended.</summary>
    public WaveRun PrepareRun()
    {
        if (Run == null || Run.State == WaveRunState.Ended)
        {
            Run = NewRun();
            _goldAtRunStart = null;
        }

        return Run;
    }

    /// <summary>
    /// Takes a stake for the next wave. Re-checks every gate of the offer, then the run's own (the cap, the minimum, the purse),
    /// charges the gold, and verifies it fell by exactly the stake (the <c>stake</c> check). The first stake of a run records the day
    /// for the town. False, with nothing changed, when any gate or the charge refuses.
    /// </summary>
    public bool PlaceStake(int amount)
    {
        var townId = _adapter.TownId;
        if (Refusal != OfferRefusal.None || townId == null)
            return false;

        var run = PrepareRun();
        var goldBefore = _purse.Gold;
        if (!run.CanStake(amount, goldBefore))
            return false;

        var firstStake = run.TotalStaked == 0;
        if (!_purse.ChargeGold(amount))
            return false;

        var goldAfter = _purse.Gold;
        run.TryStake(amount, goldBefore);
        if (firstStake)
        {
            _goldAtRunStart = goldBefore;
            Ledger.Record(townId, _adapter.Today);
        }

        var detail = $"stake={amount} gold_before={goldBefore} gold_after={goldAfter}";
        if (goldBefore - goldAfter == amount)
        {
            _outcomes.Pass(TrainingWavesChecks.Stake, "", detail);
        }
        else
        {
            _outcomes.Fail(TrainingWavesChecks.Stake, "", detail);
            _logger.LogError($"[TrainingWaves] A stake of {amount} moved the purse by {goldBefore - goldAfter}");
        }

        return true;
    }

    /// <summary>The player stops between waves and keeps the winnings. False in any other state.</summary>
    public bool Withdraw()
    {
        var run = Run;
        if (run == null || !run.Withdraw())
            return false;
        LogRunEnd(run);
        return true;
    }

    /// <summary>
    /// The wave was cleared: pays the payout and moves the run on (the last wave ends it). Logs the <c>payout</c> check. Returns the
    /// payout the run records: the gold that arrived when that is less than the payout (a purse at its limit), else the payout. 0 unless fighting.
    /// </summary>
    public int PayClearedWave()
    {
        var run = Run;
        if (run == null || run.State != WaveRunState.Fighting)
            return 0;

        var payout = run.Clear();
        var before = _purse.Gold;
        if (!_purse.GrantGold(payout))
            _logger.LogError($"[TrainingWaves] The purse refused a payout of {payout}");
        var rose = _purse.Gold - before;
        var detail = $"payout={payout} rose={rose}";
        if (rose == payout)
            _outcomes.Pass(TrainingWavesChecks.Payout, "", detail);
        else
            _outcomes.Fail(TrainingWavesChecks.Payout, "", detail);
        run.RecordPaid(rose);
        payout = run.LastPayout;

        if (run.State == WaveRunState.Ended)
            LogRunEnd(run);
        return payout;
    }

    /// <summary>The player went down: the stake stays with the town and the run ends. Returns the stake; 0 unless fighting.</summary>
    public int LoseWave()
    {
        var run = Run;
        if (run == null)
            return 0;

        var lost = run.Lose();
        if (lost > 0)
        {
            GiveToTown(lost);
            LogRunEnd(run);
        }

        return lost;
    }

    /// <summary>The fight was left unfinished: the stake goes to the town and the run ends. Logs the <c>forfeit</c> check. 0 unless fighting.</summary>
    public int Forfeit()
    {
        var run = Run;
        if (run == null)
            return 0;

        var lost = run.Forfeit();
        if (lost > 0)
        {
            if (GiveToTown(lost))
                _outcomes.Pass(TrainingWavesChecks.Forfeit, "", $"stake={lost}");
            else
                _outcomes.Fail(TrainingWavesChecks.Forfeit, "", $"stake={lost}");
            LogRunEnd(run);
        }

        return lost;
    }

    /// <summary>A wave that never started: the stake goes back to the purse and the run ends. Returns it; 0 unless a wave was due.</summary>
    public int Refund()
    {
        var run = Run;
        if (run == null)
            return 0;

        var back = run.Refund();
        if (back > 0)
        {
            if (!_purse.GrantGold(back))
                _logger.LogError($"[TrainingWaves] The purse refused a refund of {back}");
            GiveDayBackIfNoWaveWasCleared(run);
            LogRunEnd(run);
        }

        return back;
    }

    /// <summary>
    /// A fight that a fault of ours ended, not the player's doing: the stake goes back to the purse and the town gets nothing. Returns it;
    /// 0 unless fighting.
    /// </summary>
    public int AbortFight()
    {
        var run = Run;
        if (run == null)
            return 0;

        var back = run.Abort();
        if (back > 0)
        {
            if (!_purse.GrantGold(back))
                _logger.LogError($"[TrainingWaves] The purse refused a refund of {back}");
            GiveDayBackIfNoWaveWasCleared(run);
            LogRunEnd(run);
        }

        return back;
    }

    /// <summary>
    /// A run that ended in a refund before any wave was cleared was never a run: the town's day is free again, so a wave that
    /// could not start, or a fault in the first fight, does not cost the player today's try.
    /// </summary>
    private void GiveDayBackIfNoWaveWasCleared(WaveRun run)
    {
        if (run.Wave == 1 && _adapter.TownId is { } townId)
            Ledger.Forget(townId);
    }

    public void MarkDebrief() => DebriefPending = true;

    /// <summary>
    /// The master gives the debrief: clears the flag and logs the <c>debrief</c> check, a pass when vanilla's <c>AfterPractice</c> is
    /// false. Vanilla's own prize condition clears that flag as it pays (ArenaMasterCampaignBehavior.cs :312-314), so a pass shows only
    /// that the flag was not left set when our debrief ran. False when none was pending.
    /// </summary>
    public bool TakeDebrief()
    {
        if (!DebriefPending)
            return false;

        DebriefPending = false;
        var vanilla = _adapter.VanillaAfterPractice;
        if (vanilla)
            _outcomes.Fail(TrainingWavesChecks.Debrief, "", "after_practice=true");
        else
            _outcomes.Pass(TrainingWavesChecks.Debrief, "", "after_practice=false");
        return true;
    }

    /// <summary>Which greeting the master owes now: none unless a debrief is pending and the partner is the practice master.</summary>
    public DebriefKind PendingDebrief
    {
        get
        {
            var run = Run;
            if (!DebriefPending || run == null || !_adapter.TalkingToPracticeMaster)
                return DebriefKind.None;
            if (run.State == WaveRunState.BetweenWaves)
                return DebriefKind.Cleared;
            if (run.EndReason == WaveRunEnd.Lost)
                return DebriefKind.Lost;
            return run.EndReason == WaveRunEnd.Completed ? DebriefKind.Completed : DebriefKind.None;
        }
    }

    /// <summary>
    /// Any conversation ended: a run paused between waves with no stake placed is over (the player walked away), and the debrief flag
    /// clears whichever line won the <c>start</c> token (state-lifecycle-save.md, a flag on a <c>start</c> line).
    /// </summary>
    public void OnConversationEnded()
    {
        if (Run?.State == WaveRunState.BetweenWaves)
            Withdraw();
        DebriefPending = false;
    }

    private WaveRun NewRun() => new(_config.GetTable(), _tournament.MaxBetPerRound, _adapter.DeepPocketsFactor);

    private bool GiveToTown(int stake)
    {
        var given = _adapter.GiveToTown(stake);
        if (!given)
            _logger.LogError($"[TrainingWaves] The town could not take a lost stake of {stake}");
        return given;
    }

    /// <summary>The <c>run-end</c> check: the purse changed since the first stake by exactly the run's net. Skipped when no stake was placed.</summary>
    private void LogRunEnd(WaveRun run)
    {
        if (_goldAtRunStart is not { } start)
            return;

        var delta = _purse.Gold - start;
        var detail = $"reason={run.EndReason} net={run.Net} delta={delta}";
        if (delta == run.Net)
            _outcomes.Pass(TrainingWavesChecks.RunEnd, "", detail);
        else
            _outcomes.Fail(TrainingWavesChecks.RunEnd, "", detail);
    }
}
