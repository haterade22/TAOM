using System.Collections.Generic;
using TAOM.Features;

namespace TAOM.Features.NavalTravel;

/// <summary>
/// Merges MCM live values (<c>TaomSettings.Instance</c>) over JSON defaults for the three booleans.
/// <c>TaomSettings.Instance</c> can be null very early in startup or if MCM fails to load — the
/// <c>?? default</c> fallback keeps every read safe. The embark threshold + terrain-id set are
/// advanced JSON-only tuning (no MCM knob) so they pass straight through from the config.
/// </summary>
public sealed class NavalTravelSettingsProvider : INavalTravelSettingsProvider
{
    private readonly NavalTravelConfig _defaults;

    public NavalTravelSettingsProvider(INavalTravelConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableNavalTravel ?? _defaults.Enabled;

    public bool ApplyToPlayer => TaomSettings.Instance?.NavalTravelApplyToPlayer ?? _defaults.ApplyToPlayer;

    public bool ApplyToAi => TaomSettings.Instance?.NavalTravelApplyToAi ?? _defaults.ApplyToAi;

    public float EmbarkThresholdDistance => _defaults.EmbarkThresholdDistance;

    public IReadOnlyList<int> NavalTerrainTypeIds => _defaults.NavalTerrainTypeIds;

    public bool RenderBoatVisual => _defaults.RenderBoatVisual;

    public string BoatMeshName => _defaults.BoatMeshName;

    public float BoatScale => _defaults.BoatScale;

    public string SailModifierKey => _defaults.SailModifierKey;
}
