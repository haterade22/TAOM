using TAOM.Features.FieldCamp.Domain;
using TaleWorlds.Library;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// Map-scene camp visuals. Prefab meshes first (the shipped tpacs: fieldcamp_camp_a,
/// fieldcamp_palisade_ring), the source's procedural vanilla-mesh layout as the fallback when a
/// mesh is missing. Idempotent per party; all entity work null-tolerant, because the map scene may
/// not exist yet when a save loads (callers retry from the frame tick).
/// </summary>
public interface ICampVisualService
{
    /// <summary>Shows (or re-shows after load) the layout for a ready camp. True when the visuals
    /// now exist; false when the scene was not ready and the caller should retry later.</summary>
    bool Show(string partyId, CampType type, Vec2 position);

    /// <summary>Whether this party's visuals stand. ALSO the steady-state driver for the
    /// banner-cloth wind ticker: CampService.FrameTick polls it every pass while a camp visual
    /// stands (Show covers the raising window), so the command-tent flags keep waving.</summary>
    bool IsShown(string partyId);

    /// <summary>Removes this party's camp entities.</summary>
    void Remove(string partyId);

    /// <summary>Removes everything (session teardown).</summary>
    void ClearAll();
}
