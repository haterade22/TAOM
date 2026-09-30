namespace TAOM.Adapters;

/// <summary>
/// The campaign map's terrain as the realm borders read it: its extent, the navmesh terrain type
/// under a point, and the ground height. Main-thread only: every call is a native scene query.
/// </summary>
public interface IMapTerrainAdapter
{
    /// <summary>The playable extent from the scene's border markers; false with no campaign map loaded.</summary>
    bool TryGetBounds(out float minX, out float minY, out float maxX, out float maxY);

    /// <summary>
    /// The <c>TaleWorlds.Core.TerrainType</c> value of the land navmesh face under the point, or -1
    /// where the land navmesh has no face (open sea, off the map).
    /// </summary>
    int TerrainTypeAt(float x, float y);

    /// <summary>The ground height at the point, or <paramref name="fallback"/> when the scene cannot say.</summary>
    float HeightAt(float x, float y, float fallback);
}
