using TAOM.Core.Validation;
using TAOM.Features;

namespace TAOM.Features.BanditManagement;

public sealed class BanditScalingSettingsProvider : IBanditScalingSettingsProvider
{
    private readonly BanditScalingConfig _defaults;

    public BanditScalingSettingsProvider(IBanditScalingConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableBanditScaling ?? true;

    public float DensityCurve =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditDensityCurve, _defaults.DensityCurve, 0f, 5f);

    public float PartySizeCurve =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditPartySizeCurve, _defaults.PartySizeCurve, 0f, 5f);

    public float BossFightCurve =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditBossFightCurve, _defaults.BossFightCurve, 0f, 5f);

    public int MaxHideoutsPerFactionCap =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditMaxHideoutsPerFaction, _defaults.MaxHideoutsPerFactionCap, 1, 100);

    public int MaxPartiesPerHideoutCap =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditMaxPartiesPerHideout, _defaults.MaxPartiesPerHideoutCap, 1, 20);

    public int InitialHideoutsPerFaction =>
        SettingClamp.Clamp(TaomSettings.Instance?.BanditInitialHideoutsPerFaction, _defaults.InitialHideoutsPerFaction, 1, 30);

    // No MCM knob for MinPartiesToInfest -- it's a JSON-only advanced tuning value with a strict
    // upper bound derived from the live MCM cap (not the JSON default), so the invariant
    // min <= max holds at runtime even if the user lowers BanditMaxPartiesPerHideout in MCM.
    public int MinPartiesToInfest
    {
        get
        {
            var cap = MaxPartiesPerHideoutCap;
            var v = _defaults.MinPartiesToInfest;
            if (v < 1) v = 1;
            if (v > cap) v = cap;
            return v;
        }
    }
}
