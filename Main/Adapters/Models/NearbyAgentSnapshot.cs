using TaleWorlds.Library;

namespace TAOM.Adapters.Models;

/// <summary>
/// Minimal snapshot of an Agent's battlefield state, used by services that need
/// nearby-agent information without importing <c>TaleWorlds.MountAndBlade.Agent</c>.
/// All fields are captured at query time; the snapshot does not retain a reference
/// to the underlying Agent.
/// </summary>
public readonly struct NearbyAgentSnapshot
{
    public readonly int Index;
    public readonly Vec2 Position;
    public readonly bool IsMounted;
    public readonly bool IsActive;
    public readonly object TeamKey;

    public NearbyAgentSnapshot(int index, Vec2 position, bool isMounted, bool isActive, object teamKey)
    {
        Index = index;
        Position = position;
        IsMounted = isMounted;
        IsActive = isActive;
        TeamKey = teamKey;
    }
}
