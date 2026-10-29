using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege;

/// <summary>The slot-backed <see cref="IStagedSiegeFieldReader"/>.</summary>
public sealed class StagedSiegeFieldReader : IStagedSiegeFieldReader
{
    public bool OwnsField(object mission, StagedSide side) 
    {
        var snapshot = StagedSiegeSlot.SnapshotFor(mission);
        return snapshot != null && !snapshot.Finished && StagePlan.ModeFor(snapshot.Stage, side) != TacticMode.Vanilla;
    }
}
