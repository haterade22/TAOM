using System;
using TAOM.Core.Validation;
using TaleWorlds.Core;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// Terrain policy for camps. Pure: every decision is a function of the <see cref="TerrainType"/>
/// the boundary read from the map face.
///
/// <para>The tables are the SOURCE module's, decoded from its compiled relative switches
/// (<c>t - 3</c> / <c>t - 1</c> over case ordinals) against the real 1.4.8
/// <see cref="TerrainType"/> values (byte-identical to the 1.4.5 enum the source targeted, so the
/// decode is exact). Every current member is still named explicitly and the default arm answers
/// for values the engine adds later: a future terrain can never silently allow an ambush or grow
/// food.</para>
/// </summary>
public class CampTerrainService : ICampTerrainService
{
    public bool AllowsAmbush(TerrainType terrain)
    {
        switch (terrain)
        {
            // Source set: snowfields, woods, broken ground, reeds, dunes, and the choke points
            // travellers must pass (a ford and a bridge).
            case TerrainType.Snow:
            case TerrainType.Forest:
            case TerrainType.Fording:
            case TerrainType.Mountain:
            case TerrainType.Canyon:
            case TerrainType.Swamp:
            case TerrainType.Dune:
            case TerrainType.Bridge:
                return true;

            // Open, bare or impassable ground: nothing to hide behind (or in). UnderBridge (25)
            // postdates the source's cases and fell to its default-false arm; kept false.
            case TerrainType.Plain:
            case TerrainType.Desert:
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
            case TerrainType.UnderBridge:
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
            // Source: grassland and woodland forage best.
            case TerrainType.Plain:
            case TerrainType.Forest:
                return 1f;

            // Source: open steppe and wetland.
            case TerrainType.Steppe:
            case TerrainType.Swamp:
                return 0.7f;

            // Source: rough highland pickings.
            case TerrainType.Mountain:
            case TerrainType.Canyon:
                return 0.45f;

            // Source: near-barren ground.
            case TerrainType.Desert:
            case TerrainType.Snow:
                return 0.2f;

            // Source: nothing grows on open water.
            case TerrainType.Lake:
            case TerrainType.Water:
            case TerrainType.River:
            case TerrainType.CoastalSea:
            case TerrainType.OpenSea:
            case TerrainType.NonNavigableRiver:
                return 0f;

            // Everything else fell to the source's default 0.5 arm at runtime (including, oddly,
            // Cliff and the movement-restriction faces; a camp can rarely stand on those anyway,
            // and parity beats a silent retune). Named explicitly so a FUTURE member does not
            // inherit 0.5 unreviewed.
            case TerrainType.Fording:
            case TerrainType.RuralArea:
            case TerrainType.Dune:
            case TerrainType.Bridge:
            case TerrainType.Beach:
            case TerrainType.Cliff:
            case TerrainType.LandRestriction:
            case TerrainType.SeaRestriction:
            case TerrainType.UnderBridge:
                return 0.5f;

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
