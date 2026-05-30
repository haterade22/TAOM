namespace TAOM.Features.BanditManagement;

public sealed class BanditScalingService : IBanditScalingService
{
    private readonly IBanditScalingSettingsProvider _settings;

    public BanditScalingService(IBanditScalingSettingsProvider settings)
    {
        _settings = settings;
    }

    public bool IsEnabled => _settings.IsEnabled;

    public int MaxHideoutsPerFactionCap => _settings.MaxHideoutsPerFactionCap;

    public int MaxPartiesPerHideoutCap => _settings.MaxPartiesPerHideoutCap;

    public int MinPartiesToInfest => _settings.MinPartiesToInfest;

    public int InitialHideoutsPerFaction => _settings.InitialHideoutsPerFaction;

    public float GetDensityMultiplier(float playerProgress) =>
        ComputeMultiplier(playerProgress, _settings.DensityCurve);

    public float GetPartySizeMultiplier(float playerProgress) =>
        ComputeMultiplier(playerProgress, _settings.PartySizeCurve);

    public float GetBossFightMultiplier(float playerProgress) =>
        ComputeMultiplier(playerProgress, _settings.BossFightCurve);

    private static float ComputeMultiplier(float playerProgress, float curve)
    {
        var p = ClampUnit(playerProgress);
        var c = ClampNonNegative(curve);
        return 1f + c * p;
    }

    private static float ClampUnit(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) return 0f;
        return v < 0f ? 0f : v > 1f ? 1f : v;
    }

    private static float ClampNonNegative(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) return 0f;
        return v < 0f ? 0f : v;
    }
}
