using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.HeroDefence.Domain;

internal enum PackOrder
{
    Move,
    Charge,
}

/// <summary>
/// Flat distances in metres. Engage is shorter than release, so a pack at the edge does not change its order on each poll. The
/// defaults are the values spike run 5 measured for foot packs (2026-11-09). Mounted packs have no measured band yet.
/// </summary>
internal sealed record PackOrderBand
{
    public float EngageMetres { get; init; } = 12f;

    public float ReleaseMetres { get; init; } = 22f;

    /// <summary>A hero more than this far above or below a creep does not pair with it.</summary>
    public float HeightMetres { get; init; } = 6f;
}

/// <summary>When a creep pack leaves its march to fight a hero, and when it marches on.</summary>
internal static class PackOrderRule
{
    /// <summary>
    /// The pack's next order. <paramref name="heroes"/> are the heroes that can draw a charge: standing, and on the road or under the
    /// player's control; never a plateau hero. With no hero left in reach the pack moves at once, whatever the band.
    /// </summary>
    public static PackOrder Next(PackOrder current, IReadOnlyList<Vec3f> creeps, IReadOnlyList<Vec3f> heroes, PackOrderBand band)
    {
        var nearest = NearestFlat(creeps, heroes, band.HeightMetres);
        if (float.IsPositiveInfinity(nearest))
            return PackOrder.Move;

        if (current == PackOrder.Charge)
            return nearest >= band.ReleaseMetres ? PackOrder.Move : PackOrder.Charge;

        return nearest <= band.EngageMetres ? PackOrder.Charge : PackOrder.Move;
    }

    /// <summary>
    /// The flat distance of the nearest creep and hero pair whose heights differ by at most <paramref name="heightMetres"/>, or
    /// positive infinity when no pair qualifies. A NaN or infinite position never pairs.
    /// </summary>
    public static float NearestFlat(IReadOnlyList<Vec3f> creeps, IReadOnlyList<Vec3f> heroes, float heightMetres)
    {
        var nearest = float.PositiveInfinity;
        foreach (var creep in creeps)
        {
            foreach (var hero in heroes)
            {
                if (!(Math.Abs(creep.Z - hero.Z) <= heightMetres))
                    continue;

                var flat = creep.Distance2D(hero);
                if (flat < nearest)
                    nearest = flat;
            }
        }

        return nearest;
    }
}
