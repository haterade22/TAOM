using TAOM.Features.Refuge.Domain;
using TaleWorlds.Library;

namespace TAOM.Features.Refuge;

/// <summary>
/// Map-scene refuge visuals, the Refuge sibling of <c>ICampVisualService</c>. The boundary
/// builder implements it over <c>CampLayoutBuilder</c>; the service layer only ever asks for
/// show/remove so the layout code never leaks into decision logic.
/// </summary>
public interface IRefugeVisualService
{
    /// <summary>Shows (or re-shows after load) the layout for a ready refuge. True when the
    /// visuals now exist; false when the map scene was not ready and the caller should retry
    /// from the frame tick.</summary>
    bool Show(string refugeId, RefugeTier tier, bool fortified, Vec2 position);

    /// <summary>Removes this refuge's entities. Also used on a stronghold upgrade so the next
    /// Show rebuilds the layout at stronghold scale.</summary>
    void Remove(string refugeId);

    /// <summary>Removes everything (session teardown).</summary>
    void ClearAll();

    /// <summary>Real-time banner-cloth wind driver. Map-scene cloths drop their forced wind on
    /// their own, so the service layer calls this from its frame tick while any refuge visual
    /// stands; internally throttled, and idempotent against the camp-side driver because the
    /// shared ticker re-applies a constant wind.</summary>
    void TickWind();
}
