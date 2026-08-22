using TaleWorlds.Core;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// Terrain policy: which camp types the ground allows, and how well it forages. Pure decisions on
/// a <see cref="TerrainType"/> the boundary reads from the map face.
/// </summary>
public interface ICampTerrainService
{
    /// <summary>Ambush needs concealment: forest, hills, swamp, canyon, dunes, a bridge or a ford.</summary>
    bool AllowsAmbush(TerrainType terrain);

    /// <summary>Lookout needs vantage: mountain, hill, forest, steppe or plain.</summary>
    bool AllowsLookout(TerrainType terrain);

    /// <summary>Relative forage yield for this ground (plains 1.0 .. water 0).</summary>
    float ForageYield(TerrainType terrain);

    /// <summary>Hourly grain gathered: yield x (1 + scouting/100) x perTroopFactor x sqrt(troops).
    /// Non-finite inputs yield 0.</summary>
    float HourlyForage(TerrainType terrain, int troopCount, float scoutingSkill, float perTroopFactor);
}
