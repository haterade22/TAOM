namespace TAOM.Adapters;

/// <summary>
/// The damage stage a tower entity shows, which the adapter keeps moving only forward; the engine does not. It recomputes the
/// stage from the component's hit points on every hit and asks <c>OnCalculateDestructionStateIndex</c> to vet it. After a
/// regeneration heal the recomputed stage can be lower than the one on show: a smaller heal steps the tower back to an earlier
/// model with no exception, and once a heal lifts the hit points above the first line, Max x (N-1)/N, the index is 0 and the
/// engine hides the shown state and reads <c>_destructionStates[-1]</c>.
/// Hits arrive on whatever thread the engine used, so the stage takes a lock.
/// </summary>
internal sealed class FordsTowerStage
{
    private readonly object _gate = new();
    private int _stage;

    /// <summary>The stage to show for the engine's <paramref name="index"/>: the higher of it and the stage already shown.</summary>
    public int Advance(int index)
    {
        lock (_gate)
        {
            if (index > _stage)
                _stage = index;
            return _stage;
        }
    }
}
