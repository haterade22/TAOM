using TAOM.Adapters;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Duties;

public class DutyOrchestrationService : IDutyOrchestrationService
{
    /// <summary>RecentDutyIds anti-repeat cap (matches the record's persisted-field convention).</summary>
    private const int RecentDutyCap = 5;

    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IArmyRhythmSnapshotService _rhythm;
    private readonly IHeroSkillXpAdapter _skillXp;
    private readonly IDutyRotationPolicy _rotation;
    private readonly IDutySelector _selector;
    private readonly IFieldDutyRuntime _runtime;
    private readonly IInteractiveDutyPresenter _presenter;

    public DutyOrchestrationService(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        IArmyRhythmSnapshotService rhythm,
        IHeroSkillXpAdapter skillXp,
        IDutyRotationPolicy rotation,
        IDutySelector selector,
        IFieldDutyRuntime runtime,
        IInteractiveDutyPresenter presenter)
    {
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _rhythm = rhythm;
        _skillXp = skillXp;
        _rotation = rotation;
        _selector = selector;
        _runtime = runtime;
        _presenter = presenter;
    }

    public void HourlyTick(double nowDays)
    {
        if (!_store.Record.IsEnlisted)
            return;
        _runtime.HourlyUpdate(nowDays);
    }

    public void DailyOfferTick(double nowDays, double hourOfDay)
    {
        if (!_store.Record.IsEnlisted)
            return;

        var record = _contentStore.Record;
        if (record.HasActiveDuty)
            return;

        var config = _config.GetConfig();
        var scheduler = config.Scheduler;
        var rhythm = _rhythm.GetSnapshot(nowDays, hourOfDay);
        var leadership = _skillXp.GetSkillValue(_store.Record.EnlistedHeroId, "Leadership");
        var progress = record.ToProgressSnapshot(leadership);
        var pressure = rhythm.SiegePressure || rhythm.Naval || rhythm.Blockade || rhythm.PreBattle;

        if (_rotation.ShouldRollIncident(scheduler, record.DaysServed, nowDays, record.LastIncidentDay))
        {
            var incident = _selector.SelectIncident(_config.GetDuties().Incidents, progress, rhythm);
            record.LastIncidentDay = nowDays;
            if (incident != null)
            {
                _presenter.PresentIncident(incident, progress, record.Trust);
                return;
            }
        }

        if (!_rotation.ShouldOfferDuty(scheduler, record.DaysServed, record.Trust, pressure, nowDays, record.LastOfferDay))
            return;

        var offer = _selector.SelectOffer(_config.GetDuties(), progress, rhythm, record.RecentDutyIds, pressure);
        if (!offer.HasOffer)
            return;

        record.LastOfferDay = nowDays;

        if (offer.FieldDuty != null)
        {
            RememberOffered(record, offer.FieldDuty.Id);
            _runtime.Start(offer.FieldDuty, nowDays);
        }
        else if (offer.InteractiveDuty != null)
        {
            RememberOffered(record, offer.InteractiveDuty.Id);
            _presenter.PresentInteractiveDuty(offer.InteractiveDuty, progress, record.Trust);
        }
    }

    public void OnSettlementEntered(string settlementId, double nowDays)
    {
        if (!_store.Record.IsEnlisted)
            return;
        _runtime.OnSettlementEntered(settlementId, nowDays);
    }

    public void OnMobilePartyDestroyed(string partyId)
    {
        if (!_store.Record.IsEnlisted)
            return;
        _runtime.OnTargetPartyDestroyed(partyId);
    }

    public void CancelActiveDuty(string reason)
    {
        _runtime.CancelActive(reason);
    }

    private static void RememberOffered(ServiceContentRecord record, string dutyId)
    {
        record.RecentDutyIds.Remove(dutyId);
        record.RecentDutyIds.Insert(0, dutyId);
        while (record.RecentDutyIds.Count > RecentDutyCap)
            record.RecentDutyIds.RemoveAt(record.RecentDutyIds.Count - 1);
    }
}
