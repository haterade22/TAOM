using System;
using TAOM.Core.Validation;
using TaleWorlds.Core;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// Terrain policy for camps. Pure: every decision is a function of the <see cref="TerrainType"/>
/// the boundary read from the map face.
///
/// <para>Every 1.4.8 enum member is named explicitly in each switch and the default arm answers
/// for values the engine adds later: a future terrain can never silently allow an ambush or grow
/// food (the source module used compiled ordinal offsets from an older enum, which is exactly how
/// its sets drifted).</para>
/// </summary>
public class CampTerrainService : ICampTerrainService
{
    public bool AllowsAmbush(TerrainType terrain)
    {
        switch (terrain)
        {
            // Concealment: woods, broken ground, reeds, dunes, and the choke points travellers
            // must pass (a ford, a bridge and the bank beneath it).
            case TerrainType.Forest:
            case TerrainType.Fording:
            case TerrainType.Mountain:
            case TerrainType.Canyon:
            case TerrainType.Swamp:
            case TerrainType.Dune:
            case TerrainType.Bridge:
            case TerrainType.UnderBridge:
                return true;

            // Open, bare or impassable ground: nothing to hide behind (or in).
            case TerrainType.Plain:
            case TerrainType.Desert:
            case TerrainType.Snow:
            case TerrainType.Steppe:
            case TerrainType.Lake:
            case TerrainType.Water:
            case TerrainType.River:
            case TerrainType.RuralArea:
            case TerrainType.CoastalSea:
            case TerrainType.OpenSea:
            case TerrainType.Beach:
            case TerrainType.Cliff:
            case TerrainType.NonNavigableRiver:
            case TerrainType.LandRestriction:
            case TerrainType.SeaRestriction:
                return false;

            default:
                // A terrain this build has never heard of gives no concealment until someone
                // classifies it deliberately.
                return false;
        }
    }

    public bool AllowsLookout(TerrainType terrain)
    {
        switch (terrain)
        {
            // Vantage: high ground, or open ground with an unobstructed horizon.
            case TerrainType.Plain:
            case TerrainType.Forest:
            case TerrainType.Steppe:
            case TerrainType.Mountain:
            case TerrainType.Canyon:
                return true;

            case TerrainType.Desert:
            case TerrainType.Snow:
            case TerrainType.Fording:
            case TerrainType.Lake:
            case TerrainType.Water:
            case TerrainType.River:
            case TerrainType.RuralArea:
            case TerrainType.Swamp:
            case TerrainType.Dune:
            case TerrainType.Bridge:
            case TerrainType.CoastalSea:
            case TerrainType.OpenSea:
            case TerrainType.Beach:
            case TerrainType.Cliff:
            case TerrainType.NonNavigableRiver:
            case TerrainType.LandRestriction:
            case TerrainType.SeaRestriction:
            case TerrainType.UnderBridge:
                return false;

            default:
                return false;
        }
    }

    public float ForageYield(TerrainType terrain)
    {
        switch (terrain)
        {
            // Open grassland: the best foraging.
            case TerrainType.Plain:
            case TerrainType.Steppe:
                return 1f;

            // Game, berries and farmland gleanings.
            case TerrainType.Forest:
            case TerrainType.RuralArea:
                return 0.7f;

            // Rough ground and riverbanks: sparse but real pickings.
            case TerrainType.Canyon:
            case TerrainType.Swamp:
            case TerrainType.Fording:
            case TerrainType.Bridge:
            case TerrainType.UnderBridge:
                return 0.45f;

            // Near-barren ground and shoreline scraps.
            case TerrainType.Desert:
            case TerrainType.Snow:
            case TerrainType.Dune:
            case TerrainType.Beach:
                return 0.2f;

            // Nothing grows on water, bare rock, or the map's movement-restriction faces.
            case TerrainType.Mountain:
            case TerrainType.Lake:
            case TerrainType.Water:
            case TerrainType.River:
            case TerrainType.CoastalSea:
            case TerrainType.OpenSea:
            case TerrainType.Cliff:
            case TerrainType.NonNavigableRiver:
            case TerrainType.LandRestriction:
            case TerrainType.SeaRestriction:
                return 0f;

            default:
                // Unknown future terrain feeds nobody until it is classified.
                return 0f;
        }
    }

    public float HourlyForage(TerrainType terrain, int troopCount, float scoutingSkill, float perTroopFactor)
    {
        if (troopCount <= 0)
            return 0f;
        // Positive gates: NaN, infinity, zero and negative factors all land in the same "no food"
        // arm, so a corrupt config or skill value can never mint (or destroy) grain.
        if (!FiniteFloatValidator.IsFinite(scoutingSkill) || !FiniteFloatValidator.IsFinite(perTroopFactor))
            return 0f;
        if (!(perTroopFactor > 0f))
            return 0f;

        float terrainYield = ForageYield(terrain);
        if (!(terrainYield > 0f))
            return 0f;

        // A negative (corrupt) skill only loses its bonus; it never pushes the yield below base.
        float scoutingFactor = 1f + Math.Max(0f, scoutingSkill) / 100f;
        float result = terrainYield * scoutingFactor * perTroopFactor * (float)Math.Sqrt(troopCount);
        return FiniteFloatValidator.IsFinite(result) && result > 0f ? result : 0f;
    }
}
