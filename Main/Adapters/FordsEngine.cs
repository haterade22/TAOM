using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Validation;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The small engine lookups the three Fords of War adapters share: domain/engine vector conversion, the ground under a point,
/// the team of a side and a character by id. Static and stateless, so no adapter owns another's handles.
/// </summary>
internal static class FordsEngine
{
    // Probe from just above the terrain: a probe from z 0 misses the ground wherever the terrain is higher.
    private const float ProbeLift = 3f;
    private const float GroundBelow = 5f;
    private const float GroundAbove = 60f;

    public static Vec3f ToVec3f(Vec3 v) => new(v.x, v.y, v.z);

    public static Vec3 ToVec3(Vec3f v) => new(v.X, v.Y, v.Z, -1f);

    public static float YawOf(Vec3 forward) => (float)Math.Atan2(forward.y, forward.x);

    /// <summary>The terrain surface at x, y.</summary>
    public static float TerrainAt(Scene scene, float x, float y) => scene.GetTerrainHeight(new Vec2(x, y), true);

    /// <summary>
    /// The walkable ground at x, y: the first collision under a probe just above the terrain (a bridge or a rock counts), else
    /// the terrain itself when the probe finds nothing sensible.
    /// </summary>
    public static float GroundAt(Scene scene, float x, float y)
    {
        var terrain = TerrainAt(scene, x, y);
        var probe = new Vec3(x, y, terrain + ProbeLift, -1f);
        var ground = scene.GetGroundHeightAtPosition(probe, BodyFlags.CommonCollisionExcludeFlags);
        return ground > terrain - GroundBelow && ground < terrain + GroundAbove ? ground : terrain;
    }

    /// <summary>West is the player's team, East the player's enemy (the launcher gives the mission exactly these two).</summary>
    public static Team? TeamOf(Mission mission, MatchSide side) =>
        side == MatchSide.West ? mission.PlayerTeam : mission.PlayerEnemyTeam;

    /// <summary>The inverse of <see cref="TeamOf"/>: the side a team plays, null for another team or none.</summary>
    public static MatchSide? SideOf(Mission mission, Team? team)
    {
        if (team == null)
            return null;
        if (ReferenceEquals(team, TeamOf(mission, MatchSide.West)))
            return MatchSide.West;
        return ReferenceEquals(team, TeamOf(mission, MatchSide.East)) ? MatchSide.East : null;
    }

    /// <summary>
    /// A troop or hero by NPCCharacter id. A Custom Battle game registers them as BasicCharacterObject, a campaign as CharacterObject
    /// (a subclass), and the object manager's lookup of the base type walks every record that holds either, returning null for a
    /// missing id (MBObjectManager.GetObject, v1.5.6), so one lookup covers both.
    /// </summary>
    public static BasicCharacterObject? Character(string id)
    {
        var objects = MBObjectManager.Instance;
        if (objects == null || string.IsNullOrEmpty(id))
            return null;

        return objects.GetObject<BasicCharacterObject>(id);
    }

    /// <summary>The position as a domain point; null when the engine handed back a NaN or an infinity, so no rule ever sees one.</summary>
    public static Vec3f? FinitePosition(Vec3 v) =>
        FiniteFloatValidator.IsFinite(v.x) && FiniteFloatValidator.IsFinite(v.y) && FiniteFloatValidator.IsFinite(v.z) ? ToVec3f(v) : null;

    /// <summary>
    /// A troop's name without the Custom Battle sort tag some rows carry ("[AMordor] Hill Troll" shows as "Hill Troll"): one leading
    /// bracketed tag and the space after it. A name without one, or with nothing after the tag, is returned unchanged.
    /// </summary>
    public static string DisplayName(string name)
    {
        if (name.Length == 0 || name[0] != '[')
            return name;

        var close = name.IndexOf("] ", StringComparison.Ordinal);
        return close < 0 || close + 2 >= name.Length ? name : name.Substring(close + 2);
    }
}
