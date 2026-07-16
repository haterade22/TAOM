using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters.Models;

namespace TAOM.Adapters;

/// <summary>
/// Battlefield-scope queries that span multiple agents/formations or hit the engine's
/// terrain query system. Wrapped so cavalry-feature services can be unit-tested without
/// a live <c>Mission</c> or <c>Scene</c>. Per ADR-007.
/// </summary>
public interface IBattlefieldQueryAdapter
{
    /// <summary>True iff <c>Mission.Current.PlayerTeam</c> is non-null. Custom-battle
    /// spectator missions can leave this null; callers must short-circuit accordingly.</summary>
    bool HasPlayerTeam { get; }

    /// <summary>True iff the current mission is an open-field battle
    /// (<c>Mission.Current.IsFieldBattle</c> — team-AI type == FieldBattle). FALSE for
    /// siege, sally-out, hideout, naval, and every settlement/no-team-AI mission. The
    /// SmartCavalryAI line-charge feature is open-field-only, so it gates on this. False
    /// when there is no live mission.</summary>
    bool IsFieldBattle { get; }

    /// <summary>Snapshot all friendly formations on the player team OTHER than
    /// <paramref name="excludeFormationKey"/> (typically the cavalry formation itself).
    /// Used by reroute path-planning to find friendly infantry obstructing the charge line.
    /// Empty list when no PlayerTeam.</summary>
    IReadOnlyList<IFormationAdapter> GetFriendlyFormationsExcluding(object excludeFormationKey);

    /// <summary>Wraps <c>Mission.Current.GetNearbyAgents(center, radius, buffer)</c>. Returns
    /// snapshots so callers don't hold Agent references.</summary>
    void GetNearbyAgents(Vec2 center, float radius, List<NearbyAgentSnapshot> output);

    /// <summary>Wraps <c>Scene.GetGroundHeightAtPosition(pos, BodyFlags.CommonCollisionExcludeFlags)</c>.
    /// Returns 0 when no scene is available.</summary>
    float GetGroundHeightAtPosition(Vec3 position);

    /// <summary>The player team's opaque key (the <c>Team</c> reference). Used by callers
    /// to filter <see cref="NearbyAgentSnapshot.TeamKey"/> against "is friendly".</summary>
    object? PlayerTeamKey { get; }
}
