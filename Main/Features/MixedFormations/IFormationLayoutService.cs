using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Features.MixedFormations;

public interface IFormationLayoutService
{
    /// <summary>
    /// Compute the post-transform plane position for a unit, or <c>null</c> when the
    /// caller should fall through to vanilla positioning (feature disabled, formation
    /// in non-Hold state, layout is Vanilla, formation has no valid order position).
    /// </summary>
    Vec2? ComputeUnitPlanePosition(IFormationAdapter formation, int agentIndex, bool agentIsRanged);

    FormationLayoutType GetLayout(IFormationAdapter formation);
    void SetLayout(IFormationAdapter formation, FormationLayoutType layout);

    /// <summary>
    /// Cycle layout for the given formations to the next layout in the rotation.
    /// Returns the new layout that was applied and the count of formations that received it.
    /// Formations that have never had a layout set are skipped (matches developer's behavior).
    /// </summary>
    (FormationLayoutType newLayout, int affectedCount) CycleLayouts(IReadOnlyList<IFormationAdapter> formations);

    /// <summary>
    /// Auto-apply the configured default layout to formations that satisfy <see cref="IsMixedFormation"/>
    /// and don't already have a layout assigned.
    /// </summary>
    int ApplyDefaultsToFormations(IReadOnlyList<IFormationAdapter> formations);

    /// <summary>
    /// Heuristic: a formation is "mixed" (and thus a candidate for layout assignment) when it has
    /// at least 10 units, the minority class has at least 5 units, AND the minority class is at
    /// least 20% of the formation. Matches the original developer's threshold so behavior parity holds.
    /// </summary>
    bool IsMixedFormation(IFormationAdapter formation);

    /// <summary>Clear all per-formation state. Called by the MissionBehavior at OnEndMission.</summary>
    void OnMissionEnd();
}
