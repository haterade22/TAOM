namespace TAOM.Features.Messengers.Domain;

public readonly struct PositionUpdate
{
    public MapCoord NewPosition { get; }
    public bool Arrived { get; }

    public PositionUpdate(MapCoord newPosition, bool arrived)
    {
        NewPosition = newPosition;
        Arrived = arrived;
    }
}
