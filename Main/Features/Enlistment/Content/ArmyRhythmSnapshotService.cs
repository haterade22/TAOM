using System;
using TAOM.Adapters;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public interface IArmyRhythmSnapshotService
{
    /// <summary>Snapshot for the given campaign time; cached per game hour (one probe per hour, 11 donor call sites shared it).</summary>
    ArmyRhythmSnapshot GetSnapshot(double nowDays, double hourOfDay);

    void Invalidate();
}

public class ArmyRhythmSnapshotService : IArmyRhythmSnapshotService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IArmyRhythmProbeAdapter _probe;

    private ArmyRhythmSnapshot _cached;
    private double _cachedHourStamp = double.MinValue;

    public ArmyRhythmSnapshotService(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IArmyRhythmProbeAdapter probe)
    {
        _store = store;
        _contentStore = contentStore;
        _probe = probe;
    }

    public ArmyRhythmSnapshot GetSnapshot(double nowDays, double hourOfDay)
    {
        // One probe per game hour: floor(day*24) is the cache key.
        var hourStamp = Math.Floor(nowDays * 24.0);
        if (_cached != null && hourStamp == _cachedHourStamp)
            return _cached;

        var probe = _probe.Probe(_store.Record.CommanderHeroId);
        var record = _contentStore.Record;

        var snapshot = new ArmyRhythmSnapshot
        {
            Phase = PhaseOf(hourOfDay),
            InArmy = probe.InArmy,
            SiegePressure = probe.InSiege,
            Naval = probe.AtSea,
            Blockade = probe.Blockade,
            LowSupplies = probe.CommanderResolved && (probe.FoodAmount <= 20 || probe.DaysOfFoodLeft < 2f),
            WoundedCount = probe.WoundedCount,
            RecruitCount = probe.LowTierTroopCount,
            RecoveryState = probe.TroopCount > 0 && probe.WoundedCount >= Math.Max(6, probe.TroopCount / 6),
            QuietGarrison = probe.InSettlement && !probe.InSiege && !probe.EnemyPartyNearby,
            Marching = probe.Moving,
            PreBattle = probe.EnemyPartyNearby,
            ActiveCampaign = probe.FactionAtWar,
            HighScrutiny = record.Trust <= -4 || record.DutyFailures > record.DutySuccesses,
        };

        _cached = snapshot;
        _cachedHourStamp = hourStamp;
        return snapshot;
    }

    public void Invalidate()
    {
        _cached = null;
        _cachedHourStamp = double.MinValue;
    }

    private static ServicePhase PhaseOf(double hourOfDay)
    {
        if (hourOfDay < 5.0 || hourOfDay >= 21.0)
            return ServicePhase.Night;
        if (hourOfDay < 10.0)
            return ServicePhase.Dawn;
        if (hourOfDay < 17.0)
            return ServicePhase.Midday;
        return ServicePhase.Dusk;
    }
}
