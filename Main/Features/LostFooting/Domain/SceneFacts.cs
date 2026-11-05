namespace TAOM.Features.LostFooting.Domain;

/// <summary>The facts of the battle scene, read once at the start of a mission.</summary>
public sealed class SceneFacts
{
    public string SceneId { get; set; } = string.Empty;

    /// <summary><c>field</c>, <c>siege</c> or <c>sally-out</c>.</summary>
    public string MissionType { get; set; } = string.Empty;

    /// <summary>The scene's global water level, or NaN when the engine gave none.</summary>
    public float WaterLevel { get; set; } = float.NaN;
}

/// <summary>
/// What a second, fuller look at one off-mesh agent found. The snap candidate is the pair the engine uses in
/// <c>Agent.GetBaseFormationFrame</c>: the nearest navigation mesh, then the closest exit point on its boundary face.
/// </summary>
public sealed class OffMeshDetail
{
    /// <summary>A fresh position at the agent's spot also has no navigation mesh, and it is not on a dynamic one.</summary>
    public bool Confirmed { get; set; }

    /// <summary>The spot is on a dynamic navigation mesh (a moving platform), so a miss is expected.</summary>
    public bool OnDynamicNavMesh { get; set; }

    /// <summary>The engine's snap pair returned a usable point.</summary>
    public bool HasSnap { get; set; }

    public float SnapDistance { get; set; } = float.NaN;

    /// <summary>The candidate point itself has a navigation mesh.</summary>
    public bool SnapHasNavMesh { get; set; }
}
