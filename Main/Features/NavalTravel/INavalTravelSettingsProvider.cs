using System.Collections.Generic;

namespace TAOM.Features.NavalTravel;

/// <summary>
/// Live settings surface (MCM-over-JSON) for naval travel. Lets the pure
/// <see cref="NavalTravelService"/> stay free of MCM + JSON plumbing. The three booleans come from
/// MCM (<c>TaomSettings</c>) over JSON defaults; the threshold + terrain-id set are advanced,
/// JSON-only tuning (no player-facing MCM knob).
/// </summary>
public interface INavalTravelSettingsProvider
{
    bool IsEnabled { get; }
    bool ApplyToPlayer { get; }
    bool ApplyToAi { get; }
    float EmbarkThresholdDistance { get; }
    IReadOnlyList<int> NavalTerrainTypeIds { get; }

    bool RenderBoatVisual { get; }
    string BoatMeshName { get; }
    float BoatScale { get; }
    string SailModifierKey { get; }
}
