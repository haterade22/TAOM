namespace TAOM.Adapters;

internal enum MarshalCall
{
    Order,
    Agent,
    Position,
    Formation,
    TwoPositions,
    Orderable,

    /// <summary>A mark: <c>SetTargetFormation</c> on each member and no order (build-spec 8.1).</summary>
    Mark,
}
