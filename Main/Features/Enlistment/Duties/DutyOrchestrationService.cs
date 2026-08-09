using TAOM.Adapters;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

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

    /// <summary>
    /// The ONLY state in which the company can hand out work. Captivity and the
    /// commander-loss grace are both inside <c>IsEnlisted</c>, so that predicate is too wide
    /// for anything that assigns or pays.
    /// </summary>
    private bool IsAvailableForOrders()
        => _store.Record.State == EnlistmentState.EnlistedAttached;

    public void DailyOfferTick(double nowDays, double hourOfDay)
    {
        // NOT IsEnlisted. That predicate deliberately spans five states including
        // EnlistedPlayerCaptive and CommanderUnavailable — a prisoner would be offered camp work,
        // and a player whose commander has just been captured would be offered work by a company
        // that no longer has anyone to assign it. Only an attached soldier can be given orders.
        if (!IsAvailableForOrders())
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

        TryOffer(nowDays, hourOfDay);
    }

    /// <summary>
    /// Ask the commander for work. Deliberately routes through the SAME <see cref="TryOffer"/> the
    /// daily tick uses, and reuses the existing rotation cadence rather than inventing a second
    /// cooldown: asking is free, but the commander only has work when the rotation says so.
    /// A separate cooldown here would let a player farm duties by spamming the menu option.
    /// </summary>
    public DutyRequestResult RequestDutyNow(double nowDays, double hourOfDay)
    {
        if (!_store.Record.IsEnlisted)
            return DutyRequestResult.NotEnlisted;
        if (_contentStore.Record.HasActiveDuty)
            return DutyRequestResult.AlreadyOnDuty;

        return TryOffer(nowDays, hourOfDay);
    }

    /// <summary>
    /// The one offer path. Both the daily tick and an explicit request land here, so a change to
    /// how duties are chosen cannot apply to one caller and not the other.
    /// </summary>
    private DutyRequestResult TryOffer(double nowDays, double hourOfDay)
    {
        var record = _contentStore.Record;
        var config = _config.GetConfig();
        var rhythm = _rhythm.GetSnapshot(nowDays, hourOfDay);
        var leadership = _skillXp.GetSkillValue(_store.Record.EnlistedHeroId, "Leadership");
        var progress = record.ToProgressSnapshot(leadership);
        var pressure = rhythm.SiegePressure || rhythm.Naval || rhythm.Blockade || rhythm.PreBattle;

        if (!_rotation.ShouldOfferDuty(
                config.Scheduler, record.DaysServed, record.Trust, pressure, nowDays, record.LastOfferDay))
        {
            return DutyRequestResult.NoWorkAvailable;
        }

        var offer = _selector.SelectOffer(_config.GetDuties(), progress, rhythm, record.RecentDutyIds, pressure);
        if (!offer.HasOffer)
            return DutyRequestResult.NoWorkAvailable;

        record.LastOfferDay = nowDays;

        if (offer.FieldDuty != null)
        {
            RememberOffered(record, offer.FieldDuty.Id);
            if (!_runtime.Start(offer.FieldDuty, nowDays))
                return DutyRequestResult.NoWorkAvailable;

            return DutyRequestResult.DutyAssigned;
        }

        if (offer.InteractiveDuty != null)
        {
            RememberOffered(record, offer.InteractiveDuty.Id);
            _presenter.PresentInteractiveDuty(offer.InteractiveDuty, progress, record.Trust);
            return DutyRequestResult.DutyAssigned;
        }

        return DutyRequestResult.NoWorkAvailable;
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
