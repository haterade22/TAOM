using System;
using TAOM.Adapters;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public interface IArmyRhythmSnapshotService
{
    /// <summary>Snapshot for the given campaign time. At most one probe per game hour (the cache key); called by the daily service tick, the daily duty offer and a duty request.</summary>
    ArmyRhythmSnapshot GetSnapshot(double nowDays);

    /// <summary>Drop the cached snapshot. Session reset only (a load, a new campaign or game end).</summary>
    void ResetForNewSession();
}

public class ArmyRhythmSnapshotService : IArmyRhythmSnapshotService
{
    private readonly IEnlistmentStore _store;
    private readonly IArmyRhythmProbeAdapter _probe;

    private ArmyRhythmSnapshot _cached;
    private double _cachedHourStamp = double.MinValue;

    public ArmyRhythmSnapshotService(
        IEnlistmentStore store,
        IArmyRhythmProbeAdapter probe)
    {
        _store = store;
        _probe = probe;
    }

    public ArmyRhythmSnapshot GetSnapshot(double nowDays)
    {
        // One probe per game hour: floor(day*24) is the cache key.
        var hourStamp = Math.Floor(nowDays * 24.0);
        if (_cached != null && hourStamp == _cachedHourStamp)
            return _cached;

        var probe = _probe.Probe(_store.Record.CommanderHeroId);

        var snapshot = new ArmyRhythmSnapshot
        {
            InArmy = probe.InArmy,
            SiegePressure = probe.InSiege,
            Naval = probe.AtSea,
            Blockade = probe.Blockade,
            QuietGarrison = probe.InSettlement && !probe.InSiege && !probe.EnemyPartyNearby,
            Marching = probe.Moving,
            PreBattle = probe.EnemyPartyNearby,
        };

        _cached = snapshot;
        _cachedHourStamp = hourStamp;
        return snapshot;
    }

    public void ResetForNewSession()
    {
        _cached = null;
        _cachedHourStamp = double.MinValue;
    }
}
