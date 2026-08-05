using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Thin router (ADR-002) for the content layer: daily service tick, battle-end payout,
/// content SyncData (its own section — the core record stays in EnlistmentBehavior), and
/// end-of-service cleanup. All policy lives in the services.
/// </summary>
public class EnlistmentContentBehavior : CampaignBehaviorBase
{
    private const string SaveKey = "_taom_enlistment_content";

    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentDailyService _daily;
    private readonly IEnlistmentBattlePayoutService _battle;
    private readonly IDischargeConsequenceService _consequences;
    private readonly Duties.IDutyOrchestrationService _duties;
    private readonly IDischargeService _discharge;
    private readonly ICoopSessionProvider _coopSession;

    private CampaignGameStarter _lastSessionStarter;
    private bool _justLoadedFromSave;

    public EnlistmentContentBehavior(
        IEnlistmentContentStore contentStore,
        IEnlistmentDailyService daily,
        IEnlistmentBattlePayoutService battle,
        IDischargeConsequenceService consequences,
        Duties.IDutyOrchestrationService duties,
        IDischargeService discharge,
        ICoopSessionProvider coopSession)
    {
        _contentStore = contentStore;
        _daily = daily;
        _battle = battle;
        _consequences = consequences;
        _duties = duties;
        _discharge = discharge;
        _coopSession = coopSession;
        _discharge.EnlistmentEnded += OnEnlistmentEnded;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
    }

    // Battle-end payout — the SECOND (and last) promotion-relevant reward point. All
    // payout happens here at once: base win/loss XP, capped kill XP, and the merit band
    // from the mission sampler (the donor also paid per-kill mid-mission — dropped).
    private void OnMapEventEnded(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent)
    {
        if (!_coopSession.IsAuthority || mapEvent == null || !mapEvent.IsPlayerMapEvent)
            return;
        if (!_battle.PayOutBattle(mapEvent.WinningSide == mapEvent.PlayerSide))
            return;

        var sampleBand = _battle.LastBandGradeKey;
        if (!string.IsNullOrEmpty(sampleBand))
        {
            var text = new TextObject("{=taom_enlist_merit_toast}The sergeants noted your conduct: {GRADE}.");
            text.SetTextVariable("GRADE", sampleBand);
            InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
        }

        if (_battle.LastBattlePromoted)
            AnnouncePromotion(_battle.LastPromotedRank);
    }

    private static void AnnouncePromotion(Content.Domain.ServiceRank rank)
    {
        var text = new TextObject("{=taom_enlist_promoted}You have been promoted to {RANK}.");
        text.SetTextVariable("RANK", rank.ToString());
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var section = _contentStore.Serialize();
            dataStore.SyncData(SaveKey, ref section);
        }
        else
        {
            string section = null;
            dataStore.SyncData(SaveKey, ref section);
            _contentStore.Deserialize(section);
            _justLoadedFromSave = true;
        }
    }

    // CO-OP: host-only — wages move real gold and XP mutates the shared hero.
    internal void OnDailyTick()
    {
        if (!_coopSession.IsAuthority)
            return;

        var summary = _daily.RunDailyTick(CampaignTime.Now.ToDays, CampaignTime.Now.CurrentHourInDay);

        if (summary.Promoted)
            AnnouncePromotion(summary.NewRank);

        if (summary.ContractExpiredToday)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=taom_enlist_term_served}Your term of service is complete — you may ask for release with honor, or march on.").ToString()));
        }
    }

    // Consequence policy lives in the service; this only forwards the event.
    private void OnEnlistmentEnded(DischargeReason reason)
    {
        if (!_coopSession.IsAuthority)
            return;
        // Duty artifacts first — CancelActiveDuty destroys any spawned target party and is
        // a no-op when nothing is active. Consequences clear the record afterwards.
        _duties.CancelActiveDuty(reason.ToString());
        _consequences.ApplyConsequences(reason);
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        if (!_justLoadedFromSave)
            _contentStore.Clear();
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        if (_lastSessionStarter != starter)
        {
            _lastSessionStarter = starter;
            if (!_justLoadedFromSave)
                _contentStore.Clear();
        }
        _justLoadedFromSave = false;
    }
}
