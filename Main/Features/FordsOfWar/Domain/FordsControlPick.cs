using System;
using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>A unit of the commander's side as the screen shows it: the pixel it stands at, and whether it is one of the match's heroes.</summary>
internal readonly record struct FordsControlCandidate(float X, float Y, bool IsHero);

/// <summary>Which unit the pointer names when the commander takes control of a soldier.</summary>
internal static class FordsControlPick
{
    /// <summary>How far from a unit the pointer may stand, as a share of the screen's height.</summary>
    internal const float ReachOfScreenHeight = 0.08f;

    private static readonly string[] PlainMounts = { "horse", "horse_2", "camel", "mule" };

    /// <summary>Whether a soldier on this mount, or on foot with null, can be driven. A behavior tree drives any other creature, and a player's input would fight it.</summary>
    internal static bool MayRide(string? mountMonsterId) => mountMonsterId == null || Array.IndexOf(PlainMounts, mountMonsterId) >= 0;

    /// <summary>The index of the named unit, or -1: the nearest hero in reach, else the nearest soldier in reach. A hero wins because a crowd surrounds it.</summary>
    internal static int Nearest(IReadOnlyList<FordsControlCandidate> units, float x, float y, float reach)
    {
        var limit = reach * reach;
        var best = -1;
        var bestSquared = 0f;
        var bestIsHero = false;
        for (var i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            var dx = unit.X - x;
            var dy = unit.Y - y;
            var squared = dx * dx + dy * dy;
            if (!(squared <= limit))
                continue;

            if (best < 0 || (unit.IsHero && !bestIsHero) || (unit.IsHero == bestIsHero && squared < bestSquared))
            {
                best = i;
                bestSquared = squared;
                bestIsHero = unit.IsHero;
            }
        }

        return best;
    }
}
