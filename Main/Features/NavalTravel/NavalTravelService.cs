using System.Collections.Generic;

namespace TAOM.Features.NavalTravel;

/// <summary>
/// Pure implementation of <see cref="INavalTravelService"/>. All decisions reduce to the
/// MCM-over-JSON values exposed by <see cref="INavalTravelSettingsProvider"/>, keeping this class
/// free of TaleWorlds + MCM types (ADR-007) and fully unit-testable.
/// </summary>
public class NavalTravelService : INavalTravelService
{
    private readonly INavalTravelSettingsProvider _settings;
    private readonly HashSet<int> _navalTerrainIds;

    public NavalTravelService(INavalTravelSettingsProvider settings)
    {
        _settings = settings;
        // Terrain ids are JSON-only + process-lifetime cached (never change at runtime), so snapshot
        // them into an O(1) set once instead of scanning the list on every membership check.
        var ids = settings.NavalTerrainTypeIds;
        _navalTerrainIds = ids != null ? new HashSet<int>(ids) : new HashSet<int>();
    }

    public bool IsEnabled => _settings.IsEnabled;

    public float EmbarkThresholdDistance => _settings.EmbarkThresholdDistance;

    public bool CanPartySail(bool isPlayerParty)
    {
        if (!_settings.IsEnabled)
            return false;
        return isPlayerParty ? _settings.ApplyToPlayer : _settings.ApplyToAi;
    }

    public bool HasNavalCapability(bool isMainParty, bool isAtSea, bool attachedLeaderCanSail)
    {
        // A party already at sea keeps capability so it can reach land — the gates govern NEW embarks
        // (from land), not stranding a party mid-voyage when a toggle is flipped off. (The engine
        // forces IsCurrentlyAtSea onto attached parties but recomputes capability per party, so a
        // gated-off attached party would otherwise be at sea with no way to path on water.)
        if (isAtSea)
            return true;
        if (!_settings.IsEnabled)
            return false;
        if (CanPartySail(isMainParty))
            return true;
        // Army-member inheritance (mirrors NavalDLC): an attached party follows a leader that can sail.
        return !isMainParty && attachedLeaderCanSail;
    }

    public bool IsNavalTerrain(int terrainTypeId) => _navalTerrainIds.Contains(terrainTypeId);

    public bool ShouldRenderBoat(bool isAtSea, bool isInTransition) =>
        _settings.IsEnabled && _settings.RenderBoatVisual && isAtSea && !isInTransition;

    public string BoatMeshName => _settings.BoatMeshName;

    public float BoatScale => _settings.BoatScale;

    public string SailModifierKey => _settings.SailModifierKey;
}
