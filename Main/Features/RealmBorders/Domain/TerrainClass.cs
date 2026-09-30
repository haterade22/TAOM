namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// How the territory flood treats one cell of the campaign map. The terrain adapter maps the
/// engine's navmesh <c>TerrainType</c> onto these, so the domain never sees the engine enum.
/// </summary>
public enum TerrainClass : byte
{
    /// <summary>Plains, steppe, desert, rural land: the base cost.</summary>
    Open = 0,

    /// <summary>Forest, swamp, snow: slower to claim.</summary>
    Rough = 1,

    /// <summary>Mountain, cliff, canyon, restricted land: never crossed by the flood.</summary>
    Wall = 2,

    /// <summary>A river: crossed at a cost, so a border between two fiefs settles on it.</summary>
    River = 3,

    /// <summary>A ford or a bridge: crosses a river at open-ground cost.</summary>
    Crossing = 4,

    /// <summary>Sea and lakes: never crossed and never claimed.</summary>
    Water = 5,
}
