using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Features.TrainingGrounds.Waves.Hooks;

/// <summary>
/// Thin dialog boundary for the wave fight (ADR-002, docs/features/training-waves.md): registers the lines
/// (<see cref="TrainingWavesDialogLines"/>) and hands every condition and consequence to <see cref="WaveBetService"/>. Each callback
/// runs behind the shared <see cref="ScreenFaultLog"/>. The module builds this with <c>new</c> for every campaign: its constructor resets
/// the service, which holds the current run and the day ledger (a container singleton is constructed once per process), and it saves the
/// ledger's days in <c>SyncData</c>. Two conditions write: <c>CanRules</c> calls <c>PrepareRun</c>, which creates the run when none is live, and <c>CanOffer</c>
/// writes the <c>offer</c> outcome line. No condition moves gold; the stake and every other gold move run in the consequences, which
/// re-check every gate. Any conversation ending clears the debrief flag and withdraws a run left between waves.
/// </summary>
internal sealed class TrainingWavesDialogBehavior : CampaignBehaviorBase
{
    internal const string DaysKey = "_taom_tgwaves_days";

    private readonly ScreenFaultLog _faults;
    private readonly WaveBetService _bets;

    public TrainingWavesDialogBehavior(WaveBetService bets, IModLogger logger)
    {
        _faults = new ScreenFaultLog(logger, "TrainingWaves");
        _bets = bets;
        _bets.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
    }

    /// <summary>A load starts from the reset the constructor made and a key miss leaves it there; a save made before this feature has no record for this behavior at all, so this never runs and the ledger stays empty.</summary>
    public override void SyncData(IDataStore dataStore)
    {
        var days = dataStore.IsSaving ? _bets.ExportDays() : null;
        dataStore.SyncData(DaysKey, ref days);
        if (dataStore.IsLoading)
            _bets.ImportDays(days);
    }

    private void OnSessionLaunched(CampaignGameStarter starter) =>
        _faults.Run("session launched", () => TrainingWavesDialogLines.Register(starter, this));

    internal void OnConversationEnded(IEnumerable<CharacterObject> characters) =>
        _faults.Run("conversation ended", () => _bets.OnConversationEnded());

    internal bool CanOffer() => _faults.Condition("offer condition", () => _bets.ShowsOffer);

    internal bool OfferClickable(out TextObject explanation)
    {
        explanation = TextObject.GetEmpty();
        try
        {
            var refusal = _bets.Refusal;
            if (refusal == OfferRefusal.None)
                return true;
            explanation = TrainingWavesTexts.Explain(refusal, _bets.SmallestStake);
            return false;
        }
        catch (Exception ex)
        {
            _faults.LogOnce("offer clickable condition", ex);
            return false;
        }
    }

    internal bool CanRules() => _faults.Condition("rules condition", () =>
    {
        TrainingWavesTexts.SetRules(_bets.PrepareRun());
        return true;
    });

    /// <summary>The stake lines count from the largest: slot 0 is the full cap, 1 the half, 2 the quarter.</summary>
    internal bool WantsStake(int slot) => _bets.StakeChoice(slot) > 0;

    internal bool CanStakeLine(int slot) => _faults.Condition("stake condition", () =>
    {
        var amount = _bets.StakeChoice(slot);
        if (amount <= 0)
            return false;
        TrainingWavesTexts.SetStake(slot, amount);
        return true;
    });

    internal bool StakeClickable(int slot, out TextObject explanation)
    {
        explanation = TextObject.GetEmpty();
        try
        {
            if (_bets.CanAfford(_bets.StakeChoice(slot)))
                return true;
            explanation = new TextObject(TrainingWavesTexts.RefusalShort);
            return false;
        }
        catch (Exception ex)
        {
            _faults.LogOnce("stake clickable condition", ex);
            return false;
        }
    }

    internal void Stake(int slot) => _faults.Run("stake consequence", () => _bets.PlaceStake(_bets.StakeChoice(slot)));

    internal bool CanNotToday() => _faults.Condition("not today condition", () =>
        _bets.RunState is null or WaveRunState.Idle);

    internal bool CanWithdraw() => _faults.Condition("withdraw condition", () => _bets.RunState == WaveRunState.BetweenWaves);

    internal void Withdraw() => _faults.Run("withdraw consequence", () => _bets.Withdraw());

    internal bool WantsGo() => _bets.RunState == WaveRunState.WaveDue;

    internal bool CanGo() => _faults.Condition("go condition", () =>
    {
        if (!WantsGo())
            return false;
        TrainingWavesTexts.SetGo(_bets.Run!);
        return true;
    });

    internal bool CanWithdrawn() => _faults.Condition("withdrawn condition", () =>
    {
        var run = _bets.Run;
        if (run == null || run.EndReason != WaveRunEnd.Withdrew)
            return false;
        TrainingWavesTexts.SetWithdrawn(run);
        return true;
    });

    internal bool WantsDebrief(DebriefKind kind) => _bets.PendingDebrief == kind;

    internal bool CanCleared() => _faults.Condition("cleared condition", () =>
    {
        if (!WantsDebrief(DebriefKind.Cleared))
            return false;
        TrainingWavesTexts.SetCleared(_bets.Run!);
        return true;
    });

    internal bool CanLost() => _faults.Condition("lost condition", () =>
    {
        if (!WantsDebrief(DebriefKind.Lost))
            return false;
        TrainingWavesTexts.SetLost(_bets.Run!);
        return true;
    });

    internal bool CanDone() => _faults.Condition("done condition", () =>
    {
        if (!WantsDebrief(DebriefKind.Completed))
            return false;
        TrainingWavesTexts.SetDone(_bets.Run!);
        return true;
    });

    internal void Debrief() => _faults.Run("debrief consequence", () => _bets.TakeDebrief());
}
