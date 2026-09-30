namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Maps the engine's navmesh <c>TaleWorlds.Core.TerrainType</c>, passed as its integer value so the
/// domain stays engine-free, onto the classes the territory flood understands. Values checked against
/// the installed v1.5.3 enum; an unknown value is claimable open ground, never a wall by accident.
/// </summary>
public static class TerrainClassifier
{
    public static TerrainClass Classify(int terrainType)
    {
        switch (terrainType)
        {
            case 3:  // Snow
            case 4:  // Forest
            case 15: // Swamp
                return TerrainClass.Rough;
            case 7:  // Mountain
            case 13: // Canyon
            case 21: // Cliff
            case 23: // LandRestriction
                return TerrainClass.Wall;
            case 11: // River
            case 22: // NonNavigableRiver
            case 25: // UnderBridge
                return TerrainClass.River;
            case 6:  // Fording
            case 17: // Bridge
                return TerrainClass.Crossing;
            case 8:  // Lake
            case 10: // Water
            case 18: // CoastalSea
            case 19: // OpenSea
            case 24: // SeaRestriction
                return TerrainClass.Water;
            default: // Plain, Desert, Steppe, RuralArea, Dune, Beach, and anything unknown
                return TerrainClass.Open;
        }
    }
}
