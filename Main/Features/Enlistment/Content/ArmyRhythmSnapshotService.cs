using TAOM.Adapters;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public interface IArmyRhythmSnapshotService
{
    /// <summary>A fresh probe of the commander's column on every call.</summary>
    ArmyRhythmSnapshot GetSnapshot();
}

public class ArmyRhythmSnapshotService : IArmyRhythmSnapshotService
{
    private readonly IEnlistmentStore _store;
    private readonly IArmyRhythmProbeAdapter _probe;

    public ArmyRhythmSnapshotService(
        IEnlistmentStore store,
        IArmyRhythmProbeAdapter probe)
    {
        _store = store;
        _probe = probe;
    }

    public ArmyRhythmSnapshot GetSnapshot()
    {
        var probe = _probe.Probe(_store.Record.CommanderHeroId);

        return new ArmyRhythmSnapshot
        {
            InArmy = probe.InArmy,
            SiegePressure = probe.InSiege,
            Naval = probe.AtSea,
            Blockade = probe.Blockade,
            QuietGarrison = probe.InSettlement && !probe.InSiege && !probe.EnemyPartyNearby,
            Marching = probe.Moving,
            PreBattle = probe.EnemyPartyNearby,
        };
    }
}
